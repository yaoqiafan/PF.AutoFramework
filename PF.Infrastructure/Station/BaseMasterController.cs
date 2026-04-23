using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
using PF.Core.Models;
using Stateless;
using System.Collections.Concurrent;

namespace PF.Infrastructure.Station
{
    /// <summary>
    /// 全局主控调度器基类 (Infrastructure 层)
    /// </summary>
    public abstract class BaseMasterController : IMasterController
    {
        #region Fields & Properties

        public MachineState CurrentState => _globalMachine.State;
        public OperationMode CurrentMode { get; private set; } = OperationMode.Normal;

        public event EventHandler<MachineState>? MasterStateChanged;
        public event EventHandler<StationAlarmEventArgs>? MasterAlarmTriggered;

        protected readonly ILogService _logger;
        protected readonly HardwareInputEventBus? _hardwareEventBus;
        protected readonly IReadOnlyList<IStation> _subStations;
        private readonly IAlarmService? _alarmService;

        protected readonly StateMachine<MachineState, MachineTrigger> _globalMachine;
        private readonly SemaphoreSlim _machineLock = new(1, 1);

        private volatile bool _subStationStopsAreIntentional;
        private volatile bool _masterCameFromInitAlarm;
        protected bool MasterCameFromInitAlarm => _masterCameFromInitAlarm;

        // 线程安全的报警去重
        private readonly ConcurrentDictionary<string, byte> _reportedAlarmKeys = new();

        private CancellationTokenSource? _initCts;
        private Action<HardwareResetRequest>? _hardwareResetHandler;
        private volatile bool _disposed;

        #endregion

        #region Constructor

        protected BaseMasterController(
            ILogService logger,
            HardwareInputEventBus? hardwareEventBus,
            IEnumerable<IStation> subStations,
            IAlarmService? alarmService = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _alarmService = alarmService;
            _hardwareEventBus = hardwareEventBus;
            _subStations = subStations?.ToList() ?? throw new ArgumentNullException(nameof(subStations));

            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered += OnSubStationAlarm;
                station.StationAlarmAutoCleared += OnSubStationAlarmAutoCleared;
                station.StationStateChanged += OnSubStationStateChanged;
            }

            if (_hardwareEventBus != null)
                _hardwareEventBus.HardwareInputTriggered += OnHardwareInputReceived;

            _globalMachine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureGlobalMachine();
        }

        #endregion

        #region State Machine Configuration

        private void ConfigureGlobalMachine()
        {
            _globalMachine.OnTransitioned(t =>
            {
                _logger.Info($"【全局主控】状态切换: {t.Source} -> {t.Destination}");
                var handler = MasterStateChanged;
                if (handler != null)
                {
                    var dest = t.Destination;
                    _ = Task.Run(() =>
                    {
                        try { handler.Invoke(this, dest); }
                        catch (Exception ex) { _logger.Error($"【主控】MasterStateChanged 订阅者异常: {ex.Message}"); }
                    });
                }
            });

            _globalMachine.Configure(MachineState.Uninitialized)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing);

            _globalMachine.Configure(MachineState.Initializing)
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.InitAlarm);

            _globalMachine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _globalMachine.Configure(MachineState.Running)
                .OnEntryFromAsync(MachineTrigger.Start, StartAllSubStationsAsync)
                .OnEntryFromAsync(MachineTrigger.Resume, ResumeAllSubStationsAsync)
                .OnExit(t =>
                {
                    if (t.Destination == MachineState.Paused)
                        PauseAllSubStationsSafely();
                })
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _globalMachine.Configure(MachineState.Paused)
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _globalMachine.Configure(MachineState.InitAlarm)
                .OnEntry(() =>
                {
                    _masterCameFromInitAlarm = true;
                    TriggerAllSubStationAlarmsSafely();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _globalMachine.Configure(MachineState.RunAlarm)
                .OnEntry(() =>
                {
                    _masterCameFromInitAlarm = false;
                    TriggerAllSubStationAlarmsSafely();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _globalMachine.Configure(MachineState.Resetting)
                .OnEntry(() => _reportedAlarmKeys.Clear())
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized)
                .PermitDynamic(MachineTrigger.Error,
                    () => _masterCameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        private async Task StartAllSubStationsAsync()
        {
            foreach (var s in _subStations)
            {
                try { await s.StartAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.Error($"【主控】启动工站 [{s.StationName}] 失败: {ex.Message}"); }
            }
        }

        private async Task ResumeAllSubStationsAsync()
        {
            foreach (var s in _subStations)
            {
                try { await s.ResumeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.Error($"【主控】续跑工站 [{s.StationName}] 失败: {ex.Message}"); }
            }
        }

        private void PauseAllSubStationsSafely()
        {
            _subStationStopsAreIntentional = true;
            try
            {
                foreach (var s in _subStations)
                {
                    try { s.Pause(); }
                    catch (Exception ex) { _logger.Error($"工站 [{s.StationName}] 暂停失败: {ex.Message}"); }
                }
            }
            finally { _subStationStopsAreIntentional = false; }
        }

        private void TriggerAllSubStationAlarmsSafely()
        {
            foreach (var s in _subStations)
            {
                try { s.TriggerAlarm(); }
                catch (Exception ex) { _logger.Error($"【主控】级联触发工站 [{s.StationName}] 报警失败: {ex.Message}"); }
            }
        }

        #endregion

        #region Hardware Smart Routing

        protected virtual void OnHardwareInputReceived(string inputType)
        {
            switch (inputType)
            {
                case HardwareInputType.Start: _ = ExecuteSmartStartAsync(); break;
                case HardwareInputType.Pause: PauseAll(); break;
                case HardwareInputType.Reset: _ = ResetAllAsync(); break;
            }
        }

        private async Task ExecuteSmartStartAsync()
        {
            try
            {
                switch (CurrentState)
                {
                    case MachineState.Uninitialized:
                        await InitializeAllAsync().ConfigureAwait(false);
                        if (CurrentState == MachineState.Idle) await StartAllAsync().ConfigureAwait(false);
                        break;
                    case MachineState.Idle:
                        await StartAllAsync().ConfigureAwait(false);
                        break;
                    case MachineState.Paused:
                        await ResumeAllAsync().ConfigureAwait(false);
                        break;
                    default:
                        _logger.Warn($"【全局主控】当前状态 {CurrentState} 忽略启动指令。");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"【全局主控】SmartStart 执行失败: {ex.Message}");
            }
        }

        #endregion

        #region Sub-Station Events

        private void OnSubStationStateChanged(object? sender, StationStateChangedEventArgs e)
        {
            var stationName = (sender as IStation)?.StationName ?? "未知工站";
            var masterState = CurrentState;

            // 初始化阶段任一工站失败 → 取消并行初始化
            if (masterState == MachineState.Initializing
                && (e.NewState == MachineState.InitAlarm || e.NewState == MachineState.RunAlarm))
            {
                _logger.Warn($"【主控】工站 [{stationName}] 初始化失败({e.NewState})，取消其余工站初始化。");
                try { _initCts?.Cancel(); } catch { }
            }

            // 运行/暂停下工站非预期跳回 Uninitialized → 全线报警
            if (!_subStationStopsAreIntentional
                && (masterState == MachineState.Running || masterState == MachineState.Paused)
                && e.NewState == MachineState.Uninitialized
                && e.OldState != MachineState.Resetting)
            {
                _logger.Warn($"【主控守卫】状态撕裂！工站 [{stationName}] 从 {e.OldState} 跳回 Uninitialized，触发全线报警。");
                _ = Task.Run(() =>
                {
                    try { Fire(MachineTrigger.Error); }
                    catch (Exception ex) { _logger.Error($"【主控守卫】切入报警状态失败: {ex.Message}"); }
                });
            }
        }

        private void OnSubStationAlarm(object? sender, StationAlarmEventArgs e)
        {
            if (e.ErrorCode == AlarmCodes.System.CascadeAlarm) return;

            var source = (sender as IStation)?.StationName ?? "未知工站";
            var dedupKey = $"{source}:{e.ErrorCode}:{e.HardwareName ?? string.Empty}";

            if (_reportedAlarmKeys.TryAdd(dedupKey, 0))
            {
                _logger.Fatal($"【报警】{source} | {e.ErrorCode}" +
                    (e.HardwareName != null ? $" | 硬件:{e.HardwareName}" : string.Empty) +
                    (e.RuntimeMessage != null ? $" | {e.RuntimeMessage}" : string.Empty));

                _alarmService?.TriggerAlarm(source, e.ErrorCode, e.RuntimeMessage);
                try { MasterAlarmTriggered?.Invoke(this, e); }
                catch (Exception ex) { _logger.Error($"【主控】MasterAlarmTriggered 订阅者异常: {ex.Message}"); }
            }

            _ = Task.Run(() =>
            {
                try { Fire(MachineTrigger.Error); }
                catch (Exception ex) { _logger.Fatal($"【主控】尝试切入报警失败: {ex.Message}"); }
            });
        }

        private void OnSubStationAlarmAutoCleared(object? sender, EventArgs e)
        {
            var source = (sender as IStation)?.StationName ?? "未知工站";
            _logger.Info($"【主控】工站 [{source}] 硬件自恢复，主动清除报警服务记录。");
            _alarmService?.ClearAlarm(source);
        }

        #endregion

        #region Global Core API

        public Task StartAllAsync() => FireAsync(MachineTrigger.Start);

        public async Task StopAllAsync()
        {
            _subStationStopsAreIntentional = true;
            try
            {
                await Parallel.ForEachAsync(_subStations,
                    new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    async (station, _) =>
                    {
                        try { await station.StopAsync().ConfigureAwait(false); }
                        catch (Exception ex) { _logger.Warn($"【主控】工站 [{station.StationName}] 停止异常: {ex.Message}"); }
                    }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Warn($"【主控】并行停止产生异常: {ex.Message}");
            }
            finally
            {
                _subStationStopsAreIntentional = false;
            }
            await FireAsync(MachineTrigger.Stop).ConfigureAwait(false);
        }

        public void PauseAll() => Fire(MachineTrigger.Pause);

        public Task ResumeAllAsync() => FireAsync(MachineTrigger.Resume);

        public bool SetMode(OperationMode mode)
        {
            if (CurrentState != MachineState.Idle) return false;
            CurrentMode = mode;
            foreach (var s in _subStations) s.CurrentMode = mode;
            return true;
        }

        public async Task InitializeAllAsync()
        {
            if (!CanFire(MachineTrigger.Initialize)) return;

            _logger.Info("【主控】开始全线初始化(限流模式)...");
            Fire(MachineTrigger.Initialize);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            _initCts = cts;
            try
            {
                await Parallel.ForEachAsync(_subStations,
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cts.Token },
                    async (station, token) =>
                    {
                        try { await station.ExecuteInitializeAsync(token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { _logger.Error($"【主控】初始化工站 [{station.StationName}] 异常: {ex.Message}"); }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* 某工站失败导致主动取消 */ }
            catch (Exception ex)
            {
                _logger.Error($"【主控】初始化异常: {ex.Message}");
                _alarmService?.TriggerAlarm("主控", AlarmCodes.System.InitializationTimeout, "设备复位时间过长，请调整复位参数！");
                Fire(MachineTrigger.Error);
                return;
            }
            finally
            {
                Interlocked.CompareExchange(ref _initCts, null, cts);
                try { cts.Dispose(); } catch { }
            }

            bool hasFailedStation = _subStations.Any(s =>
                s.CurrentState == MachineState.InitAlarm || s.CurrentState == MachineState.RunAlarm);

            if (hasFailedStation)
            {
                _logger.Error("【主控】部分工站初始化失败，进入初始化报警。");
                Fire(MachineTrigger.Error);
            }
            else
            {
                Fire(MachineTrigger.InitializeDone);
            }
        }

        public async Task ResetAllAsync()
        {
            if (!CanFire(MachineTrigger.Reset)) return;

            _logger.Info("【主控】开始全线复位(限流并行模式)...");
            Fire(MachineTrigger.Reset);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                await Parallel.ForEachAsync(_subStations,
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cts.Token },
                    async (station, token) =>
                    {
                        try { await station.ExecuteResetAsync(token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }
                        catch (Exception ex) { _logger.Error($"【主控】复位工站 [{station.StationName}] 异常: {ex.Message}"); }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (cts.IsCancellationRequested)
                    _logger.Error("【主控】全线复位超时（30s），系统重新回到报警状态。");
                else
                    _logger.Warn("【主控】全线复位操作已被人工打断/取消。");
                Fire(MachineTrigger.Error);
                return;
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】复位失败: {ex.Message}，重新回到报警状态。");
                Fire(MachineTrigger.Error);
                return;
            }

            OnAfterResetSuccess();
            _alarmService?.ClearAllActiveAlarms();

            var resetTrigger = _masterCameFromInitAlarm
                ? MachineTrigger.ResetDoneUninitialized
                : MachineTrigger.ResetDone;

            await FireAsync(resetTrigger).ConfigureAwait(false);
        }

        #endregion

        #region Hardware Reset Mechanisms

        public Task RequestSystemResetAsync()
        {
            _logger.Info("【主控】接收到系统复位请求，开始执行全线复位...");
            return ResetAllAsync();
        }

        protected virtual void OnAfterResetSuccess() { }

        public void RegisterHardwareResetHandler(Action<HardwareResetRequest> handler)
            => _hardwareResetHandler = handler;

        public virtual void OnHardwareResetRequested(HardwareResetRequest request)
        {
            if (request == null) return;

            // 优先走注册的硬件路由
            var handler = _hardwareResetHandler;
            if (handler != null)
            {
                try { handler(request); return; }
                catch (Exception ex) { _logger.Error($"【主控】硬件复位路由异常: {ex.Message}"); }
            }

            var station = _subStations.FirstOrDefault(s => s.StationName == request.Source);
            if (station == null) return;
            if (station.CurrentState != MachineState.InitAlarm && station.CurrentState != MachineState.RunAlarm) return;

            _logger.Info($"【主控】接收到硬件复位请求，来源：{request.Source}，错误码：{string.Join(", ", request.ErrorCodes)}");

            _ = Task.Run(async () =>
            {
                try { await station.ExecuteResetAsync(CancellationToken.None).ConfigureAwait(false); }
                catch (OperationCanceledException) { _logger.Warn($"【主控】子站局部硬件复位请求已被取消，来源：{request.Source}"); }
                catch (Exception ex) { _logger.Error($"【主控】硬件复位请求执行失败，来源：{request.Source}: {ex.Message}"); }
            });
        }

        #endregion

        #region Thread-Safe Triggers

        private bool CanFire(MachineTrigger trigger) => _globalMachine.CanFire(trigger);

        private void Fire(MachineTrigger trigger)
        {
            if (_disposed) return;
            bool acquired = false;
            try
            {
                acquired = _machineLock.Wait(TimeSpan.FromSeconds(5));
                if (!acquired)
                {
                    _logger.Error($"【主控】Fire({trigger}) 获取状态锁超时。");
                    return;
                }
                if (_globalMachine.CanFire(trigger)) _globalMachine.Fire(trigger);
            }
            catch (ObjectDisposedException) { }
            finally
            {
                if (acquired) { try { _machineLock.Release(); } catch (ObjectDisposedException) { } }
            }
        }

        private async Task FireAsync(MachineTrigger trigger)
        {
            if (_disposed) return;
            bool acquired = false;
            try
            {
                acquired = await _machineLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (!acquired)
                {
                    _logger.Error($"【主控】FireAsync({trigger}) 获取状态锁超时。");
                    return;
                }
                if (_globalMachine.CanFire(trigger)) await _globalMachine.FireAsync(trigger).ConfigureAwait(false);
            }
            catch (ObjectDisposedException) { }
            finally
            {
                if (acquired) { try { _machineLock.Release(); } catch (ObjectDisposedException) { } }
            }
        }

        #endregion

        #region IDisposable

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_hardwareEventBus != null)
                _hardwareEventBus.HardwareInputTriggered -= OnHardwareInputReceived;

            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered -= OnSubStationAlarm;
                station.StationAlarmAutoCleared -= OnSubStationAlarmAutoCleared;
                station.StationStateChanged -= OnSubStationStateChanged;
            }

            try { _initCts?.Cancel(); } catch { }
            try { _initCts?.Dispose(); } catch { }
            try { _machineLock.Dispose(); } catch { }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}