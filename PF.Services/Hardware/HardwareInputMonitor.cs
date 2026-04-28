using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
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
        private readonly IParamService _paramService;
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
            IParamService paramService,
            ILogService logger)
        {
            _config = config;
            _eventBus = eventBus;
            _hardwareManager = hardwareManager;
            _paramService = paramService;
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

            // 重置 LastValue 为静止态（NC=true 常闭导通，NO=false 常开断开），防止启动瞬间误触发
            foreach (var state in _safetyInputs)
                state.LastValue = !state.Config.NormallyOpen;

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
            // 启动时从参数服务同步一次屏蔽状态
            await LoadSafetyMuteStatesAsync().ConfigureAwait(false);

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

        /// <summary>
        /// 从参数服务加载各安全门的屏蔽状态并写入 IsMuted。
        /// </summary>
        private async Task LoadSafetyMuteStatesAsync()
        {
            foreach (var state in _safetyInputs)
            {
                if (state.Config.MuteParamKey == null) continue;
                try
                {
                    bool muted = await _paramService.GetParamAsync<bool>(state.Config.MuteParamKey, false)
                        .ConfigureAwait(false);
                    state.Config.IsMuted = muted;
                    if (muted)
                        _logger.Info($"【硬件输入监控】安全门 [{state.Config.Name}] 已屏蔽（调试模式）。");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"【硬件输入监控】读取 [{state.Config.Name}] 屏蔽参数失败：{ex.Message}");
                }
            }
        }

        /// <summary>
        /// 处理单个输入点：同时支持常闭（NC）和常开（NO）接线方式。
        /// <para>NC（NormallyOpen=false）：静止态信号=true，触发沿=下降沿（true→false）。</para>
        /// <para>NO（NormallyOpen=true） ：静止态信号=false，触发沿=上升沿（false→true）。</para>
        /// 触发条件统一为：上一次未处于激活态 且 本次进入激活态。
        /// 激活态定义：当前值 == NormallyOpen（NC激活=false，NO激活=true）。
        /// </summary>
        private async Task ProcessSingleInputAsync(InputScanState state, CancellationToken token)
        {
            bool? raw = _ioCard.ReadInput(state.Config.Port);
            if (raw == null) return;

            bool current = raw.Value;
            bool no = state.Config.NormallyOpen;

            // 上一次是否处于激活态（激活 = 信号值等于 NormallyOpen）
            bool wasActive = (state.LastValue == no);
            // 当前是否处于激活态
            bool isActive  = (current == no);

            if (!state.Config.IsMuted && !wasActive && isActive)
            {
                if (state.Config.DebounceMs > 0)
                {
                    await Task.Delay(state.Config.DebounceMs, token).ConfigureAwait(false);

                    bool? confirmed = _ioCard.ReadInput(state.Config.Port);
                    // 防抖后若已不再处于激活态，丢弃本次触发
                    if (confirmed == null || confirmed.Value != no)
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
                // 初始化为静止态：NC 静止=true，NO 静止=false
                LastValue = !config.NormallyOpen;
            }
        }
    }
}