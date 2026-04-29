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
    /// 自动化工站（子线程）业务状态机基类。
    ///
    /// <para>
    /// 【状态生命周期 (State Lifecycle)】
    /// <code>
    ///   Uninitialized ──(Initialize)──→ Initializing ──(InitializeDone)──────────→ Idle
    ///                                                  └──────(Error)─────────────→ InitAlarm
    ///   Idle          ──(Start)───────→ Running
    ///                 ──(Initialize)──→ Initializing  （支持 Idle 下重新初始化）
    ///   Running       ──(Pause)───────→ Paused
    ///                 ──(Stop)────────→ Uninitialized
    ///                 ──(Error)───────→ RunAlarm
    ///   Paused        ──(Resume)──────→ Running
    ///                 ──(Stop)────────→ Uninitialized
    ///   InitAlarm     ──(Reset)───────→ Resetting ──(ResetDoneUninitialized)──→ Uninitialized
    ///   RunAlarm      ──(Reset)───────→ Resetting ──(ResetDone)──────────────→ Idle
    ///                                              └──(Error)─────────────────→ InitAlarm / RunAlarm
    /// </code>
    /// </para>
    ///
    /// <para>
    /// 【并发安全设计 (Thread Safety)】
    /// <list type="bullet">
    ///   <item>独占变迁：所有状态变迁均受 <c>_stateLock</c>（SemaphoreSlim(1,1)）保护，杜绝后台硬件报警
    ///         与 UI 交互命令并发导致的状态撕裂。</item>
    ///   <item>任务防重入：<see cref="MachineState.Running"/> 状态严格确保旧业务任务彻底退出后方可
    ///         启动新任务，消除"幽灵线程"（Orphan Threads）与竞态死锁。</item>
    ///   <item>原子报警快照：<c>_pendingAlarm</c> 由 <see cref="System.Threading.Volatile.Write"/> 写入、
    ///         <see cref="Interlocked.Exchange"/> 读取，保证 ErrorCode/Context 成对可见，杜绝并发撕裂。</item>
    ///   <item>双重释放防护：<c>_disposed</c>（int）通过 <see cref="Interlocked.CompareExchange"/> 原子
    ///         check-and-set，防止并发 Dispose 导致资源被重复释放。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 【继承与重写契约 (Inheritance Contracts)】
    /// 派生具体工艺工站时，请遵循以下规范：
    /// <list type="number">
    ///   <item>
    ///     <b>核心业务大循环（必须实现）</b>
    ///     <list type="bullet">
    ///       <item><see cref="ProcessNormalLoopAsync"/>：正常生产节拍，内部须以
    ///             <c>while (!token.IsCancellationRequested)</c> 驱动，配合
    ///             <see cref="WaitIOAsync(IIOController, int, bool, int, CancellationToken)"/>
    ///             执行非阻塞硬件交互。</item>
    ///       <item><see cref="ProcessDryRunLoopAsync"/>：空跑验证节拍，应跳过外部物料交互与跨工站同步逻辑。</item>
    ///     </list>
    ///   </item>
    ///   <item>
    ///     <b>生命周期钩子（强烈建议重写）</b>
    ///     <list type="bullet">
    ///       <item><see cref="OnInitializeAsync"/>：硬件上电初始化（伺服使能、回原点等），
    ///             不要在此钩子内直接调用 Fire；框架会在此前后自动触发状态变迁。</item>
    ///       <item><see cref="OnResetAsync"/>：报警解除后的机构安全自恢复（清错、退刀、回安全位等），
    ///             同样不要在此钩子内直接调用 Fire。</item>
    ///       <item><see cref="OnPhysicalStopAsync"/>：机构级物理制动，当工站接收 Stop 命令或被外部异常
    ///             打断时调用，必须在此切断危险源（关断气缸、急停马达等）。</item>
    ///     </list>
    ///   </item>
    ///   <item>
    ///     <b>框架行为干预（按需扩展）</b>
    ///     <list type="bullet">
    ///       <item><see cref="ProcessLoopAsync"/>：模式路由分发器，如需引入非标运行模式可在此重写。</item>
    ///       <item><see cref="CreatePauseCheckDelegate"/>：软暂停感知委托工厂，默认返回 false（兼容 CTS 取消式暂停），
    ///             实现软暂停时可在此重写并挂起等待 Resume 信号。</item>
    ///       <item><see cref="GetMechanisms"/>：返回本工站所有机构，框架在启动时向其注入 PauseCheckAsync 委托。</item>
    ///       <item><see cref="DisposeAsync"/>：非托管资源清理，重写时末尾须调用 <c>await base.DisposeAsync()</c>。</item>
    ///     </list>
    ///   </item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class StationBase<T> : IStation where T : StationMemoryBaseParam, new()
    {
        #region 1. MVVM 数据绑定

        /// <inheritdoc/>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 触发 <see cref="PropertyChanged"/> 事件，通知 UI 属性已更新。
        /// 使用 <see cref="CallerMemberNameAttribute"/> 自动捕获属性名，无需手动传参。
        /// </summary>
        protected virtual void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>获取工站的唯一标识名称，用于日志前缀、记忆参数文件名及跨工站同步键。</summary>
        public string StationName { get; }

        /// <summary>
        /// 获取工站当前所处的生命周期状态。
        /// 状态由 Stateless 状态机维护，所有变迁均在 <c>_stateLock</c> 保护下执行。
        /// </summary>
        public MachineState CurrentState => _machine.State;

        private string _currentStepDescription = "就绪";

        /// <summary>
        /// 获取或设置当前工艺步骤的文字描述，用于 UI 状态栏实时显示。
        /// 仅在值发生变化时触发 <see cref="PropertyChanged"/>，避免 UI 频繁重绘。
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
        /// 获取或设置工站当前的运行模式（Normal / DryRun 等）。
        /// 模式切换仅在 <see cref="MachineState.Idle"/> 状态由主控统一下发，运行中不得切换。
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

        #region 2. 外部交互事件

        /// <summary>
        /// 工站触发报警时引发。事件在独立线程（Task.Run）中异步派发，
        /// 订阅者不得在回调中同步调用 <see cref="Fire"/> / <see cref="FireAsync"/> 以防死锁。
        /// </summary>
        public event EventHandler<StationAlarmEventArgs>? StationAlarmTriggered;

        /// <summary>
        /// 硬件自恢复（非人工干预）导致报警自动清除时引发。
        /// 主控层收到后可主动调用 AlarmService.ClearAlarm 更新活跃报警列表。
        /// </summary>
        public event EventHandler? StationAlarmAutoCleared;

        /// <summary>
        /// 工站状态发生任意变迁时引发。事件同样在独立线程异步派发，
        /// 主控可在此监听子站异常跳转并作出全线响应。
        /// </summary>
        public event EventHandler<StationStateChangedEventArgs>? StationStateChanged;

        #endregion

        #region 3. 核心依赖与内部状态标记

        /// <summary>日志记录器，允许为 null（无日志场景或单元测试）。</summary>
        protected readonly ILogService? _logger;

        /// <summary>
        /// Stateless 状态机实例。所有状态读取与变迁均应通过
        /// <see cref="Fire"/> / <see cref="FireAsync"/> 进行，以确保线程安全。
        /// </summary>
        protected readonly StateMachine<MachineState, MachineTrigger> _machine;

        /// <summary>
        /// 状态变迁互斥锁（SemaphoreSlim(1,1)）。
        /// 保证同一时刻只有一个线程能执行状态机 Fire，防止并发报警与 UI 命令的状态撕裂。
        /// </summary>
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        /// <summary>当前业务循环的取消令牌源。Running 状态进入时创建，退出时取消并在下次进入时替换。</summary>
        private CancellationTokenSource? _runCts;

        /// <summary>运行中的业务循环 Task，用于等待其退出（Stop / Dispose 时）。</summary>
        private Task? _workflowTask;

        // ── 意图标志（语义解耦）──────────────────────────────────────────────
        // _stopRequested    : 用户/主控主动发起 Stop，抑制 workflow 异常被误判为报警
        // _pauseRequested   : 用户/主控主动发起 Pause，区分"暂停退出"与"报警退出"的日志语义
        // _alarmInterrupted : 硬件/业务主动触发报警打断 workflow，用于精确日志
        private volatile bool _stopRequested;
        private volatile bool _pauseRequested;
        private volatile bool _alarmInterrupted;

        /// <summary>
        /// 原子报警快照：将 ErrorCode 与 <see cref="StationAlarmEventArgs"/> 封装为不可变对。
        /// 写端使用 <see cref="System.Threading.Volatile.Write"/> 保证写入可见性；
        /// 读端使用 <see cref="Interlocked.Exchange"/> 原子取走，杜绝并发下 Code/Context 来自不同报警源的撕裂。
        /// </summary>
        private sealed record PendingAlarm(string Code, StationAlarmEventArgs Context);

        /// <summary>
        /// 释放标志（0 = 未释放，1 = 已释放）。
        /// 通过 <see cref="Interlocked.CompareExchange"/> 原子 check-and-set，防止并发 Dispose 双重释放。
        /// </summary>
        private int _disposed;

        /// <summary>当前待派发的报警快照，可为 null（触发无具体码的级联报警时）。</summary>
        private PendingAlarm? _pendingAlarm;

        /// <summary>标记当前进入报警是否来自初始化阶段，决定复位完成后的路由目标。</summary>
        private volatile bool _cameFromInitAlarm;

        /// <summary>
        /// 获取当前报警是否源自初始化阶段。
        /// 子类可在 <see cref="OnResetAsync"/> 内读取此值，决定复位后是否需要重新初始化。
        /// </summary>
        protected bool CameFromInitAlarm => _cameFromInitAlarm;

        #endregion

        #region 4. 构造函数与状态机配置

        /// <summary>
        /// 初始化工站基类：绑定名称、配置状态机、从磁盘读取记忆参数。
        /// </summary>
        /// <param name="name">工站唯一名称，不可为 null，用于日志前缀与记忆参数文件名。</param>
        /// <param name="logger">日志服务，可为 null（无日志模式）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="name"/> 为 null 时抛出。</exception>
        protected StationBase(string name, ILogService? logger)
        {
            StationName = name ?? throw new ArgumentNullException(nameof(name));
            _logger = logger;
            _machine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureStateMachine();
            ReadMemoryParam();
        }

        /// <summary>
        /// 配置 Stateless 状态机的全部状态、允许触发器及进出动作。
        /// 所有状态变迁回调中禁止同步调用 Fire，以免死锁；需要触发时须在独立 Task.Run 中执行。
        /// </summary>
        private void ConfigureStateMachine()
        {
            // 每次状态变迁后：更新 UI 属性绑定，并在独立线程通知外部订阅者
            _machine.OnTransitioned(t =>
            {
                _logger?.Debug($"[{StationName}] 状态变迁: {t.Source} -> {t.Destination}");
                RaisePropertyChanged(nameof(CurrentState));

                // 异步派发：避免订阅者在回调中同步调用 Fire 导致 _stateLock 递归死锁
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
                    // 进入初始化时清空残留意图标志，避免上次 Stop/Pause 的状态污染新的初始化流程
                    _stopRequested = false;
                    _pauseRequested = false;
                })
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.InitAlarm);

            _machine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing)   // 支持 Idle 下重新初始化
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _machine.Configure(MachineState.Running)
                .OnEntryAsync(OnStartRunningAsync)          // 进入 Running：创建新 CTS、启动 workflow Task
                .OnExit(_ => _runCts?.Cancel())             // 退出 Running（无论目标状态）：立即取消当前 workflow
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
                // 复位过程中再次出错：动态路由回原始报警状态（InitAlarm 或 RunAlarm）
                .PermitDynamic(MachineTrigger.Error,
                    () => _cameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        private void EnterAlarm_Init() => EnterAlarmCommon(isInit: true);
        private void EnterAlarm_Run() => EnterAlarmCommon(isInit: false);

        /// <summary>
        /// 进入报警状态时的通用处理：
        /// 1. 记录报警来源阶段（初始化 or 运行期），决定复位后路由目标；
        /// 2. 原子取走 <c>_pendingAlarm</c>（避免并发读写），构造最终报警上下文；
        /// 3. 在独立线程异步触发 <see cref="StationAlarmTriggered"/>，防止订阅者重入 Fire 导致死锁。
        /// </summary>
        private void EnterAlarmCommon(bool isInit)
        {
            _cameFromInitAlarm = isInit;

            // 原子取走快照：Interlocked.Exchange 保证只有一个线程能取到非 null 值
            var pending = Interlocked.Exchange(ref _pendingAlarm, null);
            var code = pending?.Code ?? AlarmCodes.System.CascadeAlarm;
            var context = pending?.Context ?? new StationAlarmEventArgs { ErrorCode = code };

            // 异步派发：避免订阅者在事件回调中同步调用 Fire，导致 _stateLock 死锁
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

        /// <summary>
        /// 取消并等待旧的业务循环 Task 完全退出。
        /// 用于 <see cref="StartAsync"/> 和 <see cref="ResumeAsync"/> 前的任务防重入清理。
        /// </summary>
        private async Task CancelAndAwaitOldTaskAsync()
        {
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }

            var old = _workflowTask;
            if (old is { IsCompleted: false })
            {
                try { await old.ConfigureAwait(false); }
                catch { /* 旧任务的异常已在 ProcessWrapperAsync 内处理，此处忽略 */ }
            }
        }

        /// <summary>
        /// Running 状态的进入动作（由状态机 OnEntryAsync 驱动）：
        /// 创建新的 <see cref="CancellationTokenSource"/>，向所有机构注入暂停感知委托，
        /// 然后在线程池启动业务循环 Task。
        /// </summary>
        /// <remarks>
        /// 旧 CTS 已由状态机 OnExit 触发 Cancel；此处仅负责创建新 CTS 并替换，
        /// 旧 CTS 的 Dispose 在 Interlocked.Exchange 后立即完成。
        /// </remarks>
        private Task OnStartRunningAsync()
        {
            // 旧 CTS 由 Running.OnExit 负责 Cancel；此处原子替换并释放旧实例
            var oldCts = Interlocked.Exchange(ref _runCts, new CancellationTokenSource());
            try { oldCts?.Dispose(); } catch { }

            // 清空本次运行的意图标志
            _pauseRequested = false;
            _stopRequested = false;
            _alarmInterrupted = false;

            // 向所有注册机构注入暂停感知委托，使机构层在轴停止但未到位时能正确响应
            var pauseCheck = CreatePauseCheckDelegate();
            foreach (var m in GetMechanisms())
                m.PauseCheckAsync = pauseCheck;

            // 在线程池启动业务循环；CancellationToken.None 作为外层 token 确保 Task 被正常调度，
            // 实际取消逻辑由传入 ProcessWrapperAsync 的 token（_runCts.Token）控制
            var token = _runCts!.Token;
            _workflowTask = Task.Run(() => ProcessWrapperAsync(token), CancellationToken.None);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 业务循环的安全包装层：统一处理正常取消、报警打断和未预期业务异常三种退出路径。
        /// </summary>
        /// <param name="token">与当前 <c>_runCts</c> 绑定的取消令牌。</param>
        private async Task ProcessWrapperAsync(CancellationToken token)
        {
            try
            {
                await ProcessLoopAsync(token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // 区分三种取消来源，给出精确日志
                if (_pauseRequested) _logger?.Info($"[{StationName}] 工站响应暂停命令，等待续跑...");
                else if (_alarmInterrupted) _logger?.Warn($"[{StationName}] 被外部报警打断，线程安全退出。");
                else _logger?.Warn($"[{StationName}] 子线程被安全打断并退出。");
                _alarmInterrupted = false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 业务异常: {ex.Message}");

                // 用户主动 Stop 时不触发报警，避免误判
                if (_stopRequested) return;

                // 仅在尚无更具体的报警快照时才填入默认码，保留更有诊断价值的现场信息
                var defaultAlarm = new PendingAlarm(
                    AlarmCodes.System.StationSyncError,
                    new StationAlarmEventArgs { ErrorCode = AlarmCodes.System.StationSyncError });
                Interlocked.CompareExchange(ref _pendingAlarm, defaultAlarm, null);

                // 在独立 Task 中触发状态机，避免在 workflowTask 上下文中回调 Fire 造成死锁
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
                }, token);
            }
        }

        /// <summary>
        /// 异步启动工站业务循环。
        /// 若有旧任务残留，先取消并等待其完全退出，再驱动状态机进入 Running 状态。
        /// </summary>
        public async Task StartAsync()
        {
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Start).ConfigureAwait(false);
        }

        /// <summary>
        /// 暂停工站业务循环（非阻塞）。
        /// 立即取消当前 CTS（终止 workflow），驱动状态机进入 Paused，
        /// 并在独立线程异步执行 <see cref="OnPhysicalPauseAsync"/> 物理制动动作。
        /// </summary>
        /// <remarks>
        /// 物理制动以 fire-and-forget 方式执行，状态机在制动完成之前已到达 Paused 状态。
        /// 若需要确保制动完成后再接受新指令，请重写 <see cref="OnPhysicalPauseAsync"/> 并在其中
        /// 添加等待逻辑，同时将 Pause 改为 async 调用。
        /// </remarks>
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

        /// <summary>
        /// 异步停止工站业务循环，执行物理制动，并驱动状态机回到 Uninitialized。
        /// </summary>
        /// <remarks>
        /// 流程：取消 CTS → 等待 workflow 退出（最多 3 s）→ 执行 <see cref="OnPhysicalStopAsync"/>
        /// → 触发 Stop 状态变迁（在 finally 中保证必然执行）。
        ///
        /// 若 3 s 内 workflow 未退出，将记录警告并继续执行物理制动；
        /// 因此 <see cref="OnPhysicalStopAsync"/> 的实现必须是幂等且线程安全的。
        /// </remarks>
        public async Task StopAsync()
        {
            _stopRequested = true;
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }

            var wf = _workflowTask;
            if (wf is { IsCompleted: false })
            {
                try { await wf.WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false); }
                catch (TimeoutException)
                {
                    _logger?.Warn($"[{StationName}] StopAsync 等待子线程退出超时（3 s），" +
                                  $"OnPhysicalStopAsync 将与正在退出的子线程并发执行，请确保物理制动逻辑是幂等且线程安全的。");
                }
                catch { }
            }

            try { await OnPhysicalStopAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger?.Error($"[{StationName}] OnPhysicalStopAsync 异常: {ex.Message}"); }
            finally { Fire(MachineTrigger.Stop); }  // 无论物理制动是否报错，状态机必定归位
        }

        /// <summary>
        /// 异步恢复已暂停的工站。
        /// 清除暂停标志，取消并等待旧 workflow 完全退出，再驱动状态机进入 Running 并启动新 workflow。
        /// </summary>
        public async Task ResumeAsync()
        {
            _pauseRequested = false;
            await CancelAndAwaitOldTaskAsync().ConfigureAwait(false);
            await FireAsync(MachineTrigger.Resume).ConfigureAwait(false);
        }

        #endregion

        #region 6. 工艺循环抽象与硬件控制契约

        /// <summary>
        /// 运行模式路由分发器，根据 <see cref="CurrentMode"/> 将控制权分发给对应的节拍方法。
        /// 如需引入非标运行模式（如 GRR 测试、维护模式），重写此方法进行拦截与分发。
        /// </summary>
        /// <param name="token">与当前运行生命周期绑定的取消令牌，Stop/Pause 时被取消。</param>
        protected virtual async Task ProcessLoopAsync(CancellationToken token)
        {
            switch (CurrentMode)
            {
                case OperationMode.Normal: await ProcessNormalLoopAsync(token).ConfigureAwait(false); break;
                case OperationMode.DryRun: await ProcessDryRunLoopAsync(token).ConfigureAwait(false); break;
                default: _logger?.Warn($"[{StationName}] 未知运行模式: {CurrentMode}"); break;
            }
        }

        /// <summary>
        /// 正常生产节拍大循环（必须由子类实现）。
        /// 实现规范：内部必须以 <c>while (!token.IsCancellationRequested)</c> 驱动；
        /// 所有硬件等待须调用 <see cref="WaitIOAsync(IIOController, int, bool, int, CancellationToken)"/> 等带 token 的异步方法，
        /// 确保 Stop/Pause 指令能及时打断循环。
        /// </summary>
        /// <param name="token">运行生命周期取消令牌。</param>
        protected abstract Task ProcessNormalLoopAsync(CancellationToken token);

        /// <summary>
        /// 空跑验证节拍大循环（必须由子类实现）。
        /// 实现规范：跳过外部物料交互（扫码、视觉、跨工站同步信号等），
        /// 仅驱动机构执行无料脱机跑合验证。
        /// </summary>
        /// <param name="token">运行生命周期取消令牌。</param>
        protected abstract Task ProcessDryRunLoopAsync(CancellationToken token);

        /// <summary>
        /// 初始化物理动作钩子（子类重写时只做硬件动作，不要调用 Fire）。
        /// 由 <see cref="ExecuteInitializeAsync"/> 在状态机已进入 Initializing 后调用；
        /// 成功时基类会自动触发 InitializeDone，失败时自动触发 Error。
        /// </summary>
        /// <param name="token">初始化取消令牌，超时或并行初始化被取消时触发。</param>
        protected virtual Task OnInitializeAsync(CancellationToken token) => Task.CompletedTask;

        /// <summary>
        /// 复位物理动作钩子（子类重写时只做硬件动作，不要调用 Fire）。
        /// 由 <see cref="ExecuteResetAsync"/> 在状态机已进入 Resetting 后调用；
        /// 成功时基类会自动触发 ResetDone / ResetDoneUninitialized，失败时自动触发 Error。
        /// </summary>
        /// <param name="token">复位取消令牌，超时时触发。</param>
        protected virtual Task OnResetAsync(CancellationToken token) => Task.CompletedTask;

        /// <summary>
        /// 初始化入口（框架调用，供主控的并行初始化调度）。
        /// 驱动状态机进入 Initializing → 调用 <see cref="OnInitializeAsync"/> → 触发 InitializeDone 或 Error。
        /// </summary>
        /// <param name="token">由主控传入的全局初始化取消令牌（超时或某工站失败时取消）。</param>
        public virtual async Task ExecuteInitializeAsync(CancellationToken token)
        {
            if (!Fire(MachineTrigger.Initialize)) return;  // 状态机拒绝（如已在 Initializing）则直接退出
            try
            {
                token.ThrowIfCancellationRequested();
                await OnInitializeAsync(token).ConfigureAwait(false);
                Fire(MachineTrigger.InitializeDone);
            }
            catch (OperationCanceledException)
            {
                _logger?.Warn($"[{StationName}] 初始化操作被取消。");
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

        /// <summary>
        /// 复位入口（框架调用，供主控的并行复位调度）。
        /// 驱动状态机进入 Resetting → 调用 <see cref="OnResetAsync"/> → 触发 ResetDone/ResetDoneUninitialized 或 Error。
        /// </summary>
        /// <param name="token">由主控传入的全局复位取消令牌（超时时取消）。</param>
        public virtual async Task ExecuteResetAsync(CancellationToken token)
        {
            if (!Fire(MachineTrigger.Reset)) return;
            try
            {
                token.ThrowIfCancellationRequested();
                await OnResetAsync(token).ConfigureAwait(false);
                // ResetCompletionTrigger 根据 _cameFromInitAlarm 自动路由到正确的完成触发器
                await FireAsync(ResetCompletionTrigger).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger?.Warn($"[{StationName}] 复位操作被取消。");
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

        /// <summary>
        /// 机构级物理制动钩子（Stop 命令或异常退出时调用）。
        /// 必须在此实现危险源切断（关断气缸、急停马达等），默认为空操作。
        /// </summary>
        protected virtual Task OnPhysicalStopAsync() => Task.CompletedTask;

        /// <summary>
        /// 机构级物理暂停钩子，默认委托给 <see cref="OnPhysicalStopAsync"/>。
        /// 若暂停与停止的物理动作不同（如暂停只减速不断使能），可单独重写此方法。
        /// </summary>
        protected virtual Task OnPhysicalPauseAsync() => OnPhysicalStopAsync();

        /// <summary>
        /// 返回本工站所管理的全部机构实例，框架在 Running 状态进入时向其批量注入
        /// <see cref="BaseMechanism.PauseCheckAsync"/> 委托。默认返回空集合。
        /// </summary>
        protected virtual IEnumerable<BaseMechanism> GetMechanisms() => [];

        /// <summary>
        /// 创建暂停感知委托，注入到所有机构的 <see cref="BaseMechanism.PauseCheckAsync"/>。
        /// <para>
        /// 委托语义：当机构检测到轴已停止但 MoveDone=false 时调用此委托，
        /// 若工站处于暂停状态则应挂起等待 Resume（返回 <c>true</c> 表示"曾暂停，现已恢复"，
        /// 调用方应重新发起运动）；若未暂停则立即返回 <c>false</c>（运动视为失败）。
        /// </para>
        /// <para>
        /// 默认实现返回 <c>false</c>，与当前 CTS 取消式暂停模型兼容——
        /// 暂停时 CTS 被取消，OperationCanceledException 已通过 <see cref="ProcessWrapperAsync"/> 处理，
        /// 机构层无需轮询状态。若要实现"不取消 CTS 的软暂停"，子类可重写此方法。
        /// </para>
        /// </summary>
        protected virtual Func<CancellationToken, Task<bool>> CreatePauseCheckDelegate() => _ => Task.FromResult(false);

        #endregion

        #region 7. 报警与复位控制

        /// <summary>
        /// 触发级联报警（无具体错误码）。
        /// 若当前已在报警状态则忽略；否则打断业务循环并驱动状态机进入对应报警状态。
        /// </summary>
        public void TriggerAlarm()
        {
            var state = CurrentState;
            if (state == MachineState.InitAlarm || state == MachineState.RunAlarm) return;

            _alarmInterrupted = true;
            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }
            Fire(MachineTrigger.Error);
        }

        /// <summary>
        /// 触发报警并携带结构化错误码。
        /// 先原子写入报警快照，再调用 <see cref="TriggerAlarm()"/> 触发状态机。
        /// </summary>
        /// <param name="errorCode">预定义报警码（见 <see cref="AlarmCodes"/>）。</param>
        public void TriggerAlarm(string errorCode)
        {
            Volatile.Write(ref _pendingAlarm,
                new PendingAlarm(errorCode, new StationAlarmEventArgs { ErrorCode = errorCode }));
            TriggerAlarm();
        }

        /// <summary>
        /// 触发报警并携带错误码和运行时动态消息。
        /// 运行时消息用于在活跃报警面板展示比静态字典更精确的现场信息。
        /// </summary>
        /// <param name="errorCode">预定义报警码。</param>
        /// <param name="runtimeMessage">运行时动态描述（如"X轴位置偏差 3.5 mm"），可为 null。</param>
        public void TriggerAlarm(string errorCode, string? runtimeMessage)
        {
            var ctx = new StationAlarmEventArgs { ErrorCode = errorCode, RuntimeMessage = runtimeMessage };
            Volatile.Write(ref _pendingAlarm, new PendingAlarm(errorCode, ctx));
            TriggerAlarm();
        }

        /// <summary>
        /// 从子类（派生工站内部）触发报警（仅携带错误码）。
        /// Stop 意图下自动抑制，Pause 不抑制（保留运行期硬件异常的上报路径）。
        /// </summary>
        /// <param name="errorCode">预定义报警码。</param>
        protected void RaiseAlarm(string errorCode)
        {
            if (_stopRequested) return;  // 仅 Stop 抑制；Pause 不抑制运行期硬件异常
            Volatile.Write(ref _pendingAlarm,
                new PendingAlarm(errorCode, new StationAlarmEventArgs { ErrorCode = errorCode }));
            TriggerAlarm();
        }

        /// <summary>
        /// 从子类触发报警（携带错误码和运行时动态消息）。
        /// </summary>
        /// <param name="errorCode">预定义报警码。</param>
        /// <param name="runtimeMessage">运行时动态描述。</param>
        protected void RaiseAlarm(string errorCode, string runtimeMessage)
        {
            if (_stopRequested) return;
            Volatile.Write(ref _pendingAlarm,
                new PendingAlarm(errorCode, new StationAlarmEventArgs { ErrorCode = errorCode, RuntimeMessage = runtimeMessage }));
            TriggerAlarm();
        }

        /// <summary>
        /// 从子类触发报警（携带完整的 <see cref="StationAlarmEventArgs"/> 上下文，
        /// 包含硬件名称、原始异常等诊断信息）。
        /// </summary>
        /// <param name="context">包含完整报警上下文的事件参数。</param>
        protected void RaiseAlarm(StationAlarmEventArgs context)
        {
            if (_stopRequested) return;
            Volatile.Write(ref _pendingAlarm, new PendingAlarm(context.ErrorCode, context));
            TriggerAlarm();
        }

        /// <summary>
        /// 通知上层（主控/AlarmService）当前工站的硬件已自恢复，
        /// 由子类在检测到底层硬件自动清警后调用。
        /// </summary>
        protected void RaiseStationAlarmAutoCleared()
            => StationAlarmAutoCleared?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// 触发复位流程（将状态机从报警状态推进到 Resetting）。
        /// 通常由 UI 复位按钮或主控调用；实际物理复位动作由 <see cref="ExecuteResetAsync"/> 完成。
        /// </summary>
        public void ResetAlarm() => Fire(MachineTrigger.Reset);

        /// <summary>
        /// 根据本次报警来源（初始化期 or 运行期）返回正确的复位完成触发器：
        /// <list type="bullet">
        ///   <item>初始化报警 → <see cref="MachineTrigger.ResetDoneUninitialized"/>（复位后回到 Uninitialized，需重新初始化）</item>
        ///   <item>运行期报警 → <see cref="MachineTrigger.ResetDone"/>（复位后直接回到 Idle，可重新启动）</item>
        /// </list>
        /// </summary>
        protected MachineTrigger ResetCompletionTrigger =>
            _cameFromInitAlarm ? MachineTrigger.ResetDoneUninitialized : MachineTrigger.ResetDone;

        #endregion

        #region 8. 状态跳转引擎

        /// <summary>
        /// 线程安全地同步触发状态机变迁（阻塞获取锁，最多等待 5 s）。
        /// </summary>
        /// <param name="trigger">要触发的状态机触发器。</param>
        /// <returns><c>true</c> 表示变迁成功；<c>false</c> 表示已释放、锁超时或当前状态不允许此触发。</returns>
        protected bool Fire(MachineTrigger trigger)
        {
            if (Volatile.Read(ref _disposed) != 0) return false;
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

        /// <summary>
        /// 线程安全地异步触发状态机变迁（异步等待锁，最多等待 5 s）。
        /// 供含异步 OnEntry 动作（如 <see cref="OnStartRunningAsync"/>）的状态机触发使用。
        /// </summary>
        /// <param name="trigger">要触发的状态机触发器。</param>
        /// <returns><c>true</c> 表示变迁成功；<c>false</c> 表示已释放、锁超时或当前状态不允许此触发。</returns>
        protected async Task<bool> FireAsync(MachineTrigger trigger)
        {
            if (Volatile.Read(ref _disposed) != 0) return false;
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

        /// <summary>
        /// 在业务循环中等待指定时长，响应取消令牌（Stop/Pause 可打断）。
        /// </summary>
        /// <param name="milliseconds">等待毫秒数。</param>
        /// <param name="token">运行生命周期取消令牌。</param>
        protected Task WaitAsync(int milliseconds, CancellationToken token)
            => Task.Delay(milliseconds, token);

        /// <summary>
        /// 轮询等待布尔条件成立，支持超时和取消。
        /// </summary>
        /// <param name="condition">返回 <c>true</c> 时停止等待的条件委托。</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5000 ms。</param>
        /// <param name="token">运行生命周期取消令牌，取消时向上抛出 <see cref="OperationCanceledException"/>。</param>
        /// <param name="pollIntervalMs">轮询间隔毫秒数，默认 20 ms。</param>
        /// <returns>条件在超时前成立返回 <c>true</c>；超时返回 <c>false</c>；取消时抛出异常。</returns>
        protected async Task<bool> WaitConditionAsync(
            Func<bool> condition,
            int timeoutMs = 5_000,
            int pollIntervalMs = 20 ,
            CancellationToken token = default)
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
                // 外部 token 取消（Stop/Pause），向上传播
                throw;
            }
        }

        /// <summary>
        /// 等待指定 IO 控制器的端口到达目标电平（按端口索引寻址）。
        /// </summary>
        /// <param name="io">IO 控制器实例。</param>
        /// <param name="portIndex">端口索引。</param>
        /// <param name="targetState">期望的电平状态（<c>true</c> = 高电平）。</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5000 ms。</param>
        /// <param name="token">运行生命周期取消令牌。</param>
        /// <returns>在超时前到达目标状态返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        protected async Task<bool> WaitIOAsync(IIOController io, int portIndex, bool targetState,
            int timeoutMs = 5_000, CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] → {targetState}");
            bool result = await io.WaitInputAsync(portIndex, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result) _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>
        /// 等待指定 IO 控制器的枚举信号到达目标电平（按枚举名称寻址，类型安全）。
        /// </summary>
        /// <typeparam name="TEnum">IO 信号枚举类型。</typeparam>
        /// <param name="io">IO 控制器实例。</param>
        /// <param name="inputName">枚举信号名称。</param>
        /// <param name="targetState">期望的电平状态。</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5000 ms。</param>
        /// <param name="token">运行生命周期取消令牌。</param>
        /// <returns>在超时前到达目标状态返回 <c>true</c>，否则返回 <c>false</c>。</returns>
        protected async Task<bool> WaitIOAsync<TEnum>(IIOController io, TEnum inputName, bool targetState,
            int timeoutMs = 5_000, CancellationToken token = default) where TEnum : Enum
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] → {targetState}");
            bool result = await io.WaitInputAsync(inputName, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result) _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>
        /// 等待跨工站同步信号触发（如上游工站完成放料、传送带到位等协同事件）。
        /// </summary>
        /// <param name="sync">工站同步服务实例。</param>
        /// <param name="signalName">同步信号名称（与上游工站约定的键名）。</param>
        /// <param name="timeoutMs">超时毫秒数，默认 30000 ms。</param>
        /// <param name="token">运行生命周期取消令牌。</param>
        /// <returns>信号在超时前到达返回 <c>true</c>；超时返回 <c>false</c>；取消时抛出异常。</returns>
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

        /// <summary>
        /// 工站的记忆参数实例（如上次停机时保存的工艺位置、偏移量等）。
        /// 在构造时从磁盘反序列化读取；在 <see cref="DisposeAsync"/> / <see cref="Dispose"/> 时原子写回磁盘。
        /// </summary>
        public virtual T? MemoryParam { get; set; }

        /// <summary>记忆参数文件所在目录路径。</summary>
        private static string MemoryFileDir => Path.Combine(ConstGlobalParam.ConfigPath, "StationMemoryParam");

        /// <summary>记忆参数文件完整路径，以 <see cref="StationName"/> 为文件名，JSON 格式。</summary>
        private string MemoryFilePath => Path.Combine(MemoryFileDir, $"{StationName}.json");

        /// <summary>
        /// 从磁盘读取并反序列化记忆参数。在构造函数中调用，文件不存在或解析失败时静默忽略。
        /// </summary>
        private void ReadMemoryParam()
        {
            try
            {
                var path = MemoryFilePath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var deserialized = JsonSerializer.Deserialize<T>(json);
                    if (deserialized != null)
                    {
                        deserialized.IsWrite = false;
                        MemoryParam = deserialized;
                        return;
                    }
                    _logger?.Warn($"[{StationName}] 记忆参数反序列化为 null，已忽略文件。");
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 读取记忆参数失败: {ex.Message}");
            }

            MemoryParam ??= new T();
        }

        /// <summary>
        /// 将当前记忆参数原子写回磁盘（先写临时文件，再 File.Replace，防止崩溃时损坏 JSON）。
        /// 在 <see cref="DisposeAsync"/> / <see cref="Dispose"/> 时调用。
        /// </summary>
        private void WriteMemoryParam()
        {
            // 快照引用：防止 ClearMemory() 在序列化过程中将 MemoryParam 替换为新实例
            var snapshot = MemoryParam;
            if (snapshot == null) return;
            try
            {
                Directory.CreateDirectory(MemoryFileDir);
                snapshot.IsWrite = true;
                snapshot.LastWriteTime = DateTime.Now;

                // 原子写入：先写 .tmp 临时文件，成功后 Replace 正式文件，
                // 避免进程在写入中途崩溃时损坏已有的 JSON 文件
                var finalPath = MemoryFilePath;
                var tempPath = finalPath + ".tmp";
                var json = JsonSerializer.Serialize(snapshot);
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

        /// <summary>
        /// 将当前记忆参数立即持久化到磁盘。
        /// 供子工站在关键状态变迁节点（如取料完成、退料完成）主动调用，防止崩溃导致状态丢失。
        /// </summary>
        protected void FlushMemory() => WriteMemoryParam();

        /// <summary>
        /// 清空工站记忆参数：将内存参数重置为默认值，并删除磁盘上的 JSON 持久化文件。
        /// </summary>
        public void ClearMemory()
        {
            MemoryParam = new T();
            try
            {
                var path = MemoryFilePath;
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 清空记忆参数文件失败: {ex.Message}");
            }
        }

        #endregion

        #region 11. 资源清理

        /// <summary>
        /// 异步释放工站资源：取消业务循环、等待 workflow 退出（最多 5 s）、持久化记忆参数、释放 CTS 和状态锁。
        /// 子类重写时须在末尾调用 <c>await base.DisposeAsync()</c>。
        /// </summary>
        public virtual async ValueTask DisposeAsync()
        {
            // 原子 check-and-set 防止并发 Dispose 双重释放
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }

            var wf = _workflowTask;
            if (wf is { IsCompleted: false })
            {
                try { await wf.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                catch (TimeoutException) { _logger?.Warn($"[{StationName}] DisposeAsync 等待 workflow 退出超时"); }
                catch { }
            }

            WriteMemoryParam();

            try { _runCts?.Dispose(); } catch { }
            try { _stateLock.Dispose(); } catch { }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 同步释放工站资源（优先使用 <see cref="DisposeAsync"/>）：
        /// 取消业务循环、持久化记忆参数、释放 CTS 和状态锁。
        /// 注意：同步 Dispose 不等待 workflow 退出，可能在 workflow 仍在运行时返回。
        /// </summary>
        public virtual void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

            try { _runCts?.Cancel(); } catch (ObjectDisposedException) { }
            WriteMemoryParam();
            try { _runCts?.Dispose(); } catch { }
            try { _stateLock.Dispose(); } catch { }
            GC.SuppressFinalize(this);
        }

        #endregion
    }

    /// <summary>
    /// 工站记忆参数基类，保存需要跨运行周期持久化的工艺数据（如偏移量、上次停机位置等）。
    /// 子类继承此类并添加具体参数属性，由框架在工站构造/析构时自动读写 JSON 文件。
    /// </summary>
    public class StationMemoryBaseParam
    {
        /// <summary>
        /// 标记本次是否为主动写入（由框架内部管理，子类不应手动修改）。
        /// 正常写入时由框架置 <c>true</c>；读取后置 <c>false</c>。
        /// 若文件中此值为 <c>false</c>，说明上次是崩溃或断电写入，数据可能不完整。
        /// </summary>
        public bool IsWrite { get; set; }

        /// <summary>
        /// 断电前最后执行的步序值，用于恢复时定位断点。
        /// 对应各工站 Step 枚举的整数值。
        /// </summary>
        public int PersistedStep { get; set; }

        /// <summary>
        /// 记忆参数最后一次写入磁盘的时间戳，由框架在写入前自动设置。
        /// 可用于判断数据新鲜度或排查断电时刻。
        /// </summary>
        public DateTime LastWriteTime { get; set; }
    }

    /// <summary>
    /// 带步骤枚举的工站基类扩展，适用于业务流程可拆分为若干离散步骤的工站。
    /// 提供步骤跳转、错误路由和断点续跑所需的内置字段。
    /// </summary>
    /// <typeparam name="TMemory">记忆参数类型，须继承 <see cref="StationMemoryBaseParam"/>。</typeparam>
    /// <typeparam name="TStep">步骤枚举类型（struct + Enum 约束），用于描述业务流程中的各个阶段。</typeparam>
    /// <remarks>
    /// 初始化带步骤枚举的工站基类。
    /// </remarks>
    /// <param name="name">工站名称。</param>
    /// <param name="logger">日志服务。</param>
    /// <param name="initialStep">初始步骤（通常为枚举的第一个值，如 <c>Step.Idle</c>）。</param>
    public abstract class StationBase<TMemory, TStep>(string name, ILogService? logger, TStep initialStep) : StationBase<TMemory>(name, logger)
        where TMemory : StationMemoryBaseParam, new()
        where TStep : struct, Enum
    {
        /// <summary>当前正在执行的步骤，在 ProcessNormalLoopAsync 中驱动 switch/if 分支跳转。</summary>
        protected TStep _currentStep = initialStep;

        /// <summary>
        /// 报警复位后续跑的恢复步骤。
        /// 在 <see cref="RouteToError"/> 中与 <c>errorStep</c> 一同记录，复位完成后从此步骤重新执行。
        /// </summary>
        protected TStep _resumeStep = initialStep;

        /// <summary>当前关联的报警码缓存，供错误步骤逻辑读取以上报正确的报警码。</summary>
        protected string? _cachedErrorCode;

        /// <summary>
        /// 将业务流程路由到错误处理步骤，并记录报警复位后的恢复步骤。
        /// 通常在检测到硬件异常时调用，随后触发 <see cref="StationBase{T}.RaiseAlarm(string)"/>。
        /// </summary>
        /// <param name="errorStep">错误处理步骤（执行急停、关阀等安全动作）。</param>
        /// <param name="resumeStep">复位完成后重新开始的步骤（通常为当前动作步骤或其前置步骤）。</param>
        /// <param name="errorCode">关联的报警码，可为 null（由调用方单独上报）。</param>
        protected void RouteToError(TStep errorStep, TStep resumeStep, string? errorCode = null)
        {
            _currentStep = errorStep;
            _resumeStep = resumeStep;
            _cachedErrorCode = errorCode;
        }
    }

}
