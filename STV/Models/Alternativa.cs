﻿namespace STV.Models
{
    using System.ComponentModel.DataAnnotations;

    public partial class Alternativa
    {

        public int Idalternativa { get; set; }

        public int Idquestao { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Este campo é obrigatório")]
        [StringLength(1000)]
        [Display(Name = "Descrição")]
        public string Descricao { get; set; }

        [StringLength(1000)]
        public string Justificativa { get; set; }

        public virtual Questao Questao { get; set; }


    }
}