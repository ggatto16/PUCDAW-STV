﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using STV.Models;
using STV.DAL;
using Microsoft.Owin;
using STV.Auth;
using AutoMapper;
using STV.ViewModels;

namespace STV.Controllers
{
    [Authorize]
    public class AtividadesController : Controller
    {
        private STVDbContext db = new STVDbContext();

        private Usuario UsuarioLogado;

        public AtividadesController()
        {
            SessionContext auth = new SessionContext();
            UsuarioLogado = auth.GetUserData();
        }

        public async Task<ActionResult> CarregarAtividade(int? id, int? index)
        {
            if (id == null)
            {
                TempData["msgErr"] = "Ops! Requisição inválida.";
                return RedirectToAction("Index", "Home");
            }

            Atividade atividade = await db.Atividade
                .Where(a => a.Idatividade == id)
                .SingleOrDefaultAsync();

            var AtividadeModel = Mapper.Map<Atividade, AtividadeVM>(atividade);

            if (index == null)
            {
                AtividadeModel.QuestaoToShow = atividade.Questoes.FirstOrDefault();
                AtividadeModel.QuestaoToShow.Indice = 0;
            }
            else
            {
                AtividadeModel.QuestaoToShow = atividade.Questoes.ElementAtOrDefault((int)index + 1);
                if (AtividadeModel.QuestaoToShow != null)
                    AtividadeModel.QuestaoToShow.Indice = (int)index + 1;
                else
                    return RedirectToAction("Details", "Cursos", new { id = atividade.Unidade.Idcurso, Idunidade = atividade.Idunidade });
            }

            return View("Atividade", AtividadeModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SalvarResposta(AtividadeVM atividade)
        {
            if (ModelState.IsValid)
            {
                Resposta resposta = null;
                resposta = await db.Resposta.FindAsync(UsuarioLogado.Idusuario, atividade.QuestaoToShow.Idquestao);

                if (resposta != null)
                {
                    resposta.Idalternativa = atividade.QuestaoToShow.IdAlternativaSelecionada;
                    db.Resposta.Attach(resposta);
                    db.Entry(resposta).Property(r => r.Idalternativa).IsModified = true;
                }
                else
                {
                    resposta = new Resposta
                    {
                        Idusuario = UsuarioLogado.Idusuario,
                        Idquestao = atividade.QuestaoToShow.Idquestao,
                        Idalternativa = atividade.QuestaoToShow.IdAlternativaSelecionada
                    };
                    db.Resposta.Add(resposta);
                }
                await db.SaveChangesAsync();
            }

            return RedirectToAction("CarregarAtividade", new { Id = atividade.Idatividade, index = atividade.QuestaoToShow.Indice });
        }

        public async Task<ActionResult> Finalizar(int? id)
        {
            if (id == null)
            {
                TempData["msgErr"] = "Ops! Requisição inválida.";
                return RedirectToAction("Index", "Home");
            }

            Atividade atividade = await db.Atividade
                .Where(a => a.Idatividade == id)
                .SingleOrDefaultAsync();

            var corretas = db.Resposta
                .Where(r => r.Idusuario == UsuarioLogado.Idusuario && r.Questao.Idatividade == atividade.Idatividade)
                .OrderBy(r => r.Idquestao)
                .Select(r => new { Idquestao = r.Idquestao, Idalternativa = r.Idalternativa })
                .ToList();

            var corretasHS = new HashSet<int>(corretas.Select(r => r.Idalternativa));

            var respondidas = atividade.Questoes
                .Select(q => new { Idquestao = q.Idquestao, Idalternativa = (int)q.IdalternativaCorreta })
                .OrderBy(r => r.Idquestao)
                .ToList();

            var respondidasHS = new HashSet<int>(respondidas.Select(r => r.Idalternativa));

            int total = atividade.Questoes.Count();
            int certas = total - corretasHS.Except(respondidasHS).Count();
            int valorQuestao = atividade.Valor / total;
            int pontos = certas * valorQuestao;

            Nota nota = new Nota
            {
                Idatividade = atividade.Idatividade,
                Idusuario = UsuarioLogado.Idusuario,
                Pontos = pontos
            };

            db.Nota.Add(nota);
            await db.SaveChangesAsync();

            return RedirectToAction("Details", "Cursos", new { id = atividade.Unidade.Idcurso, Idunidade = atividade.Idunidade });
        }

        // GET: Atividades
        public async Task<ActionResult> Index(int idunidade = 0)
        {
            ViewBag.MensagemSucesso = TempData["msg"];
            ViewBag.MensagemErro = TempData["msgErr"];

            if (idunidade != 0)
            {
                var atividades = from a in db.Atividade where a.Unidade.Idunidade == idunidade select a;
                return PartialView(await atividades.ToListAsync());
            }
            else
            {
                var atividades = db.Atividade.Include(m => m.Unidade);
                return PartialView(await atividades.ToListAsync());
            }
        }

        // GET: Atividades/Details/5
        public async Task<ActionResult> Details(int? id, int? Idquestao)
        {
            ViewBag.MensagemSucesso = TempData["msg"];
            ViewBag.MensagemErro = TempData["msgErr"];

            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
        
            Atividade atividade = await db.Atividade.FindAsync(id);

            if (atividade == null)
            {
                return HttpNotFound();
            }

            ViewBag.QuestaoSelecionada = Idquestao;

            return View(atividade);
        }

        // GET: Atividades/Create
        public ActionResult Create(int Idunidade)
        {
            ViewBag.Idunidade = new SelectList(db.Unidade, "Idunidade", "Titulo");
            Atividade atividade = new Atividade();
            atividade.Idunidade = Idunidade;
            return View(atividade);
        }

        // POST: Atividades/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Idatividade,Idunidade,Descricao,Valor,Dtabertura,Dtencerramento")] Atividade atividade)
        {
            if (ModelState.IsValid)
            {
                db.Atividade.Add(atividade);
                await db.SaveChangesAsync();
                return VoltarParaListagem(atividade);
            }

            ViewBag.Idunidade = new SelectList(db.Unidade, "Idunidade", "Titulo", atividade.Idunidade);
            return View(atividade);
        }

        // GET: Atividades/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Atividade atividade = await db.Atividade.FindAsync(id);
            if (atividade == null)
            {
                return HttpNotFound();
            }
            ViewBag.Idunidade = new SelectList(db.Unidade, "Idunidade", "Titulo", atividade.Idunidade);
            return View(atividade);
        }

        // POST: Atividades/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Idatividade,Idunidade,Descricao,Valor,Dtabertura,Dtencerramento")] Atividade atividade)
        {
            if (ModelState.IsValid)
            {
                db.Entry(atividade).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return VoltarParaListagem(atividade);
            }
            ViewBag.Idunidade = new SelectList(db.Unidade, "Idunidade", "Titulo", atividade.Idunidade);
            return View(atividade);
        }

        // GET: Atividades/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Atividade atividade = await db.Atividade.FindAsync(id);
            if (atividade == null)
            {
                return HttpNotFound();
            }
            return View(atividade);
        }

        // POST: Atividades/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            Atividade atividade = await db.Atividade.FindAsync(id);
            db.Atividade.Remove(atividade);
            await db.SaveChangesAsync();
            return VoltarParaListagem(atividade);
        }

        //Retorna para a tela principal do Curso
        private RedirectToRouteResult VoltarParaListagem(Atividade atividade)
        {
            try
            {
                Unidade unidade = db.Unidade.Find(atividade.Idunidade);
                return RedirectToAction("Details", "Cursos", new { id = unidade.Idcurso, Idunidade = atividade.Idunidade });
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
    }
}
