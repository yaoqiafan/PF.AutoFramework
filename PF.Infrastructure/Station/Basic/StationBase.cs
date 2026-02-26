using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Station.Basic
{
    /// <summary>
    /// 工站（子线程）状态机基础类
    ///
    /// 生命周期：
    ///   Uninitialized → (Initialize) → Initializing → (InitializeDone) → Idle
    ///                                                 → (Error)         → Alarm
    ///   Idle          → (Start)      → Running
    ///   Running       → (Stop)       → Idle
    ///   Alarm         → (Reset)      → Idle  （经由 ExecuteResetAsync）
    /// </summary>
    public abstract class StationBase : IDisposable
    {
        public string StationName { get; }
        public MachineState CurrentState => _machine.State;

        // 向上层（主控）抛出的报警事件
        public event EventHandler<string> StationAlarmTriggered;

        protected readonly ILogService _logger;
        protected readonly StateMachine<MachineState, MachineTrigger> _machine;

        // 线程控制三剑客
        private CancellationTokenSource _runCts;
        protected ManualResetEventSlim _pauseEvent;
        private Task _workflowTask;

        protected StationBase(string name, ILogService logger)
        {
            StationName = name;
            _logger = logger;
            _pauseEvent = new ManualResetEventSlim(true);

            // 初始状态：Uninitialized（硬件未就绪，禁止直接启动）
            _machine = new StateMachine<MachineState, MachineTrigger>(MachineState.Uninitialized);
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            _machine.OnTransitioned(t => _logger?.Debug($"[{StationName}] 状态变迁: {t.Source} -> {t.Destination}"));

            // --- 未初始化状态 ---
            _machine.Configure(MachineState.Uninitialized)
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
            _machine.Configure(MachineState.Running)
                .OnEntry(OnStartRunning)
                .OnExit(OnStopRunning)
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 暂停状态 ---
            _machine.Configure(MachineState.Paused)
                .OnEntry(() => _pauseEvent.Reset())
                .OnExit(() => _pauseEvent.Set())
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 报警状态 ---
            _machine.Configure(MachineState.Alarm)
                .OnEntry(() =>
                {
                    _pauseEvent.Set();
                    StationAlarmTriggered?.Invoke(this, $"[{StationName}] 发生内部异常，进入报警状态！");
                })
                .Permit(MachineTrigger.Reset, MachineState.Idle);
        }

        #region 线程生命周期管控

        private void OnStartRunning()
        {
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            _pauseEvent.Set();
            _workflowTask = Task.Run(() => ProcessWrapperAsync(_runCts.Token), _runCts.Token);
        }

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
                _logger?.Warn($"[{StationName}] 子线程被安全打断并退出。");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 业务逻辑发生异常: {ex.Message}");
                if (_machine.CanFire(MachineTrigger.Error))
                    _machine.Fire(MachineTrigger.Error);
            }
        }

        #endregion

        /// <summary>
        /// 当前运行模式（由 MasterController 在 Idle 状态下统一设置后下发至各工站）
        /// </summary>
        public OperationMode CurrentMode { get; set; } = OperationMode.Normal;

        // --- 强制子类必须实现的工艺大循环 ---
        protected abstract Task ProcessLoopAsync(CancellationToken token);

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
        /// 物理复位：先执行工站/模组级硬件复位，再将状态机从 Alarm 切回 Idle。
        /// 基类默认实现仅复位状态机；子类可 override 以执行真实硬件清警 + 回原点动作。
        /// 由 MasterController.ResetAllAsync() 在 Resetting 阶段顺序调用。
        /// </summary>
        public virtual async Task ExecuteResetAsync(CancellationToken token)
        {
            await Task.CompletedTask;
            ResetAlarm(); // Alarm → Idle
        }

        // --- 供主控调用的公开方法 ---
        public void Start() => Fire(MachineTrigger.Start);
        public void Stop() => Fire(MachineTrigger.Stop);
        public void Pause() => Fire(MachineTrigger.Pause);
        public void Resume() => Fire(MachineTrigger.Resume);
        public void TriggerAlarm() => Fire(MachineTrigger.Error);
        public void ResetAlarm() => Fire(MachineTrigger.Reset);

        /// <summary>
        /// 触发本工站状态机跳转。protected，供子类在 ExecuteInitializeAsync / ExecuteResetAsync 中驱动生命周期。
        /// </summary>
        protected void Fire(MachineTrigger trigger)
        {
            if (_machine.CanFire(trigger)) _machine.Fire(trigger);
        }

        public virtual void Dispose()
        {
            _runCts?.Cancel();
            _pauseEvent?.Set();
            try { _workflowTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _runCts?.Dispose();
            _pauseEvent?.Dispose();
        }
    }
}
