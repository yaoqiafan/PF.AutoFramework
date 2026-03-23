using PF.Core.Enums;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using Stateless;
using System.ComponentModel;
using System.Runtime.CompilerServices;

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
    ///
    /// 并发安全设计：
    ///   · 所有状态机跳转（Fire / FireAsync）均通过 _stateLock（SemaphoreSlim 1,1）独占执行，
    ///     防止后台线程报警与 UI 线程发出的 Stop/Pause 同时修改状态机内部状态。
    ///   · Running 状态采用 OnEntryAsync，确保旧任务彻底终止后再启动新任务，消除"幽灵线程"。
    /// </summary>
    public abstract class StationBase : IDisposable, INotifyPropertyChanged
    {
        // ── INotifyPropertyChanged ────────────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void RaisePropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // ── 标识 ─────────────────────────────────────────────────────────────
        public string StationName { get; }

        /// <summary>当前状态机状态（状态变化时触发 PropertyChanged）</summary>
        public MachineState CurrentState => _machine.State;

        /// <summary>
        /// 当前步序描述，供 UI 实时显示（如"正在取料..."、"等待槽位空闲..."）。
        /// 子类在每次步序切换时通过 protected set 赋值，自动触发 PropertyChanged。
        /// </summary>
        private string _currentStepDescription = "就绪";
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

        // 向上层（主控）抛出的报警事件
        public event EventHandler<string> StationAlarmTriggered;

        protected readonly ILogService _logger;
        protected readonly StateMachine<MachineState, MachineTrigger> _machine;

        // ── 并发安全：所有状态机跳转均通过此信号量独占执行 ─────────────────
        // 使用 SemaphoreSlim 而非 lock，以支持 async/await 场景下的无死锁等待。
        private readonly SemaphoreSlim _stateLock = new(1, 1);

        // 线程生命周期三剑客
        private CancellationTokenSource _runCts;
        protected ManualResetEventSlim _pauseEvent;
        private Task _workflowTask;

        // 标记当前业务线程是被"外部报警"打断（true），还是"正常停止"打断（false）。
        // volatile 确保多线程可见性：TriggerAlarm 在状态机线程写入，ProcessWrapperAsync 在业务线程读取。
        private volatile bool _alarmInterrupted = false;

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
            _machine.OnTransitioned(t =>
        {
            _logger?.Debug($"[{StationName}] 状态变迁: {t.Source} -> {t.Destination}");
            RaisePropertyChanged(nameof(CurrentState)); // 通知 UI 刷新状态绑定
        });

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
            // 【关键】OnEntryAsync 确保：旧任务彻底结束 → 再启动新任务，消除幽灵线程。
            // 注意：凡触发进入 Running 的 Fire 调用（Start / Resume）均须使用 FireAsync。
            _machine.Configure(MachineState.Running)
                .OnEntryAsync(OnStartRunningAsync)
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

        /// <summary>
        /// Running 状态异步入口：
        ///   1. 取消旧 CTS → 防止旧任务继续执行硬件指令
        ///   2. 等待旧任务彻底死亡 → 消除"幽灵线程"导致的硬件指令冲突
        ///   3. 释放旧 CTS → 建立新 CTS → 启动新任务
        /// </summary>
        private async Task OnStartRunningAsync()
        {
            // 1. 取消旧任务
            _runCts?.Cancel();

            // 2. 等待旧任务彻底结束（捕获取消异常，正常退出即可）
            if (_workflowTask is { IsCompleted: false })
            {
                try { await _workflowTask; }
                catch { /* 旧任务被取消，忽略 OperationCanceledException 及其他退出异常 */ }
            }

            // 3. 重建令牌，启动新任务
            _runCts?.Dispose();
            _runCts = new CancellationTokenSource();
            _alarmInterrupted = false; // 每次启动前清除报警标志
            _pauseEvent.Set();
            _workflowTask = Task.Run(() => ProcessWrapperAsync(_runCts.Token));
        }

        /// <summary>
        /// Running 状态同步出口：仅发出取消信号。
        /// 不在此处等待任务结束——OnStopRunning 在 _stateLock 持有期间被调用，
        /// 若同时等待可能阻塞后台任务调用 Fire(Error) 时的加锁请求，造成死锁。
        /// 任务的彻底终止由下次 OnStartRunningAsync 的 await _workflowTask 保证。
        /// </summary>
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
                // 由 TriggerAlarm() 取消：标志已在 TriggerAlarm 中置位，状态机已切到 Alarm，仅记录日志。
                // 由 Stop/正常停止取消：记录安全退出日志即可。
                if (_alarmInterrupted)
                    _logger?.Warn($"[{StationName}] 业务流程被外部报警打断，线程安全退出。");
                else
                    _logger?.Warn($"[{StationName}] 子线程被安全打断并退出。");
                _alarmInterrupted = false;
            }
            catch (Exception ex)
            {
                _logger?.Error($"[{StationName}] 业务逻辑发生异常: {ex.Message}");
                // 使用线程安全的 Fire() 而非直接调用 _machine.Fire()
                Fire(MachineTrigger.Error);
            }
        }

        #endregion

        /// <summary>
        /// 当前运行模式（由 MasterController 在 Idle 状态下统一设置后下发至各工站）。
        /// 属性变化时触发 PropertyChanged，供 UI 绑定。
        /// </summary>
        private OperationMode _currentMode = OperationMode.Normal;
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

        /// <summary>
        /// 异步启动工站。因 Running 状态配置了 OnEntryAsync，必须使用 FireAsync 触发跳转。
        /// </summary>
        public async Task StartAsync() => await FireAsync(MachineTrigger.Start);

        public void Stop() => Fire(MachineTrigger.Stop);
        public void Pause() => Fire(MachineTrigger.Pause);

        /// <summary>
        /// 异步恢复工站（Paused → Running）。因 Running 状态配置了 OnEntryAsync，必须使用 FireAsync。
        /// </summary>
        public async Task ResumeAsync() => await FireAsync(MachineTrigger.Resume);

        /// <summary>
        /// 外部触发工站报警（如主控急停、硬件异常事件）。
        /// 同时执行两件事：
        ///   1. 取消 _runCts → 打断当前所有正在阻塞的等待方法（含 Paused 状态下的等待）
        ///   2. Fire(Error)  → 驱动状态机进入 Alarm 状态
        /// </summary>
        public void TriggerAlarm()
        {
            _alarmInterrupted = true;
            _runCts?.Cancel(); // 打断业务线程

            // 🛡️ 防御性修复：如果是自己抛异常导致的 Alarm，主控反向调用时直接忽略
            if (CurrentState != MachineState.Alarm)
            {
                Fire(MachineTrigger.Error);
            }
        }
 
        public void ResetAlarm()   => Fire(MachineTrigger.Reset);

        /// <summary>
        /// 线程安全的同步状态跳转。
        /// 通过 _stateLock 确保同一时刻只有一个线程修改状态机，
        /// 防止后台报警线程与 UI 线程并发触发导致状态机内部状态崩溃。
        /// 适用于不含 OnEntryAsync/OnExitAsync 的跳转路径（Error、Stop、Pause、Reset 等）。
        /// </summary>
        protected void Fire(MachineTrigger trigger)
        {
            _stateLock.Wait();
            try
            {
                if (_machine.CanFire(trigger)) _machine.Fire(trigger);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        /// <summary>
        /// 线程安全的异步状态跳转。
        /// 与 Fire() 的区别：使用 WaitAsync 避免在 async 上下文中阻塞线程；
        /// 使用 FireAsync 以正确 await Running 状态的 OnEntryAsync 回调。
        /// 适用于可能触发 OnEntryAsync 的路径（Start → Running、Resume → Running）。
        /// </summary>
        protected async Task FireAsync(MachineTrigger trigger)
        {
            await _stateLock.WaitAsync();
            try
            {
                if (_machine.CanFire(trigger)) await _machine.FireAsync(trigger);
            }
            finally
            {
                _stateLock.Release();
            }
        }

        #region 工站业务等待辅助方法

        // ─────────────────────────────────────────────────────────────────────
        // 所有等待方法均接受 CancellationToken。
        // 当以下任一情况发生时，token 会被取消，方法立即抛出 OperationCanceledException：
        //   · Stop() 被调用       → _runCts 由 OnStopRunning() 取消
        //   · TriggerAlarm() 被调用 → _runCts 由 TriggerAlarm() 主动取消
        // ProcessWrapperAsync 统一捕获该异常，业务代码无需处理取消逻辑。
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// 显式暂停检查点：工站处于 Paused 状态时此处阻塞，恢复后继续执行。
        /// 在业务循环的每个步序入口调用，确保暂停命令能及时生效。
        /// </summary>
        protected void CheckPause(CancellationToken token) => _pauseEvent.Wait(token);

        /// <summary>
        /// 工艺延时：支持暂停中断（每 50ms 检查一次暂停状态）和取消令牌。
        /// 相比直接 await Task.Delay，此方法在暂停时会暂停计时，恢复后继续剩余延时。
        /// </summary>
        /// <param name="milliseconds">延时毫秒数</param>
        /// <param name="token">取消令牌（Stop/TriggerAlarm 时自动取消）</param>
        protected async Task WaitAsync(int milliseconds, CancellationToken token)
        {
            const int ChunkMs = 50;
            int remaining = milliseconds;
            while (remaining > 0)
            {
                _pauseEvent.Wait(token); // 暂停时阻塞，恢复或取消时继续
                int chunk = Math.Min(ChunkMs, remaining);
                await Task.Delay(chunk, token).ConfigureAwait(false);
                remaining -= chunk;
            }
        }

        /// <summary>
        /// 等待任意 bool 条件成立（轮询模式）。
        /// 超时返回 false 并记录错误日志；条件满足返回 true。
        /// </summary>
        /// <param name="condition">被轮询的条件委托（应为轻量级属性读取，不含阻塞操作）</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5 秒</param>
        /// <param name="token">取消令牌</param>
        /// <param name="pollIntervalMs">轮询间隔毫秒数，默认 20ms</param>
        protected async Task<bool> WaitConditionAsync(
            Func<bool> condition,
            int timeoutMs = 5_000,
            CancellationToken token = default,
            int pollIntervalMs = 20)
        {
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            try
            {
                while (true)
                {
                    _pauseEvent.Wait(linked.Token);
                    if (condition()) return true;
                    await Task.Delay(pollIntervalMs, linked.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                    _logger?.Error($"[{StationName}] 等待条件超时（{timeoutMs} ms）");
                throw; // 超时视为超时错误返回 false；外部取消则向上重新抛出打断流程
            }
        }

        /// <summary>
        /// 等待指定 IO 端口达到目标状态（按端口号）。
        /// 委托给 IIOController.WaitInputAsync，超时后记录错误日志。
        /// </summary>
        /// <param name="io">IO 控制器</param>
        /// <param name="portIndex">端口号</param>
        /// <param name="targetState">期望状态（true=高电平，false=低电平）</param>
        /// <param name="timeoutMs">超时毫秒数，默认 5 秒</param>
        /// <param name="token">取消令牌</param>
        protected async Task<bool> WaitIOAsync(
            IIOController io,
            int portIndex,
            bool targetState,
            int timeoutMs = 5_000,
            CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] → {targetState}");
            _pauseEvent.Wait(token);
            bool result = await io.WaitInputAsync(portIndex, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result)
                _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 端口[{portIndex}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>
        /// 等待指定 IO 端口达到目标状态（按枚举信号名）。
        /// </summary>
        /// <typeparam name="T">IO 信号枚举类型</typeparam>
        protected async Task<bool> WaitIOAsync<T>(
            IIOController io,
            T inputName,
            bool targetState,
            int timeoutMs = 5_000,
            CancellationToken token = default)
            where T : Enum
        {
            _logger?.Info($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] → {targetState}");
            _pauseEvent.Wait(token);
            bool result = await io.WaitInputAsync(inputName, targetState, timeoutMs, token).ConfigureAwait(false);
            if (!result)
                _logger?.Error($"[{StationName}] 等待 [{io.DeviceName}] 信号[{inputName}] = {targetState} 超时（{timeoutMs} ms）");
            return result;
        }

        /// <summary>
        /// 等待工站间同步信号量（流水线节拍协同）。
        /// 委托给 IStationSyncService.WaitAsync，带超时保护。
        /// </summary>
        /// <param name="sync">工站同步服务</param>
        /// <param name="signalName">信号量名称</param>
        /// <param name="timeoutMs">超时毫秒数，默认 30 秒</param>
        /// <param name="token">取消令牌</param>
        protected async Task<bool> WaitSyncAsync(
            IStationSyncService sync,
            string signalName,
            int timeoutMs = 30_000,
            CancellationToken token = default)
        {
            _logger?.Info($"[{StationName}] 等待同步信号 [{signalName}]...");
            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            try
            {
                await sync.WaitAsync(signalName, linked.Token).ConfigureAwait(false);
                _logger?.Info($"[{StationName}] 同步信号 [{signalName}] 已触发，继续执行");
                return true;
            }
            catch (OperationCanceledException)
            {
                if (timeoutCts.IsCancellationRequested)
                {
                    _logger?.Error($"[{StationName}] 等待同步信号 [{signalName}] 超时（{timeoutMs} ms）");
                    return false;
                }
                throw; // 外部取消（Stop/Alarm）→ 向上重新抛出
            }
        }

        #endregion

        public virtual void Dispose()
        {
            _runCts?.Cancel();
            _pauseEvent?.Set();

            // 等待后台任务安全退出（最多 5 秒），防止 Dispose 后幽灵线程继续访问已释放资源
            var task = _workflowTask;
            if (task is { IsCompleted: false })
            {
                try { task.Wait(TimeSpan.FromSeconds(5)); } catch { }
            }

            _runCts?.Dispose();
            _pauseEvent?.Dispose();
            _stateLock?.Dispose();
        }
    }
}
