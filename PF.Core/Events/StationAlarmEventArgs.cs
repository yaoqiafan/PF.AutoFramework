using System;

namespace PF.Core.Events
{
    /// <summary>
    /// 工站级报警事件参数，携带丰富上下文信息。
    /// 从工站传播到主控及更上层，保留设备名、运行时消息和原始异常。
    /// </summary>
    public class StationAlarmEventArgs : EventArgs
    {
        /// <summary>结构化报警码（如 "HW_SRV_001"）</summary>
        public string ErrorCode { get; init; } = string.Empty;

        /// <summary>
        /// 运行时动态消息（如 "X轴位置偏差 3.5mm"）。
        /// 非空时覆盖静态字典描述，用于活跃报警展示；历史查询仍回落到字典描述。
        /// </summary>
        public string? RuntimeMessage { get; init; }

        /// <summary>
        /// 触发报警的硬件组件名称（如 "X轴伺服"）。
        /// 软件/逻辑级报警可为 null。
        /// </summary>
        public string? HardwareName { get; init; }

        /// <summary>机构层的原始异常，便于日志追溯</summary>
        public Exception? InternalException { get; init; }
    }
}
