using PF.Core.Events;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 硬件输入监控服务（重构版，取代 OperationPanelMonitor）
    ///
    /// 职责：轮询 IO 状态 → 下降沿检测 → 条件防抖 → 发布 HardwareInputEventBus 事件。
    ///
    /// 分组扫描机制：
    ///   · Standard 组（普通按键）：30ms 轮询周期，支持 DebounceMs 防抖。
    ///   · Safety   组（急停/安全门）：10ms 轮询周期，DebounceMs = 0，零延迟响应。
    ///
    /// 通过 IPanelIoConfig 注入配置，不依赖任何具体的工站配置类。
    /// </summary>
    public class HardwareInputMonitor : IDisposable
    {
        private readonly IPanelIoConfig _config;
        private readonly HardwareInputEventBus _eventBus;
        private readonly IHardwareManagerService _hardwareManager;
        private readonly ILogService _logger;

        private IIOController _ioCard;

        private List<InputScanState> _standardInputs;
        private List<InputScanState> _safetyInputs;

        public HardwareInputMonitor(
            IPanelIoConfig config,
            HardwareInputEventBus eventBus,
            IHardwareManagerService hardwareManager,
            ILogService logger)
        {
            _config          = config;
            _eventBus        = eventBus;
            _hardwareManager = hardwareManager;
            _logger          = logger;

            // 按 ScanGroup 分组，构建状态跟踪对象
            _standardInputs = _config.MonitoredInputs
                .Where(c => c.ScanGroup == InputScanGroup.Standard)
                .Select(c => new InputScanState(c))
                .ToList();

            _safetyInputs = _config.MonitoredInputs
                .Where(c => c.ScanGroup == InputScanGroup.Safety)
                .Select(c => new InputScanState(c))
                .ToList();
        }

        /// <summary>
        /// 启动监控：解析 IO 卡，然后在两个独立线程上分别启动 Standard / Safety 扫描循环。
        /// </summary>
        public void StartMonitoring(CancellationToken token)
        {
            var device = _hardwareManager.GetDevice(_config.IoDeviceId);
            if (device is not IIOController ioCard)
            {
                _logger.Error($"【硬件输入监控】未找到 DeviceId='{_config.IoDeviceId}' 的 IO 板卡，监控启动失败！");
                return;
            }

            _ioCard = ioCard;

            // 普通按键扫描线程（30ms 轮询）
            Task.Run(() => StandardMonitorLoopAsync(token), token);

            // 安全传感器扫描线程（10ms 轮询，零防抖）
            Task.Run(() => SafetyMonitorLoopAsync(token), token);

            _logger.Info($"【硬件输入监控】已启动。Standard 组 {_standardInputs.Count} 个，Safety 组 {_safetyInputs.Count} 个。");
        }

        // ── 扫描循环 ──────────────────────────────────────────────────────────

        private async Task StandardMonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_ioCard == null || !_ioCard.IsConnected)
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    foreach (var state in _standardInputs)
                        await ProcessSingleInputAsync(state, token).ConfigureAwait(false);

                    await Task.Delay(30, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error($"【硬件输入监控/Standard】扫描异常：{ex.Message}");
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }

        private async Task SafetyMonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_ioCard == null || !_ioCard.IsConnected)
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    foreach (var state in _safetyInputs)
                        await ProcessSingleInputAsync(state, token).ConfigureAwait(false);

                    await Task.Delay(10, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Error($"【硬件输入监控/Safety】扫描异常：{ex.Message}");
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }

        // ── 通用检测方法 ──────────────────────────────────────────────────────

        /// <summary>
        /// 对单个输入点执行一次扫描：
        ///   1. 读取当前电平
        ///   2. 检测下降沿（True → False）
        ///   3. 若 DebounceMs > 0，等待后确认稳定；若已弹回则忽略
        ///   4. 确认后发布事件
        /// </summary>
        private async Task ProcessSingleInputAsync(InputScanState state, CancellationToken token)
        {
            bool? raw = _ioCard.ReadInput(state.Config.Port);
            if (raw == null) return;

            bool current = raw.Value;

            // 下降沿：上一次为高电平，本次为低电平
            if (state.LastValue && !current)
            {
                if (state.Config.DebounceMs > 0)
                {
                    // 等待防抖窗口
                    await Task.Delay(state.Config.DebounceMs, token).ConfigureAwait(false);

                    // 重新读取确认信号已稳定（仍为低电平）
                    bool? confirmed = _ioCard.ReadInput(state.Config.Port);
                    if (confirmed == null || confirmed.Value)
                    {
                        // 弹跳干扰，不触发事件
                        state.LastValue = current;
                        return;
                    }
                }

                _logger.Info($"【硬件输入】{state.Config.Name} 触发 → 类型：{state.Config.InputType}");
                _eventBus.PublishInputEvent(state.Config.InputType);
            }

            state.LastValue = current;
        }

        public void Dispose() { /* CancellationToken 由调用方管理，此处无内部资源需释放 */ }

        // ── 内部状态跟踪 ──────────────────────────────────────────────────────

        private class InputScanState
        {
            public IHardwareInputConfig Config { get; }

            /// <summary>上一次读取到的电平值。Safety（NC）类接触器初始值为 true（常闭导通）。</summary>
            public bool LastValue { get; set; }

            public InputScanState(IHardwareInputConfig config)
            {
                Config = config;
                // 常闭（NC）安全传感器通电初始状态为 true；常开（NO）普通按钮初始为 false
                LastValue = config.ScanGroup == InputScanGroup.Safety;
            }
        }
    }
}
