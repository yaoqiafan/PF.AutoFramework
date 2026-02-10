using PF.Core.Interfaces.Data;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Base
{
    /// <summary>
    /// 实体抽象类
    /// </summary>
    public abstract class BasicEntity : IEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public abstract string ID { get; set; }

        [Required]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        [Required]
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        public string? Remarks { get; set; }

    }
}
