using PF.Core.Entities.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Vision.Hikvision;

namespace PF.Infrastructure.Hardware.FrameGrabber
{
    /// <summary>
    /// 图像采集卡基类。与 <see cref="Card.BaseMotionCard"/> 并列，同为顶级板卡类设备
    /// （HardwareConfig.ParentDeviceId 为空，第 1 层初始化），相机作为子设备挂在其下。
    ///
    /// <para>本基类负责 GenICam 节点通道的统一转发；具体的枚举/打开/参数下发由厂商子类实现。</para>
    /// </summary>
    public abstract class BaseFrameGrabberCard : BaseDevice, IFrameGrabberCard
    {
        /// <summary>构造采集卡。</summary>
        protected BaseFrameGrabberCard(string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger)
        {
            Category = Core.Enums.HardwareCategory.FrameGrabber;
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
        public abstract Task ApplyFrameControlAsync(FrameControlConfig config, CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<bool> SoftwareTriggerFrameAsync(CancellationToken token = default);

        /// <inheritdoc/>
        public abstract Task<IReadOnlyList<DiscoveredDeviceInfo>> DiscoverCamerasAsync(CancellationToken token = default);

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
