
using PF.Core.Entities.Base;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Data.Entity
{
    /// <summary>
    /// 参数实体基类
    /// </summary>
    public abstract class ParamEntity : BasicEntity
    {
        /// <summary>
        /// 名称
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 描述
        /// </summary>
        [MaxLength(500)]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// TypeFull名称
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string? TypeFullName { get; set; } = string.Empty;

        /// <summary>
        /// Json值
        /// </summary>
        [Required]
        public string JsonValue { get; set; } = string.Empty;

        /// <summary>
        /// Category
        /// </summary>
        [MaxLength(50)]
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// Version
        /// </summary>
        public int Version { get; set; } = 1;
    }

}
