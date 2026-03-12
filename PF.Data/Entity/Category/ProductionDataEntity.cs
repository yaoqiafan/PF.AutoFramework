using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PF.Data.Entity.Category
{
    /// <summary>
    /// 生产过程数据实体。
    /// 继承 ParamEntity 复用 Name / JsonValue / Category / TypeFullName 字段，
    /// 追加设备、类型、批次、采集时间等生产专属字段。
    /// DeviceId / RecordType / BatchId 均为可空字段，不是所有场景都需要填写。
    /// </summary>
    [Table("ProductionData")]
    public class ProductionDataEntity : ParamEntity
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public override string ID { get; set; } = Guid.NewGuid().ToString();

        /// <summary>设备标识（可为空，按需使用）</summary>
        [MaxLength(100)]
        public string? DeviceId { get; set; }

        /// <summary>记录类型，用于分类过滤（可为空，按需使用）</summary>
        [MaxLength(100)]
        public string? RecordType { get; set; }

        /// <summary>数据采集时间（区别于 CreateTime 数据库写入时间）</summary>
        public DateTime RecordTime { get; set; } = DateTime.Now;

        /// <summary>批次/Lot 编号（可为空，按需使用）</summary>
        [MaxLength(100)]
        public string? BatchId { get; set; }
    }
}
