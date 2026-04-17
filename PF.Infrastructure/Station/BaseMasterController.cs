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
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Station
{
    /// <summary>
    /// 全局主控调度器基类 (Infrastructure)
    /// 封装了所有非标自动化设备通用的状态流转、并发安全保护、以及面板事件响应。
    /// </summary>
    public abstract class BaseMasterController : IMasterController, IDisposable
    {
        public MachineState CurrentState => _globalMachine.State;
        public OperationMode CurrentMode { get; private set; } = OperationMode.Normal;

        public event EventHandler<MachineState> MasterStateChanged;
        public event EventHandler<string> MasterAlarmTriggered;

        protected readonly ILogService _logger;
        protected readonly HardwareInputEventBus _hardwareEventBus;
        protected readonly List<StationBase<StationMemoryBaseParam>> _subStations;
        protected readonly StateMachine<MachineState, MachineTrigger> _globalMachine;

        // 并发安全：所有状态机跳转均通过此信号量独占执行
        private readonly SemaphoreSlim _machineLock = new(1, 1);

        // 主控主动让子工站停机的意图标志：在 StopAllAsync / PauseAll 等
        // 主控自己下发 station.StopAsync()/station.Pause() 的窗口期内置位，
        // 用于区分"主控命令导致的子工站回 Uninitialized"与"子工站自发撕裂"，
        // 防止 OnSubStationStateChanged 守卫误触发全线报警。
        private volatile bool _subStationStopsAreIntentional = false;

        // 记录主控进入 Resetting 前的报警来源，决定复位成功后回到 Uninitialized 还是 Idle
        private bool _masterCameFromInitAlarm = false;

        // 报警服务：可选注入，不影响已有子类的 DI 注册；注入后自动接入结构化报警流水线
        private readonly IAlarmService? _alarmService;

        // 硬件复位请求委托：由 Shell 通过 RegisterHardwareResetHandler 注入，使 PF.Infrastructure 无需依赖 Prism
        private Action<HardwareResetRequest>? _hardwareResetHandler;

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

            // 监听所有子工站的软件报警事件、硬件自恢复事件与状态变迁事件
            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered   += OnSubStationAlarm;
                station.StationAlarmAutoCleared += OnSubStationAlarmAutoCleared;
                station.StationStateChanged     += OnSubStationStateChanged;  // 防撕裂守卫
            }

            // 🌟 监听底层事件总线广播的物理按键事件
            if (_hardwareEventBus != null)
            {
                _hardwareEventBus.HardwareInputTriggered += OnHardwareInputReceived;
            }

            _globalMachine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureGlobalMachine();
        }

        private void ConfigureGlobalMachine()
        {
            _globalMachine.OnTransitioned(t =>
            {
                _logger.Info($"【全局主控】状态切换: {t.Source} -> {t.Destination}");
                MasterStateChanged?.Invoke(this, t.Destination);
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
                    if (t.Destination == MachineState.Paused)
                    {
                        // Pause：挂起子工站业务线程，主控进入 Paused 等待 Resume
                        _subStationStopsAreIntentional = true;
                        try { foreach (var s in _subStations) s.Pause(); }
                        finally { _subStationStopsAreIntentional = false; }
                    }
                    // Stop → Uninitialized：物理停止已由 StopAllAsync 在 FireAsync(Stop) 前完成
                    // Error → RunAlarm：RunAlarm.OnEntry 负责调用 TriggerAlarm 通知子工站
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
                    foreach (var s in _subStations) s.TriggerAlarm();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _globalMachine.Configure(MachineState.RunAlarm)
                .OnEntry(() =>
                {
                    _masterCameFromInitAlarm = false;
                    foreach (var s in _subStations) s.TriggerAlarm();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _globalMachine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.ResetDoneUninitialized, MachineState.Uninitialized)
                .PermitDynamic(MachineTrigger.Error,
                    () => _masterCameFromInitAlarm ? MachineState.InitAlarm : MachineState.RunAlarm);
        }

        // ── 物理按键智能路由 ──────────────────────────────────────────────

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

        // ── 全局核心指令 ──────────────────────────────────────────────────

        /// <summary>
        /// 防状态撕裂守卫：监听所有子工站的状态变迁，在发现不一致流转时立即触发全线报警。
        ///
        /// 守卫规则：
        ///   · 主控处于 Running/Paused，而子工站意外跳回 Uninitialized（且不是主控命令路径）→ 状态撕裂
        ///
        /// 防死锁设计：
        ///   OnTransitioned 回调在持有 Stateless 状态机内部锁的上下文中执行，
        ///   若直接在此调用 Fire(Error) 会尝试再次进入状态机，
        ///   导致与 _machineLock 或 Stateless 内部锁形成重入死锁。
        ///   因此必须通过 Task.Run 将报警动作投入后台线程执行，
        ///   彻底脱离当前调用栈的锁上下文。
        /// </summary>
        private void OnSubStationStateChanged(object sender, StationStateChangedEventArgs e)
        {
            var stationName = (sender as StationBase<StationMemoryBaseParam>)?.StationName ?? "未知工站";
            var masterState = CurrentState;

            _logger.Debug($"【主控】工站 [{stationName}] 状态: {e.OldState} → {e.NewState}" +
                           $"，主控当前={masterState}");

            // 守卫：主控运行/暂停中，子工站意外回到 Uninitialized（排除主控命令与复位完成路径）
            // 正常路径：Resetting → Uninitialized 是 ExecuteResetAsync 中 FireAsync(ResetDoneUninitialized) 触发的，合法
            // 豁免路径：主控主动 StopAllAsync/Pause 下发时（_subStationStopsAreIntentional=true），不是撕裂
            if (!_subStationStopsAreIntentional
                && (masterState == MachineState.Running || masterState == MachineState.Paused)
                && e.NewState == MachineState.Uninitialized
                && e.OldState != MachineState.Resetting)
            {
                _logger.Fatal($"【主控守卫】检测到状态撕裂！主控={masterState}，" +
                               $"工站 [{stationName}] 意外从 {e.OldState} 跳回 Uninitialized。" +
                               "触发全线报警。");

                // 防死锁：通过 Task.Run 脱离当前 OnTransitioned 回调的调用栈，
                // 避免与 _machineLock / Stateless 状态机内部锁形成重入死锁
                Task.Run(() =>
                {
                    try
                    {
                        _alarmService?.TriggerAlarm(stationName, AlarmCodes.System.StationSyncError);
                        Fire(MachineTrigger.Error);
                    }
                    catch (Exception ex)
                    {
                        _logger.Fatal($"【主控守卫】切入报警状态失败: {ex.Message}");
                    }
                });
            }
        }

        private void OnSubStationAlarmAutoCleared(object sender, EventArgs e)
        {
            var source = (sender as StationBase<StationMemoryBaseParam>)?.StationName ?? "未知工站";
            _logger.Info($"【主控】工站 [{source}] 硬件自恢复，主动清除报警服务中对应记录。");
            _alarmService?.ClearAlarm(source);
        }

        private void OnSubStationAlarm(object sender, string errorCode)
        {
            // sender 即触发报警的子工站实例，从中提取结构化来源标识
            var source = (sender as StationBase<StationMemoryBaseParam>)?.StationName ?? "未知工站";

            _logger.Fatal($"【主控接收到子站报警】{source}: {errorCode}");

            // ── UI 拦截墙：过滤被动联锁停机产生的兜底报警码 ────────────────
            // StationSyncError 是子站"被动拉停"时（_pendingAlarmCode 为空）由 Alarm.OnEntry 兜底填入的占位码。
            // 此类事件的作用是维护主控与子站的状态同步（下方 Fire(Error) 保证），
            // 本身不代表任何真实故障，禁止写入全局 AlarmService，否则一次急停会淹没 N 条无意义记录，
            // 掩盖真正的根本故障（Root Cause）。
            if (errorCode != AlarmCodes.System.StationSyncError)
            {
                // 此处是真实子站故障向上汇聚的唯一入口，写入报警服务，保证：
                // · AlarmCenterView 实时显示活跃报警
                // · Fatal 级别触发主窗口阻断对话框（见 MainWindowViewModel）
                // · 报警记录异步持久化到年份分表
                _alarmService?.TriggerAlarm(source, errorCode);

                // 保留旧事件，确保已订阅 MasterAlarmTriggered 的外部代码不受影响
                MasterAlarmTriggered?.Invoke(this, errorCode);
            }

            // 🚨 核心修复：切断同步调用链，防止底层 SemaphoreSlim 发生重入死锁
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

        public async Task StartAllAsync() => await FireAsync(MachineTrigger.Start);

        /// <summary>
        /// 异步停止：并行等待所有子工站物理停稳后，推进主控状态机到 Uninitialized。
        /// </summary>
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

        public void PauseAll() => Fire(MachineTrigger.Pause);

        public async Task ResumeAllAsync() => await FireAsync(MachineTrigger.Resume);

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

        public async Task InitializeAllAsync()
        {
            if (!CanFire(MachineTrigger.Initialize)) return;

            _logger.Info("【主控】开始全线初始化(限流模式)...");
            Fire(MachineTrigger.Initialize);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                // 1. 配置并行选项
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = 4, // 限制最多同时初始化 4 个工位（请根据实际硬件承受能力调整）
                    CancellationToken = cts.Token
                };

                // 2. 执行限流的并发初始化
                await Parallel.ForEachAsync(_subStations, parallelOptions, async (station, token) =>
                {
                    await station.ExecuteInitializeAsync(token);
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】初始化异常: {ex.Message}");
                _alarmService?.TriggerAlarm("主控", AlarmCodes.System.InitializationTimeout);
                Fire(MachineTrigger.Error);
                return;
            }

            Fire(MachineTrigger.InitializeDone);
        }

        public async Task ResetAllAsync()
        {
            if (!CanFire(MachineTrigger.Reset)) return;

            _logger.Info("【主控】开始全线复位(限流并行模式)...");
            Fire(MachineTrigger.Reset);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                // 与 InitializeAllAsync 对齐：限流并行执行各工站复位，缩短复位总耗时；
                // 任何一个工站抛异常 → Parallel.ForEachAsync 向上抛 → 统一回退到 Alarm
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
                Fire(MachineTrigger.Error);  // Resetting → InitAlarm/RunAlarm，确保系统不永久卡死在 Resetting
                return;
            }

            // 复位成功：先执行子类专属清理，再清除报警服务中的所有活跃记录
            OnAfterResetSuccess();
            _alarmService?.ClearAllActiveAlarms();

            // 双轨出口：来自 InitAlarm → 回 Uninitialized（强制重新初始化）；来自 RunAlarm → 回 Idle
            var resetTrigger = _masterCameFromInitAlarm
                ? MachineTrigger.ResetDoneUninitialized
                : MachineTrigger.ResetDone;
            await FireAsync(resetTrigger);
        }

        /// <inheritdoc/>
        public async Task RequestSystemResetAsync()
        {
            _logger.Info("【主控】接收到系统复位请求，开始执行全线复位...");

            // 复位成功后 ResetAllAsync 内部会统一调用 ClearAllActiveAlarms，无需在此提前清理；
            // 否则若硬件复位失败，UI 看到 AlarmCenter 清空但实际仍在 Alarm 态，造成误导。
            await ResetAllAsync();
        }

        /// <summary>
        /// 供子类重写：复位成功回到 Idle 之前执行的专属逻辑（如清理信号量）
        /// </summary>
        protected virtual void OnAfterResetSuccess() { }

        /// <inheritdoc/>
        public void RegisterHardwareResetHandler(Action<HardwareResetRequest> handler)
            => _hardwareResetHandler = handler;

        /// <summary>
        /// 响应硬件复位请求：按 Source 匹配子工站并在后台触发硬件清警复位。
        /// Shell 通过 <c>RegisterHardwareResetHandler</c> 将 Prism EA 事件路由到此方法，
        /// 使 PF.Infrastructure 无需直接依赖 Prism。
        /// 子类可 override 以实现更精细的机构级路由。
        /// </summary>
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

        // ── 线程安全的触发器封装 ───────────────────────────────────────────

        private bool CanFire(MachineTrigger trigger)
        {
            return _globalMachine.CanFire(trigger);
        }

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

        public virtual void Dispose()
        {
            // 必须移除事件订阅，防止内存泄漏
            if (_hardwareEventBus != null)
            {
                _hardwareEventBus.HardwareInputTriggered -= OnHardwareInputReceived;
            }

            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered   -= OnSubStationAlarm;
                station.StationAlarmAutoCleared -= OnSubStationAlarmAutoCleared;
                station.StationStateChanged     -= OnSubStationStateChanged;
            }

            _machineLock?.Dispose();
        }
    }
}
