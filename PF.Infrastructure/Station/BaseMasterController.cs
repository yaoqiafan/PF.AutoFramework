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

            // 监听所有子工站的软件报警事件
            foreach (var station in _subStations)
            {
                station.StationAlarmTriggered += OnSubStationAlarm;
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
                .Permit(MachineTrigger.ResetDone, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);  // 复位失败可退回 Alarm，允许再次复位
        }

        // ── 物理按键智能路由 ──────────────────────────────────────────────

        protected virtual void OnHardwareInputReceived(string inputType)
        {
            switch (inputType)
            {
                case HardwareInputType.EStop:
                    EmergencyStop();
                    break;
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
            catch (Exception)
            {
                EmergencyStop();
            }
        }

        // ── 全局核心指令 ──────────────────────────────────────────────────

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

            // 急停由主控统一上报；AlarmService 复合键幂等，子站后续触发的相同 source+code 会被跳过
            _alarmService?.TriggerAlarm("主控", AlarmCodes.System.StationSyncError);

            // 1. 软件切入 Alarm 状态（打断逻辑协同）
            Fire(MachineTrigger.Error);

            // 2. 强制底层硬件下电制动（已处于 Alarm 的工站通过 TriggerAlarm 已取消业务线程，无需重复调用 Stop）
            foreach (var station in _subStations)
            {
                if (station.CurrentState != MachineState.Alarm)
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
                _alarmService?.TriggerAlarm("主控", AlarmCodes.System.InitializationTimeout);
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
                _logger.Error($"【主控】复位失败: {ex.Message}，重新回到报警状态。");
                Fire(MachineTrigger.Error);  // Resetting → Alarm，确保系统不永久卡死在 Resetting
                return;
            }

            // 复位成功：先执行子类专属清理，再清除报警服务中的所有活跃记录
            OnAfterResetSuccess();
            _alarmService?.ClearAllActiveAlarms();

            await FireAsync(MachineTrigger.ResetDone);
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
            if (station == null || station.CurrentState != MachineState.Alarm) return;

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
                station.StationAlarmTriggered -= OnSubStationAlarm;
            }

            _machineLock?.Dispose();
        }
    }
}
