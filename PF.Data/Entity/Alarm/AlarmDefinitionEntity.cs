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

        /// <summary>报警描述文本（中文）</summary>
        [MaxLength(512)]
        public string Message { get; set; } = string.Empty;

        /// <summary>报警描述文本（英文，SECS/GEM 上传用）</summary>
        [MaxLength(35)]
        public string MessageEn { get; set; } = string.Empty;

        /// <summary>严重程度</summary>
        public AlarmSeverity Severity { get; set; }

        /// <summary>排故 SOP 指导文本（支持换行符 \n）</summary>
        [MaxLength(4096)]
        public string Solution { get; set; } = string.Empty;

        /// <summary>报警信息ID(用于上传SECSGEM)</summary>
        public int MessageID { get; set; }

        /// <summary>报警信息英文文本(用于上传SECSGEM)，最长40 位</summary>
        [MaxLength(40)]
        public string MessageIDHex { get; set; } = string.Empty;
    }
}
