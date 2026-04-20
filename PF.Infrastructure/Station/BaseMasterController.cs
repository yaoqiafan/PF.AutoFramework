using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
using PF.Core.Models;
using PF.Infrastructure.Station.Basic;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Infrastructure.Station
{
    /// <summary>
    /// 全局主控调度器基类 (Infrastructure层)
    /// 封装了所有非标自动化设备通用的状态流转、并发安全保护、以及面板事件响应。
    /// 采用 Stateless 状态机管理生命周期，并通过严格的锁机制保证并发安全。
    /// </summary>
    /// <remarks>
    /// 💡 【使用指南】
    /// 
    /// 本类为抽象基类，必须在具体机台项目中派生并注入到 IoC 容器。
    /// 
    /// 📌 必须依赖的注入配置 (以 DryIoc 为例)：
    /// 1. 需将所有子工站按单例注册，以便底层框架通过 <see cref="IEnumerable{T}"/> 自动收集。
    /// 2. 需将派生的主控类注册为 <see cref="IMasterController"/> 接口的单例。
    /// 
    /// 📌 推荐重写的扩展点 (Virtual Methods)：
    /// 
    /// - <see cref="OnAfterResetSuccess"/> (最常用):
    ///   触发时机：全线复位动作成功完成，但主控状态尚未切回 <see cref="MachineState.Idle"/> 之前。
    ///   使用场景：清理上一模的残存业务数据（如扫码缓存）、清空全局气缸互锁标志、重置生产计数等。
    /// 
    /// - <see cref="OnHardwareInputReceived(string)"/>:
    ///   触发时机：底层 <see cref="HardwareInputEventBus"/> 广播了物理按钮事件时。
    ///   使用场景：如果当前机台除了标准的启动/暂停/复位外，还有专属的物理按键（如：急停、点动、模式切换旋钮），
    ///   可在此处拦截并扩展路由。重写时请保留对基类 <see cref="OnHardwareInputReceived(string)"/> 的调用以处理标准按键。
    /// 
    /// - <see cref="OnHardwareResetRequested(HardwareResetRequest)"/>:
    ///   触发时机：UI 面板或其它模块通过 Prism EA 路由了特定工站的复位请求时。
    ///   使用场景：如果机台包含复杂的组合机构，需要比默认“按名称匹配对应子工站并触发清警”更精细的控制逻辑，可以在此重写。
    /// 
    /// - <see cref="Dispose"/>:
    ///   使用场景：如果派生类中订阅了额外的全局事件或占用了非托管资源，需在此释放。
    ///   注意：务必在末尾调用基类的 <see cref="Dispose()"/>，以确保基础的防撕裂守卫和底层总线事件被安全解绑，防止内存泄漏。
    /// </remarks>
    public abstract class BaseMasterController : IMasterController, IDisposable
    {
        #region Fields & Properties (字段与属性)

        /// <summary>
        /// 主控当前所处的运行状态
        /// </summary>
        public MachineState CurrentState => _globalMachine.State;

        /// <summary>
        /// 主控当前的操作模式（正常、空跑、维修等）
        /// </summary>
        public OperationMode CurrentMode { get; private set; } = OperationMode.Normal;

        /// <summary>
        /// 当主控状态发生流转时触发
        /// </summary>
        public event EventHandler<MachineState> MasterStateChanged;

        /// <summary>
        /// 当主控或子工站触发真实报警时触发，携带硬件名、运行时消息等上下文。
        /// </summary>
        public event EventHandler<StationAlarmEventArgs> MasterAlarmTriggered;

        // 基础服务依赖
        /// <summary>全局日志记录服务。</summary>
        protected readonly ILogService _logger;

        /// <summary>底层硬件输入事件总线，用于监听物理按键等信号。</summary>
        protected readonly HardwareInputEventBus _hardwareEventBus;

        /// <summary>已注册的受控子工站实例集合。</summary>
        protected readonly List<StationBase<StationMemoryBaseParam>> _subStations;

        private readonly IAlarmService? _alarmService;

        // 核心状态机
        /// <summary>全局核心状态机实例，控制主控在各种运行状态之间的流转。</summary>
        protected readonly StateMachine<MachineState, MachineTrigger> _globalMachine;

        /// <summary>
        /// 并发安全锁：保证所有的状态机跃迁（Fire）都是串行且独占的，防止多线程引发状态竞态。
        /// </summary>
        private readonly SemaphoreSlim _machineLock = new(1, 1);

        /// <summary>
        /// 意图标志位：用于区分“主控下发的停机指令”与“子工站异常发生的自发停机”。
        /// 在执行 StopAllAsync/PauseAll 等操作内置位，用于屏蔽状态防撕裂守卫的误拦截。
        /// </summary>
        private volatile bool _subStationStopsAreIntentional = false;

        /// <summary>
        /// 报警来源追溯：记录主控进入 Resetting 之前是因为初始化报警（InitAlarm）还是运行报警（RunAlarm）。
        /// 决定复位成功后的去向（Uninitialized 还是 Idle）。
        /// </summary>
        private bool _masterCameFromInitAlarm = false;

        /// <summary>指示主控当前报警来源是否为初始化阶段，供派生类在 OnAfterResetSuccess 中决定是否重置信号量。</summary>
        protected bool MasterCameFromInitAlarm => _masterCameFromInitAlarm;

        /// <summary>
        /// 报警去重集合：记录已上报到 AlarmService 的 (ErrorCode + HardwareName) 组合键。
        /// 防止共享硬件设备经由多个工站重复上报产生重复弹窗。
        /// 进入 Resetting 状态时自动清除。
        /// </summary>
        private readonly HashSet<string> _reportedAlarmKeys = new();

        /// <summary>
        /// 初始化阶段的取消令牌源：当任意工站初始化失败时，通过取消此令牌中断其余工站的初始化。
        /// 仅在主控处于 Initializing 状态期间有效。
        /// </summary>
        private CancellationTokenSource? _initCts;

        /// <summary>
        /// 硬件复位请求委托：由外部框架（如 Prism EA）通过 <see cref="RegisterHardwareResetHandler(Action{HardwareResetRequest})"/> 注入，实现低耦合路由。
        /// </summary>
        private Action<HardwareResetRequest>? _hardwareResetHandler;

        #endregion

        #region Constructor (构造与初始化)

        /// <summary>
        /// 实例化全局主控调度器
        /// </summary>
        /// <param name="logger">日志服务实例。</param>
        /// <param name="hardwareEventBus">硬件输入事件总线。</param>
        /// <param name="subStations">需被调度的子工站集合。</param>
        /// <param name="alarmService">全局报警处理服务（可选）。</param>
        protected BaseMasterController(
            ILogService logger,
            HardwareInputEventBus hardwareEventBus,
            IEnumerable<StationBase<StationMemoryBaseParam>> subStations,
            IAlarmService? alarmService = null)
        {
            _alarmService = alarmService;
            _logger = logger;
            _hardwareEventBus = hardwareEventBus;
            _subStations = new List<StationBase<StationMemoryBaseParam>>(subStations);

            // 监听子工站生命周期事件
            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered += OnSubStationAlarm;
                station.StationAlarmAutoCleared += OnSubStationAlarmAutoCleared;
                station.StationStateChanged += OnSubStationStateChanged;  // 绑定状态防撕裂守卫
            }

            // 监听底层事件总线广播的物理按键事件（如实体启动、暂停、复位按钮）
            if (_hardwareEventBus != null)
            {
                _hardwareEventBus.HardwareInputTriggered += OnHardwareInputReceived;
            }

            // 初始化并配置全局状态机
            _globalMachine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureGlobalMachine();
        }

        #endregion

        #region State Machine Configuration (状态机流转配置)

        /// <summary>
        /// 配置全局状态机的状态拓扑流转图及进出状态的回调动作
        /// </summary>
        private void ConfigureGlobalMachine()
        {
            // 全局状态变迁监听
            _globalMachine.OnTransitioned(t =>
            {
                _logger.Info($"【全局主控】状态切换: {t.Source} -> {t.Destination}");
                MasterStateChanged?.Invoke(this, t.Destination);
            });

            // 1. 未初始化状态
            _globalMachine.Configure(MachineState.Uninitialized)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing);

            // 2. 初始化中状态
            _globalMachine.Configure(MachineState.Initializing)
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.InitAlarm);

            // 3. 待机状态
            _globalMachine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            // 4. 运行中状态
            _globalMachine.Configure(MachineState.Running)
                .OnEntryFromAsync(MachineTrigger.Start, async () =>
                {
                    foreach (var s in _subStations) await s.StartAsync();
                })
                .OnEntryFromAsync(MachineTrigger.Resume, async () =>
                {
                    foreach (var s in _subStations) await s.ResumeAsync();
                })
                .OnExit(t =>
                {
                    // 若目标状态是暂停，挂起所有子工站业务线程
                    if (t.Destination == MachineState.Paused)
                    {
                        _subStationStopsAreIntentional = true;
                        try { foreach (var s in _subStations) s.Pause(); }
                        finally { _subStationStopsAreIntentional = false; }
                    }
                })
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            // 5. 暂停状态
            _globalMachine.Configure(MachineState.Paused)
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Uninitialized)
                .Permit(MachineTrigger.Error, MachineState.RunAlarm);

            // 6. 初始化故障报警状态
            _globalMachine.Configure(MachineState.InitAlarm)
                .OnEntry(() =>
                {
                    _masterCameFromInitAlarm = true;
                    foreach (var s in _subStations) s.TriggerAlarm();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            // 7. 运行故障报警状态
            _globalMachine.Configure(MachineState.RunAlarm)
                .OnEntry(() =>
                {
                    _masterCameFromInitAlarm = false;
                    foreach (var s in _subStations) s.TriggerAlarm();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            // 8. 复位中状态
            _globalMachine.Configure(MachineState.Resetting)
                .OnEntry(() => _reportedAlarmKeys.Clear())
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized)
                .PermitDynamic(MachineTrigger.Error,
                    () => _masterCameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        #endregion

        #region Hardware Smart Routing (物理输入与智能路由)

        /// <summary>
        /// 处理底层物理按键/信号的输入并路由到对应的状态指令。
        /// </summary>
        /// <param name="inputType">物理输入事件的类型（如 Start, Pause, Reset）。</param>
        protected virtual void OnHardwareInputReceived(string inputType)
        {
            switch (inputType)
            {
                case HardwareInputType.Start:
                    _ = ExecuteSmartStartAsync();
                    break;
                case HardwareInputType.Pause:
                    PauseAll();
                    break;
                case HardwareInputType.Reset:
                    _ = ResetAllAsync();
                    break;
            }
        }

        /// <summary>
        /// 智能启动逻辑：根据当前所处状态自动决定是执行初始化、启动还是恢复。
        /// </summary>
        private async Task ExecuteSmartStartAsync()
        {
            try
            {
                if (CurrentState == MachineState.Uninitialized)
                {
                    await InitializeAllAsync();
                    if (CurrentState == MachineState.Idle)
                    {
                        await StartAllAsync();
                    }
                }
                else if (CurrentState == MachineState.Idle)
                {
                    await StartAllAsync();
                }
                else if (CurrentState == MachineState.Paused)
                {
                    await ResumeAllAsync();
                }
                else
                {
                    _logger.Warn($"【全局主控】当前状态 {CurrentState} 忽略启动指令。");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn($"【全局主控】SmartStart 执行失败: {ex.Message}");
            }
        }

        #endregion

        #region Safety Guards & Sub-Station Events (状态守卫与子站事件)

        /// <summary>
        /// 状态防撕裂守卫：监听所有子工站的状态变迁，发现不一致流转时立即触发全线报警。
        /// </summary>
        private void OnSubStationStateChanged(object sender, StationStateChangedEventArgs e)
        {
            var stationName = (sender as StationBase<StationMemoryBaseParam>)?.StationName ?? "未知工站";
            var masterState = CurrentState;

            // 初始化阶段：工站进入报警态 → 立即取消共享令牌，中断其余工站初始化
            if (masterState == MachineState.Initializing
                && (e.NewState == MachineState.InitAlarm || e.NewState == MachineState.RunAlarm))
            {
                _logger.Warn($"【主控】工站 [{stationName}] 初始化失败({e.NewState})，取消其余工站初始化。");
                try { _initCts?.Cancel(); } catch { }
            }

            // 撕裂判定：仅在意非预期地回落到 Uninitialized 的异常场景
            if (!_subStationStopsAreIntentional
                && (masterState == MachineState.Running || masterState == MachineState.Paused)
                && e.NewState == MachineState.Uninitialized
                && e.OldState != MachineState.Resetting)
            {
                _logger.Warn($"【主控守卫】检测到状态撕裂！主控={masterState}，工站 [{stationName}] 意外从 {e.OldState} 跳回 Uninitialized。触发全线报警。");

                // 脱离调用栈上下文，防止与锁形成重入死锁
                Task.Run(() =>
                {
                    try
                    {
                        Fire(MachineTrigger.Error);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"【主控守卫】切入报警状态失败: {ex.Message}");
                    }
                });
            }
        }

        /// <summary>
        /// 处理子工站报警事件：写入 AlarmService 并触发主控状态跳转。
        /// 级联标识码（CascadeAlarm）为主控全局报警时级联触发的内部标记，静默忽略。
        /// StationSyncError 为真实的兜底报警（非预期异常、缺少报警码），必须正常上报。
        /// </summary>
        private void OnSubStationAlarm(object sender, StationAlarmEventArgs e)
        {
            // 静默忽略：仅限主控级联 TriggerAlarm() 产生的内部标识码
            if (e.ErrorCode == AlarmCodes.System.CascadeAlarm)
                return;

            var source = (sender as StationBase<StationMemoryBaseParam>)?.StationName ?? "未知工站";

            // 去重：相同 (ErrorCode + HardwareName) 只上报 AlarmService 一次
            string dedupKey = $"{e.ErrorCode}:{e.HardwareName ?? ""}";
            bool shouldReport = _reportedAlarmKeys.Add(dedupKey);

            if (shouldReport)
            {
                _logger.Fatal($"【报警】{source} | {e.ErrorCode}" +
                    (e.HardwareName != null ? $" | 硬件:{e.HardwareName}" : "") +
                    (e.RuntimeMessage != null ? $" | {e.RuntimeMessage}" : ""));

                _alarmService?.TriggerAlarm(source, e.ErrorCode, e.RuntimeMessage);
                MasterAlarmTriggered?.Invoke(this, e);
            }

            // 即使是重复报警也触发主控状态机级联（确保所有工站进入报警态）
            Task.Run(() =>
            {
                try
                {
                    Fire(MachineTrigger.Error);
                }
                catch (Exception ex)
                {
                    _logger.Fatal($"【主控】尝试切入报警状态时发生致命异常: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 响应子工站硬件级报警自恢复信号。
        /// </summary>
        private void OnSubStationAlarmAutoCleared(object sender, EventArgs e)
        {
            var source = (sender as StationBase<StationMemoryBaseParam>)?.StationName ?? "未知工站";
            _logger.Info($"【主控】工站 [{source}] 硬件自恢复，主动清除报警服务中对应记录。");
            _alarmService?.ClearAlarm(source);
        }

        #endregion

        #region Global Core API (全局核心指令)

        /// <summary>触发全线子工站启动操作。</summary>
        /// <returns>表示异步状态流转的任务。</returns>
        public async Task StartAllAsync() => await FireAsync(MachineTrigger.Start);

        /// <summary>
        /// 全线异步停止：并行等待所有子工站物理停稳后，主控退回未初始化状态。
        /// </summary>
        /// <returns>表示异步停止与状态流转的任务。</returns>
        public async Task StopAllAsync()
        {
            _subStationStopsAreIntentional = true;
            try
            {
                await Parallel.ForEachAsync(_subStations,
                    new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    async (station, _) => await station.StopAsync());
            }
            finally
            {
                _subStationStopsAreIntentional = false;
            }
            await FireAsync(MachineTrigger.Stop);
        }

        /// <summary>全线挂起暂停当前所有的工作流程。</summary>
        public void PauseAll() => Fire(MachineTrigger.Pause);

        /// <summary>触发全线子工站从暂停状态恢复运行。</summary>
        /// <returns>表示异步恢复状态流转的任务。</returns>
        public async Task ResumeAllAsync() => await FireAsync(MachineTrigger.Resume);

        /// <summary>
        /// 切换设备运行模式（仅在待机状态允许切换）
        /// </summary>
        /// <param name="mode">要设置的运行模式枚举值。</param>
        /// <returns>如果状态允许且切换成功返回 true，否则返回 false。</returns>
        public bool SetMode(OperationMode mode)
        {
            if (CurrentState != MachineState.Idle) return false;

            CurrentMode = mode;
            foreach (var s in _subStations)
            {
                s.CurrentMode = mode;
            }
            return true;
        }

        /// <summary>
        /// 执行全线并发限流初始化。
        /// </summary>
        /// <returns>表示异步初始化操作的任务。</returns>
        public async Task InitializeAllAsync()
        {
            if (!CanFire(MachineTrigger.Initialize)) return;

            _logger.Info("【主控】开始全线初始化(限流模式)...");
            Fire(MachineTrigger.Initialize);

            _initCts = new CancellationTokenSource(TimeSpan.FromSeconds(120));
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4, // 限制峰值并发降低硬件瞬时负荷
                    CancellationToken = _initCts.Token
                };

                await Parallel.ForEachAsync(_subStations, parallelOptions, async (station, token) =>
                {
                    await station.ExecuteInitializeAsync(token);
                });
            }
            catch (OperationCanceledException)
            {
                // 某工站失败取消了令牌，或超时，以下方的工站状态检查为准
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】初始化异常: {ex.Message}");
                _alarmService?.TriggerAlarm("主控", AlarmCodes.System.InitializationTimeout,"设备复位时间过长，请调整复位参数！");
                _initCts.Dispose();
                _initCts = null;
                Fire(MachineTrigger.Error);
                return;
            }
            finally
            {
                _initCts.Dispose();
                _initCts = null;
            }

            // 检查是否有工站进入了报警态（Fire(Error); return 不会抛异常，Parallel 视为成功）
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
        /// 执行全线并发限流硬件复位与清警。
        /// </summary>
        /// <returns>表示异步复位操作的任务。</returns>
        public async Task ResetAllAsync()
        {
            if (!CanFire(MachineTrigger.Reset)) return;

            _logger.Info("【主控】开始全线复位(限流并行模式)...");
            Fire(MachineTrigger.Reset);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4,
                    CancellationToken = cts.Token
                };

                await Parallel.ForEachAsync(_subStations, parallelOptions, async (station, token) =>
                {
                    await station.ExecuteResetAsync(token);
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】复位失败: {ex.Message}，重新回到报警状态。");
                // 确保系统不永久卡死在 Resetting
                Fire(MachineTrigger.Error);
                return;
            }

            // 复位成功：执行钩子并清理报警记录
            OnAfterResetSuccess();
            _alarmService?.ClearAllActiveAlarms();

            // 双轨出口策略
            var resetTrigger = _masterCameFromInitAlarm
                ? MachineTrigger.ResetDoneUninitialized
                : MachineTrigger.ResetDone;

            await FireAsync(resetTrigger);
        }

        #endregion

        #region Hardware Reset Mechanisms (硬件级复位路由)

        /// <inheritdoc/>
        public async Task RequestSystemResetAsync()
        {
            _logger.Info("【主控】接收到系统复位请求，开始执行全线复位...");
            await ResetAllAsync();
        }

        /// <summary>
        /// 供派生类重写的生命周期钩子：在系统复位成功、主控状态跳回之前执行专属清理（如清空全局残存数据或气缸信号）。
        /// </summary>
        protected virtual void OnAfterResetSuccess() { }

        /// <inheritdoc/>
        public void RegisterHardwareResetHandler(Action<HardwareResetRequest> handler)
            => _hardwareResetHandler = handler;

        /// <summary>
        /// 精准机构级复位路由：按 Source 匹配异常工站并在后台单独执行清警复位。
        /// 通常响应 UI 端或事件聚合器发起的局部硬件复位请求。
        /// </summary>
        /// <param name="request">包含受影响工站来源和错误代码集合的复位请求对象。</param>
        public virtual void OnHardwareResetRequested(HardwareResetRequest request)
        {
            if (request == null) return;

            var station = _subStations.FirstOrDefault(s => s.StationName == request.Source);
            if (station == null ||
                (station.CurrentState != MachineState.InitAlarm && station.CurrentState != MachineState.RunAlarm)) return;

            _logger.Info($"【主控】接收到硬件复位请求，来源：{request.Source}，错误码：{string.Join(", ", request.ErrorCodes)}");

            _ = Task.Run(async () =>
            {
                try
                {
                    await station.ExecuteResetAsync(CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.Error($"【主控】硬件复位请求执行失败，来源：{request.Source}: {ex.Message}");
                }
            });
        }

        #endregion

        #region Thread-Safe Triggers (线程安全状态跃迁包装)

        /// <summary>验证是否允许执行某项状态跃迁</summary>
        private bool CanFire(MachineTrigger trigger) => _globalMachine.CanFire(trigger);

        /// <summary>
        /// 线程安全的同步状态跃迁
        /// </summary>
        private void Fire(MachineTrigger trigger)
        {
            _machineLock.Wait();
            try
            {
                if (_globalMachine.CanFire(trigger))
                {
                    _globalMachine.Fire(trigger);
                }
            }
            finally
            {
                _machineLock.Release();
            }
        }

        /// <summary>
        /// 线程安全的异步状态跃迁
        /// </summary>
        private async Task FireAsync(MachineTrigger trigger)
        {
            await _machineLock.WaitAsync();
            try
            {
                if (_globalMachine.CanFire(trigger))
                {
                    await _globalMachine.FireAsync(trigger);
                }
            }
            finally
            {
                _machineLock.Release();
            }
        }

        #endregion

        #region IDisposable Implementation (资源释放)

        /// <summary>
        /// 释放主控资源并严格解绑所有事件监听，防止 WPF/Prism 框架下的内存泄漏。
        /// </summary>
        public virtual void Dispose()
        {
            if (_hardwareEventBus != null)
            {
                _hardwareEventBus.HardwareInputTriggered -= OnHardwareInputReceived;
            }

            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered -= OnSubStationAlarm;
                station.StationAlarmAutoCleared -= OnSubStationAlarmAutoCleared;
                station.StationStateChanged -= OnSubStationStateChanged;
            }

            _machineLock?.Dispose();
        }

        #endregion
    }
}