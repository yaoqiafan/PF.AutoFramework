using PF.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace PF.Data.Entity.Alarm
{
    /// <summary>
    /// 报警字典实体（持久化表：AlarmDefinitions）。
    /// 用于存储由实施人员在数据库中扩展或覆盖的报警规则。
    /// 数据库条目优先级高于代码内置的 AlarmInfoAttribute 定义。
    /// </summary>
    public class AlarmDefinitionEntity
    {
        /// <summary>报警代码（主键，如 "HW_SRV_001"）</summary>
        [Key]
        [MaxLength(64)]
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>报警分类</summary>
        [MaxLength(64)]
        public string Category { get; set; } = string.Empty;

        /// <summary>报警描述文本</summary>
        [MaxLength(512)]
        public string Message { get; set; } = string.Empty;

        /// <summary>严重程度</summary>
        public AlarmSeverity Severity { get; set; }

        /// <summary>排故 SOP 指导文本（支持换行符 \n）</summary>
        [MaxLength(4096)]
        public string Solution { get; set; } = string.Empty;
    }
}
