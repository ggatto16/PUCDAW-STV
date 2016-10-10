﻿using AutoMapper;
using STV.Auth;
using STV.DAL;
using STV.Models;
using STV.Models.Validation;
using STV.ViewModels;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace STV.Controllers
{
    [Authorize]
    public class MateriaisController : Controller
    {

        #region Propriedades

        private STVDbContext db = new STVDbContext();

        private Usuario UsuarioLogado;

        #endregion

        #region Construtor

        public MateriaisController()
        {
            SessionContext auth = new SessionContext();
            UsuarioLogado = auth.GetUserData();
        }

        #endregion

        #region Actions

        // GET: Materiais
        //public async Task<ActionResult> Index(int? idunidade)
        //{

        //    if (idunidade == null)
        //    {
        //        TempData["msgErr"] = "Ops! Requisição inválida.";
        //        return RedirectToAction("Index", "Home");
        //    }

        //    var materiais = db.Material.Where(m => m.Unidade.Idunidade == idunidade);

        //    return PartialView(await materiais.ToListAsync());
        //}



        // GET: Materiais/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            try
            {
                if (id == null)
                    throw new ApplicationException("Ops! Requisição inválida.");

                Material material = await db.Material.FindAsync(id);

                ViewBag.DescricaoTipo = GetText(material.Tipo);

                if (material == null)
                    throw new ApplicationException("Material não encontrado.");

                if (!CommonValidation.CanSee(material.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    return View("NaoAutorizado");

                GetArquivoInfo(ref material);

                return View(material);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Tipo
        public async Task<ActionResult> MostrarArquivo(int Id)
        {
            var material = await db.Material.FindAsync(Id);

            if (material != null)
            {
                if (!CommonValidation.CanSee(material.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    return View("NaoAutorizado");

                RegistrarVisualizacao(material);
            }
            else
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            GetArquivoInfo(ref material);

            switch (material.Tipo)
            {
                case TipoMaterial.Link:
                    break;

                case TipoMaterial.Imagem:
                    var blobArquivo = await db.Arquivo.Where(a => a.Idmaterial == Id)
                        .Select(a => new
                        {
                            Blob = a.Blob
                        }).SingleAsync();
                    material.Arquivo.Blob = blobArquivo.Blob;
                    break;

                default:
                    break;
            }
            return PartialView("ConteudoArquivo", material);
        }

        public FileResult BaixarArquivo(int Id)
        {
            var material = db.Material.Find(Id);

            if (material != null)
            {
                if (!CommonValidation.CanSee(material.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    return null;

                RegistrarVisualizacao(material);
            }

            var blobArquivo = db.Arquivo.Where(a => a.Idmaterial == Id)
                .Select(a => new
                {
                    Blob = a.Blob,
                    Nome = a.Nome,
                    ContentType = a.ContentType
                }).Single();

            Response.AppendHeader("Content-Disposition", "inline; filename=" + blobArquivo.Nome);
            return File(blobArquivo.Blob, blobArquivo.ContentType);
        }

        // GET: Materiais/Create
        public ActionResult Create(int Idunidade)
        {
            Material material = new Material();
            var materialVM = Mapper.Map<Material, MaterialVM>(material);
            materialVM.Idunidade = Idunidade;
            var unidade = db.Unidade.Find(Idunidade);
            materialVM.Unidade = unidade;
            materialVM.Idcurso = unidade.Idcurso;
            return View(materialVM);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<JsonResult> UploadFile(int id)
        {
            using (DbContextTransaction transaction = db.Database.BeginTransaction())
            {
                try
                {
                    var form = Request.Form;
                    var material = new Material
                    {
                        Idmaterial = Convert.ToInt32(form["Idmaterial"]),
                        Descricao = form["Descricao"],
                        Idunidade = Convert.ToInt32(form["Idunidade"]),
                        Tipo = (TipoMaterial)Convert.ToInt32(form["Tipo"]),
                        Unidade = db.Unidade.Find(Convert.ToInt32(form["Idunidade"])),
                        URL = form["URL"]
                    };

                    foreach (string file in Request.Files)
                    {
                        var fileContent = Request.Files[file];
                        if (fileContent != null && fileContent.ContentLength > 0)
                        {
                            GetUploadInfo(ref material, fileContent);
                            db.Material.Add(material);
                            await db.SaveChangesAsync();

                            //Grava o conteúdo do arquivo no banco de dados
                            using (VarbinaryStream blob = new VarbinaryStream(
                                db.Database.Connection.ConnectionString,
                                "Arquivo",
                                "Blob",
                                "Idmaterial",
                                material.Idmaterial, db, fileContent.ContentLength))
                            {
                                await fileContent.InputStream.CopyToAsync(blob, 65536);
                            }
                        }
                    }

                    transaction.Commit();
                    TempData["msg"] = "Material criado!";
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    return Json("File uploaded successfully");
                }
                catch (Exception ex)
                {
                    transaction.Rollback();
                    Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    return Json(ex.Message);
                }
            }
        }

        // GET: Materiais/Edit/5
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Edit(int? id)
        {
            Material material = null;
            try
            {
                if (id == null)
                    throw new Exception("Ops! Requisição inválida.");

                material = await db.Material.FindAsync(id);

                if (material == null)
                    throw new Exception("Material não encontrado.");

                if (material.Unidade.Encerrada)
                    throw new ApplicationException("Unidade encerrada. Material não pode ser alterado.");

                GetArquivoInfo(ref material);

                var materialVM = Mapper.Map<Material, MaterialVM>(material);
                materialVM.Idunidade = material.Unidade.Idunidade;
                materialVM.Idcurso = material.Unidade.Idcurso;

                ViewBag.URL = material.URL;

                return View(materialVM);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(material);
            }
            catch (Exception ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Materiais/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Edit([Bind(Include = "Idmaterial,Idunidade,Descricao,Tipo,URL")] MaterialVM materialVM)
        {
            var material = Mapper.Map<MaterialVM, Material>(materialVM);

            if (ModelState.IsValid)
            {
                db.Entry(material).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["msg"] = "Dados salvos!";
            }

            return VoltarParaListagem(material);
        }


        // GET: Materiais/Delete/5
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> Delete(int? id)
        {
            Material material = null;
            try
            {
                if (id == null)
                    throw new Exception("Ops! Requisição inválida.");

                material = await db.Material.FindAsync(id);
                if (material == null)
                    throw new Exception("Material não econtrado.");

                if (material.Unidade.Encerrada)
                    throw new ApplicationException("Unidade encerrada. Material não pode eser excluído.");

                return View(material);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(material);
            }
            catch (Exception ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // POST: Materiais/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            Material material = await db.Material.FindAsync(id);
            try
            {
                //await db.Database.ExecuteSqlCommandAsync(@"DELETE FROM [Arquivo] WHERE Idmaterial = {0}", id);
                db.Entry(material).Collection("UsuariosConsulta").Load(); //Para remover também a referência
                db.Material.Remove(material);
                await db.SaveChangesAsync();
                TempData["msg"] = "Material excluído!";
                return VoltarParaListagem(material);
            }
            catch (Exception)
            {
                TempData["msgErr"] = "Material não pode ser excluído.";
                return RedirectToAction("Details", "Cursos", new { id = material.Unidade.Idcurso, Idunidade = material.Unidade.Idunidade });
            }
        }

        //Retorna para a tela principal do Curso
        private RedirectToRouteResult VoltarParaListagem(Material material)
        {
            Unidade unidade = db.Unidade.Find(material.Idunidade);
            return RedirectToAction("Details", "Cursos", new { id = unidade.Idcurso, Idunidade = material.Idunidade });
        }

        #endregion

        #region Métodos Locais

        private static string GetText(object e)
        {
            FieldInfo fieldInfo = e.GetType().GetField(e.ToString());
            DisplayAttribute[] displayAttributes = fieldInfo.GetCustomAttributes(typeof(DisplayAttribute), false) as DisplayAttribute[];
            return null != displayAttributes && displayAttributes.Length > 0 ? displayAttributes[0].Name : e.ToString();
        }

        private void GetArquivoInfo(ref Material material)
        {
            int Idmaterial = material.Idmaterial;

            var arquivoInfo = db.Arquivo.Where(a => a.Idmaterial == Idmaterial)
                .Select(a => new
                {
                    Idmaterial = a.Idmaterial,
                    Nome = a.Nome,
                    ContentType = a.ContentType,
                    Tamanho = a.Tamanho
                }).FirstOrDefault();

            if (arquivoInfo != null)
            {
                material.Arquivo = new Arquivo
                {
                    Nome = arquivoInfo.Nome,
                    Idmaterial = arquivoInfo.Idmaterial,
                    ContentType = arquivoInfo.ContentType,
                    Tamanho = arquivoInfo.Tamanho
                };
            }
        }

        private void GetUploadInfo(ref Material material, HttpPostedFileBase upload)
        {
            try
            {
                if (upload != null && upload.ContentLength > 0)
                {
                    var arquivo = new Arquivo
                    {
                        Nome = Path.GetFileName(upload.FileName),
                        ContentType = upload.ContentType,
                        Idmaterial = material.Idmaterial,
                        Tamanho = upload.ContentLength
                    };

                    material.Arquivo = arquivo;
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        private void GetUploadContent(ref Material material, HttpPostedFileBase upload)
        {
            try
            {
                if (upload != null && upload.ContentLength > 0)
                {
                    var arquivo = new Arquivo
                    {
                        Nome = Path.GetFileName(upload.FileName),
                        ContentType = upload.ContentType,
                        Idmaterial = material.Idmaterial
                    };

                    using (BinaryReader b = new BinaryReader(upload.InputStream))
                    {
                        byte[] filedata = b.ReadBytes((int)upload.InputStream.Length);
                        material.Arquivo.Blob = filedata;
                    }

                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public ActionResult CarregarTipo(int Idtipo, string url)
        {
            ViewBag.Tipo = (TipoMaterial)Idtipo;
            ViewBag.URL = url;
            return PartialView("Upload");
        }

        private void RegistrarVisualizacao(Material material)
        {
            if (User.IsInRole("Admin")) return;

            var isInstrutor = db.Curso.Where(c => c.Idcurso == material.Unidade.Idcurso).FirstOrDefault()
                .IdusuarioInstrutor == UsuarioLogado.Idusuario;
            if (isInstrutor) return;

            var usuarioToUpdate = db.Usuario
                    .Include(u => u.MateriaisConsultados)
                    .Where(i => i.Idusuario == UsuarioLogado.Idusuario)
                    .Single();

            if (!usuarioToUpdate.MateriaisConsultados.Contains(material))
            {
                usuarioToUpdate.MateriaisConsultados.Add(material);
                db.SaveChanges();
            }
        }

        #endregion
    }
}
