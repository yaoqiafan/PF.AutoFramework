using PF.Core.Interfaces.Data;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PF.SecsGem.DataBase.Entities.Basic
{
    /// <summary>
    /// 实体抽象基类
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
