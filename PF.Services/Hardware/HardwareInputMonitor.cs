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
    /// IHardwareInputMonitor 监控器
    /// </summary>
    public class HardwareInputMonitor : IHardwareInputMonitor
    {
        private readonly IPanelIoConfig _config;
        private readonly HardwareInputEventBus _eventBus;
        private readonly IHardwareManagerService _hardwareManager;
        private readonly ILogService _logger;

        private IIOController _ioCard;

        private readonly List<InputScanState> _standardInputs;
        private readonly List<InputScanState> _safetyInputs;

        // --- Standard 组独立线程控制 ---
        private CancellationTokenSource _standardCts;
        private Task _standardTask;

        // --- Safety 组独立线程控制 ---
        private CancellationTokenSource _safetyCts;
        private Task _safetyTask;

        /// <summary>
        /// HardwareInputMonitor 监控器
        /// </summary>
        public HardwareInputMonitor(
            IPanelIoConfig config,
            HardwareInputEventBus eventBus,
            IHardwareManagerService hardwareManager,
            ILogService logger)
        {
            _config = config;
            _eventBus = eventBus;
            _hardwareManager = hardwareManager;
            _logger = logger;

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
        /// 确保 IO 板卡已正确获取
        /// </summary>
        private bool TryInitializeIoCard()
        {
            if (_ioCard != null) return true;

            var device = _hardwareManager.GetDevice(_config.IoDeviceId);
            if (device is not IIOController ioCard)
            {
                _logger.Error($"【硬件输入监控】未找到 DeviceId='{_config.IoDeviceId}' 的 IO 板卡！");
                return false;
            }

            _ioCard = ioCard;
            return true;
        }

        // ==========================================
        // Standard (普通按键) 控制
        // ==========================================

        /// <summary>
        /// StartStandardMonitoring 监控器
        /// </summary>
        public void StartStandardMonitoring(CancellationToken externalToken = default)
        {
            if (_standardCts != null && !_standardCts.IsCancellationRequested)
            {
                _logger.Info("【硬件输入监控】Standard 扫描线程已经在运行中。");
                return;
            }

            if (!TryInitializeIoCard()) return;

            _standardCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = _standardCts.Token;

            _standardTask = Task.Run(() => StandardMonitorLoopAsync(token), token);
            _logger.Info($"【硬件输入监控】Standard 组已启动（{_standardInputs.Count} 个）。");
        }

        /// <summary>
        /// StopStandardMonitoring 监控器
        /// </summary>
        public void StopStandardMonitoring()
        {
            if (_standardCts == null || _standardCts.IsCancellationRequested) return;

            _logger.Info("【硬件输入监控】正在停止 Standard 扫描线程...");
            _standardCts.Cancel();

            try
            {
                if (_standardTask != null)
                    Task.WaitAll(new[] { _standardTask }, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex) { /* 忽略 Task 取消或超时异常 */ }
            finally
            {
                _standardCts.Dispose();
                _standardCts = null;
                _standardTask = null;
            }
        }

        // ==========================================
        // Safety (安全装置) 控制
        // ==========================================

        /// <summary>
        /// StartSafetyMonitoring 监控器
        /// </summary>
        public void StartSafetyMonitoring(CancellationToken externalToken = default)
        {
            if (_safetyCts != null && !_safetyCts.IsCancellationRequested)
            {
                _logger.Info("【硬件输入监控】Safety 扫描线程已经在运行中。");
                return;
            }

            if (!TryInitializeIoCard()) return;

            // 每次启动 Safety 时，重置 LastValue，防止启动瞬间由于状态不一致产生误触发
            foreach (var state in _safetyInputs)
            {
                state.LastValue = true; // 安全传感器(NC常闭)默认导通状态为 true
            }

            _safetyCts = CancellationTokenSource.CreateLinkedTokenSource(externalToken);
            var token = _safetyCts.Token;

            _safetyTask = Task.Run(() => SafetyMonitorLoopAsync(token), token);
            _logger.Info($"【硬件输入监控】Safety 组已启动（{_safetyInputs.Count} 个）。");
        }

        /// <summary>
        /// StopSafetyMonitoring 监控器
        /// </summary>
        public void StopSafetyMonitoring()
        {
            if (_safetyCts == null || _safetyCts.IsCancellationRequested) return;

            _logger.Info("【硬件输入监控】正在停止 Safety 扫描线程...");
            _safetyCts.Cancel();

            try
            {
                if (_safetyTask != null)
                    Task.WaitAll(new[] { _safetyTask }, TimeSpan.FromSeconds(2));
            }
            catch (Exception ex) { /* 忽略 Task 取消或超时异常 */ }
            finally
            {
                _safetyCts.Dispose();
                _safetyCts = null;
                _safetyTask = null;
            }
        }

        // ==========================================
        // 全局控制 & 扫描循环
        // ==========================================

        /// <summary>
        /// 停止All
        /// </summary>
        public void StopAll()
        {
            StopSafetyMonitoring();
            StopStandardMonitoring();
            _logger.Info("【硬件输入监控】所有扫描线程已停止。");
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            StopAll();
        }

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
                catch (OperationCanceledException) { break; }
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
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.Error($"【硬件输入监控/Safety】扫描异常：{ex.Message}");
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }

        private async Task ProcessSingleInputAsync(InputScanState state, CancellationToken token)
        {
            bool? raw = _ioCard.ReadInput(state.Config.Port);
            if (raw == null) return;

            bool current = raw.Value;

            if (!state.Config.IsMuted && state.LastValue && !current)
            {
                if (state.Config.DebounceMs > 0)
                {
                    await Task.Delay(state.Config.DebounceMs, token).ConfigureAwait(false);

                    bool? confirmed = _ioCard.ReadInput(state.Config.Port);
                    if (confirmed == null || confirmed.Value)
                    {
                        state.LastValue = current;
                        return;
                    }
                }

                _logger.Info($"【硬件输入】{state.Config.Name} 触发 → 类型：{state.Config.InputType}");
                _eventBus.PublishInputEvent(state.Config.InputType);
            }

            state.LastValue = current;
        }

        private class InputScanState
        {
            public IHardwareInputConfig Config { get; }
            public bool LastValue { get; set; }

            public InputScanState(IHardwareInputConfig config)
            {
                Config = config;
                LastValue = config.ScanGroup == InputScanGroup.Safety;
            }
        }
    }
}