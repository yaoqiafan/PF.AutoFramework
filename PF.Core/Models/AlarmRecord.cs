using PF.Core.Enums;

namespace PF.Core.Models
{
    /// <summary>
    /// 报警流水记录（内存/UI 模型）。
    /// 由 <c>IAlarmService</c> 维护并向 UI 层暴露，包含从字典反查的描述字段。
    /// </summary>
    public sealed class AlarmRecord
    {
        /// <summary>数据库主键（0 = 尚未落盘）</summary>
        public long Id { get; set; }

        /// <summary>报警代码</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>来源标识（如机构名、工站名）</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>首次触发时间</summary>
        public DateTime TriggerTime { get; set; }

        /// <summary>清除时间（null = 仍处于活跃状态）</summary>
        public DateTime? ClearTime { get; set; }

        /// <summary>是否仍处于活跃状态</summary>
        public bool IsActive { get; set; }

        // ── 从字典反查，供 UI 直接绑定 ────────────────────────────────────────

        /// <summary>报警分类</summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>报警描述文本</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>严重程度</summary>
        public AlarmSeverity Severity { get; set; }

        /// <summary>排故 SOP 指导文本</summary>
        public string Solution { get; set; } = string.Empty;

        /// <summary>严重程度显示名（供 DataGrid 列绑定）</summary>
        public string SeverityDisplay => Severity switch
        {
            AlarmSeverity.Information => "信息",
            AlarmSeverity.Warning     => "警告",
            AlarmSeverity.Error       => "错误",
            AlarmSeverity.Fatal       => "致命",
            _                         => Severity.ToString()
        };
    }
}
