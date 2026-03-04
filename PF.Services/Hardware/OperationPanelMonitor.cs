using PF.Core.Entities.Configuration;
using PF.Core.Entities.Hardware;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 实体操作面板监控服务
    /// 职责：轮询 IO 状态 -> 边缘检测防抖 -> 触发自定义事件总线。
    /// 完全独立于具体的业务调度逻辑。
    /// </summary>
    public class OperationPanelMonitor : IDisposable
    {
        private readonly PhysicalButtonEventBus _hardwareEventBus;
        private readonly IHardwareManagerService _hardwareManager;
        private readonly ILogService _logger;
        private readonly PanelIoConfig _config;

        private IIOController _ioCard;
        private CancellationTokenSource _monitorCts;
        private Task _monitorTask;

        // 历史状态，用于边缘检测 (下降沿触发)
        private bool _lastStart;
        private bool _lastPause;
        private bool _lastReset;
        private bool _lastEStop = true; // 急停为常闭，默认通电为 true

        public OperationPanelMonitor(
            PhysicalButtonEventBus hardwareEventBus,
            IHardwareManagerService hardwareManager,
            ILogService logger,
            PanelIoConfig config)
        {
            _hardwareEventBus = hardwareEventBus;
            _hardwareManager = hardwareManager;
            _logger = logger;
            _config = config;
        }

        public void StartMonitoring()
        {
            var device = _hardwareManager.GetDevice(_config.IoDeviceId);
            if (device is not IIOController ioCard)
            {
                _logger.Error($"【面板监控】未找到 DeviceId 为 '{_config.IoDeviceId}' 的 IO 板卡，实体按钮绑定失败！");
                return;
            }

            _ioCard = ioCard;
            _monitorCts?.Cancel();
            _monitorCts = new CancellationTokenSource();

            _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token), _monitorCts.Token);
            _logger.Info("【面板监控】实体按键监控服务已启动。");
        }

        public void StopMonitoring()
        {
            _monitorCts?.Cancel();
        }

        private async Task MonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_ioCard == null || !_ioCard.IsConnected)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                try
                {
                    // 1. 读取当前 IO 状态
                    bool? currentStart = _ioCard.ReadInput(_config.StartButtonPort);
                    bool? currentPause = _ioCard.ReadInput(_config.PauseButtonPort);
                    bool? currentReset = _ioCard.ReadInput(_config.ResetButtonPort);
                    bool? currentEStop = _ioCard.ReadInput(_config.EStopButtonPort);

                    // 2. 边缘检测与事件触发

                    // 🚨【急停】：绝对下降沿（断开瞬间），无需等待抬起或防抖
                    if (!currentEStop.Value  && _lastEStop)
                    {
                        _logger.Fatal("【硬件面板】检测到实体急停按钮被拍下！");
                        _hardwareEventBus.PublishPhysicalButton(PhysicalButtonType.EStop);
                    }

                    // 🟢【启动】：按下后抬起触发 (True -> False)
                    if (!currentStart.Value  && _lastStart)
                    {
                        await Task.Delay(20, token); // 机械防抖
                        if (!_ioCard.ReadInput(_config.StartButtonPort).Value ) // 确认已稳定松开
                        {
                            _logger.Info("【硬件面板】启动按钮按下后抬起，触发启动指令");
                            _hardwareEventBus.PublishPhysicalButton(PhysicalButtonType.Start);
                        }
                    }

                    // 🟡【暂停】：按下后抬起触发
                    if (!currentPause.Value  && _lastPause)
                    {
                        await Task.Delay(20, token);
                        if (!_ioCard.ReadInput(_config.PauseButtonPort).Value )
                        {
                            _logger.Info("【硬件面板】暂停按钮按下后抬起，触发暂停指令");
                            _hardwareEventBus.PublishPhysicalButton(PhysicalButtonType.Pause);
                        }
                    }

                    // 🔵【复位】：按下后抬起触发
                    if (!currentReset.Value  && _lastReset)
                    {
                        await Task.Delay(20, token);
                        if (!_ioCard.ReadInput(_config.ResetButtonPort).Value )
                        {
                            _logger.Info("【硬件面板】复位按钮按下后抬起，触发复位指令");
                            _hardwareEventBus.PublishPhysicalButton(PhysicalButtonType.Reset);
                        }
                    }

                    // 3. 更新历史状态
                    _lastStart = currentStart.Value ;
                    _lastPause = currentPause.Value ;
                    _lastReset = currentReset.Value ;
                    _lastEStop = currentEStop.Value ;

                    await Task.Delay(30, token); // 轮询周期
                }
                catch (Exception ex)
                {
                    _logger.Error($"【面板监控】读取 IO 异常: {ex.Message}");
                    await Task.Delay(1000, token);
                }
            }
        }

        public void Dispose()
        {
            StopMonitoring();
            _monitorCts?.Dispose();
        }
    }
}