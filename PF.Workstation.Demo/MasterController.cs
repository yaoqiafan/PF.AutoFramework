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
    /// 职责：
    ///   · 管理所有子工站的生命周期（启动/暂停/恢复/停止/报警）
    ///   · 在构造时向 IStationSyncService 注册本方案所需的所有流水线信号量
    ///   · 在系统复位时重置信号量，确保下一轮启动状态正确
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
            // 规则：初始计数决定了哪个工站"先行"
            //   SlotEmpty   = 1：槽位初始为空 → 取放工站可立即开始第一轮
            //   ProductReady= 0：初始无产品 → 点胶工站初始阻塞，等取放工站先放料
            _sync.Register(WorkstationSignals.SlotEmpty,    initialCount: 1, maxCount: 1);
            _sync.Register(WorkstationSignals.ProductReady, initialCount: 0, maxCount: 1);

            // 监听所有子工站的报警事件
            foreach (var station in _subStations)
                station.StationAlarmTriggered += OnSubStationAlarm;

            _globalMachine = new StateMachine<MachineState, MachineTrigger>(MachineState.Idle);
            ConfigureGlobalMachine();
        }

        private void ConfigureGlobalMachine()
        {
            _globalMachine.OnTransitioned(t =>
            {
                _logger.Info($"【全局主控】状态切换: {t.Source} -> {t.Destination}");
                MasterStateChanged?.Invoke(this, t.Destination);
            });

            // 状态转移配置
            _globalMachine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // 【主控启动】：启动所有的子线程
            _globalMachine.Configure(MachineState.Running)
                .OnEntry(() => _subStations.ForEach(s => s.Start()))
                .OnExit(() => _subStations.ForEach(s => s.Stop())) // 退出运行时，急停所有子工站
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // 【主控暂停】：暂停所有的子线程
            _globalMachine.Configure(MachineState.Paused)
                .OnEntry(() => _subStations.ForEach(s => s.Pause()))
                .OnExit(() => _subStations.ForEach(s => s.Resume())) // 退出暂停时恢复它们
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            _globalMachine.Configure(MachineState.Alarm)
                .OnEntry(() => _subStations.ForEach(s => s.TriggerAlarm())) // 强制所有工站切入报警
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            // 【主控复位中】：等待 ResetAllAsync 完成各工站物理复位后触发 ResetDone
            _globalMachine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle);
        }

        /// <summary>
        /// 核心联动逻辑：只要有任何一个子线程报警，主控立刻触发全局急停！
        /// </summary>
        private void OnSubStationAlarm(object sender, string errorMessage)
        {
            _logger.Fatal($"【主控接收到报警】: {errorMessage}，立即触发全线急停！");

            // 修复：原代码将 MasterAlarmTriggered 嵌套在 CanFire 判断内部——
            // 若主控已处于 Alarm 状态（如两个工站几乎同时报警），CanFire(Error)=false，
            // 导致 MasterAlarmTriggered 事件永远不触发，UI 无法弹出第二条报警信息。
            // 修复：先无条件通知 UI，再尝试驱动状态机。
            MasterAlarmTriggered?.Invoke(this, errorMessage); // 始终通知 UI 弹窗

            // 触发全局报警，由于配置了 OnExit(Stop)，这会瞬间中断所有其他正常运行的子线程！
            if (_globalMachine.CanFire(MachineTrigger.Error))
            {
                _globalMachine.Fire(MachineTrigger.Error);
            }
        }

        // --- 供 UI 绑定的主控一键操作指令 ---
        public void StartAll() => Fire(MachineTrigger.Start);
        public void StopAll() => Fire(MachineTrigger.Stop);
        public void PauseAll() => Fire(MachineTrigger.Pause);
        public void ResumeAll() => Fire(MachineTrigger.Resume);

        /// <summary>
        /// 设置运行模式。只允许在 Idle 状态下调用，同时向所有子工站下发新模式。
        /// </summary>
        /// <returns>设置成功返回 true；非 Idle 状态时返回 false 并记录警告。</returns>
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
        /// 物理复位流程（异步）：
        ///   Alarm → Resetting → 顺序调用各工站 ExecuteResetAsync → 复位信号量 → Idle
        /// 建议 UI 以 fire-and-forget（_ = controller.ResetAllAsync()）调用，不阻塞界面线程。
        /// </summary>
        public async Task ResetAllAsync()
        {
            if (!_globalMachine.CanFire(MachineTrigger.Reset))
            {
                _logger.Warn($"【主控】当前状态 {CurrentState} 无法执行复位");
                return;
            }

            _logger.Info("【主控】开始全线物理复位...");
            _globalMachine.Fire(MachineTrigger.Reset); // Alarm → Resetting

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                foreach (var station in _subStations)
                    await station.ExecuteResetAsync(cts.Token);
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】复位过程中发生异常: {ex.Message}");
            }

            _sync.ResetAll();                           // 复位信号量至初始状态，准备下一轮启动
            Fire(MachineTrigger.ResetDone);             // Resetting → Idle
            _logger.Success("【主控】全线复位完成，已回到 Idle 状态。");
        }

        private void Fire(MachineTrigger trigger)
        {
            if (_globalMachine.CanFire(trigger)) _globalMachine.Fire(trigger);
        }
    }
}
