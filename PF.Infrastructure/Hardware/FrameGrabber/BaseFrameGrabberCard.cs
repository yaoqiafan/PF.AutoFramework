using PF.Core.Constants;
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
        public bool FeatureFileMissing { get; private protected set; }

        /// <summary>
        /// 固定属性文件名（<c>HardwareFeatureFiles\</c> 下），子类可按厂商导出格式覆盖。
        /// 默认对齐海康 MVS 客户端导出格式。
        /// </summary>
        protected virtual string FeatureFileName => "FrameGrabberCard.hcf";

        /// <inheritdoc/>
        public string FeatureFilePath => Path.Combine(ConstGlobalParam.ConfigPath, "HardwareFeatureFiles", FeatureFileName);

        /// <inheritdoc/>
        public Task<bool> ReimportFeatureFileAsync(CancellationToken token = default)
            => Task.Run(AutoImportFeatureFile, token);

        /// <summary>
        /// 从固定路径 <see cref="FeatureFilePath"/> 导入接线属性。文件不存在时只记 Warn 并沿用
        /// 设备当前配置，不视为失败——现场首次上电、还没来得及导出文件时不应阻塞整机初始化。
        ///
        /// <para>连接成功、<see cref="NodeAccessor"/> 赋值后由 <c>InternalConnectAsync</c> 同步调用一次
        /// （此时已运行在 SDK 专用的 Task.Run 线程里，直接调 <see cref="GenICamNodeAccessor.ImportFeatureFile"/>
        /// 即可，无需再嵌套一层 Task.Run）；也可由 <see cref="ReimportFeatureFileAsync"/> 在调试面板
        /// 按需手动重新触发。</para>
        /// </summary>
        /// <returns>是否真的导入了文件（false 表示文件缺失，只是建了占位）。</returns>
        private protected bool AutoImportFeatureFile()
        {
            var acc = NodeAccessor;
            string dir = Path.Combine(ConstGlobalParam.ConfigPath, "HardwareFeatureFiles");
            string path = FeatureFilePath;

            if (acc == null || !File.Exists(path))
            {
                FeatureFileMissing = true;
                FeatureFileScaffold.EnsurePlaceholder(dir, path, DeviceName, HardwareLogger);
                HardwareLogger.Warn($"[{DeviceName}] 属性文件不存在：{path}，跳过导入，沿用设备当前配置。");
                return false;
            }

            FeatureFileMissing = false;
            if (acc.ImportFeatureFile(path))
            {
                HardwareLogger.Success($"[{DeviceName}] 属性文件已导入（逐节点结果见 Hardware 日志）。");
                return true;
            }

            HardwareLogger.Error($"[{DeviceName}] 属性文件导入失败，详见 Hardware 日志。");
            return false;
        }

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

        /// <inheritdoc/>
        public Task<bool> ImportFeatureFileAsync(string filePath, CancellationToken token = default)
        {
            var acc = NodeAccessor;
            if (acc == null) return Task.FromResult(false);

            // 文件里成百上千个节点逐条下发，耗时可到秒级，必须离开调用方线程
            return Task.Run(() => acc.ImportFeatureFile(filePath), token);
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<GenICamNode>> EnumerateNodesAsync(CancellationToken token = default)
        {
            var acc = NodeAccessor;
            if (acc == null) return Task.FromResult<IReadOnlyList<GenICamNode>>(Array.Empty<GenICamNode>());

            // 上千个节点逐个查权限+读值，耗时可到秒级，必须离开 UI 线程
            return Task.Run(() => acc.EnumerateNodes(), token);
        }

        #endregion
    }
}
