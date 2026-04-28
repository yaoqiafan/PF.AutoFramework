using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.TowerLight;

namespace PF.Services.Hardware
{
    /// <summary>
    /// ITowerLightDoWriter 实现：通过 IHardwareManagerService 获取 IO 板卡，
    /// 根据 ITowerLightDoWriterConfig 将逻辑 tag 解析为端口索引后调用 WriteOutput。
    /// </summary>
    public class TowerLightDoWriter : ITowerLightDoWriter
    {
        private readonly IHardwareManagerService _hardwareManager;
        private readonly ITowerLightDoWriterConfig _config;
        private readonly ILogService _logger;

        public TowerLightDoWriter(
            IHardwareManagerService hardwareManager,
            ITowerLightDoWriterConfig config,
            ILogService logger)
        {
            _hardwareManager = hardwareManager;
            _config = config;
            _logger = logger;
        }

        public void Write(string tag, bool value)
        {
            int port = _config.GetPort(tag);
            if (port < 0)
            {
                _logger.Warn($"【三色灯】tag '{tag}' 未配置端口，跳过写入。");
                return;
            }

            if (_hardwareManager.GetDevice(_config.IoDeviceId) is not IIOController ioCard)
            {
                _logger.Warn($"【三色灯】DeviceId='{_config.IoDeviceId}' 的 IO 板卡未找到或类型不匹配。");
                return;
            }

            if (!ioCard.IsConnected)
            {
                _logger.Warn($"【三色灯】IO 板卡未连接，跳过写入 tag='{tag}' value={value}。");
                return;
            }

            ioCard.WriteOutput(port, value);
        }
    }
}
