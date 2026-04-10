using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PF.Data.Entity.Alarm
{
    /// <summary>
    /// 报警流水记录实体。
    /// 不存储 Message 等描述字段，查询时通过 ErrorCode 联查字典。
    /// 对应的物理表名按年份动态分表（如 AlarmRecord_2026），
    /// 由 <see cref="Context.AlarmDbContext"/> 在 OnModelCreating 中按当前年份路由。
    /// </summary>
    public class AlarmRecordEntity
    {
        /// <summary>自增主键</summary>
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        /// <summary>报警代码（关联字典，不做外键约束以保留独立性）</summary>
        [MaxLength(64)]
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>来源标识（机构名、工站名等）</summary>
        [MaxLength(128)]
        public string Source { get; set; } = string.Empty;

        /// <summary>首次触发时间</summary>
        public DateTime TriggerTime { get; set; }

        /// <summary>清除时间（null = 仍活跃）</summary>
        public DateTime? ClearTime { get; set; }

        /// <summary>是否仍处于活跃状态</summary>
        public bool IsActive { get; set; }
    }
}
