﻿using AutoMapper;
using STV.Auth;
using STV.DAL;
using STV.Models;
using STV.Models.Validation;
using STV.ViewModels;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace STV.Controllers
{
    [Authorize]
    [HandleError]
    public class AtividadesController : Controller
    {
        private STVDbContext db = new STVDbContext();

        private Usuario UsuarioLogado;

        public AtividadesController()
        {
            SessionContext auth = new SessionContext();
            UsuarioLogado = auth.GetUserData();
        }

        public async Task<ActionResult> Responder(int? id, int? idProxima, bool finalizar = false)
        {
            try
            {
                if (id == null)
                    throw new ApplicationException("Ops! Requisição inválida.");

                Atividade atividade = await db.Atividade
                    .Where(a => a.Idatividade == id)
                    .SingleOrDefaultAsync();

                if (!AtividadeValidation.CanDo(atividade, UsuarioLogado.Idusuario, User))
                    throw new UnauthorizedAccessException("Não Autorizado");

                var AtividadeModel = Mapper.Map<Atividade, AtividadeVM>(atividade);
                AtividadeModel.Questoes = AtividadeModel.Questoes.Where(q => q.Alternativas.Count() > 0 && q.IdalternativaCorreta != null).ToList();

                if (idProxima != null)
                {
                    AtividadeModel.QuestaoToShow = AtividadeModel.Questoes.Where(q => q.Idquestao == idProxima).FirstOrDefault();
                    Response.StatusCode = (int)HttpStatusCode.OK;
                    return PartialView("Questao", AtividadeModel);
                }
                else
                {
                    if (finalizar)
                    {
                        TempData["msg"] = "Respostas salvas!";
                        return RedirectToAction("Details", "Cursos", new { id = atividade.Unidade.Idcurso, Idunidade = atividade.Idunidade });
                    }
                    AtividadeModel.QuestaoToShow = AtividadeModel.Questoes.FirstOrDefault();
                }

                return View("Atividade", AtividadeModel);
            }
            catch (ApplicationException ex)
            {
                Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                return Json(ex.Message);
            }
        }

        public async Task<ActionResult> Revisao(int? id, int? index)
        {
            Atividade atividade = null;
            try
            {
                if (id == null)
                    throw new Exception("Ops! Requisição inválida.");

                atividade = await db.Atividade
                    .Where(a => a.Idatividade == id)
                    .SingleOrDefaultAsync();

                if (atividade == null)
                    throw new Exception("Atividade não encontrada.");

                if (!CommonValidation.CanSee(atividade.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    throw new UnauthorizedAccessException("Não Autorizado");
                
                var AtividadeModel = Mapper.Map<Atividade, AtividadeVM>(atividade);
                AtividadeModel.IsRevisao = true;

                Resposta resp;
                foreach(var questao in AtividadeModel.Questoes)
                {
                    resp = db.Resposta.Find(UsuarioLogado.Idusuario, questao.Idquestao);
                    if (resp != null) questao.IdAlternativaSelecionada = resp.Idalternativa;
                }

                return View("Revisao", AtividadeModel);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Details", "Cursos", new { id = atividade.Unidade.Idcurso, Idunidade = atividade.Idunidade });
            }
            catch (Exception ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SalvarResposta(AtividadeVM atividade, int? idProxima, bool finalizar = false)
        {

            Resposta resposta = null;
            resposta = await db.Resposta.FindAsync(UsuarioLogado.Idusuario, atividade.QuestaoToShow.Idquestao);

            //Ainda não respondeu
            if (resposta != null)
            {
                resposta.Idalternativa = atividade.QuestaoToShow.IdAlternativaSelecionada;
                db.Resposta.Attach(resposta);
                db.Entry(resposta).Property(r => r.Idalternativa).IsModified = true;
            }
            else
            {
                //Não respondeu mas quer voltar
                if (atividade.QuestaoToShow.IdAlternativaSelecionada == 0 && !finalizar)
                    return RedirectToAction("Responder", new { Id = atividade.Idatividade, idProxima = idProxima });
                //Não respondeu mas quer mudar de questão
                else if (atividade.QuestaoToShow.IdAlternativaSelecionada == 0)
                    return RedirectToAction("Responder", new { Id = atividade.Idatividade, idProxima = idProxima, finalizar = finalizar });
                //Respondeu
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
            }
            await db.SaveChangesAsync();

            return RedirectToAction("Responder", new { Id = atividade.Idatividade, idProxima = idProxima, finalizar = finalizar });
        }

        public async Task<ActionResult> Finalizar(int? id)
        {
            try
            {
                if (id == null)
                    throw new ApplicationException("Ops! Requisição inválida.");

                Atividade atividade = await db.Atividade
                    .Where(a => a.Idatividade == id)
                    .SingleOrDefaultAsync();

                if (!AtividadeValidation.CanDo(atividade, UsuarioLogado.Idusuario, User))
                    throw new UnauthorizedAccessException("Não Autorizado");

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
                TempData["msg"] = "Atividade Finalizada! Aguarde o encerramento para verificar o gabarito.";

                return RedirectToAction("Details", "Cursos", new { id = atividade.Unidade.Idcurso, Idunidade = atividade.Idunidade });

            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Atividades/Details/5
        public async Task<ActionResult> Details(int? id, int? Idquestao)
        {
            try
            {
                if (id == null)
                    throw new HttpRequestValidationException("Ops! Requisição inválida.");

                Atividade atividade = await db.Atividade.FindAsync(id);

                if (atividade == null)
                    throw new KeyNotFoundException("Atividade não encontrada.");

                if (!CommonValidation.CanSee(atividade.Unidade.Curso, UsuarioLogado.Idusuario, User))
                    throw new UnauthorizedAccessException("Não Autorizado");

                ViewBag.MensagemSucesso = TempData["msg"];
                ViewBag.MensagemErro = TempData["msgErr"];
                TempData.Clear();
                ViewBag.QuestaoSelecionada = Idquestao;

                return View(atividade);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return RedirectToAction("Index", "Home");
            }
        }

        // GET: Atividades/Create
        public ActionResult Create(int? Idunidade)
        {
            try
            {
                if (Idunidade == null)
                    throw new HttpRequestValidationException("Ops! Requisição inválida.");

                var unidade = db.Unidade.Find(Idunidade);
                if (unidade == null)
                    throw new KeyNotFoundException("Unidade não encontrada.");

                Atividade atividade = new Atividade();
                atividade.Idunidade = (int)Idunidade;
                atividade.Unidade = unidade;

                AtividadeValidation.CanEdit(atividade, UsuarioLogado.Idusuario, User);

                return View(atividade);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(Idunidade);
            }
        }

        private void ValidarDatas(ref Atividade atv)
        {
            List<string> erros = new List<string>();

            if (atv.DataAbertura > atv.DataEncerramento)
                erros.Add("Data de abertura não pode ser posterior à data de encerramento.");

            var dataAberturaUnidade = db.Unidade.Find(atv.Idunidade).DataAbertura;
            if (dataAberturaUnidade != null && dataAberturaUnidade.HasValue)
            {
                if (atv.DataAbertura < dataAberturaUnidade)
                    erros.Add("Data de abertura não pode ser anterior à data de abertura da unidade que é " + dataAberturaUnidade.Value.ToShortDateString());
            }

            if (CommonValidation.Encerrada(atv.DataEncerramento))
                erros.Add("Data de encerramento não pode ser anterior à data atual.");

            if (atv.DataAbertura < DateTime.Now)
                erros.Add("Data de abertura não pode ser anterior à data atual.");

            AddErrors(erros);
        }

        private void AddErrors(List<string> erros)
        {
            foreach (var error in erros)
            {
                ModelState.AddModelError("", error);
            }
        }

        // POST: Atividades/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Idatividade,Idunidade,Descricao,Valor,DataAbertura,DataEncerramento")] Atividade atividade)
        {
            ValidarDatas(ref atividade);

            if (ModelState.IsValid)
            {
                db.Atividade.Add(atividade);
                await db.SaveChangesAsync();
                TempData["msg"] = "Dados salvos!";
                return VoltarParaListagem(atividade.Idunidade);
            }

            atividade.Idunidade = atividade.Idunidade;
            atividade.Unidade = db.Unidade.Find(atividade.Idunidade);
            ViewBag.Idunidade = new SelectList(db.Unidade, "Idunidade", "Titulo", atividade.Idunidade);
            return View(atividade);
        }

        // GET: Atividades/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            Atividade atividade = null;
            try
            {
                if (id == null)
                    throw new HttpRequestValidationException("Requisição inválida");

                atividade = await db.Atividade.FindAsync(id);
                AtividadeValidation.CanEdit(atividade, UsuarioLogado.Idusuario, User);

                ViewBag.Idunidade = new SelectList(db.Unidade, "Idunidade", "Titulo", atividade.Idunidade);
                var atividadeVM = Mapper.Map<Atividade, AtividadeVM>(atividade);
                return View(atividadeVM);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(atividade.Idunidade);
            }
        }

        // POST: Atividades/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Idatividade,Idunidade,Descricao,Valor,DataAbertura,DataEncerramento")] AtividadeVM atividadeVM)
        {
            var atividade = Mapper.Map<AtividadeVM, Atividade>(atividadeVM);

            ValidarDatas(ref atividade);

            if (ModelState.IsValid)
            {
                db.Entry(atividade).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["msg"] = "Dados salvos!";
                return VoltarParaListagem(atividade.Idunidade);
            }

            var atividadeR = db.Atividade.Find(atividade.Idatividade);
            atividadeVM = Mapper.Map<Atividade, AtividadeVM>(atividadeR);
            return View(atividadeVM);
        }

        // GET: Atividades/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            Atividade atividade = null;
            try
            {
                if (id == null)
                    throw new Exception("Ops! Requisição inválida.");

                atividade = await db.Atividade.FindAsync(id);

                AtividadeValidation.CanDelete(atividade, UsuarioLogado.Idusuario, User);

                return View(atividade);
            }
            catch (ApplicationException ex)
            {
                TempData["msgErr"] = ex.Message;
                return VoltarParaListagem(atividade.Idunidade);
            }
        }

        // POST: Atividades/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            Atividade atividade = await db.Atividade.FindAsync(id);
            try
            {
                db.Entry(atividade).Collection("Questoes").Load(); //Para remover também a referência
                db.Atividade.Remove(atividade);
                await db.SaveChangesAsync();
                TempData["msg"] = "Atividade excluída!";
                return VoltarParaListagem(atividade.Idunidade);
            }
            catch (Exception)
            {
                TempData["msgErr"] = "Atividade não pode ser excluída.";
                return RedirectToAction("Details", "Cursos", new { id = atividade.Unidade.Idcurso, Idunidade = atividade.Unidade.Idunidade });
            }
        }

        //Retorna para a tela principal do Curso
        private RedirectToRouteResult VoltarParaListagem(int? Idunidade)
        {
            Unidade unidade = db.Unidade.Find(Idunidade);
            return RedirectToAction("Details", "Cursos", new { id = unidade.Idcurso, Idunidade = Idunidade });
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
