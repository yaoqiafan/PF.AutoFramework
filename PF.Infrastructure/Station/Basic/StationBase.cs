using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Mechanisms;
using Stateless;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;


namespace PF.Infrastructure.Station.Basic
{
    /// <summary>
    /// 自动化工站（子线程）业务状态机基类
    ///
    /// 【状态生命周期 (State Lifecycle)】
    ///   Uninitialized ──(Initialize)──→ Initializing ──(InitializeDone)─────────→ Idle
    ///                                                  └─────────(Error)──────────────────→ Alarm
    ///   Idle          ──(Start)───────→ Running
    ///   Running       ──(Stop)────────→ Idle
    ///   Alarm         ──(Reset)───────→ Resetting  ──(ResetDone)────────────────→ Idle          (运行期报警复位)
    ///                                                  └─────────(ResetDoneUninitialized)─→ Uninitialized (初始化报警复位，强制重置)
    ///
    /// 【并发安全设计 (Concurrency  和 Thread Safety)】
    ///   · 独占变迁：所有状态变迁（<see cref="Fire(MachineTrigger)"/> / <see cref="FireAsync(MachineTrigger)"/>）均受 <see cref="_stateLock"/> 信号量保护，杜绝后台硬件报警与 UI 交互命令（如 Stop/Pause）导致的并发状态撕裂。
    ///   · 任务防重入：<see cref="MachineState.Running"/> 状态严格确保前置业务任务彻底销毁后，方可启动新任务，彻底消除“幽灵线程”（Orphan Threads）与竞态死锁。
    ///
    /// 💡 【继承与重写契约 (Inheritance Contracts)】
    /// 派生具体工艺工站时，需遵循以下方法重写规范：
    ///
    /// 1. 核心业务大循环（必须实现 <see langword="abstract"/>）：
    ///   - <see cref="ProcessNormalLoopAsync"/> : 正常生产节拍。内部须由 <see langword="while"/> (!<see cref="CancellationToken.IsCancellationRequested"/>) 驱动，配合 <see cref="WaitIOAsync"/> 执行非阻塞硬件交互。
    ///   - <see cref="ProcessDryRunLoopAsync"/> : 空跑验证节拍。主要用于设备无料脱机跑合，应故意跳过外部物料交互与同步协同逻辑。
    ///
    /// 2. 硬件控制与生命周期钩子（强烈建议重写 <see langword="virtual"/>）：
    ///   - <see cref="ExecuteInitializeAsync"/> : 硬件上电初始化（如伺服使能、回原点）。
    ///     * 守则：首行须显式推演 <see cref="Fire(MachineTrigger)"/> 触发 <see cref="MachineTrigger.Initialize"/>；执行成功以 <see cref="MachineTrigger.InitializeDone"/> 收尾；发生异常则向上抛出以进入 <see cref="MachineState.InitAlarm"/>。
    ///   - <see cref="ExecuteResetAsync"/>      : 报警解除后的机构安全自恢复（如清错、退刀、回安全位）。
    ///     * 守则：首行须显式推演 <see cref="Fire(MachineTrigger)"/> 触发 <see cref="MachineTrigger.Reset"/>；物理动作完成后，须 <see langword="await"/> <see cref="FireAsync(MachineTrigger)"/> 触发 <see cref="ResetCompletionTrigger"/> 完成状态闭环路由。
    ///   - <see cref="OnPhysicalStopAsync"/>    : 机构级物理制动。当工站接收 Stop 命令或被外部异常打断时调用。必须在此实现危险源切断（如关断气缸、急停马达）。
    ///
    /// 3. 框架底层行为干预（按需扩展 <see langword="virtual"/>）：
    ///   - <see cref="ProcessLoopAsync"/>       : 模式路由分发器。若需引入非标运行模式（如 GRR 测试、设备维护模式），重写此方法进行拦截与分发。
    ///   - <see cref="DisposeAsync"/>           : 非托管资源清理。若子类独占了相机句柄、串口连接等资源，需重写以安全释放，且末尾必须调用 <see langword="await"/> 基础类的清理方法。
    /// </summary>
    /// namespace PF.Infrastructure.Station.Basic


    public abstract class StationBase<T> : IStation where T : StationMemoryBaseParam, new()
    {
        #region 1. MVVM 数据绑定

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string StationName { get; }
        public MachineState CurrentState => _machine.State;

        private string _currentStepDescription = "就绪";
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            protected set
            {
                if (_currentStepDescription == value) return;
                _currentStepDescription = value;
                RaisePropertyChanged();
            }
        }

        private OperationMode _currentMode = OperationMode.Normal;
        public OperationMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (_currentMode == value) return;
                _currentMode = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region 2. 外部交互事件

        public event EventHandler<StationAlarmEventArgs>? StationAlarmTriggered;
        public event EventHandler? StationAlarmAutoCleared;
        public event EventHandler<StationStateChangedEventArgs>? StationStateChanged;

        #endregion

        #region 3. 核心依赖与内部状态标记

        protected readonly ILogService? _logger;
        protected readonly StateMachine<MachineState, MachineTrigger> _machine;
        private readonly SemaphoreSlim _stateLock = new(1, 1);
        private CancellationTokenSource? _runCts;
        private Task? _workflowTask;

        // 语义解耦：
        // _stopRequested: 用户/主控显式发起 Stop，用于抑制 workflow 异常被误判为报警
        // _pauseRequested: 用户/主控显式发起 Pause，用于抑制 Resume 前的报警
        // _alarmInterrupted: 硬件/业务主动触发报警
        private volatile bool _stopRequested;
        private volatile bool _pauseRequested;
        private volatile bool _alarmInterrupted;
        private volatile bool _disposed;

        private volatile string? _pendingAlarmCode;
        private volatile StationAlarmEventArgs? _pendingAlarmContext;

        private volatile bool _cameFromInitAlarm;
        protected bool CameFromInitAlarm => _cameFromInitAlarm;

        #endregion

        #region 4. 构造函数与状态机配置

        protected StationBase(string name, ILogService? logger)
        {
            StationName = name ?? throw new ArgumentNullException(nameof(name));
            _logger = logger;
            _machine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureStateMachine();
            ReadMemoryParam();
        }

        private void ConfigureStateMachine()
        {
            _machine.OnTransitioned(t =>
            {
                _logger?.Debug($"[{StationName}] 状态变迁: {t.Source} -> {t.Destination}");
                RaisePropertyChanged(nameof(CurrentState));

                // 异步派发，避免订阅者同步回调引发状态机重入死锁
                var handler = StationStateChanged;
                if (handler != null)
                {
                    var args = new StationStateChangedEventArgs { OldState = t.Source, NewState = t.Destination };
                    _ = Task.Run(() =>
                    {
                        try { handler.Invoke(this, args); }
                        catch (Exception ex) { _logger?.Error($"[{StationName}] StationStateChanged 订阅者异常: {ex.Message}"); }
                    });
                }
            });

            _machine.Configure(MachineState.Uninitialized)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing);

            _machine.Configure(MachineState.Initializing)
                .OnEntry(() =>
                {
                    _stopRequested = false;
                    _pauseRequested = false;
                })
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.InitAlarm);

            _machine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _machine.Configure(MachineState.Running)
                .OnEntryAsync(OnStartRunningAsync)
                .OnExit(_ => _runCts?.Cancel())
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _machine.Configure(MachineState.Paused)
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _machine.Configure(MachineState.InitAlarm)
                .OnEntry(EnterAlarm_Init)
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _machine.Configure(MachineState.RunAlarm)
                .OnEntry(EnterAlarm_Run)
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _machine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized)
                .PermitDynamic(MachineTrigger.Error,
                    () => _cameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        private void EnterAlarm_Init() => EnterAlarmCommon(isInit: true);
        private void EnterAlarm_Run() => EnterAlarmCommon(isInit: false);

        private void EnterAlarmCommon(bool isInit)
        {
            _cameFromInitAlarm = isInit;
            var code = Interlocked.Exchange(ref _pendingAlarmCode, null) ?? AlarmCodes.System.CascadeAlarm;
            var context = Interlocked.Exchange(ref _pendingAlarmContext, null)
                          ?? new StationAlarmEventArgs { ErrorCode = code };

            // 异步派发，避免订阅者重入 Fire 死锁
            var handler = StationAlarmTriggered;
            if (handler != null)
            {
                _ = Task.Run(() =>
                {
                    try { handler.Invoke(this, context); }
                    catch (Exception ex) { _logger?.Error($"[{StationName}] StationAlarmTriggered 订阅者异常: {ex.Message}"); }
                });
            }
        }

        #endregion

        #region 5. 线程与生命周期管控

        private async Task CancelAndAwaitOldTaskAsync()
        {
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }

            var old = _workflowTask;
            if (old is { IsCompleted: false })
            {
                try { await old.ConfigureAwait(false); }
                catch { /* 忽略旧任务异常 */ }
            }
        }

        private Task OnStartRunningAsync()
        {
            // 旧 CTS 由 OnExit 负责 Cancel；此处新建
            var oldCts = Interlocked.Exchange(ref _runCts, new CancellationTokenSource());
            try { oldCts?.Dispose(); } catch { }

            _pauseRequested = false;
            _stopRequested = false;
            _alarmInterrupted = false;

            var pauseCheck = CreatePauseCheckDelegate();
            foreach (var m in GetMechanisms())
                m.PauseCheckAsync = pauseCheck;

            var token = _runCts!.Token;
            _workflowTask = Task.Run(() => ProcessWrapperAsync(token), CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task ProcessWrapperAsync(CancellationToken token)
        {
            try
            {
                await ProcessLoopAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                if (_pauseRequested) _logger?.Info($"[{StationName}] 工站响应暂停命令，等待续跑...");
                else if (_alarmInterrupted) _logger?.Warn($"[{StationName}] 被外部报警打断，线程安全退出。");
                else _logger?.Warn($"[{StationName}] 子线程被安全打断并退出。");
                _alarmInterrupted = false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 业务异常: {ex.Message}");

                // 故意停止时忽略
                if (_stopRequested) return;

                Interlocked.CompareExchange(ref _pendingAlarmCode, AlarmCodes.System.StationSyncError, null);

                // 独立调度以避免在 workflowTask 上下文中回触发状态机
                _ = Task.Run(() =>
                {
                    try
                    {
                        var state = _machine.State;
                        if (state != MachineState.InitAlarm && state != MachineState.RunAlarm)
                            Fire(MachineTrigger.Error);
                    }
                    catch (Exception fireEx)
                    {
                        _logger?.Fatal($"[{StationName}] 业务异常后触发报警状态失败: {fireEx.Message}");
                    }
                });
            }
        }

        public async Task StartAsync()
        {
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Start).ConfigureAwait(false);
        }

        public void Pause()
        {
            _pauseRequested = true;
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }
            Fire(MachineTrigger.Pause);
            _ = Task.Run(async () =>
            {
                try { await OnPhysicalPauseAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger?.Error($"[{StationName}] OnPhysicalPauseAsync 异常: {ex.Message}"); }
            });
        }

        public async Task StopAsync()
        {
            _stopRequested = true;
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }

            var wf = _workflowTask;
            if (wf is { IsCompleted: false })
            {
                try { await wf.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
                catch { /* 超时/异常均忽略 */ }
            }

            try { await OnPhysicalStopAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger?.Error($"[{StationName}] OnPhysicalStopAsync 异常: {ex.Message}"); }
            finally { Fire(MachineTrigger.Stop); }  // 无论物理制动是否报错，必定归位
        }

        public async Task ResumeAsync()
        {
            _pauseRequested = false;
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Resume).ConfigureAwait(false);
        }

        #endregion

        #region 6. 工艺循环抽象与硬件控制契约

        protected virtual async Task ProcessLoopAsync(CancellationToken token)
        {
            switch (CurrentMode)
            {
                case OperationMode.Normal: await ProcessNormalLoopAsync(token).ConfigureAwait(false); break;
                case OperationMode.DryRun: await ProcessDryRunLoopAsync(token).ConfigureAwait(false); break;
                default: _logger?.Warn($"[{StationName}] 未知运行模式: {CurrentMode}"); break;
            }
        }

        protected abstract Task ProcessNormalLoopAsync(CancellationToken token);
        protected abstract Task ProcessDryRunLoopAsync(CancellationToken token);

        /// <summary>
        /// 初始化钩子。子类 override 时不要再调用 Fire，仅做物理动作。
        /// </summary>
        protected virtual Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;

        /// <summary>
        /// 复位钩子。子类 override 时不要再调用 Fire，仅做物理动作。
        /// </summary>
        protected virtual Task OnResetAsync(CancellationToken token) => Task.CompletedTask;

        public virtual async Task ExecuteInitializeAsync(CancellationToken token)
        {
            if (!Fire(MachineTrigger.Initialize)) return;  // 状态机拒绝则不推进
            try
            {
                token.ThrowIfCancellationRequested();
                await OnInitializeAsync(token).ConfigureAwait(false);
                Fire(MachineTrigger.InitializeDone);
            }
            catch (OperationCanceledException)
            {
                _logger?.Warn($"[{StationName}] 初始化基类操作被取消。");
                Fire(MachineTrigger.Error);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 初始化异常: {ex.Message}");
                Fire(MachineTrigger.Error);
                throw;
            }
        }

        public virtual async Task ExecuteResetAsync(CancellationToken token)
        {
            if (!Fire(MachineTrigger.Reset)) return;
            try
            {
                token.ThrowIfCancellationRequested();
                await OnResetAsync(token).ConfigureAwait(false);
                await FireAsync(ResetCompletionTrigger).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Warn($"[{StationName}] 复位基类操作被取消。");
                Fire(MachineTrigger.Error);
                throw;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 复位异常: {ex.Message}");
                Fire(MachineTrigger.Error);
                throw;
            }
        }

        protected virtual Task OnPhysicalStopAsync() => Task.CompletedTask;
        protected virtual Task OnPhysicalPauseAsync() => OnPhysicalStopAsync();

        protected virtual IEnumerable<BaseMechanism> GetMechanisms() => Enumerable.Empty<BaseMechanism>();

        private Func<CancellationToken, Task<bool>> CreatePauseCheckDelegate() => _ => Task.FromResult(false);

        #endregion

        #region 7. 报警与复位控制

        public void TriggerAlarm()
        {
            var state = CurrentState;
            if (state == MachineState.InitAlarm || state == MachineState.RunAlarm) return;

            _alarmInterrupted = true;
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }
            Fire(MachineTrigger.Error);
        }

        public void TriggerAlarm(string errorCode)
        {
            _pendingAlarmCode = errorCode;
            TriggerAlarm();
        }

        public void TriggerAlarm(string errorCode, string? runtimeMessage)
        {
            _pendingAlarmCode = errorCode;
            _pendingAlarmContext = runtimeMessage != null
                ? new StationAlarmEventArgs { ErrorCode = errorCode, RuntimeMessage = runtimeMessage }
                : null;
            TriggerAlarm();
        }

        protected void RaiseAlarm(string errorCode)
        {
            if (_stopRequested) return;  // 仅 Stop 抑制；Pause 不抑制运行期硬件异常
            _pendingAlarmCode = errorCode;
            TriggerAlarm();
        }

        protected void RaiseAlarm(string errorCode, string runtimeMessage)
        {
            if (_stopRequested) return;
            _pendingAlarmCode = errorCode;
            _pendingAlarmContext = new StationAlarmEventArgs { ErrorCode = errorCode, RuntimeMessage = runtimeMessage };
            TriggerAlarm();
        }

        protected void RaiseAlarm(StationAlarmEventArgs context)
        {
            if (_stopRequested) return;
            _pendingAlarmCode = context.ErrorCode;
            _pendingAlarmContext = context;
            TriggerAlarm();
        }

        protected void RaiseStationAlarmAutoCleared()
            => StationAlarmAutoCleared?.Invoke(this, EventArgs.Empty);

        public void ResetAlarm() => Fire(MachineTrigger.Reset);

        protected MachineTrigger ResetCompletionTrigger =>
            _cameFromInitAlarm ? MachineTrigger.ResetDoneUninitialized : MachineTrigger.ResetDone;

        #endregion

        #region 8. 状态跳转引擎

        /// <summary>
        /// 线程安全地触发状态机。返回值：是否真正执行了跳转。
        /// </summary>
        protected bool Fire(MachineTrigger trigger)
        {
            if (_disposed) return false;
            bool acquired = false;
            try
            {
                acquired = _stateLock.Wait(TimeSpan.FromSeconds(5));
                if (!acquired)
                {
                    _logger?.Error($"[{StationName}] Fire({trigger}) 获取状态锁超时 — 可能存在死锁风险。");
                    return false;
                }
                if (_machine.CanFire(trigger))
                {
                    _machine.Fire(trigger);
                    return true;
                }
                return false;
            }
            catch (ObjectDisposedException) { return false; }
            finally
            {
                if (acquired)
                {
                    try { _stateLock.Release(); } catch (ObjectDisposedException) { }
                }
            }
        }

        protected async Task<bool> FireAsync(MachineTrigger trigger)
        {
            if (_disposed) return false;
            bool acquired = false;
            try
            {
                acquired = await _stateLock.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                if (!acquired)
                {
                    _logger?.Error($"[{StationName}] FireAsync({trigger}) 获取状态锁超时 — 可能存在死锁风险。");
                    return false;
                }
                if (_machine.CanFire(trigger))
                {
                    await _machine.FireAsync(trigger).ConfigureAwait(false);
                    return true;
                }
                return false;
            }
            catch (ObjectDisposedException) { return false; }
            finally
            {
                if (acquired)
                {
                    try { _stateLock.Release(); } catch (ObjectDisposedException) { }
                }
            }
        }

        #endregion

        #region 9. 工站业务等待与 IO 辅助方法

        protected Task WaitAsync(int milliseconds, CancellationToken token)
            => Task.Delay(milliseconds, token);

        protected async Task<bool> WaitConditionAsync(
            Func<bool> condition,
            int timeoutMs = 5_000,
            CancellationToken token = default,
            int pollIntervalMs = 20)
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            try
            {
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    try
                    {
                        if (condition()) return true;
                    }
                    catch (Exception ex)
                    {
                        _logger?.Error($"[{StationName}] WaitConditionAsync 的 condition 委托抛异常: {ex.Message}");
                        throw;
                    }
                    await Task.Delay(pollIntervalMs, linked.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    _logger?.Error($"[{StationName}] 等待条件超时（{timeoutMs} ms）");
                    return false;
                }
                throw;
            }
        }

        protected async Task<bool> WaitIOAsync(IIOController io, int portIndex, bool targetState,
            int timeoutMs = 5_000, CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] → {targetState}");
            bool result = await io.WaitInputAsync(portIndex, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result) _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        protected async Task<bool> WaitIOAsync<TEnum>(IIOController io, TEnum inputName, bool targetState,
            int timeoutMs = 5_000, CancellationToken token = default) where TEnum : Enum
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] → {targetState}");
            bool result = await io.WaitInputAsync(inputName, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result) _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        protected async Task<bool> WaitSyncAsync(IStationSyncService sync, string signalName,
            int timeoutMs = 30_000, CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待同步信号 [{signalName}]...");
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            try
            {
                await sync.WaitAsync(signalName, linked.Token).ConfigureAwait(false);
                _logger?.Info($"[{StationName}] 同步信号 [{signalName}] 已触发");
                return true;
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    _logger?.Error($"[{StationName}] 等待同步信号 [{signalName}] 超时（{timeoutMs} ms）");
                    return false;
                }
                throw;
            }
        }

        #endregion

        #region 10. 记忆参数管理

        public virtual T? MemoryParam { get; set; }

        private string MemoryFileDir => Path.Combine(ConstGlobalParam.ConfigPath, "StationMemoryParam");
        private string MemoryFilePath => Path.Combine(MemoryFileDir, $"{StationName}.json");

        private void ReadMemoryParam()
        {
            try
            {
                var path = MemoryFilePath;
                if (!File.Exists(path)) return;
                var json = File.ReadAllText(path);
                var deserialized = JsonSerializer.Deserialize<T>(json);
                if (deserialized == null)
                {
                    _logger?.Warn($"[{StationName}] 记忆参数反序列化为 null，已忽略文件。");
                    return;
                }
                deserialized.IsWrite = false;
                MemoryParam = deserialized;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 读取记忆参数失败: {ex.Message}");
            }
        }

        private void WriteMemoryParam()
        {
            if (MemoryParam == null) return;
            try
            {
                Directory.CreateDirectory(MemoryFileDir);
                MemoryParam.IsWrite = true;

                // 原子写入：先写临时文件，再 Replace，避免中途崩溃损坏 JSON
                var finalPath = MemoryFilePath;
                var tempPath = finalPath + ".tmp";
                var json = JsonSerializer.Serialize(MemoryParam);
                File.WriteAllText(tempPath, json);

                if (File.Exists(finalPath))
                    File.Replace(tempPath, finalPath, null);
                else
                    File.Move(tempPath, finalPath);
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 写入记忆参数失败: {ex.Message}");
            }
        }

        #endregion

        #region 11. 资源清理

        public virtual async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;

            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }

            var wf = _workflowTask;
            if (wf is { IsCompleted: false })
            {
                try { await wf.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch (TimeoutException) { _logger?.Warn($"[{StationName}] DisposeAsync 等待退出超时"); }
                catch { }
            }

            WriteMemoryParam();

            try { _runCts?.Dispose(); } catch { }
            try { _stateLock.Dispose(); } catch { }
            GC.SuppressFinalize(this);
        }

        public virtual void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }
            WriteMemoryParam();
            try { _runCts?.Dispose(); } catch { }
            try { _stateLock.Dispose(); } catch { }
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    public class StationMemoryBaseParam
    {
        public bool IsWrite { get; set; }
    }

    public abstract class StationBase<TMemory, TStep> : StationBase<TMemory>
        where TMemory : StationMemoryBaseParam, new()
        where TStep : struct, Enum
    {
        protected TStep _currentStep;
        protected TStep _resumeStep;
        protected string? _cachedErrorCode;

        protected StationBase(string name, ILogService? logger, TStep initialStep) : base(name, logger)
        {
            _currentStep = initialStep;
            _resumeStep = initialStep;
        }

        protected void RouteToError(TStep errorStep, TStep resumeStep, string? errorCode = null)
        {
            _currentStep = errorStep;
            _resumeStep = resumeStep;
            _cachedErrorCode = errorCode;
        }
    }
}
