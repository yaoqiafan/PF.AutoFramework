using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.TowerLight;
using System.Collections.Concurrent;

namespace PF.Services.Hardware
{
    /// <summary>
    /// ITowerLightDoWriter 实现：通过 IHardwareManagerService 获取 IO 板卡，
    /// 根据 ITowerLightDoWriterConfig 将逻辑 tag 解析为端口索引后调用 WriteOutput。
    /// <list type="bullet">
    ///   <item>记住每个 tag 最近一次期望的电平；IO 板卡重新连上（掉线恢复、热重载重建）后按期望重写一遍，
    ///     否则常亮的灯在板卡复位后一直是灭的，直到下一次状态变化。</item>
    ///   <item>板卡不可用时的警告只在"可用 → 不可用"时记一次：闪烁每 0.5 s 写一次，逐次告警会刷屏。</item>
    /// </list>
    /// </summary>
    public class TowerLightDoWriter : ITowerLightDoWriter
    {
        private readonly IHardwareManagerService _hardwareManager;
        private readonly ITowerLightDoWriterConfig _config;
        private readonly ILogService _logger;

        private readonly ConcurrentDictionary<string, bool> _desired = new();
        private IHardwareDevice? _watchedDevice;
        private readonly object _watchLock = new();
        private volatile bool _unavailableWarned;

        /// <summary>构造</summary>
        public TowerLightDoWriter(IHardwareManagerService hardwareManager, ITowerLightDoWriterConfig config, ILogService logger)
        {
            _hardwareManager = hardwareManager;
            _config = config;
            _logger = logger;
            _hardwareManager.DeviceAdded += OnDeviceAdded;
        }

        /// <summary>
        /// IO写入器
        /// </summary>
        public void Write(string tag, bool value)
        {
            int port = _config.GetPort(tag);
            if (port < 0)
            {
                _logger.Warn($"【三色灯】tag '{tag}' 未配置端口，跳过写入。");
                return;
            }

            _desired[tag] = value;

            if (_hardwareManager.GetDevice(_config.IoDeviceId) is not IIOController ioCard)
            {
                WarnUnavailable($"DeviceId='{_config.IoDeviceId}' 的 IO 板卡未找到或类型不匹配");
                return;
            }

            Watch(ioCard);

            if (!ioCard.IsConnected)
            {
                WarnUnavailable("IO 板卡未连接");
                return;
            }

            _unavailableWarned = false;
            ioCard.WriteOutput(port, value);
        }

        private void WarnUnavailable(string reason)
        {
            if (_unavailableWarned) return;
            _unavailableWarned = true;
            _logger.Warn($"【三色灯】{reason}，暂停写出（恢复连接后自动按当前状态重写，期间不再重复提示）。");
        }

        private void OnDeviceAdded(object? sender, IHardwareDevice device)
        {
            if (device.DeviceId == _config.IoDeviceId) Watch(device);
        }

        // 跟踪当前板卡实例的连接状态：热重载会换实例，需改订阅新实例
        private void Watch(IHardwareDevice device)
        {
            lock (_watchLock)
            {
                if (ReferenceEquals(_watchedDevice, device)) return;
                if (_watchedDevice != null) _watchedDevice.ConnectionChanged -= OnConnectionChanged;
                _watchedDevice = device;
                device.ConnectionChanged += OnConnectionChanged;
            }
        }

        private void OnConnectionChanged(object? sender, bool connected)
        {
            if (!connected || sender is not IIOController ioCard) return;

            foreach (var (tag, value) in _desired)
            {
                int port = _config.GetPort(tag);
                if (port >= 0) ioCard.WriteOutput(port, value);
            }
            _unavailableWarned = false;
            _logger.Info("【三色灯】IO 板卡已连接，已按当前状态重写三色灯输出。");
        }
    }
}
