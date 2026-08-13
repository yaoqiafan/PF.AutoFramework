using PF.Core.Entities.Hardware.Vision;

namespace PF.Core.Interfaces.Device.Hardware.Card
{
    /// <summary>
    /// 图像采集卡（CameraLink / CoaXPress / XoF）。与 <see cref="IMotionCard"/> 并列，
    /// 同为"可作为父设备被子设备挂载"的板卡类设备。
    ///
    /// <para>职责边界：采集卡负责**流的切帧**——一帧累计多少行、从哪一刻开始收这一帧、
    /// 收不满时何时超时出图。相机本体只负责逐行曝光（行触发/编码器始终配在相机上）。</para>
    ///
    /// <para>拓扑约定：采集卡的 HardwareConfig.ParentDeviceId 为空（顶级设备，第 1 层初始化），
    /// 挂在其下的相机把 ParentDeviceId 填为本卡的 DeviceId（第 2 层），
    /// 由 HardwareManagerService 保证"卡先于相机"的初始化顺序并完成挂载注入。</para>
    ///
    /// <para>相机若不挂采集卡（HardwareConfig.ParentDeviceId 为空），则为 GigE/USB 直连形态，
    /// 帧控制回落到相机自身的节点树。配置拓扑本身即表达了链路类型，无需额外的链路开关。</para>
    /// </summary>
    public interface IFrameGrabberCard : IHardwareDevice, IGenICamNodeAccess
    {
        /// <summary>采集卡型号名称（连接后可用，未连接时为空）。</summary>
        string ModelName { get; }

        /// <summary>采集卡序列号（连接后可用，未连接时为空）。</summary>
        string SerialNumber { get; }

        /// <summary>
        /// 下发帧控制配置（帧长、帧超时、帧触发、残帧策略等）。
        /// <para>配置中为 null / 小于等于 0 的字段视为"不下发、沿用当前值"。
        /// 单个节点下发失败只记警告并继续，不中断整体配置。</para>
        /// </summary>
        Task ApplyFrameControlAsync(FrameControlConfig config, CancellationToken token = default);

        /// <summary>
        /// 发送一次帧软触发（对应 StreamSoftwareTrigger / QuickSoftwareTrigger0 命令）。
        /// <para>具体使用哪条命令由最近一次 <see cref="ApplyFrameControlAsync"/> 下发的触发源决定。</para>
        /// </summary>
        Task<bool> SoftwareTriggerFrameAsync(CancellationToken token = default);

        /// <summary>枚举挂在本卡上的相机（供配置阶段挑选设备、调试面板展示）。</summary>
        Task<IReadOnlyList<DiscoveredDeviceInfo>> DiscoverCamerasAsync(CancellationToken token = default);
    }
}
