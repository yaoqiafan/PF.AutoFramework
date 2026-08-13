using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Camera.LineScan;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Vision.Hikvision;

namespace PF.Infrastructure.Hardware.Camera.LineScan
{
    /// <summary>
    /// 线阵（线扫）相机基类。
    ///
    /// <para>继承 <see cref="AttachedDeviceBase{TParent}"/> 并把父设备类型定为
    /// <see cref="IFrameGrabberCard"/>：挂了采集卡即为 CameraLink/CXP/XoF 链路，
    /// 没挂即为 GigE/USB 直连。**配置拓扑本身就表达了链路类型**，无需额外的链路开关字段。</para>
    ///
    /// <para>与 <see cref="IntelligentCamera.BaseIntelligentCamera"/> 并列但不复用：
    /// 智能相机返回结果字符串，线阵相机返回原始图像。</para>
    ///
    /// <para>本基类不引用任何运动轴。线扫必须配合轴运动才能扫出完整图像，
    /// 但那属于机构层的时序编排职责。</para>
    /// </summary>
    public abstract class BaseLineScanCamera : AttachedDeviceBase<IFrameGrabberCard>, ILineScanCamera
    {
        /// <summary>构造线阵相机。</summary>
        protected BaseLineScanCamera(string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.Camera;
        }

        /// <summary>
        /// GenICam 节点访问器，由子类在连接成功后赋值、断开时置空。
        /// 未连接时为 null，此时所有节点操作静默返回失败。
        /// </summary>
        private protected GenICamNodeAccessor? NodeAccessor { get; set; }

        /// <inheritdoc/>
        public abstract string ModelName { get; }

        /// <inheritdoc/>
        public abstract string SerialNumber { get; }

        /// <inheritdoc/>
        public abstract CameraTransportLayer TransportLayer { get; }

        /// <summary>
        /// 是否经采集卡取流：挂载了采集卡父设备即为 true。
        /// </summary>
        public bool HasFrameGrabber => Parent != null;

        /// <inheritdoc/>
        public abstract double LineSpacingUm { get; }

        /// <inheritdoc/>
        public abstract bool IsGrabbing { get; }

        /// <inheritdoc/>
        public abstract Task ApplyConfigAsync(LineScanCameraConfig config, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> ArmAsync(CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task StopAsync(CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> SoftwareTriggerFrameAsync(CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<LineScanFrame> WaitFrameAsync(int timeoutMs, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> SaveLastImageAsync(string filePath, ImageFileFormat format, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<IReadOnlyList<DiscoveredDeviceInfo>> DiscoverAsync(CancellationToken token = default);

        #region 最近一帧图像

        /// <summary>最近一帧图像的原始数据。对齐 BaseBarcodeScan 的做法，供调试面板直接取用。</summary>
        public byte[] LastImageData { get; protected set; } = Array.Empty<byte>();

        /// <summary>最近一帧图像宽度。</summary>
        public int LastImageWidth { get; protected set; }

        /// <summary>最近一帧图像高度。</summary>
        public int LastImageHeight { get; protected set; }

        /// <summary>最近一帧图像的像素格式。</summary>
        public ImagePixelFormat LastImagePixelFormat { get; protected set; } = ImagePixelFormat.Unknown;

        /// <inheritdoc/>
        public event EventHandler<LineScanFrame>? FrameReceived;

        /// <summary>
        /// 由子类在收到一帧图像后调用：更新"最近一帧"缓存并广播事件。
        /// <para>⚠️ 通常运行在 SDK 回调线程上，订阅方不得阻塞。事件订阅方抛出的异常
        /// 会被吞掉并记警告，避免单个订阅者（如 UI）把 SDK 回调线程带崩。</para>
        /// </summary>
        protected void OnFrameReceived(LineScanFrame frame)
        {
            if (frame == null) return;

            LastImageData = frame.Data;
            LastImageWidth = frame.Width;
            LastImageHeight = frame.Height;
            LastImagePixelFormat = frame.PixelFormat;

            try
            {
                FrameReceived?.Invoke(this, frame);
            }
            catch (Exception ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] 帧事件订阅方处理异常：{ex.Message}", ex);
            }
        }

        #endregion

        #region IGenICamNodeAccess

        /// <inheritdoc/>
        public Task<bool> IsNodeAvailableAsync(string nodeName, CancellationToken token = default)
            => Task.FromResult(NodeAccessor?.IsNodeAvailable(nodeName) ?? false);

        /// <inheritdoc/>
        public Task<string?> GetNodeAsync(string nodeName, CancellationToken token = default)
            => Task.FromResult(NodeAccessor?.GetNode(nodeName));

        /// <inheritdoc/>
        public Task<bool> SetNodeAsync(string nodeName, string value, CancellationToken token = default)
            => Task.FromResult(NodeAccessor?.SetNode(nodeName, value) ?? false);

        /// <inheritdoc/>
        public Task<IReadOnlyList<string>> GetEnumEntriesAsync(string nodeName, CancellationToken token = default)
            => Task.FromResult(NodeAccessor?.GetEnumEntries(nodeName) ?? Array.Empty<string>());

        /// <inheritdoc/>
        public Task<bool> ExecuteCommandAsync(string nodeName, CancellationToken token = default)
            => Task.FromResult(NodeAccessor?.ExecuteCommand(nodeName) ?? false);

        #endregion
    }
}
