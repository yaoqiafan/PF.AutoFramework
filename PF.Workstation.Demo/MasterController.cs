using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.Demo.Sync;
using Stateless;

namespace PF.Workstation.Demo
{
    /// <summary>
    /// 全局主控状态机（主线程管理器）
    ///
    /// 生命周期：
    ///   Uninitialized → InitializeAllAsync()  → Initializing → Idle
    ///   Idle          → StartAllAsync()        → Running
    ///   Running       → StopAll()              → Idle
    ///   Alarm         → ResetAllAsync()        → Resetting → Idle
    ///
    /// 职责：
    ///   · 管理所有子工站的生命周期（初始化/启动/暂停/恢复/停止/复位）
    ///   · 在构造时向 IStationSyncService 注册本方案所需的所有流水线信号量
    ///   · 在系统复位时重置信号量，确保下一轮启动状态正确
    ///
    /// 并发安全设计：
    ///   · 所有状态机跳转通过 _machineLock（SemaphoreSlim 1,1）独占执行。
    ///   · Running 状态使用 OnEntryAsync，启动所有子工站前等待其旧任务结束。
    ///   · ResetAllAsync 采用熔断机制：任一子工站复位失败则立即中断，
    ///     绝不在有异常的情况下触发 ResetDone，杜绝"假复位"。
    /// </summary>
    public class MasterController
    {
        public MachineState CurrentState => _globalMachine.State;

        /// <summary>
        /// 当前运行模式。只允许在 Idle 状态下通过 <see cref="SetMode"/> 修改。
        /// </summary>
        public OperationMode CurrentMode { get; private set; } = OperationMode.Normal;

        // 供 UI 绑定的主控状态改变事件
        public event EventHandler<MachineState> MasterStateChanged;
        public event EventHandler<string> MasterAlarmTriggered;

        private readonly ILogService _logger;
        private readonly IStationSyncService _sync;
        private readonly StateMachine<MachineState, MachineTrigger> _globalMachine;

        // ── 并发安全：所有状态机跳转均通过此信号量独占执行 ─────────────────
        private readonly SemaphoreSlim _machineLock = new(1, 1);

        // 管理的子工站列表
        private readonly List<StationBase> _subStations;

        public MasterController(
            ILogService logger,
            IStationSyncService sync,
            IEnumerable<StationBase> subStations)
        {
            _logger = logger;
            _sync   = sync;
            _subStations = new List<StationBase>(subStations);

            // ── 注册本工站方案所需的流水线信号量 ──────────────────────────
            _sync.Register(WorkstationSignals.SlotEmpty,    initialCount: 1, maxCount: 1);
            _sync.Register(WorkstationSignals.ProductReady, initialCount: 0, maxCount: 1);

            // 监听所有子工站的报警事件
            foreach (var station in _subStations)
                station.StationAlarmTriggered += OnSubStationAlarm;

            // 初始状态：Uninitialized
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

            // --- 未初始化状态 ---
            _globalMachine.Configure(MachineState.Uninitialized)
                .Permit(MachineTrigger.Initialize, MachineState.Initializing);

            // --- 初始化中状态 ---
            _globalMachine.Configure(MachineState.Initializing)
                .Permit(MachineTrigger.InitializeDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 待机状态 ---
            _globalMachine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // 【主控启动/恢复】：异步启动所有子工站
            // OnEntryAsync 会依序 await 每个 station.StartAsync()，
            // 确保新任务在旧任务彻底终止后才启动（由 StationBase.OnStartRunningAsync 保证）。
            _globalMachine.Configure(MachineState.Running)
                .OnEntryAsync(async () =>
                {
                    foreach (var s in _subStations)
                        await s.StartAsync();
                })
                .OnExit(() => _subStations.ForEach(s => s.Stop()))
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // 【主控暂停】：暂停所有子工站
            // OnExit 不再调用 s.Resume()：
            //   · 各子工站在进入 Paused 前已由 Running.OnExit 调用 Stop() 回到 Idle；
            //   · 恢复时由 Running.OnEntryAsync 重新调用 StartAsync()，从检查点继续；
            //   · 这避免了原设计中 Stop-from-Paused 路径意外启动子工站的隐患。
            _globalMachine.Configure(MachineState.Paused)
                .OnEntry(() => _subStations.ForEach(s => s.Pause()))
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            _globalMachine.Configure(MachineState.Alarm)
                .OnEntry(() => _subStations.ForEach(s => s.TriggerAlarm()))
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            // 【主控复位中】：等待 ResetAllAsync 完成各工站物理复位后触发 ResetDone
            _globalMachine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle);
        }

        /// <summary>
        /// 核心联动逻辑：只要有任何一个子线程报警，主控立刻触发全线急停！
        /// 此方法可能从子工站后台线程调用，通过 Fire() 的 _machineLock 保证线程安全。
        /// </summary>
        private void OnSubStationAlarm(object sender, string errorMessage)
        {
            _logger.Fatal($"【主控接收到报警】: {errorMessage}，立即触发全线急停！");
            MasterAlarmTriggered?.Invoke(this, errorMessage);
            Fire(MachineTrigger.Error);
        }

        // --- 供 UI 绑定的主控一键操作指令 ---

        /// <summary>异步启动全线（触发 Running 状态的 OnEntryAsync）</summary>
        public async Task StartAllAsync()  => await FireAsync(MachineTrigger.Start);
        public void StopAll()              => Fire(MachineTrigger.Stop);
        public void PauseAll()             => Fire(MachineTrigger.Pause);
        /// <summary>异步恢复全线（触发 Running 状态的 OnEntryAsync）</summary>
        public async Task ResumeAllAsync() => await FireAsync(MachineTrigger.Resume);

        /// <summary>
        /// 设置运行模式。只允许在 Idle 状态下调用，同时向所有子工站下发新模式。
        /// </summary>
        public bool SetMode(OperationMode mode)
        {
            if (CurrentState != MachineState.Idle)
            {
                _logger.Warn($"【主控】只允许在 Idle 状态下切换运行模式，当前状态: {CurrentState}");
                return false;
            }
            CurrentMode = mode;
            _subStations.ForEach(s => s.CurrentMode = mode);
            _logger.Info($"【主控】运行模式已切换为: {mode}");
            return true;
        }

        /// <summary>
        /// 全线初始化流程（异步，一次性）：
        ///   Uninitialized → Initializing → 顺序调用各工站 ExecuteInitializeAsync → Idle
        ///
        /// 各工站的 ExecuteInitializeAsync 负责驱动自身状态机完成
        /// Uninitialized → Initializing → Idle 的跳转。
        /// 任意工站初始化失败（抛出异常），主控将进入 Alarm 状态。
        /// 建议 UI 以 fire-and-forget（_ = controller.InitializeAllAsync()）调用。
        /// </summary>
        public async Task InitializeAllAsync()
        {
            if (!_globalMachine.CanFire(MachineTrigger.Initialize))
            {
                _logger.Warn($"【主控】当前状态 {CurrentState} 无法执行初始化（仅限 Uninitialized）");
                return;
            }

            _logger.Info("【主控】开始全线初始化...");
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                foreach (var station in _subStations)
                    await station.ExecuteInitializeAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】初始化过程发生异常: {ex.Message}");
                Fire(MachineTrigger.Error); // Initializing → Alarm
                return;
            }

            Fire(MachineTrigger.InitializeDone); // Initializing → Idle
            _logger.Success("【主控】全线初始化完成，已进入 Idle 待机状态。");
        }

        /// <summary>
        /// 物理复位流程（异步，熔断式）：
        ///   Alarm → Resetting → 顺序调用各工站 ExecuteResetAsync → 复位信号量 → Idle
        ///
        /// 熔断策略：
        ///   · 任一子工站的 ExecuteResetAsync 抛出异常，立即中断后续复位流程。
        ///   · 发生异常时绝不触发 ResetDone，系统保持 Resetting 状态（需人工干预）。
        ///   · 只有全部子工站复位成功后，才调用 _sync.ResetAll() 并流转到 Idle。
        /// </summary>
        public async Task ResetAllAsync()
        {
            if (!_globalMachine.CanFire(MachineTrigger.Reset))
            {
                _logger.Warn($"【主控】当前状态 {CurrentState} 无法执行复位");
                return;
            }

            _logger.Info("【主控】开始全线物理复位...");
            Fire(MachineTrigger.Reset); // Alarm → Resetting

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                foreach (var station in _subStations)
                    await station.ExecuteResetAsync(cts.Token);
            }
            catch (Exception ex)
            {
                // 【熔断】复位过程有异常 → 中断流程，系统保持 Resetting/Alarm 状态
                // 绝对禁止在此处调用 Fire(ResetDone)，防止"假复位"导致带故障状态启动
                _logger.Error($"【主控】复位过程中发生异常，中止复位流程！原因: {ex.Message}");
                _logger.Error("【主控】系统保持当前状态，请排查故障后重新执行复位！");
                return;
            }

            // 只有全部工站无异常时，才安全地重置信号量并回到 Idle
            _sync.ResetAll();
            await FireAsync(MachineTrigger.ResetDone); // Resetting → Idle
            _logger.Success("【主控】全线复位完成，已回到 Idle 状态。");
        }

        /// <summary>
        /// 线程安全的同步状态跳转（适用于无 OnEntryAsync 的路径）。
        /// 后台报警线程与 UI 线程可同时调用，_machineLock 保证互斥。
        /// </summary>
        private void Fire(MachineTrigger trigger)
        {
            _machineLock.Wait();
            try
            {
                if (_globalMachine.CanFire(trigger)) _globalMachine.Fire(trigger);
            }
            finally
            {
                _machineLock.Release();
            }
        }

        /// <summary>
        /// 线程安全的异步状态跳转（适用于含 OnEntryAsync 的路径：Start、Resume）。
        /// 使用 WaitAsync 避免在 async 上下文中阻塞调用线程。
        /// </summary>
        private async Task FireAsync(MachineTrigger trigger)
        {
            await _machineLock.WaitAsync();
            try
            {
                if (_globalMachine.CanFire(trigger)) await _globalMachine.FireAsync(trigger);
            }
            finally
            {
                _machineLock.Release();
            }
        }
    }
}
