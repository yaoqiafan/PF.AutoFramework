using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using Stateless;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System;

namespace PF.Infrastructure.Station.Basic
{
    /// <summary>
    /// 自动化工站（子线程）业务状态机基类
    ///
    /// 【状态生命周期 (State Lifecycle)】
    ///   Uninitialized ──(Initialize)──→ Initializing ──(InitializeDone)─────────→ Idle
    ///                                        └─────────(Error)──────────────────→ Alarm
    ///   Idle          ──(Start)───────→ Running
    ///   Running       ──(Stop)────────→ Idle
    ///   Alarm         ──(Reset)───────→ Resetting  ──(ResetDone)────────────────→ Idle          (运行期报警复位)
    ///                                        └─────────(ResetDoneUninitialized)─→ Uninitialized (初始化报警复位，强制重置)
    ///
    /// 【并发安全设计 (Concurrency & Thread Safety)】
    ///   · 独占变迁：所有状态变迁（<see cref="Fire(MachineTrigger)"/> / <see cref="FireAsync(MachineTrigger)"/>）均受 <see cref="_stateLock"/> 信号量保护，杜绝后台硬件报警与 UI 交互命令（如 Stop/Pause）导致的并发状态撕裂。
    ///   · 任务防重入：<see cref="MachineState.Running"/> 状态严格确保前置业务任务彻底销毁后，方可启动新任务，彻底消除“幽灵线程”（Orphan Threads）与竞态死锁。
    ///
    /// 💡 【继承与重写契约 (Inheritance Contracts)】
    /// 派生具体工艺工站时，需遵循以下方法重写规范：
    ///
    /// 1. 核心业务大循环（必须实现 <see langword="abstract"/>）：
    ///   - <see cref="ProcessNormalLoopAsync"/> : 正常生产节拍。内部须由 <see langword="while"/> (!<see cref="CancellationToken.IsCancellationRequested"/>) 驱动，并严格埋点 <see langword="await"/> <see cref="CheckPauseAsync(CancellationToken)"/> 响应暂停，配合 <see cref="WaitIOAsync"/> 执行非阻塞硬件交互。
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
    public abstract class StationBase<T> : IDisposable, IAsyncDisposable, INotifyPropertyChanged where T : StationMemoryBaseParam
    {
        #region 1. MVVM 数据绑定与核心属性 (Properties & MVVM)

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public string StationName { get; }

        /// <summary>当前状态机状态（状态变化时触发 PropertyChanged）</summary>
        public MachineState CurrentState => _machine.State;

        private string _currentStepDescription = "就绪";
        /// <summary>
        /// 当前步序描述，供 UI 实时显示（如"正在取料..."、"等待槽位空闲..."）。
        /// 子类在每次步序切换时通过 protected set 赋值，自动触发 PropertyChanged。
        /// </summary>
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
        /// <summary>
        /// 当前运行模式（由 MasterController 在 Idle 状态下统一设置后下发至各工站）。
        /// 属性变化时触发 PropertyChanged，供 UI 绑定。
        /// </summary>
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

        #region 2. 外部交互事件 (Events)

        // 向上层（主控）抛出的报警事件（string = AlarmCodes.* 常量，非自由文本）
        public event EventHandler<string> StationAlarmTriggered;

        /// <summary>
        /// 工站内所有机构均已自恢复（硬件清警）时触发，通知主控可主动清除 AlarmService 对应记录。
        /// 由子类在订阅模组 AlarmAutoCleared 事件后调用 <see cref="RaiseStationAlarmAutoCleared"/>。
        /// </summary>
        public event EventHandler StationAlarmAutoCleared;

        /// <summary>
        /// 工站状态机发生跳转时触发，携带跳转前后的状态信息。
        /// <see cref="BaseMasterController"/> 订阅此事件以实施防状态撕裂守卫：
        /// 当主控处于 Running/Paused 而子站意外回到 Idle 时，主控可立即触发全线急停。
        /// </summary>
        public event EventHandler<StationStateChangedEventArgs> StationStateChanged;

        #endregion

        #region 3. 核心依赖与内部状态标记 (Fields & State Variables)

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

        // 结构化报警码：业务代码或外部调用在触发报警前设置，InitAlarm/RunAlarm.OnEntry 读取后上报并重置。
        // 未显式设置时回落至 AlarmCodes.System.StationSyncError（通用工站异常兜底）。
        private volatile string _pendingAlarmCode;

        // 记录本次进入 Resetting 前的报警类型：
        //   true  → 来自 InitAlarm（复位成功后回 Uninitialized，强制重新初始化）
        //   false → 来自 RunAlarm（复位成功后回 Idle，可直接再启动）
        // 用状态本身替代旧 _initializationFailed 布尔标记，语义更清晰。
        private bool _cameFromInitAlarm = false;

        // Stop/Pause 主动取消业务线程时置位，阻止 ProcessWrapperAsync 的 catch(Exception)
        // 将副作用异常（非 OCE）误判为报警。Reset in OnStartRunningAsync。
        private volatile bool _cancelledIntentionally = false;

        #endregion

        #region 4. 构造函数与状态机配置 (Constructor & Configuration)

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

                // 通知主控发生了状态跳转：主控据此实施防撕裂守卫（如意外回到 Idle 时急停全线）
                StationStateChanged?.Invoke(this, new StationStateChangedEventArgs
                {
                    OldState = t.Source,
                    NewState = t.Destination
                });
            });

            // --- 未初始化状态 ---
            _machine.Configure(MachineState.Uninitialized)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing);

            // --- 初始化中状态：失败→ InitAlarm（强制重新初始化），成功→ Idle ---
            _machine.Configure(MachineState.Initializing)
                .OnEntry(() => _cancelledIntentionally = false)  // 进入初始化时清除停止标记
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.InitAlarm);

            // --- 待机状态：允许启动、重新初始化、主动停止、运行期异常 ---
            _machine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing)  // 支持从 Idle 重新初始化
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)       // Stop → Uninitialized（不再 Ignore）
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            // --- 运行状态 ---
            // 【关键】OnEntryAsync 确保：旧任务彻底结束 → 再启动新任务，消除幽灵线程。
            // 注意：凡触发进入 Running 的 Fire 调用（Start / Resume）均须使用 FireAsync。
            _machine.Configure(MachineState.Running)
                .OnEntryAsync(OnStartRunningAsync)
                .OnExit(OnStopRunning)
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)  // Stop → Uninitialized（物理停稳后再改状态）
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            // --- 暂停状态 ---
            _machine.Configure(MachineState.Paused)
                .OnEntry(() =>
                    // 关门：新建未完成 TCS，业务线程在 CheckPauseAsync 处挂起（不占用线程）
                    _pauseGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
                .OnExit(() =>
                    // 开门：完成当前 TCS，释放所有挂起在 CheckPauseAsync 的续体
                    _pauseGate.TrySetResult(true))
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)  // Stop → Uninitialized
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            // --- 初始化报警：复位后强制回 Uninitialized，坐标系可能不可信 ---
            _machine.Configure(MachineState.InitAlarm)
                .OnEntry(() =>
                {
                    _cameFromInitAlarm = true;
                    // 开门：防止 Paused → InitAlarm 时业务续体永久阻塞
                    _pauseGate.TrySetResult(true);
                    var code = _pendingAlarmCode ?? AlarmCodes.System.StationSyncError;
                    _pendingAlarmCode = null;
                    StationAlarmTriggered?.Invoke(this, code);
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            // --- 运行期报警：复位后回 Idle，坐标系有效，可直接再启动 ---
            _machine.Configure(MachineState.RunAlarm)
                .OnEntry(() =>
                {
                    _cameFromInitAlarm = false;
                    _pauseGate.TrySetResult(true);
                    var code = _pendingAlarmCode ?? AlarmCodes.System.StationSyncError;
                    _pendingAlarmCode = null;
                    StationAlarmTriggered?.Invoke(this, code);
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            // --- 复位中状态：双轨出口 + 动态错误回退 ---
            _machine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized)
                // 复位失败：按来源报警类型回退，避免 InitAlarm 路径错误回到 RunAlarm
                .PermitDynamic(MachineTrigger.Error,
                    () => _cameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        #endregion

        #region 5. 线程与生命周期管控 (Thread & Lifecycle Management)

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
            _cancelledIntentionally = false;
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
                        // 防止重入：主控已通过 TriggerAlarm() 将本工站拉入 InitAlarm/RunAlarm 后，
                        // 业务线程的 OperationCanceledException 在极端竞态下可能漏入此 catch，
                        // 若再次 Fire(Error) 会导致 Stateless 因"当前状态无此转换"抛出异常。
                        if (_machine.State != MachineState.InitAlarm && _machine.State != MachineState.RunAlarm
                            && !_cancelledIntentionally)
                            Fire(MachineTrigger.Error);
                    }
                    catch (Exception fireEx)
                    {
                        _logger?.Fatal($"[{StationName}] 业务异常后尝试触发报警状态失败: {fireEx.Message}");
                    }
                });
            }
        }

        // --- 供主控调用的外层控制方法 ---

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

        public void Stop()
        {
            _cancelledIntentionally = true;
            Fire(MachineTrigger.Stop);
        }

        public void Pause()
        {
            _cancelledIntentionally = true;
            Fire(MachineTrigger.Pause);
        }

        /// <summary>
        /// 异步停止：取消业务线程 → 等待任务退出 → 物理制动 → 推进状态机到 Uninitialized。
        /// </summary>
        public async Task StopAsync()
        {
            _cancelledIntentionally = true;
            _runCts?.Cancel();
            if (_workflowTask is { IsCompleted: false })
            {
                try { await _workflowTask.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
                catch { }
            }
            await OnPhysicalStopAsync().ConfigureAwait(false);
            Fire(MachineTrigger.Stop);
        }

        /// <summary>
        /// 异步恢复工站（Paused → Running）。
        ///   同 StartAsync，先在锁外等待旧任务退出，再持锁触发 Resume 跳转。
        /// </summary>
        public async Task ResumeAsync()
        {
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Resume);
        }

        #endregion

        #region 6. 工艺循环抽象与硬件控制契约 (Process & Hardware Hooks)

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

        /// <summary>正常生产模式工艺循环</summary>
        protected abstract Task ProcessNormalLoopAsync(CancellationToken token);

        /// <summary>空跑验证模式工艺循环（跳过物料等待与部分外部协同）</summary>
        protected abstract Task ProcessDryRunLoopAsync(CancellationToken token);

        /// <summary>
        /// 硬件初始化钩子，由 MasterController.InitializeAllAsync() 在 Initializing 阶段顺序调用。
        /// 基类默认实现：直接推进 Uninitialized → Initializing → Idle（无真实硬件）。
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
        /// </summary>
        public virtual async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // InitAlarm/RunAlarm → Resetting
            try
            {
                await Task.CompletedTask;  // 基类无硬件动作；子类 override 在此处插入硬件复位逻辑
                await FireAsync(ResetCompletionTrigger);  // Resetting → Idle 或 Uninitialized（取决于报警来源）
            }
            catch (Exception)
            {
                Fire(MachineTrigger.Error);  // Resetting → InitAlarm/RunAlarm，确保不卡死在 Resetting
                throw;
            }
        }

        /// <summary>
        /// 子类重写以实现机构级物理制动（停止轴、关断气缸等）。
        /// 基类默认无操作。
        /// </summary>
        protected virtual Task OnPhysicalStopAsync() => Task.CompletedTask;

        #endregion

        #region 7. 报警与复位控制 (Alarm & Reset Control)

        /// <summary>
        /// 外部触发工站报警（如主控硬件异常事件）。
        /// </summary>
        public void TriggerAlarm()
        {
            _alarmInterrupted = true;
            _runCts?.Cancel(); // 打断业务线程

            // 防御性：如果自身已处于报警态，主控反向调用时直接忽略，防止重入
            if (CurrentState != MachineState.InitAlarm && CurrentState != MachineState.RunAlarm)
            {
                Fire(MachineTrigger.Error);
            }
        }

        /// <summary>
        /// 携带结构化报警码的外部触发重载（推荐）。
        /// </summary>
        public void TriggerAlarm(string errorCode)
        {
            _pendingAlarmCode = errorCode;
            TriggerAlarm();
        }

        /// <summary>
        /// 业务代码报警入口（供子类工艺循环内调用）。
        /// 设置精确报警码后立即触发报警流程——取消当前业务线程并切换至 Alarm 状态。
        /// </summary>
        protected void RaiseAlarm(string errorCode)
        {
            if (_cancelledIntentionally) return;
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

        /// <summary>触发复位流程入口</summary>
        public void ResetAlarm() => Fire(MachineTrigger.Reset);

        /// <summary>复位完成时应使用的状态跳转触发器</summary>
        protected MachineTrigger ResetCompletionTrigger =>
            _cameFromInitAlarm ? MachineTrigger.ResetDoneUninitialized : MachineTrigger.ResetDone;

        #endregion

        #region 8. 状态跳转引擎 (State Transition Engine)

        /// <summary>
        /// 线程安全的同步状态跳转。
        /// 通过 _stateLock 确保同一时刻只有一个线程修改状态机。
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

        #endregion

        #region 9. 工站业务等待与 IO 辅助方法 (Awaiters & IO Sync)

        // ─────────────────────────────────────────────────────────────────────
        // 所有等待方法均接受 CancellationToken。
        // 当以下任一情况发生时，token 会被取消，方法立即抛出 OperationCanceledException：
        //   · Stop() 被调用        → _runCts 由 OnStopRunning() 取消
        //   · TriggerAlarm() 被调用 → _runCts 由 TriggerAlarm() 主动取消
        // ProcessWrapperAsync 统一捕获该异常，业务代码无需处理取消逻辑。
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 异步暂停检查点：工站处于 Paused 状态时挂起当前 async 流程（不占用线程），恢复后继续执行。
        /// </summary>
        protected Task CheckPauseAsync(CancellationToken token)
        {
            var gate = _pauseGate;
            return gate.Task.IsCompleted
                ? Task.CompletedTask
                : gate.Task.WaitAsync(token);
        }

        [Obsolete("请在 async 方法中改用 await CheckPauseAsync(token)，避免同步阻塞线程池线程。")]
        protected void CheckPause(CancellationToken token) => _pauseGate.Task.Wait(token);

        /// <summary>工艺延时：支持暂停中断（每 50ms 检查一次暂停状态）和取消令牌。</summary>
        protected async Task WaitAsync(int milliseconds, CancellationToken token)
        {
            const int ChunkMs = 50;
            int remaining = milliseconds;
            while (remaining > 0)
            {
                await CheckPauseAsync(token).ConfigureAwait(false);
                int chunk = Math.Min(ChunkMs, remaining);
                await Task.Delay(chunk, token).ConfigureAwait(false);
                remaining -= chunk;
            }
        }

        /// <summary>等待任意 bool 条件成立（轮询模式）。</summary>
        protected async Task<bool> WaitConditionAsync(Func<bool> condition, int timeoutMs = 5_000, CancellationToken token = default, int pollIntervalMs = 20)
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
                    return false;
                }
                throw;
            }
        }

        /// <summary>等待指定 IO 端口达到目标状态（按端口号）。</summary>
        protected async Task<bool> WaitIOAsync(IIOController io, int portIndex, bool targetState, int timeoutMs = 5_000, CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] → {targetState}");
            await CheckPauseAsync(token).ConfigureAwait(false);
            bool result = await io.WaitInputAsync(portIndex, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result)
                _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>等待指定 IO 端口达到目标状态（按枚举信号名）。</summary>
        protected async Task<bool> WaitIOAsync<TEnum>(IIOController io, TEnum inputName, bool targetState, int timeoutMs = 5_000, CancellationToken token = default) where TEnum : Enum
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] → {targetState}");
            await CheckPauseAsync(token).ConfigureAwait(false);
            bool result = await io.WaitInputAsync(inputName, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result)
                _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>等待工站间同步信号量（流水线节拍协同）。</summary>
        protected async Task<bool> WaitSyncAsync(IStationSyncService sync, string signalName, int timeoutMs = 30_000, CancellationToken token = default)
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
                throw;
            }
        }

        #endregion

        #region 10. 记忆参数管理 (Memory & Param Management)

        public virtual T MemoryParam { get; set; }

        private void ReadMemoryParam()
        {
            string filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam\\{this.StationName}.json";
            try
            {
                if (File.Exists(filepath))
                {
                    var json = File.ReadAllText(filepath);
                    MemoryParam = JsonSerializer.Deserialize<T>(json);
                    MemoryParam.IsWrite = false;
                }
            }
            catch (Exception)
            {
                // 忽略或记录日志
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
            string json = JsonSerializer.Serialize(MemoryParam);
            File.WriteAllText($"{filepath}\\{this.StationName}.json", json);
        }

        #endregion

        #region 11. 资源清理 (IDisposable)

        /// <summary>
        /// 异步清理（推荐路径）：正确等待业务任务退出，不阻塞调用方线程。
        /// </summary>
        public virtual async ValueTask DisposeAsync()
        {
            _runCts?.Cancel();
            _pauseGate.TrySetResult(true);

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
        /// 同步清理（兼容路径）：发出取消信号后，将等待卸载到线程池，切断 SynchronizationContext。
        /// </summary>
        public virtual void Dispose()
        {
            _runCts?.Cancel();
            _pauseGate.TrySetResult(true);

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

        #endregion
    }

    public class StationMemoryBaseParam
    {
        public bool IsWrite { get; set; }
    }
}