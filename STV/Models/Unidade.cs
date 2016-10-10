﻿namespace STV.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("Unidade")]
    public partial class Unidade
    {
        public int Idunidade { get; set; }

        public int Idcurso { get; set; }

        [Display(Name = "Título")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Este campo é obrigatório")]
        [StringLength(100, ErrorMessage = "Este campo suporta até 100 caracteres")]
        public string Titulo { get; set; }

        [Display(Name = "Data Abertura")]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        [Column(TypeName = "date")]
        [DataType(DataType.Date, ErrorMessage = "Data em formato inválido")]
        public DateTime? DataAbertura { get; set; }

        public bool Encerrada { get; set; }

        [Display(Name = "Data de Criação")]
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:dd/MM/yyyy}")]
        [Column(TypeName = "date")]
        public DateTime Stamp { get; set; }

        public virtual Curso Curso { get; set; }

        public virtual ICollection<Atividade> Atividades { get; set; }

        public virtual ICollection<Material> Materiais { get; set; }

    }

}