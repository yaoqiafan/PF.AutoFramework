using PF.Core.Enums;

namespace PF.Core.Models
{
    /// <summary>
    /// 报警字典条目（内存模型）。
    /// 来源：反射扫描 <c>AlarmCodes</c> 常量 + 数据库扩展表，两者合并后缓存。
    /// </summary>
    public sealed class AlarmInfo
    {
        /// <summary>报警代码（唯一键，如 "HW_SRV_001"）</summary>
        public string ErrorCode { get; init; } = string.Empty;

        /// <summary>报警分类</summary>
        public string Category { get; init; } = string.Empty;

        /// <summary>报警描述文本</summary>
        public string Message { get; init; } = string.Empty;

        /// <summary>严重程度</summary>
        public AlarmSeverity Severity { get; init; }

        /// <summary>排故 SOP 指导文本</summary>
        public string Solution { get; init; } = string.Empty;

        /// <summary>图片路径，如果有</summary>
        public string? ImagePath { get; init; } = string.Empty;

        /// <summary>
        /// 是否来自数据库扩展（true = 数据库定义，可覆盖代码内置；false = 代码内置）
        /// </summary>
        public bool IsFromDatabase { get; init; }
    }
}
