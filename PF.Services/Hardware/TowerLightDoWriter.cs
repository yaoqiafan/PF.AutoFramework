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
    /// <remarks>
    /// 构造
    /// </remarks>
    /// <param name="hardwareManager"></param>
    /// <param name="config"></param>
    /// <param name="logger"></param>
    public class TowerLightDoWriter(
        IHardwareManagerService hardwareManager,
        ITowerLightDoWriterConfig config,
        ILogService logger) : ITowerLightDoWriter
    {
        private readonly IHardwareManagerService _hardwareManager = hardwareManager;
        private readonly ITowerLightDoWriterConfig _config = config;
        private readonly ILogService _logger = logger;

        /// <summary>
        /// IO写入器
        /// </summary>
        /// <param name="tag"></param>
        /// <param name="value"></param>
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
