using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;

namespace PF.Core.Interfaces.Device.Hardware.Camera.LineScan
{
    /// <summary>
    /// 线阵（线扫）相机。
    ///
    /// <para>与 <see cref="IntelligentCamera.IIntelligentCamera"/> 的区别：智能相机是
    /// "触发一次 → 相机内部跑完算法 → 返回结果字符串"的语义（如基恩士 OCR 相机）；
    /// 线阵相机吐的是**原始图像数据**，算法在上位机侧做，两者不复用同一接口。</para>
    ///
    /// <para>本接口只负责相机本体：参数下发、行触发/编码器配置、开流、收帧。
    /// **不感知任何运动轴**——线扫需要配合轴运动才能扫出完整图像，
    /// 但"轴与相机的时序编排"属于机构层（BaseMechanism）职责，设备层不引用 IAxis。</para>
    ///
    /// <para>取流为异步事件 + 主动等待双模式：连续取流时订阅 <see cref="FrameReceived"/>
    /// （调试面板实时预览用）；单次扫描时用 <see cref="WaitFrameAsync"/> 等待一帧（工站流程用）。</para>
    /// </summary>
    public interface ILineScanCamera : IHardwareDevice, IGenICamNodeAccess
    {
        /// <summary>相机型号名称（连接后可用）。</summary>
        string ModelName { get; }

        /// <summary>相机序列号（连接后可用）。</summary>
        string SerialNumber { get; }

        /// <summary>传输层类型（由枚举结果判定）。</summary>
        CameraTransportLayer TransportLayer { get; }

        /// <summary>
        /// 本相机是否经采集卡取流。
        /// <para>true = 帧长/帧触发下发到采集卡节点树；false = 下发到相机自身节点树。</para>
        /// </summary>
        bool HasFrameGrabber { get; }

        /// <summary>
        /// 当前配置换算出的行间距（微米/行），即图像 Y 向的物理分辨率。
        /// 编码器模式下 = 读数头当量 × 分频系数；未使用编码器时为 0。
        /// </summary>
        double LineSpacingUm { get; }

        /// <summary>当前是否处于取流状态。</summary>
        bool IsGrabbing { get; }

        /// <summary>
        /// 下发完整配置（相机本体参数 + 行触发 + 帧控制）。
        /// <para>配置中为 null 的字段视为"不下发、沿用设备当前值"；
        /// 单个节点下发失败只记警告并继续，不中断整体配置。</para>
        /// </summary>
        Task ApplyConfigAsync(LineScanCameraConfig config, CancellationToken token = default);

        /// <summary>
        /// 开流并武装：开始接收图像，等待帧触发。
        /// <para>必须在轴开始运动**之前**调用，否则会漏掉起始若干行。</para>
        /// </summary>
        Task<bool> ArmAsync(CancellationToken token = default);

        /// <summary>停止取流。</summary>
        Task StopAsync(CancellationToken token = default);

        /// <summary>
        /// 发送一次帧软触发。
        /// <para>经采集卡时转发给采集卡的 Stream 软触发命令；直连时执行相机自身的帧软触发命令。</para>
        /// </summary>
        Task<bool> SoftwareTriggerFrameAsync(CancellationToken token = default);

        /// <summary>
        /// 等待并取走一帧完整图像。
        /// </summary>
        /// <param name="timeoutMs">等待超时（毫秒）。线扫单帧耗时 = 扫描行程 ÷ 扫描速度，需按实际行程放宽。</param>
        /// <param name="token">取消令牌。取消时抛 <see cref="OperationCanceledException"/>。</param>
        /// <returns>一帧图像；超时抛 <see cref="TimeoutException"/>。</returns>
        Task<LineScanFrame> WaitFrameAsync(int timeoutMs, CancellationToken token = default);

        /// <summary>
        /// 收到一帧图像时触发（连续取流预览用）。
        /// <para>⚠️ 在 SDK 回调线程上触发，订阅方不得阻塞，UI 侧需自行切回调度线程。</para>
        /// </summary>
        event EventHandler<LineScanFrame>? FrameReceived;

        /// <summary>最近一帧图像的原始数据（供调试面板显示/存盘，无图时为空数组）。</summary>
        byte[] LastImageData { get; }

        /// <summary>最近一帧图像宽度。</summary>
        int LastImageWidth { get; }

        /// <summary>最近一帧图像高度。</summary>
        int LastImageHeight { get; }

        /// <summary>最近一帧图像的像素格式。</summary>
        ImagePixelFormat LastImagePixelFormat { get; }

        /// <summary>
        /// 将最近一帧图像存盘。
        /// </summary>
        /// <param name="filePath">目标文件完整路径。</param>
        /// <param name="format">存盘格式。</param>
        /// <param name="token">取消令牌。</param>
        /// <returns>true = 存盘成功；无可用图像时返回 false。</returns>
        Task<bool> SaveLastImageAsync(string filePath, ImageFileFormat format, CancellationToken token = default);

        /// <summary>
        /// 枚举当前在线的相机（含未被本实例占用的）。
        /// 供配置阶段挑选设备与调试面板的"扫描在线设备"使用，不影响本实例的连接状态。
        /// </summary>
        Task<IReadOnlyList<DiscoveredDeviceInfo>> DiscoverAsync(CancellationToken token = default);
    }
}
