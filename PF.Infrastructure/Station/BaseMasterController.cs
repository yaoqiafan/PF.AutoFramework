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
    /// 全局主控调度器基类（Infrastructure 层）。
    ///
    /// <para>
    /// 【职责概述】
    /// 负责统筹协调整条产线上所有子工站（<see cref="IStation"/>）的生命周期、状态流转与运行模式切换，
    /// 是产线级状态机与子工站状态机之间的桥接层。
    /// </para>
    ///
    /// <para>
    /// 【状态生命周期 (State Lifecycle)】
    /// <code>
    ///   Uninitialized ──(Initialize)──→ Initializing ──(InitializeDone)──────────→ Idle
    ///                                                  └──────(Error)─────────────→ InitAlarm
    ///   Idle          ──(Start)───────→ Running
    ///                 ──(Initialize)──→ Initializing  （Idle 下支持重新初始化）
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
    /// 【并发安全设计】
    /// <list type="bullet">
    ///   <item>状态变迁互斥：<c>_machineLock</c>（SemaphoreSlim(1,1)）保护所有 Fire / FireAsync 调用。</item>
    ///   <item>硬件按钮防抖：<c>_hardwareOpGate</c>（SemaphoreSlim(1,1)，非阻塞 WaitAsync(0)）
    ///         防止 Start/Reset 按钮抖动或快速重复触发产生并发调用。</item>
    ///   <item>报警去重：<c>_reportedAlarmKeys</c>（ConcurrentDictionary）在同一报警周期内对相同
    ///         "工站+错误码+硬件名"组合去重，避免日志和 AlarmService 被淹没。</item>
    ///   <item>停止意图标志：<c>_subStationStopsAreIntentional</c> 在 Stop/Pause 期间置位，
    ///         抑制 OnSubStationStateChanged 守卫因预期的 Uninitialized 跳转触发误报。
    ///         标志在 FireAsync(Stop) 完成之后才归零，消除状态机跳转前的误报窗口。</item>
    ///   <item>双重释放防护：<c>_disposed</c>（int）通过 Interlocked.CompareExchange 原子 check-and-set。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 【继承扩展点】
    /// <list type="bullet">
    ///   <item><see cref="OnHardwareInputReceived"/>：重写以处理自定义硬件输入类型（默认处理 Start/Pause/Reset）。</item>
    ///   <item><see cref="OnAfterResetSuccess"/>：全线复位成功后的回调，可在此清理业务状态或发送通知。</item>
    ///   <item><see cref="OnHardwareResetRequested"/>：处理来自硬件层的单工站局部复位请求，可重写以添加自定义路由逻辑。</item>
    /// </list>
    /// </para>
    /// </summary>
    public abstract class BaseMasterController : IMasterController
    {
        #region Fields & Properties

        /// <summary>获取全局主控当前所处的生命周期状态，由 Stateless 状态机维护。</summary>
        public MachineState CurrentState => _globalMachine.State;

        /// <summary>获取当前全局运行模式（Normal / DryRun 等），由 <see cref="SetMode"/> 在 Idle 下统一切换。</summary>
        public OperationMode CurrentMode { get; private set; } = OperationMode.Normal;

        /// <summary>
        /// 全局主控状态变迁时触发，携带新目标状态。
        /// 在独立 Task.Run 线程中异步派发，订阅者不得在回调中同步调用 Fire。
        /// </summary>
        public event EventHandler<MachineState>? MasterStateChanged;

        /// <summary>
        /// 任一子工站触发真实报警（非级联码）时，在主控层向上汇聚后触发。
        /// 同一报警周期内对相同"工站+错误码+硬件名"组合去重，避免事件风暴。
        /// </summary>
        public event EventHandler<StationAlarmEventArgs>? MasterAlarmTriggered;

        /// <summary>日志服务，构造时注入，不允许为 null。</summary>
        protected readonly ILogService _logger;

        /// <summary>
        /// 硬件输入事件总线（可选）。订阅 <see cref="HardwareInputEventBus.HardwareInputTriggered"/>，
        /// 将物理按钮（启动/暂停/复位）信号路由到对应的业务方法。
        /// </summary>
        protected readonly HardwareInputEventBus? _hardwareEventBus;

        /// <summary>受主控管理的所有子工站列表（只读快照，构造时传入）。</summary>
        protected readonly IReadOnlyList<IStation> _subStations;

        /// <summary>报警服务（可选），用于持久化活跃报警记录和触发报警通知。</summary>
        private readonly IAlarmService? _alarmService;

        /// <summary>全局 Stateless 状态机实例，表示整条产线的运行状态。</summary>
        protected readonly StateMachine<MachineState, MachineTrigger> _globalMachine;

        /// <summary>
        /// 状态变迁互斥锁（SemaphoreSlim(1,1)）。
        /// 保证同一时刻只有一个线程能执行 _globalMachine.Fire，防止并发报警与 API 调用的状态撕裂。
        /// </summary>
        private readonly SemaphoreSlim _machineLock = new(1, 1);

        /// <summary>
        /// 停止意图标志：在主控主动发起 Stop / Pause 时置 <c>true</c>，完成后置 <c>false</c>。
        /// <see cref="OnSubStationStateChanged"/> 守卫读取此标志，在预期的 Uninitialized 跳转时抑制误报。
        /// </summary>
        private volatile bool _subStationStopsAreIntentional;

        /// <summary>标记当前全局报警是否源自初始化阶段，决定复位完成后的路由目标。</summary>
        private volatile bool _masterCameFromInitAlarm;

        /// <summary>
        /// 初始化中止标志：由 <see cref="StopAllAsync"/> 在取消 <see cref="_initCts"/> 前置 <c>true</c>，
        /// <see cref="InitializeAllAsync"/> 检测到此标志后跳过后续状态触发，由 Stop 路径接管状态机。
        /// </summary>
        private volatile bool _abortInitRequested;

        /// <summary>
        /// 获取当前全局报警是否源自初始化阶段。
        /// 子类可在 <see cref="OnAfterResetSuccess"/> 中读取，决定是否需要重新初始化。
        /// </summary>
        protected bool MasterCameFromInitAlarm => _masterCameFromInitAlarm;

        /// <summary>
        /// 同一报警周期内已上报的报警键集合（格式："工站名:错误码:硬件名"）。
        /// ConcurrentDictionary 作为线程安全的 HashSet 使用（Value 固定为 0），
        /// 进入 Resetting 状态时自动清空，为下次报警重新计数。
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _reportedAlarmKeys = new();

        /// <summary>
        /// 全线初始化的取消令牌源，由 <see cref="InitializeAllAsync"/> 创建并通过
        /// <see cref="Interlocked.Exchange"/> 原子替换；任一子工站初始化失败时，
        /// <see cref="OnSubStationStateChanged"/> 调用 Cancel 中断其余子站的并行初始化。
        /// </summary>
        private CancellationTokenSource? _initCts;

        /// <summary>
        /// 外部注册的硬件复位路由委托（通过 <see cref="RegisterHardwareResetHandler"/> 注入）。
        /// 存在时优先执行，实现 Shell 层与 Infrastructure 层的解耦。
        /// </summary>
        private Action<HardwareResetRequest>? _hardwareResetHandler;

        /// <summary>
        /// 释放标志（0 = 未释放，1 = 已释放）。
        /// 通过 Interlocked.CompareExchange 原子 check-and-set，防止并发 Dispose 双重释放。
        /// </summary>
        private int _disposed;

        /// <summary>
        /// 硬件操作互斥门（SemaphoreSlim(1,1)，非阻塞 WaitAsync(0)）。
        /// 防止硬件按钮抖动或快速重复触发时 ExecuteSmartStartAsync / ExecuteHardwareResetAsync 并发执行。
        /// 后到的指令被直接丢弃并记录警告日志，不进入等待队列。
        /// </summary>
        private readonly SemaphoreSlim _hardwareOpGate = new(1, 1);

        #endregion

        #region Constructor

        /// <summary>
        /// 初始化全局主控基类：注册子工站事件、订阅硬件输入总线、配置全局状态机。
        /// </summary>
        /// <param name="logger">日志服务，不可为 null。</param>
        /// <param name="hardwareEventBus">硬件输入事件总线，可为 null（无物理按钮时）。</param>
        /// <param name="subStations">受管理的子工站集合，不可为 null。</param>
        /// <param name="alarmService">报警持久化服务，可为 null（无 AlarmService 时）。</param>
        /// <exception cref="ArgumentNullException"><paramref name="logger"/> 或 <paramref name="subStations"/> 为 null 时抛出。</exception>
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

            // 订阅所有子工站的报警、自恢复和状态变迁事件，汇聚到主控层统一处理
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

        /// <summary>
        /// 配置全局 Stateless 状态机的所有状态、允许触发器及进出动作。
        /// </summary>
        private void ConfigureGlobalMachine()
        {
            // 每次状态变迁后在独立线程通知外部订阅者，避免回调中同步调用 Fire 导致 _machineLock 死锁
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
                .Permit(MachineTrigger.Error, MachineState.InitAlarm)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized);

            _globalMachine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            _globalMachine.Configure(MachineState.Running)
                // 从 Start 进入：逐站启动业务循环（顺序）
                .OnEntryFromAsync(MachineTrigger.Start, StartAllSubStationsAsync)
                // 从 Resume 进入：逐站恢复业务循环（顺序）
                .OnEntryFromAsync(MachineTrigger.Resume, ResumeAllSubStationsAsync)
                // 退出到 Paused 时同步暂停所有子站（OnExit 在状态机锁内同步执行，不可异步）
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
                    // 级联触发所有子工站进入报警状态，使子站业务循环同步终止
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
                // 进入复位时清空报警去重字典，为本次复位后可能触发的新报警重新计数
                .OnEntry(() => _reportedAlarmKeys.Clear())
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized)
                // 复位过程中再次出错：动态路由回原始报警状态
                .PermitDynamic(MachineTrigger.Error,
                    () => _masterCameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        /// <summary>
        /// 按顺序逐站启动所有子工站的业务循环。
        /// 顺序启动确保依赖前序工站就绪的工站能按正确顺序进入 Running 状态。
        /// 单个工站启动失败不阻断其余工站。
        /// </summary>
        private async Task StartAllSubStationsAsync()
        {
            foreach (var s in _subStations)
            {
                try { await s.StartAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.Error($"【主控】启动工站 [{s.StationName}] 失败: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 按顺序逐站恢复所有子工站的业务循环（暂停后续跑）。
        /// </summary>
        private async Task ResumeAllSubStationsAsync()
        {
            foreach (var s in _subStations)
            {
                try { await s.ResumeAsync().ConfigureAwait(false); }
                catch (Exception ex) { _logger.Error($"【主控】续跑工站 [{s.StationName}] 失败: {ex.Message}"); }
            }
        }

        /// <summary>
        /// 在状态机 OnExit 回调中同步暂停所有子工站。
        /// 设置 <c>_subStationStopsAreIntentional</c> 抑制 OnSubStationStateChanged 的守卫误报。
        /// </summary>
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

        /// <summary>
        /// 级联触发所有子工站进入报警状态（CascadeAlarm）。
        /// 在主控进入 InitAlarm / RunAlarm 时调用，使各子站业务循环同步终止并进入安全状态。
        /// </summary>
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

        /// <summary>
        /// 处理来自 <see cref="HardwareInputEventBus"/> 的物理按钮输入。
        /// 默认路由：Start → SmartStart（带互斥门）；Pause → PauseAll；Reset → SmartReset（带互斥门）。
        /// 如需处理自定义输入类型，重写此方法并在 base.OnHardwareInputReceived 前/后添加分支。
        /// </summary>
        /// <param name="inputType">硬件输入类型（见 <see cref="HardwareInputType"/>）。</param>
        protected virtual void OnHardwareInputReceived(string inputType)
        {
            switch (inputType)
            {
                case HardwareInputType.Start: _ = ExecuteSmartStartAsync(); break;
                case HardwareInputType.Pause: PauseAll(); break;
                case HardwareInputType.Reset: _ = ExecuteHardwareResetAsync(); break;
                case HardwareInputType.SafeDoor:
                    PauseAll();
                    _alarmService?.TriggerAlarm("主控", AlarmCodes.Safety.SafeDoorOpen, null);
                    break;
            }
        }

        /// <summary>
        /// 硬件 Start 按钮的智能路由执行方法：
        /// 根据当前状态自动选择 Initialize+Start、Start 或 Resume。
        /// 通过 <c>_hardwareOpGate</c> 互斥门防止并发触发，后到的指令直接丢弃。
        /// </summary>
        private async Task ExecuteSmartStartAsync()
        {
            // 非阻塞尝试获取互斥门；获取失败说明另一操作正在进行，丢弃本次指令
            if (!await _hardwareOpGate.WaitAsync(0).ConfigureAwait(false))
            {
                _logger.Warn("【全局主控】SmartStart 指令被丢弃，硬件操作互斥门正忙。");
                return;
            }
            try
            {
                switch (CurrentState)
                {
                    case MachineState.Uninitialized:
                        // 未初始化时：先初始化，成功后自动启动
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
            finally
            {
                _hardwareOpGate.Release();
            }
        }

        /// <summary>
        /// 硬件 Reset 按钮的执行方法，通过 <c>_hardwareOpGate</c> 互斥门防止并发触发。
        /// 内部调用 <see cref="ResetAllAsync"/>，后到的指令直接丢弃。
        /// </summary>
        private async Task ExecuteHardwareResetAsync()
        {
            if (!await _hardwareOpGate.WaitAsync(0).ConfigureAwait(false))
            {
                _logger.Warn("【全局主控】Reset 指令被丢弃，硬件操作互斥门正忙。");
                return;
            }
            try { await ResetAllAsync().ConfigureAwait(false); }
            catch (Exception ex) { _logger.Error($"【全局主控】硬件触发复位失败: {ex.Message}"); }
            finally { _hardwareOpGate.Release(); }
        }

        #endregion

        #region Sub-Station Events

        /// <summary>
        /// 监听子工站状态变迁，实现两个守卫逻辑：
        /// <list type="number">
        ///   <item>初始化阶段：任一子站进入报警 → 取消 <c>_initCts</c> 中断其余子站的并行初始化。</item>
        ///   <item>运行/暂停阶段：子站非预期跳回 Uninitialized → 触发全线报警（状态撕裂保护）。</item>
        /// </list>
        /// </summary>
        private void OnSubStationStateChanged(object? sender, StationStateChangedEventArgs e)
        {
            var stationName = (sender as IStation)?.StationName ?? "未知工站";
            var masterState = CurrentState;

            // 守卫一：初始化阶段任一工站失败，取消整批并行初始化任务
            if (masterState == MachineState.Initializing
                && (e.NewState == MachineState.InitAlarm || e.NewState == MachineState.RunAlarm))
            {
                _logger.Warn($"【主控】工站 [{stationName}] 初始化失败({e.NewState})，取消其余工站初始化。");
                try { _initCts?.Cancel(); } catch { }
            }

            // 守卫二：运行/暂停期间子站非预期跳回 Uninitialized（如子站独立 Stop 或异常崩溃）
            // _subStationStopsAreIntentional 为 true 时说明是主控主动 Stop/Pause，忽略此守卫
            if (!_subStationStopsAreIntentional
                && (masterState == MachineState.Running || masterState == MachineState.Paused)
                && e.NewState == MachineState.Uninitialized
                && e.OldState != MachineState.Resetting)
            {
                _logger.Warn($"【主控守卫】状态撕裂！工站 [{stationName}] 从 {e.OldState} 跳回 Uninitialized，触发全线报警。");
                // 在独立线程中触发，避免在事件回调中同步调用 Fire 导致 _machineLock 死锁
                _ = Task.Run(() =>
                {
                    try { Fire(MachineTrigger.Error); }
                    catch (Exception ex) { _logger.Error($"【主控守卫】切入报警状态失败: {ex.Message}"); }
                });
            }
        }

        /// <summary>
        /// 处理子工站上报的真实报警事件：
        /// <list type="number">
        ///   <item>过滤级联报警码（CascadeAlarm），避免主控自身触发的级联信号造成循环。</item>
        ///   <item>对相同"工站+错误码+硬件名"组合去重，防止重复写入日志和 AlarmService。</item>
        ///   <item>向上触发 <see cref="MasterAlarmTriggered"/> 事件，通知 Shell / UI 层。</item>
        ///   <item>驱动主控状态机进入 RunAlarm / InitAlarm 状态。</item>
        /// </list>
        /// </summary>
        private void OnSubStationAlarm(object? sender, StationAlarmEventArgs e)
        {
            // 忽略来自主控自身级联触发的 CascadeAlarm，防止事件回环
            if (e.ErrorCode == AlarmCodes.System.CascadeAlarm) return;

            var source = (sender as IStation)?.StationName ?? "未知工站";
            var dedupKey = $"{source}:{e.ErrorCode}:{e.HardwareName ?? string.Empty}";

            // TryAdd 返回 true 说明是此周期内首次上报，执行日志和 AlarmService 写入
            if (_reportedAlarmKeys.TryAdd(dedupKey, 0))
            {
                _logger.Fatal($"【报警】{source} | {e.ErrorCode}" +
                    (e.HardwareName != null ? $" | 硬件:{e.HardwareName}" : string.Empty) +
                    (e.RuntimeMessage != null ? $" | {e.RuntimeMessage}" : string.Empty));

                _alarmService?.TriggerAlarm(source, e.ErrorCode, e.RuntimeMessage);
                try { MasterAlarmTriggered?.Invoke(this, e); }
                catch (Exception ex) { _logger.Error($"【主控】MasterAlarmTriggered 订阅者异常: {ex.Message}"); }
            }

            // 无论是否去重，均尝试驱动主控进入报警状态（Fire 内部有 CanFire 保护，重复调用安全）
            _ = Task.Run(() =>
            {
                try { Fire(MachineTrigger.Error); }
                catch (Exception ex) { _logger.Fatal($"【主控】尝试切入报警失败: {ex.Message}"); }
            });
        }

        /// <summary>
        /// 处理子工站硬件自恢复事件，通知 AlarmService 清除该工站的活跃报警记录。
        /// </summary>
        private void OnSubStationAlarmAutoCleared(object? sender, EventArgs e)
        {
            var source = (sender as IStation)?.StationName ?? "未知工站";
            _logger.Info($"【主控】工站 [{source}] 硬件自恢复，主动清除报警服务记录。");
            _alarmService?.ClearAlarm(source);
        }

        #endregion

        #region Global Core API

        /// <summary>
        /// 驱动全局状态机从 Idle 进入 Running，状态机 OnEntry 会自动逐站调用 StartAsync。
        /// </summary>
        public Task StartAllAsync() => FireAsync(MachineTrigger.Start);

        /// <summary>
        /// 并行停止所有子工站（最多 4 路并发），等待各站安全停稳后驱动主控状态机回到 Uninitialized。
        /// </summary>
        /// <remarks>
        /// <c>_subStationStopsAreIntentional</c> 在整个操作期间保持 <c>true</c>，
        /// 在 FireAsync(Stop) 完成后才归零，避免子站过渡期间 OnSubStationStateChanged 守卫触发误报。
        /// </remarks>
        public async Task StopAllAsync()
        {
            _subStationStopsAreIntentional = true;
            try
            {
                // 初始化中止：先设标志、取消 _initCts，再立即 Fire(Stop) 抢占状态机，
                // 防止 InitializeAllAsync 的 Error/InitializeDone 路径先于 Stop 触发。
                if (CurrentState == MachineState.Initializing)
                {
                    _abortInitRequested = true;
                    var initCts = Interlocked.Exchange(ref _initCts, null);
                    try { initCts?.Cancel(); } catch { }
                    try { initCts?.Dispose(); } catch { }
                    Fire(MachineTrigger.Stop);  // Initializing → Uninitialized
                }

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
                // 必须在 flag 归零之前完成状态机跳转，避免仍在过渡的子站状态变更触发误报警
                // 若已由 Initializing 中止路径转移到 Uninitialized，则无需再次 Fire
                if (CurrentState != MachineState.Uninitialized)
                    await FireAsync(MachineTrigger.Stop).ConfigureAwait(false);
            }
            finally
            {
                _subStationStopsAreIntentional = false;
            }
        }

        /// <summary>
        /// 同步暂停所有子工站并驱动全局状态机进入 Paused。
        /// 实际子站暂停动作在 ConfigureGlobalMachine 的 Running.OnExit 中触发。
        /// </summary>
        public void PauseAll() => Fire(MachineTrigger.Pause);

        /// <summary>
        /// 驱动全局状态机从 Paused 进入 Running，状态机 OnEntry 会自动逐站调用 ResumeAsync。
        /// </summary>
        public Task ResumeAllAsync() => FireAsync(MachineTrigger.Resume);

        /// <summary>
        /// 设置全局运行模式并同步下发到所有子工站。
        /// 仅在 <see cref="MachineState.Idle"/> 状态下允许切换，运行中调用将被拒绝并返回 <c>false</c>。
        /// </summary>
        /// <param name="mode">目标运行模式。</param>
        /// <returns>设置成功返回 <c>true</c>；当前不在 Idle 状态返回 <c>false</c>。</returns>
        public bool SetMode(OperationMode mode)
        {
            if (CurrentState != MachineState.Idle) return false;
            CurrentMode = mode;
            foreach (var s in _subStations) s.CurrentMode = mode;
            return true;
        }

        /// <summary>
        /// 全线并行初始化（限流模式，最多 4 路并发，全局超时 120 s）。
        /// 流程：驱动主控进入 Initializing → 并行 ExecuteInitializeAsync → 检查结果 → InitializeDone 或 Error。
        /// 任一子站失败时取消其余子站的初始化（通过 <c>_initCts</c>）并触发 InitAlarm。
        /// </summary>
        public async Task InitializeAllAsync()
        {
            _logger.Info("【主控】开始全线初始化(限流模式)...");
            // 使用 Fire 返回值原子判断：在锁内完成 CanFire + Fire，消除 TOCTOU 竞态
            if (!Fire(MachineTrigger.Initialize)) return;

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            // 原子替换 _initCts，取消并释放上次遗留的（若有）
            var oldCts = Interlocked.Exchange(ref _initCts, cts);
            try { oldCts?.Cancel(); oldCts?.Dispose(); } catch { }
            try
            {
                await Parallel.ForEachAsync(_subStations,
                    new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = cts.Token },
                    async (station, token) =>
                    {
                        try { await station.ExecuteInitializeAsync(token).ConfigureAwait(false); }
                        catch (OperationCanceledException) { throw; }  // 向上传播，终止 ForEachAsync
                        catch (Exception ex) { _logger.Error($"【主控】初始化工站 [{station.StationName}] 异常: {ex.Message}"); }
                    }).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* 某工站失败触发 _initCts.Cancel()，属于预期流程 */ }
            catch (Exception ex)
            {
                _logger.Error($"【主控】初始化异常: {ex.Message}");
                _alarmService?.TriggerAlarm("主控", AlarmCodes.System.InitializationTimeout, "设备复位时间过长，请调整复位参数！");
                Fire(MachineTrigger.Error);
                return;
            }
            finally
            {
                // 仅在 _initCts 仍为本次 cts 时才清空，防止并发调用时误清另一次的 cts
                Interlocked.CompareExchange(ref _initCts, null, cts);
                try { cts.Dispose(); } catch { }
            }

            // 外部 StopAllAsync 请求中止初始化：跳过后续状态触发，由 Stop 路径负责状态机
            if (_abortInitRequested)
            {
                _abortInitRequested = false;
                return;
            }

            // 检查是否有子站仍处于报警状态（初始化失败但未抛出异常的情况）
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

        /// <summary>
        /// 全线并行复位（限流模式，最多 4 路并发，全局超时 30 s）。
        /// 流程：驱动主控进入 Resetting → 并行 ExecuteResetAsync → 触发 ResetDone 或 ResetDoneUninitialized。
        /// 超时或任意子站复位失败时触发 Error 回退到报警状态。
        /// </summary>
        public async Task ResetAllAsync()
        {
            _logger.Info("【主控】开始全线复位(限流并行模式)...");
            if (!Fire(MachineTrigger.Reset)) return;

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
                    _logger.Error("【主控】全线复位超时（30 s），系统重新回到报警状态。");
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

            // 根据报警来源阶段选择复位完成触发器
            var resetTrigger = _masterCameFromInitAlarm
                ? MachineTrigger.ResetDoneUninitialized   // 初始化报警：复位后回 Uninitialized，需重新初始化
                : MachineTrigger.ResetDone;               // 运行期报警：复位后直接回 Idle，可重新启动

            await FireAsync(resetTrigger).ConfigureAwait(false);
        }

        #endregion

        #region Hardware Reset Mechanisms

        /// <summary>
        /// 系统全局复位入口，由 Shell 层在收到 SystemResetRequestedEvent 时调用。
        /// 直接委托给 <see cref="ResetAllAsync"/>。
        /// </summary>
        public Task RequestSystemResetAsync()
        {
            _logger.Info("【主控】接收到系统复位请求，开始执行全线复位...");
            return ResetAllAsync();
        }

        /// <summary>
        /// 清空所有子工站的记忆参数。仅在设备处于未初始化状态时允许执行。
        /// </summary>
        /// <exception cref="InvalidOperationException">设备不处于未初始化状态。</exception>
        public void ClearAllStationMemory()
        {
            if (_globalMachine.State != MachineState.Uninitialized)
                throw new InvalidOperationException($"清空记忆仅允许在未初始化状态下执行，当前状态：{_globalMachine.State}");

            _logger.Info("【主控】开始清空所有子工站记忆参数...");
            foreach (var station in _subStations)
            {
                try
                {
                    station.ClearMemory();
                    _logger.Info($"【主控】已清空工站 [{station.StationName}] 的记忆参数。");
                }
                catch (Exception ex)
                {
                    _logger.Error($"【主控】清空工站 [{station.StationName}] 记忆参数失败: {ex.Message}");
                }
            }
            _logger.Success("【主控】所有子工站记忆参数已清空。");
        }

        /// <summary>
        /// 全线复位成功后的扩展钩子，在 AlarmService.ClearAllActiveAlarms 之前调用。
        /// 子类可在此清理业务状态、发送通知或更新 UI。默认为空操作。
        /// </summary>
        protected virtual void OnAfterResetSuccess() { }

        /// <summary>
        /// 注册硬件复位请求处理委托（由 Shell 层注入，实现 Prism 事件总线与 Infrastructure 层的解耦）。
        /// 注册后 <see cref="OnHardwareResetRequested"/> 将优先调用此委托，而非默认路由逻辑。
        /// </summary>
        /// <param name="handler">处理 <see cref="HardwareResetRequest"/> 的委托。</param>
        public void RegisterHardwareResetHandler(Action<HardwareResetRequest> handler)
            => _hardwareResetHandler = handler;

        /// <summary>
        /// 处理来自硬件层的单工站局部复位请求（如伺服驱动器自动发出的清错请求）。
        /// <list type="number">
        ///   <item>优先调用通过 <see cref="RegisterHardwareResetHandler"/> 注册的自定义委托。</item>
        ///   <item>否则按 request.Source 定位目标子站，验证其处于报警状态后，
        ///         在独立线程（带 30 s 超时）执行 ExecuteResetAsync。</item>
        /// </list>
        /// </summary>
        /// <param name="request">硬件复位请求，包含来源工站名和错误码列表。</param>
        public virtual void OnHardwareResetRequested(HardwareResetRequest request)
        {
            if (request == null) return;

            // 优先走注册的自定义路由（Shell 层通过 Prism 事件总线桥接）
            var handler = _hardwareResetHandler;
            if (handler != null)
            {
                try { handler(request); return; }
                catch (Exception ex) { _logger.Error($"【主控】硬件复位路由异常: {ex.Message}"); }
            }

            var station = _subStations.FirstOrDefault(s => s.StationName == request.Source);
            if (station == null) return;
            // 仅对当前处于报警状态的工站执行复位，防止误操作正常运行中的工站
            if (station.CurrentState != MachineState.InitAlarm && station.CurrentState != MachineState.RunAlarm) return;

            _logger.Info($"【主控】接收到硬件复位请求，来源：{request.Source}，错误码：{string.Join(", ", request.ErrorCodes)}");

            _ = Task.Run(async () =>
            {
                // 使用 30 s 超时 CTS，防止复位操作因硬件无响应而永久阻塞线程池线程
                using var resetCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
                try { await station.ExecuteResetAsync(resetCts.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) { _logger.Warn($"【主控】子站局部硬件复位超时（30 s），来源：{request.Source}"); }
                catch (Exception ex) { _logger.Error($"【主控】硬件复位请求执行失败，来源：{request.Source}: {ex.Message}"); }
            });
        }

        #endregion

        #region Thread-Safe Triggers

        /// <summary>
        /// 线程安全地同步触发全局状态机变迁（阻塞获取锁，最多等待 5 s）。
        /// </summary>
        /// <param name="trigger">要触发的状态机触发器。</param>
        /// <returns><c>true</c> 表示变迁成功；<c>false</c> 表示已释放、锁超时或当前状态不允许此触发。</returns>
        private bool Fire(MachineTrigger trigger)
        {
            if (Volatile.Read(ref _disposed) != 0) return false;
            bool acquired = false;
            try
            {
                acquired = _machineLock.Wait(TimeSpan.FromSeconds(5));
                if (!acquired)
                {
                    _logger.Error($"【主控】Fire({trigger}) 获取状态锁超时。");
                    return false;
                }
                if (_globalMachine.CanFire(trigger)) { _globalMachine.Fire(trigger); return true; }
                return false;
            }
            catch (ObjectDisposedException) { return false; }
            finally
            {
                if (acquired) { try { _machineLock.Release(); } catch (ObjectDisposedException) { } }
            }
        }

        /// <summary>
        /// 线程安全地异步触发全局状态机变迁（异步等待锁，最多等待 5 s）。
        /// 供含异步 OnEntry 动作（如 StartAllSubStationsAsync）的状态机触发使用。
        /// </summary>
        /// <param name="trigger">要触发的状态机触发器。</param>
        private async Task FireAsync(MachineTrigger trigger)
        {
            if (Volatile.Read(ref _disposed) != 0) return;
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

        /// <summary>
        /// 释放主控资源：注销所有事件订阅、取消并释放 <c>_initCts</c>、释放状态锁和硬件操作门。
        /// 注意：此方法不调用子工站的 Dispose，子工站生命周期由外部容器管理。
        /// </summary>
        public virtual void Dispose()
        {
            // 原子 check-and-set 防止并发 Dispose 双重释放
            if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) return;

            // 注销硬件输入总线订阅，防止 Dispose 后仍收到硬件事件
            if (_hardwareEventBus != null)
                _hardwareEventBus.HardwareInputTriggered -= OnHardwareInputReceived;

            // 注销所有子工站事件，防止子站报警事件触发已释放主控的回调
            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered -= OnSubStationAlarm;
                station.StationAlarmAutoCleared -= OnSubStationAlarmAutoCleared;
                station.StationStateChanged -= OnSubStationStateChanged;
            }

            // 原子取出 _initCts 并取消，中断可能仍在进行的并行初始化
            var initCts = Interlocked.Exchange(ref _initCts, null);
            try { initCts?.Cancel(); } catch { }
            try { initCts?.Dispose(); } catch { }
            try { _machineLock.Dispose(); } catch { }
            try { _hardwareOpGate.Dispose(); } catch { }

            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
