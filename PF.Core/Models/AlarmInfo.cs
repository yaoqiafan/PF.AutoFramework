using PF.Core.Enums;

namespace PF.Core.Models
{
    /// <summary>
    /// 报警字典条目（内存模型）。
    /// 来源：反射扫描 <c>[AlarmInfo]</c> 常量；其中 <see cref="RequiredLevel"/> 以数据库配置为准，其余字段以代码为准。
    /// </summary>
    public sealed class AlarmInfo
    {
        /// <summary>报警代码（唯一键，如 "HW_SRV_001"）</summary>
        public string ErrorCode { get; init; } = string.Empty;

        /// <summary>报警分类</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>报警描述文本（中文）</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>报警描述文本（英文，SECS/GEM 上传用）</summary>
        public string MessageEn { get; init; } = string.Empty;

        /// <summary>严重程度</summary>
        public AlarmSeverity Severity { get; init; }

        /// <summary>排故 SOP 指导文本</summary>
        public string Solution { get; init; } = string.Empty;

        /// <summary>图片路径，如果有</summary>
        public string? ImagePath { get; init; } = string.Empty;

        /// <summary>
        /// 已废弃：报警字典不再从数据库合并加载扩展报警码，恒为 false。
        /// </summary>
        [Obsolete("报警字典不再从数据库合并加载扩展报警码，该属性恒为 false。")]
        public bool IsFromDatabase { get; init; }

        /// <summary>手动清除/复位该报警所需的最低用户等级（当前生效值，来自数据库配置）</summary>
        public UserLevel RequiredLevel { get; init; } = UserLevel.Operator;

        /// <summary>代码中 <c>[AlarmInfo(ClearLevel = ...)]</c> 定义的默认等级，用于"恢复默认"</summary>
        public UserLevel DefaultRequiredLevel { get; init; } = UserLevel.Operator;

        /// <summary>
        /// 报警信息ID(用于上传SECSGEM)
        /// </summary>
        public int MessageID { get; init; }

        /// <summary>
        /// 报警信息英文文本(用于上传SECSGEM)，最长40 位。
        /// </summary>
        public string MessageIDHex { get; init; } = string.Empty;

        /// <summary>复制当前条目并替换生效等级（字典运行时更新等级用，AlarmInfo 本身不可变）</summary>
        public AlarmInfo WithRequiredLevel(UserLevel level) => new()
        {
            ErrorCode            = ErrorCode,
            Category             = Category,
            Message              = Message,
            MessageEn            = MessageEn,
            Severity             = Severity,
            Solution             = Solution,
            ImagePath            = ImagePath,
            MessageID            = MessageID,
            MessageIDHex         = MessageIDHex,
            RequiredLevel        = level,
            DefaultRequiredLevel = DefaultRequiredLevel,
        };
    }
}
