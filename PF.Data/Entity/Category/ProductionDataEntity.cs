using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PF.Data.Entity.Category
{
    /// <summary>
    /// 生产过程数据实体。
    /// 独立实体，不继承 ParamEntity，仅保留生产数据记录必要字段。
    /// </summary>
    [Table("ProductionData")]
    public class ProductionDataEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public string ID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>JSON 序列化数据</summary>
        public string JsonValue { get; set; } = string.Empty;

        /// <summary>数据类型全名（用于反序列化）</summary>
        [MaxLength(200)]
        public string? TypeFullName { get; set; }

        /// <summary>记录类型（用于分类过滤）</summary>
        [MaxLength(100)]
        public string? RecordType { get; set; }

        /// <summary>数据采集时间</summary>
        public DateTime RecordTime { get; set; } = DateTime.Now;

        /// <summary>记录创建时间（数据库写入时间）</summary>
        public DateTime CreateTime { get; set; } = DateTime.Now;
    }
}
