using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.Demo.Mechanisms;

namespace PF.Workstation.Demo
{
    /// <summary>
    /// 【工站层示例】取放工站（多线程工艺大循环）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  线程模型（完整调用链）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///  主线程(UI)
    ///    │  MasterController.StartAll()
    ///    │    └─ PickPlaceStation.Start()
    ///    │         └─ StationBase 状态机 Idle → Running
    ///    │              └─ OnStartRunning():
    ///    │                   _runCts = new CancellationTokenSource()
    ///    │                   _workflowTask = Task.Run(ProcessWrapperAsync)
    ///    │
    ///    └──────────────────────────────────────────────────────────────────
    ///
    ///  工站线程（Task.Run 新线程池线程）
    ///    │  ProcessWrapperAsync(token)           ← StationBase 提供，全局异常守护
    ///    │    └─ ProcessLoopAsync(token)         ← 本类实现，工艺大循环
    ///    │         │
    ///    │         ├─ _pauseEvent.Wait(token)    ← 暂停点：MasterController.PauseAll()
    ///    │         │     ManualResetEventSlim 闸门关闭时此处阻塞，直到 ResumeAll() 开闸
    ///    │         │
    ///    │         ├─ await WaitForMaterialAsync() ← 等待上游信号（IO / 传感器）
    ///    │         │
    ///    │         ├─ _pauseEvent.Wait(token)    ← 再次检查暂停（防等待过程中被暂停）
    ///    │         │
    ///    │         ├─ await _gantry.PickAsync(token)
    ///    │         │     └─ _xAxis.MoveAbsoluteAsync → await Task.Delay(ms, token)
    ///    │         │     └─ _vacuumIO.WriteOutput / WaitInputAsync
    ///    │         │     ← 急停时 token.Cancel() 使所有 await 立即抛 OperationCanceledException
    ///    │         │
    ///    │         ├─ _pauseEvent.Wait(token)    ← 取放之间的暂停点
    ///    │         │
    ///    │         └─ await _gantry.PlaceAsync(token)
    ///    │
    ///    │  异常捕获（ProcessWrapperAsync）：
    ///    │    · OperationCanceledException → 正常退出（急停/停止）
    ///    │    · Exception                 → 记录日志 + 触发状态机 Error
    ///    │
    ///    └──────────────────────────────────────────────────────────────────
    ///
    ///  报警级联（事件驱动，无需轮询）：
    ///    Hardware.AlarmTriggered
    ///      └─ BaseMechanism.OnHardwareAlarmTriggered
    ///           └─ GantryMechanism.AlarmTriggered（本类订阅）
    ///                └─ OnMechanismAlarm() → TriggerAlarm()
    ///                     └─ 状态机 Running → Alarm
    ///                          └─ OnExit Running: _runCts.Cancel()（中断工站线程）
    ///                          └─ StationAlarmTriggered 事件
    ///                               └─ MasterController.OnSubStationAlarm()
    ///                                    └─ 全局状态机 → Alarm（全线急停）
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    public class PickPlaceStation : StationBase
    {
        private readonly GantryMechanism _gantry;
        private int _cycleCount;

        public PickPlaceStation(GantryMechanism gantry, ILogService logger)
            : base("取放工站", logger)
        {
            _gantry = gantry;

            // 订阅模组报警事件：硬件故障将通过此路径驱动本工站进入 Alarm 状态
            _gantry.AlarmTriggered += OnMechanismAlarm;
        }

        // ── 工艺大循环（运行在独立后台线程中）────────────────────────────────

        /// <summary>
        /// ProcessLoopAsync 由 StationBase.ProcessWrapperAsync 在后台线程中调用。
        /// 本方法抛出的任何非 OperationCanceledException 异常都会被 ProcessWrapperAsync
        /// 捕获，并自动触发状态机 Error → Alarm。
        /// </summary>
        protected override async Task ProcessLoopAsync(CancellationToken token)
        {
            // ── 启动时初始化模组（阻塞直至完成）──────────────────────────────
            _logger.Info($"[{StationName}] 正在初始化龙门模组...");
            if (!await _gantry.InitializeAsync(token))
                throw new Exception($"[{StationName}] 龙门模组初始化失败，工站无法启动！");

            _logger.Success($"[{StationName}] 初始化完成，进入工艺循环 ▶");

            while (!token.IsCancellationRequested)
            {
                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ①
                //  MasterController.PauseAll() 会关闭 _pauseEvent 闸门
                //  此处阻塞（不消耗 CPU），直到 ResumeAll() 重新开闸
                //  同时传入 token，使急停时能打断阻塞状态
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                _cycleCount++;
                _logger.Info($"[{StationName}] ══ Cycle #{_cycleCount} 开始 ══");

                // ── Step 1: 等待上游物料到位信号 ──────────────────────────
                // 实际项目替换为：await _ioCtrl.WaitInputAsync(MATERIAL_IN_PORT, true, token: token)
                _logger.Info($"[{StationName}] [1/4] 等待上游物料到位...");
                await WaitForMaterialAsync(token);

                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ②（防在等待 IO 过程中被暂停后立即执行动作）
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                // ── Step 2: 执行取料 ───────────────────────────────────────
                _logger.Info($"[{StationName}] [2/4] 执行取料动作...");
                await _gantry.PickAsync(token);
                // 若 PickAsync 内部抛异常（真空超时、轴故障等），
                // ProcessWrapperAsync 会捕获并自动触发 Error 状态机

                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ③（取放之间提供暂停窗口）
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                // ── Step 3: 执行放料 ───────────────────────────────────────
                _logger.Info($"[{StationName}] [3/4] 执行放料动作...");
                await _gantry.PlaceAsync(token);

                // ── Step 4: 通知下游（如触发下游工站启动信号）─────────────
                _logger.Info($"[{StationName}] [4/4] 通知下游接收物料");
                await NotifyDownstreamAsync(token);

                _logger.Success($"[{StationName}] ══ Cycle #{_cycleCount} 完成 ══\n");

                // 节拍间隙：防止循环过于紧凑，给其他线程 CPU 时间
                await Task.Delay(100, token);
            }
        }

        // ── 辅助方法 ───────────────────────────────────────────────────────

        /// <summary>
        /// 等待上游物料到位信号（模拟）
        /// 实际项目替换为真实 IO 等待：
        ///   await _ioCtrl.WaitInputAsync(MATERIAL_SENSOR_PORT, true, timeoutMs: 30000, token)
        /// </summary>
        private async Task WaitForMaterialAsync(CancellationToken token)
        {
            await Task.Delay(800, token); // 模拟等待约 800ms
        }

        /// <summary>
        /// 通知下游设备（模拟）
        /// 实际项目替换为：_ioCtrl.WriteOutput(DOWNSTREAM_TRIGGER_PORT, true)
        /// </summary>
        private async Task NotifyDownstreamAsync(CancellationToken token)
        {
            await Task.Delay(50, token);
        }

        // ── 报警处理 ───────────────────────────────────────────────────────

        /// <summary>
        /// 接收模组报警事件 → 驱动本工站状态机进入 Alarm 状态
        ///
        /// 事件回调运行在触发报警的线程上（通常是工站线程或硬件回调线程）。
        /// TriggerAlarm() 内部的状态机 Fire() 是线程安全的（Stateless 库保证）。
        /// </summary>
        private void OnMechanismAlarm(object sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            TriggerAlarm(); // → StationBase 状态机 Running/Paused → Alarm
                            // → Alarm.OnEntry: StationAlarmTriggered 事件
                            // → MasterController 接管，触发全线急停
        }

        public override void Dispose()
        {
            _gantry.AlarmTriggered -= OnMechanismAlarm;
            _gantry.Dispose();
            base.Dispose();
        }
    }
}
