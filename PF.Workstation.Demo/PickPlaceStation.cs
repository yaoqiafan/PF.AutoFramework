using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.Demo.Mechanisms;
using PF.Workstation.Demo.Sync;

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
    ///    │  ProcessWrapperAsync(token)            ← StationBase 提供，全局异常守护
    ///    │    └─ ProcessLoopAsync(token)          ← 本类实现，工艺大循环
    ///    │         │
    ///    │         ├─ _pauseEvent.Wait(token)     ← 暂停点①
    ///    │         │
    ///    │         ├─ await WaitForMaterialAsync  ← 等待上游物料（传感器/IO）
    ///    │         │
    ///    │         ├─ _pauseEvent.Wait(token)     ← 暂停点②
    ///    │         │
    ///    │         ├─ await _gantry.PickAsync     ← 从上游取料
    ///    │         │
    ///    │         ├─ _pauseEvent.Wait(token)     ← 暂停点③
    ///    │         │
    ///    │         ├─ await _sync.WaitAsync(SlotEmpty)   ← ★ 协同点：等待槽位空闲
    ///    │         │     阻塞直到点胶工站完成当前产品并 Release(SlotEmpty)
    ///    │         │
    ///    │         ├─ await _gantry.PlaceAsync    ← 将产品放入工作台
    ///    │         │
    ///    │         ├─ _sync.Release(ProductReady) ← ★ 协同点：通知点胶工站产品已到位
    ///    │         │
    ///    │         └─ await NotifyDownstreamAsync
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
        private readonly IStationSyncService _sync;
        private int _cycleCount;

        public PickPlaceStation(GantryMechanism gantry, IStationSyncService sync, ILogService logger)
            : base("取放工站", logger)
        {
            _gantry = gantry;
            _sync   = sync;

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
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                _cycleCount++;
                _logger.Info($"[{StationName}] ══ Cycle #{_cycleCount} 开始 ══");

                // ── Step 1: 等待上游物料到位信号 ──────────────────────────
                _logger.Info($"[{StationName}] [1/5] 等待上游物料到位...");
                await WaitForMaterialAsync(token);

                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ②（防在等待 IO 过程中被暂停后立即执行动作）
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                // ── Step 2: 从上游取料 ─────────────────────────────────────
                _logger.Info($"[{StationName}] [2/5] 执行取料动作...");
                await _gantry.PickAsync(token);

                // ════════════════════════════════════════════════════════════
                //  暂停检查点 ③（取放之间提供暂停窗口）
                // ════════════════════════════════════════════════════════════
                _pauseEvent.Wait(token);

                // ── Step 3: 等待工作台槽位空闲（★ 流水线协同核心）──────────
                // 若点胶工站尚未完成上一个产品，此处阻塞，直到它调用 Release(SlotEmpty)
                _logger.Info($"[{StationName}] [3/5] 等待工作台槽位空闲...");
                await _sync.WaitAsync(WorkstationSignals.SlotEmpty, token);

                // ── Step 4: 将产品放入工作台 ───────────────────────────────
                _logger.Info($"[{StationName}] [4/5] 执行放料动作（放入工作台）...");
                await _gantry.PlaceAsync(token);

                // ── Step 5: 通知点胶工站：产品已到位（★ 流水线协同核心）────
                _sync.Release(WorkstationSignals.ProductReady);
                _logger.Info($"[{StationName}] [5/5] 已通知点胶工站：产品到位");

                await NotifyDownstreamAsync(token);

                _logger.Success($"[{StationName}] ══ Cycle #{_cycleCount} 完成 ══\n");

                // 节拍间隙
                await Task.Delay(100, token);
            }
        }

        // ── 辅助方法 ───────────────────────────────────────────────────────

        /// <summary>
        /// 等待上游物料到位信号（模拟）
        /// 实际项目替换为：await _ioCtrl.WaitInputAsync(MATERIAL_SENSOR_PORT, true, timeoutMs: 30000, token)
        /// </summary>
        private async Task WaitForMaterialAsync(CancellationToken token)
        {
            await Task.Delay(800, token);
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

        private void OnMechanismAlarm(object sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            TriggerAlarm();
        }

        public override void Dispose()
        {
            _gantry.AlarmTriggered -= OnMechanismAlarm;
            _gantry.Dispose();
            base.Dispose();
        }
    }
}
