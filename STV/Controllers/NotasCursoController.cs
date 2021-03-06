﻿using STV.Auth;
using STV.DAL;
using STV.Models;
using STV.Models.Validation;
using System;
using System.Net;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace STV.Controllers
{
    [Authorize]
    public class NotasCursoController : Controller
    {

        private STVDbContext db = new STVDbContext();

        private Usuario UsuarioLogado;

        public NotasCursoController()
        {
            SessionContext auth = new SessionContext();
            UsuarioLogado = auth.GetUserData();
        }

        [HttpPost]
        public async Task<ActionResult> Avaliar(int? Idcurso, int nota)
        {
            if (Idcurso == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var curso = db.Curso.Find(Idcurso);
            if (!CommonValidation.CanSee(curso, UsuarioLogado.Idusuario, User))
                throw new UnauthorizedAccessException("Não Autorizado");

            var notaAtual = await db.NotaCurso.FindAsync(UsuarioLogado.Idusuario, Idcurso);

            if (notaAtual != null)
            {
                notaAtual.Pontos = nota;
                db.NotaCurso.Attach(notaAtual);
                db.Entry(notaAtual).Property(q => q.Pontos).IsModified = true;
            }
            else
            {
                var novaNota = new NotaCurso
                {
                    Idcurso = (int)Idcurso,
                    Idusuario = UsuarioLogado.Idusuario,
                    Pontos = nota
                };
                db.NotaCurso.Add(novaNota);
            }

            await db.SaveChangesAsync();

            return new HttpStatusCodeResult(HttpStatusCode.OK);
        }

    }
}
