﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Web;

namespace STV.Models
{
    public class Instrutor : Usuario
    {
        public virtual ICollection<Curso> Cursos { get; set; }

    }
}