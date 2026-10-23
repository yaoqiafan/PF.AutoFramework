using PF.Core.Enums;
using System.ComponentModel.DataAnnotations;

namespace PF.Data.Entity.Alarm
{
    /// <summary>
    /// 报警字典实体（持久化表：AlarmDefinitions）。
    /// 由程序启动时从代码 <c>[AlarmInfo]</c> 同步写入（同默认参数）：
    /// <see cref="RequiredLevel"/> 以数据库为准（可在「异常权限配置」页面修改），
    /// 其余文案字段每次启动按代码刷新，代码已删除的报警码会被移除。
    /// 数据库条目不再覆盖代码定义的报警文案（已取消扩展报警码合并加载）。
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

        /// <summary>
        /// 手动清除/复位该报警所需的最低用户等级（以数据库为准）。
        /// 老库没有此列时由 AlarmDictionaryService 启动时幂等补列，默认 0 = Operator。
        /// </summary>
        public UserLevel RequiredLevel { get; set; } = UserLevel.Operator;
    }
}
