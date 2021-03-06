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

        // GET: Materiais/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            try
            {
                if (id == null)
                    throw new ApplicationException("Ops! Requisição inválida.");

                Material material = await db.Material.FindAsync(id);

                ViewBag.DescricaoTipo = GetText(material.Tipo);
                ViewBag.IsInstrutorOrAdmin = material.Unidade.Curso.IdusuarioInstrutor == UsuarioLogado.Idusuario || User.IsInRole("Admin");

                if (material == null)
                    throw new ApplicationException("Material não encontrado.");

                if (!CommonValidation.CanSee(material.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    throw new UnauthorizedAccessException("Não Autorizado");

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
                    throw new UnauthorizedAccessException("Não Autorizado");

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

        public async Task<ActionResult> RedirecionarURL(int Id)
        {
            var material = await db.Material.FindAsync(Id);

            if (material != null || material.Tipo != TipoMaterial.Link)
            {
                if (!CommonValidation.CanSee(material.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    throw new UnauthorizedAccessException("Não Autorizado");

                RegistrarVisualizacao(material);
            }
            else
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return Redirect(material.URL);
        }

        // GET: Materiais/Create
        public ActionResult Create(int? Idunidade)
        {
            try
            {
                if (Idunidade == null)
                    throw new Exception("Ops! Requisição inválida.");
                
                var unidade = db.Unidade.Find(Idunidade);
                MaterialValidation.CanEdit(unidade, UsuarioLogado.Idusuario, User);

                var materialVM = new MaterialVM();
                materialVM.Idunidade = (int)Idunidade;
                materialVM.Unidade = unidade;
                materialVM.Idcurso = unidade.Idcurso;

                return View(materialVM);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(Idunidade);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> UploadFile(int id)
        {
            try
            {
                var form = Request.Form;

                if (string.IsNullOrEmpty(form["Idunidade"]))
                    throw new ApplicationException("Ops! Requisição inválida.");

                var unidade = db.Unidade.Find(Convert.ToInt32(form["Idunidade"]));
                MaterialValidation.CanEdit(unidade, UsuarioLogado.Idusuario, User);

                if (string.IsNullOrEmpty(form["Descricao"]))
                    throw new ApplicationException("Descrição não informada.");

                int tipoID = 0;
                if (string.IsNullOrEmpty(form["Tipo"]) || form["Tipo"] == "0")
                    throw new ApplicationException("Tipo não informado.");
                else if (Convert.ToInt16(form["Tipo"]) < 1)
                    throw new ApplicationException("Tipo inválido.");
                else
                    tipoID = Convert.ToInt16(form["Tipo"]);

                bool isArquivo = tipoID == 1 || tipoID == 2 || tipoID == 4;

                if (isArquivo)
                {
                    if (Request.Files.Count == 0 || Request.Files[0].ContentLength == 0)
                        throw new ApplicationException("Arquivo não selecionado.");
                }
                else if (string.IsNullOrEmpty(form["URL"]))
                    throw new ApplicationException("URL não informada.");

                var material = new Material
                {
                    Idmaterial = Convert.ToInt32(form["Idmaterial"]),
                    Descricao = form["Descricao"],
                    Idunidade = Convert.ToInt32(form["Idunidade"]),
                    Tipo = (TipoMaterial)Convert.ToInt16(form["Tipo"]),
                    Unidade = db.Unidade.Find(Convert.ToInt32(form["Idunidade"])),
                    URL = form["URL"]
                };

                if (isArquivo)
                {
                    string extensao = Path.GetExtension(Request.Files[0].FileName);
                    string[] valids;
                    switch (material.Tipo)
                    {
                        case TipoMaterial.Video:
                            valids = new string[2] { ".mp4", ".webm" };
                            if (!valids.Contains(extensao))
                                throw new ApplicationException("Extensão do arquivo inválida.");
                            break;
                        case TipoMaterial.Arquivo:
                            valids = new string[11] { ".doc", ".docx", ".xls", ".xlsx", ".pdf", ".rar", ".zip", ".txt", ".ppt", ".pptx", ".exe" };
                            if (!valids.Contains(extensao))
                                throw new ApplicationException("Extensão do arquivo inválida.");
                            break;
                        case TipoMaterial.Imagem:
                            valids = new string[3] { ".jpg", ".png", ".gif" };
                            if (!valids.Contains(extensao))
                                throw new ApplicationException("Extensão do arquivo inválida.");
                            break;
                        default:
                            break;
                    }

                    using (DbContextTransaction transaction = db.Database.BeginTransaction())
                    {
                        try
                        {
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
                        }
                        catch (Exception)
                        {
                            transaction.Rollback();
                            throw;
                        }

                    }
                }
                else
                {
                    ModelState.Clear();
                    TryValidateModel(material);
                    if (!ModelState.IsValidField("URL"))
                        throw new ApplicationException(ModelState["URL"].Errors.First().ErrorMessage);

                    db.Material.Add(material);
                    await db.SaveChangesAsync();
                }

                TempData["msg"] = "Material criado!";
                Response.StatusCode = (int)HttpStatusCode.OK;
                return Json("Material criado!");
            }
            catch (Exception ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(ex.Message);
            }
        }

        // GET: Materiais/Edit/5
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

                MaterialValidation.CanEdit(material.Unidade, UsuarioLogado.Idusuario, User);

                GetArquivoInfo(ref material);

                var materialVM = Mapper.Map<Material, MaterialVM>(material);
                materialVM.Idunidade = material.Idunidade;
                materialVM.Idcurso = material.Unidade.Idcurso;

                ViewBag.URL = material.URL;

                return View(materialVM);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(material.Idunidade);
            }
        }

        // POST: Materiais/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Idmaterial,Idunidade,Descricao,Tipo,URL")] MaterialVM materialVM)
        {
            var material = Mapper.Map<MaterialVM, Material>(materialVM);

            if (ModelState.IsValid)
            {
                db.Entry(material).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["msg"] = "Dados salvos!";
            }

            return VoltarParaListagem(material.Idunidade);
        }


        // GET: Materiais/Delete/5
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

                MaterialValidation.CanEdit(material.Unidade, UsuarioLogado.Idusuario, User);

                return View(material);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(material.Idunidade);
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
                return VoltarParaListagem(material.Idunidade);
            }
            catch (Exception)
            {
                TempData["msgErr"] = "Material não pode ser excluído.";
                return RedirectToAction("Details", "Cursos", new { id = material.Unidade.Idcurso, Idunidade = material.Idunidade });
            }
        }

        //Retorna para a tela principal do Curso
        private RedirectToRouteResult VoltarParaListagem(int? Idunidade)
        {
            Unidade unidade = db.Unidade.Find(Idunidade);
            return RedirectToAction("Details", "Cursos", new { id = unidade.Idcurso, Idunidade = Idunidade });
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
