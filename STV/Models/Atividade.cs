﻿namespace STV.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Atividade")]
    public partial class Atividade
    {
        [Key]
        public int Idatividade { get; set; }

        public int Idunidade { get; set; }

        public string Descricao { get; set; }

        public int Valor { get; set; }

        public virtual Unidade Unidade { get; set; }

        public virtual ICollection<Questao> Questoes { get; set; }

        public virtual ICollection<Nota> Notas { get; set; }

        [NotMapped]
        public Questao QuestaoToShow { get; set; }

        public decimal PorcentagemQuestao
        {
            get
            {
                if (Questoes != null)
                    return (decimal)100 / Questoes.Count;
                else
                    return 0;
            }
        }

        [NotMapped]
        public decimal Realizado { get; set; } = 0;

    }
}