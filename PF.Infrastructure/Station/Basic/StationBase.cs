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
        private CancellationTokenSource _runCts;            // 用于终止线程
        protected ManualResetEventSlim _pauseEvent;         // 用于挂起/恢复线程 (保护级别设为protected，方便子类使用)
        private Task _workflowTask;                         // 后台子线程本身

        protected StationBase(string name, ILogService logger)
        {
            StationName = name;
            _logger = logger;
            _pauseEvent = new ManualResetEventSlim(true); // 默认闸门打开

            // 初始化状态机
            _machine = new StateMachine<MachineState, MachineTrigger>(MachineState.Idle);
            ConfigureStateMachine();
        }

        private void ConfigureStateMachine()
        {
            // 状态变迁日志（可选）
            _machine.OnTransitioned(t => _logger?.Debug($"[{StationName}] 状态变迁: {t.Source} -> {t.Destination}"));

            // --- 待机状态 ---
            _machine.Configure(MachineState.Idle)
                .Permit(MachineTrigger.Start, MachineState.Running)
                .Permit(MachineTrigger.Error, MachineState.Alarm)
                .Ignore(MachineTrigger.Stop);

            // --- 运行状态 ---
            _machine.Configure(MachineState.Running)
                .OnEntry(OnStartRunning)       // 核心：进入运行时，分配并启动子线程 Task
                .OnExit(OnStopRunning)         // 核心：退出运行时，取消 CancellationToken
                .Permit(MachineTrigger.Pause, MachineState.Paused)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 暂停状态 ---
            _machine.Configure(MachineState.Paused)
                .OnEntry(() => _pauseEvent.Reset())  // 关闸门，挂起线程
                .OnExit(() => _pauseEvent.Set())     // 开闸门，放行线程
                .Permit(MachineTrigger.Resume, MachineState.Running)
                .Permit(MachineTrigger.Stop, MachineState.Idle)
                .Permit(MachineTrigger.Error, MachineState.Alarm);

            // --- 报警状态 ---
            _machine.Configure(MachineState.Alarm)
                .OnEntry(() =>
                {
                    _pauseEvent.Set(); // 如果在暂停时发生报警，必须开闸，让线程随CancellationToken死亡
                    // 触发对外的报警事件，通知主控
                    StationAlarmTriggered?.Invoke(this, $"[{StationName}] 发生内部异常，进入报警状态！");
                })
                .Permit(MachineTrigger.Reset, MachineState.Idle);
        }

        #region 线程生命周期管控

        private void OnStartRunning()
        {
            // 修复：反复 Start/Stop/Start 时，旧 CTS 必须先释放，否则每次重启泄漏一个对象。
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            _pauseEvent.Set();
            // 启动专属于该工站的后台任务（子线程）
            _workflowTask = Task.Run(() => ProcessWrapperAsync(_runCts.Token), _runCts.Token);
        }

        private void OnStopRunning()
        {
            _runCts?.Cancel(); // 发出终止信号，打断正在等待的底层硬件动作
        }

        /// <summary>
        /// 内部包装器，负责全局的异常捕获，防止子线程崩溃导致程序闪退
        /// </summary>
        private async Task ProcessWrapperAsync(CancellationToken token)
        {
            try
            {
                // 调用子类实现的具体业务逻辑
                await ProcessLoopAsync(token);
            }
            catch (OperationCanceledException)
            {
                _logger?.Warn($"[{StationName}] 子线程被安全打断并退出。");
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 业务逻辑发生异常: {ex.Message}");
                // 如果子线程抛出异常，触发自身状态机进入报警状态
                if (_machine.CanFire(MachineTrigger.Error))
                {
                    _machine.Fire(MachineTrigger.Error);
                }
            }
        }

        #endregion

        // --- 强制子类必须实现的工艺大循环 ---
        protected abstract Task ProcessLoopAsync(CancellationToken token);

        // --- 供主控调用的公开方法 ---
        public void Start() => Fire(MachineTrigger.Start);
        public void Stop() => Fire(MachineTrigger.Stop);
        public void Pause() => Fire(MachineTrigger.Pause);
        public void Resume() => Fire(MachineTrigger.Resume);
        public void TriggerAlarm() => Fire(MachineTrigger.Error);
        public void ResetAlarm() => Fire(MachineTrigger.Reset);

        private void Fire(MachineTrigger trigger)
        {
            if (_machine.CanFire(trigger)) _machine.Fire(trigger);
        }

        public virtual void Dispose()
        {
            _runCts?.Cancel();
            // 修复①：如果线程阻塞在 _pauseEvent.Wait() 暂停点，必须先开闸，
            //         否则 Cancel 信号无法触达，_workflowTask 永远不退出。
            _pauseEvent?.Set();
            // 修复②：等待后台任务真正退出（最多 5 秒），防止 Dispose 返回后
            //         任务仍在访问已释放的资源（如 _pauseEvent 本身）。
            try { _workflowTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _runCts?.Dispose();
            _pauseEvent?.Dispose();
        }
    }
}
