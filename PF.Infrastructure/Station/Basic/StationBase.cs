using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using Stateless;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PF.Infrastructure.Station.Basic
{
    /// <summary>
    /// 工站（子线程）状态机基础类
    ///
    /// 生命周期：
    ///   Uninitialized → (Initialize) → Initializing → (InitializeDone)        → Idle
    ///                                                 → (Error)                → Alarm
    ///   Idle          → (Start)      → Running
    ///   Running       → (Stop)       → Idle
    ///   Alarm         → (Reset)      → Resetting → (ResetDone)            → Idle          （正常运行报警后复位）
    ///                                            → (ResetDoneUninitialized) → Uninitialized （初始化失败后复位，强制重新初始化）
    ///
    /// 并发安全设计：
    ///   · 所有状态机跳转（Fire / FireAsync）均通过 _stateLock（SemaphoreSlim 1,1）独占执行，
    ///     防止后台线程报警与 UI 线程发出的 Stop/Pause 同时修改状态机内部状态。
    ///   · Running 状态采用 OnEntryAsync，确保旧任务彻底终止后再启动新任务，消除"幽灵线程"。
    /// </summary>
    public abstract class StationBase<T> : IDisposable, IAsyncDisposable, INotifyPropertyChanged where T : StationMemoryBaseParam
    {
        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ── 标识 ─────────────────────────────────────────────────────────────
        public string StationName { get; }

        /// <summary>当前状态机状态（状态变化时触发 PropertyChanged）</summary>
        public MachineState CurrentState => _machine.State;

        /// <summary>
        /// 当前步序描述，供 UI 实时显示（如"正在取料..."、"等待槽位空闲..."）。
        /// 子类在每次步序切换时通过 protected set 赋值，自动触发 PropertyChanged。
        /// </summary>
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

        // 向上层（主控）抛出的报警事件（string = AlarmCodes.* 常量，非自由文本）
        public event EventHandler<string> StationAlarmTriggered;

        /// <summary>
        /// 工站内所有机构均已自恢复（硬件清警）时触发，通知主控可主动清除 AlarmService 对应记录。
        /// 由子类在订阅模组 AlarmAutoCleared 事件后调用 <see cref="RaiseStationAlarmAutoCleared"/>。
        /// </summary>
        public event EventHandler StationAlarmAutoCleared;

        protected readonly ILogService _logger;
        protected readonly StateMachine<MachineState, MachineTrigger> _machine;

        // ── 并发安全：所有状态机跳转均通过此信号量独占执行 ─────────────────
        // 使用 SemaphoreSlim 而非 lock，以支持 async/await 场景下的无死锁等待。
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        // 线程生命周期管理
        private CancellationTokenSource _runCts;
        private Task _workflowTask;

        // 异步暂停门：volatile 保证多线程可见性；RunContinuationsAsynchronously 防止 TrySetResult 内联执行续体导致死锁。
        // Paused.OnEntry 替换为新建未完成 TCS（关门），Paused.OnExit / Alarm.OnEntry 调用 TrySetResult(true)（开门）。
        private volatile TaskCompletionSource<bool> _pauseGate;

        // 标记当前业务线程是被"外部报警"打断（true），还是"正常停止"打断（false）。
        // volatile 确保多线程可见性：TriggerAlarm 在状态机线程写入，ProcessWrapperAsync 在业务线程读取。
        private volatile bool _alarmInterrupted = false;

        // 结构化报警码：业务代码或外部调用在触发报警前设置，Alarm.OnEntry 读取后上报并重置。
        // 未显式设置时回落至 AlarmCodes.System.StationSyncError（通用工站异常兜底）。
        private volatile string _pendingAlarmCode;

        // 标记当前报警是否源于初始化阶段（Initializing → Alarm）。
        // 若为 true，复位完成后回到 Uninitialized 而非 Idle，强制重新执行初始化。
        // 在 Uninitialized.OnEntry 中清除，确保成功初始化后状态干净。
        protected bool _initializationFailed = false;

        protected StationBase(string name, ILogService logger)
        {
            StationName = name;
            _logger = logger;

            // 初始状态：未暂停（gate 已完成，业务线程可直接通过）
            _pauseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pauseGate.TrySetResult(true);

            // 初始状态：Uninitialized（硬件未就绪，禁止直接启动）
            _machine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureStateMachine();
            ReadMemoryParam();
        }



    

        private void ConfigureStateMachine()
        {
            _machine.OnTransitioned(t =>
        {
            _logger?.Debug($"[{StationName}] 状态变迁: {t.Source} -> {t.Destination}");
            RaisePropertyChanged(nameof(CurrentState)); // 通知 UI 刷新状态绑定
        });

            // --- 未初始化状态 ---
            _machine.Configure(MachineState.Uninitialized)
                .OnEntry(() => _initializationFailed = false)  // 进入未初始化时清除初始化失败标记
                .Permit(MachineTrigger.Initialize, MachineState.Initializing);

            // --- 初始化中状态 ---
            _machine.Configure(MachineState.Initializing)
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 待机状态 ---
            _machine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Error, MachineState.Alarm)
                .Ignore(MachineTrigger.Stop);

            // --- 运行状态 ---
            // 【关键】OnEntryAsync 确保：旧任务彻底结束 → 再启动新任务，消除幽灵线程。
            // 注意：凡触发进入 Running 的 Fire 调用（Start / Resume）均须使用 FireAsync。
            _machine.Configure(MachineState.Running)
                .OnEntryAsync(OnStartRunningAsync)
                .OnExit(OnStopRunning)
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 暂停状态 ---
            _machine.Configure(MachineState.Paused)
                .OnEntry(() =>
                    // 关门：新建未完成 TCS，业务线程在 CheckPauseAsync 处挂起（不占用线程）
                    _pauseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
                .OnExit(() =>
                    // 开门：完成当前 TCS，释放所有挂起在 CheckPauseAsync 的续体
                    _pauseGate.TrySetResult(true))
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 报警状态 ---
            _machine.Configure(MachineState.Alarm)
                .OnEntry(t =>
                {
                    // 若报警来源于初始化阶段，标记需要重新初始化（复位后回到 Uninitialized 而非 Idle）。
                    // 若来源于 Resetting（复位失败再次报警），保留已有标记，确保最终仍回到正确目标状态。
                    if (t.Source == MachineState.Initializing)
                        _initializationFailed = true;

                    // 开门：防止 Paused → Alarm 时业务续体永久阻塞在已关闭的门上
                    _pauseGate.TrySetResult(true);

                    // 读取并重置待上报的结构化报警码（未设置时兜底使用通用工站异常码）。
                    // 必须无条件触发：主控依赖此事件感知子站已进入 Alarm，以维护整机状态同步。
                    // "过滤兜底码不写入全局 AlarmService"的职责上移至 BaseMasterController.OnSubStationAlarm。
                    var code = _pendingAlarmCode ?? AlarmCodes.System.StationSyncError;
                    _pendingAlarmCode = null;
                    StationAlarmTriggered?.Invoke(this, code);
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting); // Alarm → Resetting（对齐主控复位路径）

            // --- 复位中状态 ---
            _machine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)                    // 复位成功 → Idle（正常运行报警）
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized) // 复位成功 → Uninitialized（初始化失败报警）
                .Permit(MachineTrigger.Error, MachineState.Alarm);                      // 复位失败 → 回到 Alarm，允许再次复位
        }

        #region 线程生命周期管控

        /// <summary>
        /// 取消旧任务并等待其彻底退出，在获取 _stateLock 之前调用。
        ///
        /// 【关键设计】此方法必须在 FireAsync(Start/Resume) 持锁之前完成：
        ///   若在锁内等待旧任务，而旧任务的 catch(Exception) 路径调用 Fire(Error)
        ///   尝试同步获取同一把锁，将形成循环等待 → 永久死锁。
        ///   将等待移至锁外，可彻底消除该循环依赖。
        /// </summary>
        private async Task CancelAndAwaitOldTaskAsync()
        {
            _runCts?.Cancel();
            if (_workflowTask is { IsCompleted: false })
            {
                try { await _workflowTask.ConfigureAwait(false); }
                catch { /* 旧任务取消或异常退出，均忽略 */ }
            }
        }

        /// <summary>
        /// Running 状态同步入口（已简化）：
        ///   旧任务的取消与等待已由 StartAsync/ResumeAsync 在锁外完成，
        ///   此处仅负责建立新 CTS、启动新任务。
        /// </summary>
        private Task OnStartRunningAsync()
        {
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            _alarmInterrupted = false;
            _workflowTask = Task.Run(() => ProcessWrapperAsync(_runCts.Token));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Running 状态同步出口：仅发出取消信号。
        /// 不在此处等待任务结束——OnStopRunning 在 _stateLock 持有期间被调用，
        /// 若同时等待可能阻塞后台任务调用 Fire(Error) 时的加锁请求，造成死锁。
        /// 任务的彻底终止由下次 OnStartRunningAsync 的 await _workflowTask 保证。
        /// </summary>
        private void OnStopRunning()
        {
            _runCts?.Cancel();
        }

        /// <summary>
        /// 内部包装器，负责全局的异常捕获，防止子线程崩溃导致程序闪退
        /// </summary>
        private async Task ProcessWrapperAsync(CancellationToken token)
        {
            try
            {
                await ProcessLoopAsync(token);
            }
            catch (OperationCanceledException)
            {
                if (_alarmInterrupted)
                    _logger?.Warn($"[{StationName}] 业务流程被外部报警打断，线程安全退出。");
                else
                    _logger?.Warn($"[{StationName}] 子线程被安全打断并退出。");
                _alarmInterrupted = false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 业务逻辑发生异常: {ex.Message}");

                // 业务代码未显式调用 RaiseAlarm 时兜底赋码，确保未预期崩溃能被 AlarmService 正确上报。
                // 显式调用过 RaiseAlarm 的路径 _pendingAlarmCode 已非空，?? 不会覆盖精确报警码。
                _pendingAlarmCode ??= AlarmCodes.System.StationSyncError;

                // 🚨 修复 P0 级死锁：
                // 必须通过 Task.Run 脱离当前任务上下文，让 _workflowTask 立即结束
                // 从而释放 OnStartRunningAsync 中的 await 锁等待，防止与 StartAsync 形成循环死锁。
                _ = Task.Run(() =>
                {
                    try
                    {
                        // 防止重入：主控急停已通过 TriggerAlarm() 将本工站拉入 Alarm 后，
                        // 业务线程的 OperationCanceledException 在极端竞态下可能漏入此 catch，
                        // 若再次 Fire(Error) 会导致 Stateless 因"当前状态无此转换"抛出异常。
                        if (_machine.State != MachineState.Alarm)
                            Fire(MachineTrigger.Error);
                    }
                    catch (Exception fireEx)
                    {
                        _logger?.Fatal($"[{StationName}] 业务异常后尝试触发报警状态失败: {fireEx.Message}");
                    }
                });
            }
        }

        #endregion

        /// <summary>
        /// 当前运行模式（由 MasterController 在 Idle 状态下统一设置后下发至各工站）。
        /// 属性变化时触发 PropertyChanged，供 UI 绑定。
        /// </summary>
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

        // --- 强制子类必须实现的工艺大循环（按模式细分） ---

        /// <summary>
        /// 正常生产模式工艺循环
        /// </summary>
        protected abstract Task ProcessNormalLoopAsync(CancellationToken token);

        /// <summary>
        /// 空跑验证模式工艺循环（跳过物料等待与部分外部协同）
        /// </summary>
        protected abstract Task ProcessDryRunLoopAsync(CancellationToken token);

        /// <summary>
        /// 核心工艺循环分发器。根据当前模式自动路由至对应的子类实现。
        /// 子类通常无需重写此方法。
        /// </summary>
        protected virtual async Task ProcessLoopAsync(CancellationToken token)
        {
            switch (CurrentMode)
            {
                case OperationMode.Normal:
                    await ProcessNormalLoopAsync(token);
                    break;
                case OperationMode.DryRun:
                    await ProcessDryRunLoopAsync(token);
                    break;
                default:
                    _logger?.Warn($"[{StationName}] 未知或不支持的运行模式: {CurrentMode}，工站业务线程安全退出。");
                    break;
            }
        }

        /// <summary>
        /// 硬件初始化钩子，由 MasterController.InitializeAllAsync() 在 Initializing 阶段顺序调用。
        /// 基类默认实现：直接推进 Uninitialized → Initializing → Idle（无真实硬件）。
        /// 子类 override 规范：
        ///   1. 首行调用 Fire(MachineTrigger.Initialize)，将本工站推入 Initializing。
        ///   2. 执行真实硬件初始化动作（连接 / 使能 / 回原点等）。
        ///   3. 成功后调用 Fire(MachineTrigger.InitializeDone) 进入 Idle；
        ///      失败时调用 Fire(MachineTrigger.Error) 进入 Alarm，再 throw/rethrow。
        /// </summary>
        public virtual async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize);    // Uninitialized → Initializing
            await Task.CompletedTask;
            Fire(MachineTrigger.InitializeDone); // Initializing → Idle
        }

        /// <summary>
        /// 物理复位模板：驱动本工站完整经历 Alarm → Resetting → Idle/Uninitialized 的复位路径。
        /// 由 MasterController.ResetAllAsync() 在其自身进入 Resetting 后顺序调用。
        ///
        /// 复位目标状态由 <see cref="_initializationFailed"/> 决定：
        ///   · 报警来源于初始化失败 → 复位完成后回到 Uninitialized（强制重新初始化，逻辑闭环）
        ///   · 报警来源于运行阶段   → 复位完成后回到 Idle（可直接启动）
        ///
        /// 子类 override 规范：
        ///   1. 调用 Fire(MachineTrigger.Reset) 将本工站推入 Resetting。
        ///   2. 在 try 块中执行真实硬件清警 / 回原点动作。
        ///   3. 成功后调用 await FireAsync(ResetCompletionTrigger) 进入正确目标状态；
        ///      失败时在 catch 中调用 Fire(MachineTrigger.Error) 回到 Alarm，再 throw/rethrow。
        /// 基类默认实现适用于无真实硬件的工站（纯软件复位）。
        /// </summary>
        public virtual async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                await Task.CompletedTask;  // 基类无硬件动作；子类 override 在此处插入硬件复位逻辑
                await FireAsync(ResetCompletionTrigger);  // Resetting → Idle 或 Uninitialized（取决于报警来源）
            }
            catch (Exception)
            {
                Fire(MachineTrigger.Error);  // Resetting → Alarm，确保不卡死在 Resetting
                throw;
            }
        }

        // --- 供主控调用的公开方法 ---

        /// <summary>
        /// 异步启动工站。
        ///   1. 锁外：取消并等待旧任务退出（消除 Fire/FireAsync 持锁期间的循环等待死锁）。
        ///   2. 持锁：触发 Start 状态跳转，Running.OnEntryAsync 启动新任务。
        /// </summary>
        public async Task StartAsync()
        {
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Start);
        }

        public void Stop() => Fire(MachineTrigger.Stop);
        public void Pause() => Fire(MachineTrigger.Pause);

        /// <summary>
        /// 异步恢复工站（Paused → Running）。
        ///   同 StartAsync，先在锁外等待旧任务退出，再持锁触发 Resume 跳转。
        /// </summary>
        public async Task ResumeAsync()
        {
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Resume);
        }

        /// <summary>
        /// 外部触发工站报警（如主控急停、硬件异常事件）。
        /// 同时执行两件事：
        ///   1. 取消 _runCts → 打断当前所有正在阻塞的等待方法（含 Paused 状态下的等待）
        ///   2. Fire(Error)  → 驱动状态机进入 Alarm 状态
        /// </summary>
        public void TriggerAlarm()
        {
            _alarmInterrupted = true;
            _runCts?.Cancel(); // 打断业务线程

            // 🛡️ 防御性修复：如果是自己抛异常导致的 Alarm，主控反向调用时直接忽略
            if (CurrentState != MachineState.Alarm)
            {
                Fire(MachineTrigger.Error);
            }
        }

        /// <summary>
        /// 携带结构化报警码的外部触发重载（推荐）。
        /// 主控或外部调用此重载可将精确的 <see cref="AlarmCodes"/> 常量传递给 <c>StationAlarmTriggered</c>，
        /// 使上层 <see cref="IAlarmService"/> 能展示正确的报警描述与排故指导。
        /// </summary>
        /// <param name="errorCode">报警代码，应使用 <see cref="AlarmCodes"/> 中定义的常量。</param>
        public void TriggerAlarm(string errorCode)
        {
            _pendingAlarmCode = errorCode;
            TriggerAlarm();
        }

        /// <summary>
        /// 业务代码报警入口（供子类工艺循环内调用）。
        /// 设置精确报警码后立即触发报警流程——取消当前业务线程并切换至 Alarm 状态。
        /// <code>
        /// bool ok = await WaitIOAsync(io, IoSignal.VacuumReady, true, token: token);
        /// if (!ok) { RaiseAlarm(AlarmCodes.Hardware.IoModuleError); return; }
        /// </code>
        /// </summary>
        /// <param name="errorCode">报警代码，应使用 <see cref="AlarmCodes"/> 中定义的常量。</param>
        protected void RaiseAlarm(string errorCode)
        {
            _pendingAlarmCode = errorCode;
            TriggerAlarm();
        }

        /// <summary>
        /// 子类调用：通知主控此工站的硬件已自恢复，可主动清除 AlarmService 中对应的活跃报警记录。
        /// </summary>
        protected void RaiseStationAlarmAutoCleared()
        {
            StationAlarmAutoCleared?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 触发复位流程入口（Alarm → Resetting）。
        /// ⚠️ 注意：此方法仅将状态机推入 Resetting，不会自动完成复位。
        /// 完整复位路径请调用 ExecuteResetAsync()，它会在硬件复位成功后触发 FireAsync(ResetCompletionTrigger)。
        /// </summary>
        public void ResetAlarm() => Fire(MachineTrigger.Reset);

        /// <summary>
        /// 复位完成时应使用的状态跳转触发器：
        ///   · 若报警来源于初始化失败 → ResetDoneUninitialized（回到 Uninitialized，强制重新初始化）
        ///   · 否则                   → ResetDone（回到 Idle，可直接启动）
        /// 子类 override ExecuteResetAsync 时应使用此属性替代硬编码的 MachineTrigger.ResetDone。
        /// </summary>
        protected MachineTrigger ResetCompletionTrigger =>
            _initializationFailed ? MachineTrigger.ResetDoneUninitialized : MachineTrigger.ResetDone;

        /// <summary>
        /// 线程安全的同步状态跳转。
        /// 通过 _stateLock 确保同一时刻只有一个线程修改状态机，
        /// 防止后台报警线程与 UI 线程并发触发导致状态机内部状态崩溃。
        /// 适用于不含 OnEntryAsync/OnExitAsync 的跳转路径（Error、Stop、Pause、Reset 等）。
        /// </summary>
        protected void Fire(MachineTrigger trigger)
        {
            _stateLock.Wait();
            try
            {
                if (_machine.CanFire(trigger)) _machine.Fire(trigger);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// 线程安全的异步状态跳转。
        /// 与 Fire() 的区别：使用 WaitAsync 避免在 async 上下文中阻塞线程；
        /// 使用 FireAsync 以正确 await Running 状态的 OnEntryAsync 回调。
        /// 适用于可能触发 OnEntryAsync 的路径（Start → Running、Resume → Running）。
        /// </summary>
        protected async Task FireAsync(MachineTrigger trigger)
        {
            await _stateLock.WaitAsync();
            try
            {
                if (_machine.CanFire(trigger)) await _machine.FireAsync(trigger);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        #region 工站业务等待辅助方法

        // ─────────────────────────────────────────────────────────────────────
        // 所有等待方法均接受 CancellationToken。
        // 当以下任一情况发生时，token 会被取消，方法立即抛出 OperationCanceledException：
        //   · Stop() 被调用       → _runCts 由 OnStopRunning() 取消
        //   · TriggerAlarm() 被调用 → _runCts 由 TriggerAlarm() 主动取消
        // ProcessWrapperAsync 统一捕获该异常，业务代码无需处理取消逻辑。
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 异步暂停检查点：工站处于 Paused 状态时挂起当前 async 流程（不占用线程），恢复后继续执行。
        /// 在业务循环的每个步序入口 await 此方法，确保暂停命令能及时生效。
        /// </summary>
        protected Task CheckPauseAsync(CancellationToken token)
        {
            // 捕获当前 gate 引用，避免与 Paused.OnEntry 的赋值产生竞争
            var gate = _pauseGate;
            return gate.Task.IsCompleted
                ? Task.CompletedTask
                : gate.Task.WaitAsync(token);
        }

        /// <summary>
        /// 同步暂停检查点（已过时）。
        /// 此方法会同步阻塞调用线程；在 async 业务方法中请改用 await CheckPauseAsync(token)。
        /// </summary>
        [Obsolete("请在 async 方法中改用 await CheckPauseAsync(token)，避免同步阻塞线程池线程。")]
        protected void CheckPause(CancellationToken token) => _pauseGate.Task.Wait(token);

        /// <summary>
        /// 工艺延时：支持暂停中断（每 50ms 检查一次暂停状态）和取消令牌。
        /// 相比直接 await Task.Delay，此方法在暂停时会暂停计时，恢复后继续剩余延时。
        /// </summary>
        /// <param name="milliseconds">延时毫秒数</param>
        /// <param name="token">取消令牌（Stop/TriggerAlarm 时自动取消）</param>
        protected async Task WaitAsync(int milliseconds, CancellationToken token)
        {
            const int ChunkMs = 50;
            int remaining = milliseconds;
            while (remaining > 0)
            {
                await CheckPauseAsync(token).ConfigureAwait(false); // 暂停时异步挂起，不占线程
                int chunk = Math.Min(ChunkMs, remaining);
                await Task.Delay(chunk, token).ConfigureAwait(false);
                remaining -= chunk;
            }
        }

        /// <summary>
        /// 等待任意 bool 条件成立（轮询模式）。
        /// 超时返回 false 并记录错误日志；条件满足返回 true。
        /// </summary>
        /// <param name="condition">被轮询的条件委托（应为轻量级属性读取，不含阻塞操作）</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5 秒</param>
        /// <param name="token">取消令牌</param>
        /// <param name="pollIntervalMs">轮询间隔毫秒数，默认 20ms</param>
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
                    await CheckPauseAsync(linked.Token).ConfigureAwait(false);
                    if (condition()) return true;
                    await Task.Delay(pollIntervalMs, linked.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    _logger?.Error($"[{StationName}] 等待条件超时（{timeoutMs} ms）");
                    return false;  // 超时 → 返回 false（与 WaitIOAsync 行为对齐）
                }
                throw; // 外部取消（Stop/Alarm）→ 向上重新抛出打断流程
            }
        }

        /// <summary>
        /// 等待指定 IO 端口达到目标状态（按端口号）。
        /// 委托给 IIOController.WaitInputAsync，超时后记录错误日志。
        /// </summary>
        /// <param name="io">IO 控制器</param>
        /// <param name="portIndex">端口号</param>
        /// <param name="targetState">期望状态（true=高电平，false=低电平）</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5 秒</param>
        /// <param name="token">取消令牌</param>
        protected async Task<bool> WaitIOAsync(
            IIOController io,
            int portIndex,
            bool targetState,
            int timeoutMs = 5_000,
            CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] → {targetState}");
            await CheckPauseAsync(token).ConfigureAwait(false);
            bool result = await io.WaitInputAsync(portIndex, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result)
                _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>
        /// 等待指定 IO 端口达到目标状态（按枚举信号名）。
        /// </summary>
        /// <typeparam name="T">IO 信号枚举类型</typeparam>
        protected async Task<bool> WaitIOAsync<T>(
            IIOController io,
            T inputName,
            bool targetState,
            int timeoutMs = 5_000,
            CancellationToken token = default)
            where T : Enum
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] → {targetState}");
            await CheckPauseAsync(token).ConfigureAwait(false);
            bool result = await io.WaitInputAsync(inputName, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result)
                _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>
        /// 等待工站间同步信号量（流水线节拍协同）。
        /// 委托给 IStationSyncService.WaitAsync，带超时保护。
        /// </summary>
        /// <param name="sync">工站同步服务</param>
        /// <param name="signalName">信号量名称</param>
        /// <param name="timeoutMs">超时毫秒数，默认 30 秒</param>
        /// <param name="token">取消令牌</param>
        protected async Task<bool> WaitSyncAsync(
            IStationSyncService sync,
            string signalName,
            int timeoutMs = 30_000,
            CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待同步信号 [{signalName}]...");
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            try
            {
                await sync.WaitAsync(signalName, linked.Token).ConfigureAwait(false);
                _logger?.Info($"[{StationName}] 同步信号 [{signalName}] 已触发，继续执行");
                return true;
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    _logger?.Error($"[{StationName}] 等待同步信号 [{signalName}] 超时（{timeoutMs} ms）");
                    return false;
                }
                throw; // 外部取消（Stop/Alarm）→ 向上重新抛出
            }
        }



      



        #endregion

        /// <summary>
        /// 异步清理（推荐路径）：正确等待业务任务退出，不阻塞调用方线程。
        /// 在支持 IAsyncDisposable 的 DI 容器 / using await 语句中自动调用。
        /// </summary>
        public virtual async ValueTask DisposeAsync()
        {
            _runCts?.Cancel();
            _pauseGate.TrySetResult(true); // 释放任何挂起在 CheckPauseAsync 的续体

            if (_workflowTask is { IsCompleted: false })
            {
                try
                {
                    await _workflowTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    _logger?.Warn($"[{StationName}] DisposeAsync 等待业务任务退出超时（5s）");
                }
                catch { }
            }

            _runCts?.Dispose();
            _stateLock?.Dispose();
        }

        /// <summary>
        /// 同步清理（兼容路径）：发出取消信号后，将等待卸载到线程池，
        /// 切断 SynchronizationContext 捕获，防止 UI 线程调用时发生 Sync-over-Async 死锁。
        /// 推荐优先使用 DisposeAsync()。
        /// </summary>
        public virtual void Dispose()
        {
            _runCts?.Cancel();
            _pauseGate.TrySetResult(true);

            // 卸载到线程池，切断 SynchronizationContext，防止 UI 线程死锁
            var task = _workflowTask;
            if (task is { IsCompleted: false })
            {
                try
                {
                    Task.Run(async () =>
                    {
                        await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    }).GetAwaiter().GetResult();
                }
                catch { }
            }
            WriteMemoryParam();
            _runCts?.Dispose();
            _stateLock?.Dispose();
        }


        /// <summary>
        /// 
        /// </summary>
        public virtual T MemoryParam { get; set; }



        private void ReadMemoryParam()
        {
            string filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam\\{this.StationName}.json";
            try
            {
                if (File.Exists(filepath))
                {
                    var json = File.ReadAllText(filepath);
                    MemoryParam = System.Text.Json.JsonSerializer.Deserialize<T>(json);
                    MemoryParam.IsWrite = false;
                }

            }
            catch (Exception ex)
            {

            }

        }



        private void WriteMemoryParam()
        {
            string filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam";
            if (!Directory.Exists(filepath))
            {
                Directory.CreateDirectory(filepath);
            }
            MemoryParam.IsWrite = true;
            string json = System.Text.Json.JsonSerializer.Serialize(MemoryParam);
            File.WriteAllText($"{filepath}\\{this.StationName}.json", json);
        }



    }


    public class StationMemoryBaseParam
    {


        public bool IsWrite { get; set; }




    }



}
