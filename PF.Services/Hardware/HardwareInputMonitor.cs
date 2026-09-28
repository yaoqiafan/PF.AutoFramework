using PF.Core.Constants;
using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
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
    /// <remarks>
    /// 安全门门锁：锁输出统一按 <c>上锁许可 &amp;&amp; IsEnabled &amp;&amp; !IsMuted</c> 计算，
    /// 上锁许可由主控按机台状态驱动（<see cref="SetLockPermit"/>），工站只通过 <see cref="SetSafetyDoorEnabled"/>
    /// 开"允许开门"的窗口。Safety 组的开门触发同样受上锁许可约束，未持有许可时开门不报警。
    /// </remarks>
    public class HardwareInputMonitor : IHardwareInputMonitor
    {
        private readonly IPanelIoConfig _config;
        private readonly HardwareInputEventBus _eventBus;
        private readonly IHardwareManagerService _hardwareManager;
        private readonly IParamService _paramService;
        private readonly ILogService _logger;
        private readonly IAlarmService _alarmService;

        // volatile：热重载时由 DeviceRemoved/DeviceAdded 事件线程写入，扫描线程读取
        private volatile IIOController? _ioCard;

        private readonly List<InputScanState> _standardInputs;
        private readonly List<InputScanState> _safetyInputs;

        // --- Standard 组独立线程控制 ---
        private CancellationTokenSource _standardCts;
        private Task _standardTask;

        // --- Safety 组独立线程控制 ---
        private CancellationTokenSource _safetyCts;
        private Task _safetyTask;

        // 连续读取失败自警计数器 + 阈值
        private int _consecutiveSafetyReadFails;
        private int _consecutiveStandardReadFails;
        private const int MonitorFailThreshold = 5;

        // --- 门锁 ---
        // 上锁许可与锁输出写入统一在 _lockSync 内完成，保证"许可/启用/屏蔽 → 输出"的计算不被并发撕裂
        private readonly object _lockSync = new();
        private volatile bool _lockPermit;
        private bool _lockWriteFailed;
        private long _lastLockReconcileTicks;
        private const int LockReconcileIntervalMs = 500;

        /// <summary>
        /// HardwareInputMonitor 监控器
        /// </summary>
        public HardwareInputMonitor(
            IPanelIoConfig config,
            HardwareInputEventBus eventBus,
            IHardwareManagerService hardwareManager,
            IParamService paramService,
            ILogService logger,
            IAlarmService alarmService)
        {
            _config = config;
            _eventBus = eventBus;
            _hardwareManager = hardwareManager;
            _paramService = paramService;
            _logger = logger;
            _alarmService = alarmService;

            _standardInputs = _config.MonitoredInputs
                .Where(c => c.ScanGroup == InputScanGroup.Standard)
                .Select(c => new InputScanState(c))
                .ToList();

            _safetyInputs = _config.MonitoredInputs
                .Where(c => c.ScanGroup == InputScanGroup.Safety)
                .Select(c => new InputScanState(c))
                .ToList();

            _paramService.ParamChanged += OnParamChanged;

            // 热重载（ReloadAllAsync / 切换模拟模式）会 Dispose 并重建设备实例：
            // 若不跟随刷新 _ioCard，扫描线程将永远持有旧实例（IsConnected=false），
            // 安全门/面板按钮检测静默失效且无任何报警
            _hardwareManager.DeviceRemoved += OnDeviceRemoved;
            _hardwareManager.DeviceAdded += OnDeviceAdded;

            // 构造时立即加载一次屏蔽状态，使 IsMuted 在 StartSafetyMonitoring 之前就已就绪
            _ = LoadSafetyMuteStatesAsync();
        }

        private void OnDeviceRemoved(object? sender, string deviceId)
        {
            if (deviceId != _config.IoDeviceId) return;
            _ioCard = null;
            _logger.Warn($"【硬件输入监控】IO 板卡 '{deviceId}' 已被移除（热重载），扫描暂停，等待设备重新注册...");
        }

        private void OnDeviceAdded(object? sender, IHardwareDevice device)
        {
            if (device.DeviceId != _config.IoDeviceId || device is not IIOController ioCard) return;
            _ioCard = ioCard;
            _logger.Info($"【硬件输入监控】IO 板卡 '{device.DeviceId}' 已重新注册，扫描自动恢复。");
            // 新板卡实例的输出处于复位态，按当前期望重写一遍门锁输出
            ApplyAllLocks();
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

        /// <inheritdoc/>
        public bool IsSafetyMonitoringRunning =>
            _safetyCts != null && !_safetyCts.IsCancellationRequested &&
            _safetyTask is { IsCompleted: false };

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

            if (!TryInitializeIoCard())
            {
                // 配了安全装置却拿不到 IO 板卡：安全门检测整体失效，必须报警而非只写日志
                if (_safetyInputs.Count > 0)
                    _alarmService?.TriggerAlarm("HardwareInputMonitor", AlarmCodes.Safety.MonitorFailure,
                        $"未找到 IO 板卡 '{_config.IoDeviceId}'，Safety 监控无法启动，安全门检测已失效");
                return;
            }

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

            // 重置所有安全门触发标志，防止跨生命周期状态泄漏
            foreach (var state in _safetyInputs)
                state.WasTriggeredWhileEnabled = false;

            // 扫描线程停止后无人检测开门，门锁不能继续保持
            SetLockPermit(false);
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
            _hardwareManager.DeviceRemoved -= OnDeviceRemoved;
            _hardwareManager.DeviceAdded -= OnDeviceAdded;
            _paramService.ParamChanged -= OnParamChanged;
            StopAll();
            // 退出兜底：扫描线程可能从未启动（StopSafetyMonitoring 提前返回），仍须解锁
            SetLockPermit(false);
        }

        private async Task StandardMonitorLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // 局部快照：_ioCard 可能被热重载事件线程置 null，两次读取之间不做快照会 NRE
                var io = _ioCard;
                if (io == null || !io.IsConnected)
                {
                    // 板卡缺失/断开同样计入失败：否则断线期间面板检测静默失效且无自警
                    CountReadFailure(ref _consecutiveStandardReadFails, "Standard 监控 IO 板卡不可用", "操作面板检测可能已失效");
                    await Task.Delay(500, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    bool allReadsOk = true;
                    foreach (var state in _standardInputs)
                        allReadsOk &= await ProcessSingleInputAsync(io, state, token).ConfigureAwait(false);

                    if (allReadsOk)
                    {
                        // 本周期读取成功：重置失败计数，若此前已达阈值则自动清除自警
                        int prev = Interlocked.Exchange(ref _consecutiveStandardReadFails, 0);
                        if (prev >= MonitorFailThreshold)
                            _alarmService?.ClearAlarm("HardwareInputMonitor", AlarmCodes.Safety.MonitorFailure);
                    }
                    else
                    {
                        // 底层 ReadInput 出错时返回 null 而非抛异常，读取失败必须在此计数，
                        // 否则监控失效自警永远不会触发
                        CountReadFailure(ref _consecutiveStandardReadFails, "Standard 监控 IO 连续读取失败", "操作面板检测可能已失效");
                    }

                    await Task.Delay(30, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.Error($"【硬件输入监控/Standard】扫描异常：{ex.Message}");
                    CountReadFailure(ref _consecutiveStandardReadFails, "Standard 监控 IO 连续读取失败", "操作面板检测可能已失效");
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
                // 局部快照：_ioCard 可能被热重载事件线程置 null，两次读取之间不做快照会 NRE
                var io = _ioCard;
                if (io == null || !io.IsConnected)
                {
                    // 板卡缺失/断开同样计入失败：否则断线期间安全门检测静默失效且无自警
                    if (_safetyInputs.Count > 0)
                        CountReadFailure(ref _consecutiveSafetyReadFails, "Safety 监控 IO 板卡不可用", "安全门检测可能已失效");
                    await Task.Delay(500, token).ConfigureAwait(false);
                    continue;
                }

                // 仿真 IO 的 ReadInput 恒为 false：常闭门会被当成"一直开着"，扫描只会制造误报，直接跳过
                if (io.IsSimulated)
                {
                    await Task.Delay(500, token).ConfigureAwait(false);
                    continue;
                }

                try
                {
                    bool allReadsOk = true;
                    foreach (var state in _safetyInputs)
                        allReadsOk &= await ProcessSingleInputAsync(io, state, token).ConfigureAwait(false);

                    if (allReadsOk)
                    {
                        // 本周期读取成功：重置失败计数，若此前已达阈值则自动清除自警
                        int prev = Interlocked.Exchange(ref _consecutiveSafetyReadFails, 0);
                        if (prev >= MonitorFailThreshold)
                            _alarmService?.ClearAlarm("HardwareInputMonitor", AlarmCodes.Safety.MonitorFailure);
                    }
                    else
                    {
                        // 底层 ReadInput 出错时返回 null 而非抛异常，读取失败必须在此计数，
                        // 否则监控失效自警永远不会触发
                        CountReadFailure(ref _consecutiveSafetyReadFails, "Safety 监控 IO 连续读取失败", "安全门检测可能已失效");
                    }

                    ReconcileLocksIfDue();

                    await Task.Delay(10, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) { break; }
                catch (Exception ex)
                {
                    _logger.Error($"【硬件输入监控/Safety】扫描异常：{ex.Message}");
                    CountReadFailure(ref _consecutiveSafetyReadFails, "Safety 监控 IO 连续读取失败", "安全门检测可能已失效");
                    await Task.Delay(1000, token).ConfigureAwait(false);
                }
            }
        }

        private void CountReadFailure(ref int counter, string what, string consequence)
        {
            int fails = Interlocked.Increment(ref counter);
            if (fails == MonitorFailThreshold)
                _alarmService?.TriggerAlarm("HardwareInputMonitor", AlarmCodes.Safety.MonitorFailure,
                    $"{what}（连续 {fails} 次），{consequence}");
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
            ApplyAllLocks();
        }

        private void OnParamChanged(object? sender, ParamChangedEventArgs e)
        {
            if (e.NewValue is not bool b) return;
            foreach (var state in _safetyInputs)
            {
                if (state.Config.MuteParamKey != e.ParamName) continue;
                bool wasMuted = state.Config.IsMuted;
                state.Config.IsMuted = b;
                // 取消屏蔽 = 重新纳入检测：门若此时开着必须立即触发，而不是等下一次开关门
                if (wasMuted && !b) state.RearmRequested = true;
                ApplyLock(state);
            }
        }

        /// <inheritdoc/>
        public void SetSafetyDoorEnabled(string name, bool enabled)
        {
            var state = _safetyInputs.FirstOrDefault(s => s.Config.Name == name);
            if (state == null)
            {
                _logger.Warn($"【硬件输入监控】未找到安全门 [{name}]，无法设置启用状态。");
                return;
            }
            bool wasEnabled = state.IsEnabled;
            state.IsEnabled = enabled;
            // 停用期间 LastValue 照常跟随门的实际状态；重新启用时若门仍开着，不重新布防就没有边沿、永远不触发
            if (enabled && !wasEnabled) state.RearmRequested = true;
            ApplyLock(state);
            _logger.Info($"【硬件输入监控】安全门 [{name}] 已{(enabled ? "启用" : "停用")}。");
        }

        /// <inheritdoc/>
        public void ResetAllSafetyDoorsEnabled()
        {
            foreach (var state in _safetyInputs)
            {
                if (!state.IsEnabled) state.RearmRequested = true;
                state.IsEnabled = true;
            }
            ApplyAllLocks();
        }

        /// <inheritdoc/>
        public IReadOnlyList<SafetyDoorState> GetSafetyDoorSnapshot()
        {
            var io = _ioCard;
            var result = new List<SafetyDoorState>(_safetyInputs.Count);
            foreach (var state in _safetyInputs)
            {
                bool? signal = io?.ReadInput(state.Config.Port);
                bool? isActive = signal.HasValue
                    ? signal.Value == state.Config.NormallyOpen
                    : null;

                result.Add(new SafetyDoorState(
                    name: state.Config.Name,
                    isEnabled: state.IsEnabled,
                    isMuted: state.Config.IsMuted,
                    signalValue: signal,
                    isActive: isActive,
                    isLocked: ReadLockState(io, state)));
            }
            return result;
        }

        // ==========================================
        // 门锁
        // ==========================================

        /// <inheritdoc/>
        public bool IsLockPermitted => _lockPermit;

        /// <inheritdoc/>
        public void SetLockPermit(bool permit)
        {
            lock (_lockSync)
            {
                if (permit == _lockPermit) return;
                if (permit)
                {
                    // 取得许可 = 开门检测从无到有，门若仍开着必须立即触发
                    foreach (var state in _safetyInputs)
                        state.RearmRequested = true;
                }
                _lockPermit = permit;
                ApplyAllLocksCore();
            }
            _logger.Info($"【硬件输入监控】安全门上锁许可：{(permit ? "上锁" : "解锁")}。");

            // 上锁的前提是有人在检测开门；老项目可能在 Uninitialized 时停过扫描线程
            if (permit && !IsSafetyMonitoringRunning)
                StartSafetyMonitoring();
        }

        /// <inheritdoc/>
        public IReadOnlyList<string> GetOpenArmedDoors()
        {
            var io = _ioCard;
            var result = new List<string>();
            // 仿真 IO 读不到真实门状态（ReadInput 恒为 false），不据此拦截启动
            if (io is { IsSimulated: true }) return result;

            foreach (var state in _safetyInputs)
            {
                if (!state.IsEnabled || state.Config.IsMuted) continue;

                if (io == null || !io.IsConnected)
                {
                    result.Add($"{state.Config.Name}(IO板卡不可用)");
                    continue;
                }

                bool? signal = io.ReadInput(state.Config.Port);
                if (signal == null)
                    result.Add($"{state.Config.Name}(信号读取失败)");
                else if (signal.Value == state.Config.NormallyOpen)
                    result.Add(state.Config.Name);
            }
            return result;
        }

        private static bool HasLock(InputScanState state) =>
            state.Config.LockOutputPorts is { Count: > 0 };

        /// <summary>该门此刻应处于上锁态。</summary>
        private bool ShouldLock(InputScanState state) =>
            _lockPermit && state.IsEnabled && !state.Config.IsMuted;

        private void ApplyAllLocks()
        {
            lock (_lockSync) ApplyAllLocksCore();
        }

        private void ApplyLock(InputScanState state)
        {
            lock (_lockSync)
            {
                ApplyLockCore(_ioCard, state, out bool ok);
                UpdateLockWriteAlarm(ok);
            }
        }

        private void ApplyAllLocksCore()
        {
            var io = _ioCard;
            bool allOk = true;
            foreach (var state in _safetyInputs)
            {
                ApplyLockCore(io, state, out bool ok);
                allOk &= ok;
            }
            UpdateLockWriteAlarm(allOk);
        }

        /// <summary>按当前期望写出该门全部锁输出。须在 _lockSync 内调用。</summary>
        private void ApplyLockCore(IIOController? io, InputScanState state, out bool ok)
        {
            ok = true;
            if (!HasLock(state)) return;

            // 板卡不可用时无从写出，由 OnDeviceAdded / 周期对账在恢复后补写，此处不计为写失败
            if (io == null || !io.IsConnected) return;

            bool level = ShouldLock(state) == state.Config.LockOutputActiveHigh;
            foreach (var port in state.Config.LockOutputPorts!)
            {
                if (io.WriteOutput(port, level)) continue;
                ok = false;
                _logger.Error($"【硬件输入监控】安全门 [{state.Config.Name}] 门锁输出 {port} 写入失败（目标电平 {level}）。");
            }
        }

        /// <summary>写失败首次触发报警，恢复后自动清除。须在 _lockSync 内调用。</summary>
        private void UpdateLockWriteAlarm(bool ok)
        {
            if (!ok && !_lockWriteFailed)
            {
                _lockWriteFailed = true;
                _alarmService?.TriggerAlarm("HardwareInputMonitor", AlarmCodes.Safety.DoorLockFailure, null);
            }
            else if (ok && _lockWriteFailed)
            {
                _lockWriteFailed = false;
                _alarmService?.ClearAlarm("HardwareInputMonitor", AlarmCodes.Safety.DoorLockFailure);
            }
        }

        /// <summary>
        /// 周期对账：回读锁输出与期望不一致就重写（防 IO 模块掉电复位、被调试页手动改写等）。
        /// </summary>
        private void ReconcileLocksIfDue()
        {
            long now = Environment.TickCount64;
            if (now - _lastLockReconcileTicks < LockReconcileIntervalMs) return;
            _lastLockReconcileTicks = now;

            lock (_lockSync)
            {
                var io = _ioCard;
                // 仿真 IO 的 ReadOutput 恒为 false，无法对账
                if (io == null || !io.IsConnected || io.IsSimulated) return;

                bool allOk = true;
                foreach (var state in _safetyInputs)
                {
                    if (!HasLock(state)) continue;
                    bool level = ShouldLock(state) == state.Config.LockOutputActiveHigh;
                    if (state.Config.LockOutputPorts!.All(p => io.ReadOutput(p) == level)) continue;

                    _logger.Warn($"【硬件输入监控】安全门 [{state.Config.Name}] 门锁输出与期望不一致，重新写出。");
                    ApplyLockCore(io, state, out bool ok);
                    allOk &= ok;
                }
                UpdateLockWriteAlarm(allOk);
            }
        }

        private static bool? ReadLockState(IIOController? io, InputScanState state)
        {
            if (!HasLock(state) || io == null) return null;
            bool locked = true;
            foreach (var port in state.Config.LockOutputPorts!)
            {
                bool? value = io.ReadOutput(port);
                if (value == null) return null;
                locked &= value.Value == state.Config.LockOutputActiveHigh;
            }
            return locked;
        }

        /// <summary>
        /// 处理单个输入点：同时支持常闭（NC）和常开（NO）接线方式。
        /// <para>NC（NormallyOpen=false）：静止态信号=true，触发沿=下降沿（true→false）。</para>
        /// <para>NO（NormallyOpen=true） ：静止态信号=false，触发沿=上升沿（false→true）。</para>
        /// 触发条件统一为：上一次未处于激活态 且 本次进入激活态。
        /// 激活态定义：当前值 == NormallyOpen（NC激活=false，NO激活=true）。
        /// Safety 组额外要求持有上锁许可（机台处于上锁状态）。
        /// </summary>
        /// <returns>true = 本次 IO 读取正常；false = 底层读取失败（ReadInput 返回 null），由调用方计入连续失败自警计数。</returns>
        private async Task<bool> ProcessSingleInputAsync(IIOController io, InputScanState state, CancellationToken token)
        {
            bool? raw = io.ReadInput(state.Config.Port);
            if (raw == null) return false;

            bool current = raw.Value;
            bool no = state.Config.NormallyOpen;
            bool isSafety = state.Config.ScanGroup == InputScanGroup.Safety;

            // 重新布防：视上一次为静止态，使"启用/取得许可时门已开着"也能产生触发沿
            bool rearm = state.RearmRequested;
            if (rearm) state.RearmRequested = false;

            // 上一次是否处于激活态（激活 = 信号值等于 NormallyOpen）
            bool wasActive = !rearm && (state.LastValue == no);
            // 当前是否处于激活态
            bool isActive  = (current == no);

            bool armed = state.IsEnabled && !state.Config.IsMuted && (!isSafety || _lockPermit);

            if (armed && !wasActive && isActive)
            {
                if (state.Config.DebounceMs > 0)
                {
                    await Task.Delay(state.Config.DebounceMs, token).ConfigureAwait(false);

                    bool? confirmed = io.ReadInput(state.Config.Port);
                    if (confirmed == null)
                    {
                        state.LastValue = current;
                        return false;
                    }
                    // 防抖后若已不再处于激活态，丢弃本次触发
                    if (confirmed.Value != no)
                    {
                        state.LastValue = current;
                        return true;
                    }
                }

                _logger.Info($"【硬件输入】{state.Config.Name} 触发 → 类型：{state.Config.InputType}");
                state.WasTriggeredWhileEnabled = true;
                _eventBus.PublishInputEvent(state.Config.InputType);
            }
            else if (isSafety
                     && !state.Config.IsMuted
                     && state.WasTriggeredWhileEnabled
                     && (state.LastValue == no) && !isActive)
            {
                _logger.Info($"【硬件输入】{state.Config.Name} 恢复 → 类型：{state.Config.InputType}");
                state.WasTriggeredWhileEnabled = false;
                _eventBus.PublishRestoreEvent(state.Config.InputType);
            }

            state.LastValue = current;
            return true;
        }

        private class InputScanState
        {
            public IHardwareInputConfig Config { get; }
            public bool LastValue { get; set; }
            public volatile bool IsEnabled = true;
            // 仅当 IsEnabled=true 时发生的触发才允许后续恢复事件，防止禁用期间开关门产生误恢复
            public bool WasTriggeredWhileEnabled { get; set; }
            // 由启用/取得许可/取消屏蔽的线程置位，扫描线程消费；用标志而非直接改 LastValue，
            // 避免与扫描线程同时写 LastValue 时被其覆盖而丢失触发沿
            public volatile bool RearmRequested;

            public InputScanState(IHardwareInputConfig config)
            {
                Config = config;
                // 初始化为静止态：NC 静止=true，NO 静止=false
                LastValue = !config.NormallyOpen;
            }
        }
    }
}
