﻿namespace STV.Models
{

    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Questao")]
    public partial class Questao
    {
        public int Idquestao { get; set; }

        public int Idatividade { get; set; }

        [ForeignKey("AlternativaCorreta")]
        public int? IdalternativaCorreta { get; set; }

        [Display(Name = "Enunciado")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Este campo é obrigatório")]
        [StringLength(1000, ErrorMessage = "Este campo suporta no máximo 1000 caracteres")]
        [DataType(DataType.MultilineText)]
        public string Descricao { get; set; }

        [Display(Name = "Número")]
        public int? Numero { get; set; }

        public virtual Atividade Atividade { get; set; }

        public virtual Alternativa AlternativaCorreta { get; set; }

        public virtual ICollection<Alternativa> Alternativas { get; set; }

        [NotMapped]
        public int IdAlternativaSelecionada { get; set; }

        [NotMapped]
        public int? Indice { get; set; }

        public virtual ICollection<Resposta> Respostas { get; set; }

    }
}