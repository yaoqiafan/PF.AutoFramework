namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 安全门状态快照（只读）。
    /// </summary>
    /// <remarks>
    /// 构造
    /// </remarks>
    /// <param name="name"></param>
    /// <param name="isEnabled"></param>
    /// <param name="isMuted"></param>
    /// <param name="signalValue"></param>
    /// <param name="isActive"></param>
    public sealed class SafetyDoorState(string name, bool isEnabled, bool isMuted,
        bool? signalValue, bool? isActive)
    {

        /// <summary>可读名称，如"电磁门锁1_2信号"。</summary>
        public string Name { get; } = name;

        /// <summary>是否启用（运行时业务控制）。</summary>
        public bool IsEnabled { get; } = isEnabled;

        /// <summary>是否屏蔽（调试参数，持久化到数据库）。</summary>
        public bool IsMuted { get; } = isMuted;

        /// <summary>当前 IO 信号原始值，null 表示无法读取。</summary>
        public bool? SignalValue { get; } = signalValue;

        /// <summary>
        /// 当前是否处于激活态（信号值 == NormallyOpen）。
        /// null 表示无法判定（SignalValue 为 null）。
        /// </summary>
        public bool? IsActive { get; } = isActive;

    }
}
