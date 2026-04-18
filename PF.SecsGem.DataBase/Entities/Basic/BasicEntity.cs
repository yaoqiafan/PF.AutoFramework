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
        /// <summary>
        /// 标识
        /// </summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public abstract string ID { get; set; }

        /// <summary>
        /// Create时间
        /// </summary>
        [Required]
        public DateTime CreateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Update时间
        /// </summary>
        [Required]
        public DateTime UpdateTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Remarks
        /// </summary>
        public string? Remarks { get; set; }
    }
}
