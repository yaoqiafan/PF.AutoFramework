using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Station;
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
        protected readonly PhysicalButtonEventBus _hardwareEventBus;
        protected readonly List<StationBase> _subStations;
        protected readonly StateMachine<MachineState, MachineTrigger> _globalMachine;

        // 并发安全：所有状态机跳转均通过此信号量独占执行
        private readonly SemaphoreSlim _machineLock = new(1, 1);

        protected BaseMasterController(
            ILogService logger,
            PhysicalButtonEventBus hardwareEventBus,
            IEnumerable<StationBase> subStations)
        {
            _logger = logger;
            _hardwareEventBus = hardwareEventBus;
            _subStations = new List<StationBase>(subStations);

            // 监听所有子工站的软件报警事件
            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered += OnSubStationAlarm;
            }

            // 🌟 监听底层事件总线广播的物理按键事件
            if (_hardwareEventBus != null)
            {
                _hardwareEventBus.PhysicalButtonPressed += OnPhysicalButtonPressed;
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
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            _globalMachine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            _globalMachine.Configure(MachineState.Running)
                .OnEntryAsync(async () =>
                {
                    foreach (var s in _subStations) await s.StartAsync();
                })
                .OnExit(() =>
                {
                    foreach (var s in _subStations) s.Stop();
                })
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            _globalMachine.Configure(MachineState.Paused)
                .OnEntry(() =>
                {
                    foreach (var s in _subStations) s.Pause();
                })
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            _globalMachine.Configure(MachineState.Alarm)
                .OnEntry(() =>
                {
                    foreach (var s in _subStations) s.TriggerAlarm();
                })
                .Permit(MachineTrigger.Reset, MachineState.Resetting);

            _globalMachine.Configure(MachineState.Resetting)
                .Permit(MachineTrigger.ResetDone, MachineState.Idle);
        }

        // ── 物理按键智能路由 ──────────────────────────────────────────────

        private void OnPhysicalButtonPressed(PhysicalButtonType buttonType)
        {
            switch (buttonType)
            {
                case PhysicalButtonType.EStop:
                    EmergencyStop();
                    break;
                case PhysicalButtonType.Start:
                    _ = ExecuteSmartStartAsync();
                    break;
                case PhysicalButtonType.Pause:
                    PauseAll();
                    break;
                case PhysicalButtonType.Reset:
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
            catch (Exception)
            {
                EmergencyStop();
            }
        }

        // ── 全局核心指令 ──────────────────────────────────────────────────

        private void OnSubStationAlarm(object sender, string errorMessage)
        {
            _logger.Fatal($"【主控接收到子站报警】: {errorMessage}");
            MasterAlarmTriggered?.Invoke(this, errorMessage);

            // 🚨 核心修复：切断同步调用链，防止底层 SemaphoreSlim 发生重入死锁
            Task.Run(() => Fire(MachineTrigger.Error));
        }

        public async Task StartAllAsync() => await FireAsync(MachineTrigger.Start);

        public void StopAll() => Fire(MachineTrigger.Stop);

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

        public void EmergencyStop()
        {
            _logger.Fatal("【全局主控】触发全局急停指令！");

            // 1. 软件切入 Alarm 状态（打断逻辑协同）
            Fire(MachineTrigger.Error);

            // 2. 强制底层硬件下电制动
            foreach (var station in _subStations)
            {
                 station.Stop();
            }
        }

        public async Task InitializeAllAsync()
        {
            if (!CanFire(MachineTrigger.Initialize)) return;

            _logger.Info("【主控】开始全线初始化...");
            Fire(MachineTrigger.Initialize);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                foreach (var station in _subStations)
                {
                    await station.ExecuteInitializeAsync(cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】初始化异常: {ex.Message}");
                Fire(MachineTrigger.Error);
                return;
            }

            Fire(MachineTrigger.InitializeDone);
        }

        public async Task ResetAllAsync()
        {
            if (!CanFire(MachineTrigger.Reset)) return;

            _logger.Info("【主控】开始全线复位...");
            Fire(MachineTrigger.Reset);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try
            {
                foreach (var station in _subStations)
                {
                    await station.ExecuteResetAsync(cts.Token);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"【主控】复位失败: {ex.Message}，保持报警状态。");
                return;
            }

            OnAfterResetSuccess();

            await FireAsync(MachineTrigger.ResetDone);
        }

        /// <summary>
        /// 供子类重写：复位成功回到 Idle 之前执行的专属逻辑（如清理信号量）
        /// </summary>
        protected virtual void OnAfterResetSuccess() { }

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
                _hardwareEventBus.PhysicalButtonPressed -= OnPhysicalButtonPressed;
            }

            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered -= OnSubStationAlarm;
            }

            _machineLock?.Dispose();
        }
    }
}
