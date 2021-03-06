﻿using STV.DAL;
using System;
using System.Data.Entity;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace STV.Controllers
{
    [Authorize]
    public class ArquivosController : Controller
    {
        private STVDbContext db = new STVDbContext();

        // GET: Arquivos
        public ActionResult Index(int id)
        {
            var fileToRetrieve = db.Arquivo.Find(id);
            return File(fileToRetrieve.Blob, fileToRetrieve.ContentType);
        }

        [HttpDelete]
        public async Task<ActionResult> Excluir(int id)
        {
            HttpStatusCodeResult HttpResult;
            try
            {
                await db.Database.ExecuteSqlCommandAsync(@"DELETE FROM [Arquivo] WHERE Idmaterial = {0}", id);
                var material = await db.Material.FindAsync(id);
                //material.Tipo = null;
                db.Entry(material).State = EntityState.Modified;
                //TODO: Atualizar Tipo do material para null

                HttpResult = new HttpStatusCodeResult(HttpStatusCode.OK);
                return HttpResult;
            }
            catch (Exception ex)
            {
                HttpResult = new HttpStatusCodeResult(HttpStatusCode.InternalServerError);
                ViewBag.MensagemErro = ex.Message;
                return HttpResult;
            }
        }

    }
}