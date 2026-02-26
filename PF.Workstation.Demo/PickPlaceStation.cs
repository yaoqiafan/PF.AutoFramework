using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.Demo.Mechanisms;
using PF.Workstation.Demo.Sync;

namespace PF.Workstation.Demo
{
    /// <summary>
    /// 【工站层示例】取放工站（步序状态机 + 运行模式支持）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  步序枚举（PickPlaceStep）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///   Init (0)
    ///     │  初始化龙门模组（连接 → 使能 → 回原点）
    ///     ▼
    ///   WaitMaterial (10)
    ///     │  Normal：等待真实传感器到位信号（800 ms 模拟）
    ///     │  DryRun ：跳过等待，延迟 200 ms 后直接进入下一步
    ///     ▼
    ///   Pick (20)
    ///     │  执行取料（移到取料位 → 慢降 → 开真空 → 提升）
    ///     ▼
    ///   WaitSlotEmpty (30)
    ///     │  等待点胶工站完成当前产品，释放 SlotEmpty 信号量（★ 流水线协同）
    ///     ▼
    ///   Place (40)
    ///     │  执行放料（移到放料位 → 慢降 → 关真空 → 退位）
    ///     │  放料完成后 Release(ProductReady)，通知点胶工站
    ///     ▼
    ///   NotifyDownstream (50)
    ///     │  通知下游（模拟：延迟 50 ms）
    ///     └──→ 回到 WaitMaterial，开始下一个 Cycle
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  ExecuteResetAsync 智能回跳策略
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///   步序 < Pick(20)         → Init     （重新初始化）
    ///   Pick(20) ≤ 步序 < Place(40) → Pick （重新取料；初始化后吸盘已空）
    ///   步序 ≥ Place(40)        → WaitMaterial（放料已完成或在途，跳过取料）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    public class PickPlaceStation : StationBase
    {
        // ── 步序枚举（显式间隔整数值，便于将来插入中间步序）─────────────────
        private enum PickPlaceStep
        {
            Init             = 0,   // 初始化模组（仅启动/复位后执行一次）
            WaitMaterial     = 10,  // 等待上游物料到位
            Pick             = 20,  // 执行取料
            WaitSlotEmpty    = 30,  // 等待工作台槽位空闲
            Place            = 40,  // 执行放料 + 释放 ProductReady 信号量
            NotifyDownstream = 50,  // 通知下游
        }

        private readonly GantryMechanism _gantry;
        private readonly IStationSyncService _sync;
        private int _cycleCount;
        private PickPlaceStep _currentStep = PickPlaceStep.Init;

        public PickPlaceStation(GantryMechanism gantry, IStationSyncService sync, ILogService logger)
            : base("取放工站", logger)
        {
            _gantry = gantry;
            _sync   = sync;

            // 订阅模组报警事件：硬件故障将通过此路径驱动本工站进入 Alarm 状态
            _gantry.AlarmTriggered += OnMechanismAlarm;
        }

        // ── 工艺大循环（步序状态机）──────────────────────────────────────────

        /// <summary>
        /// ProcessLoopAsync 由 StationBase.ProcessWrapperAsync 在后台线程中调用。
        /// 使用 switch(_currentStep) 驱动步序状态机，支持从任意步序恢复（配合 ExecuteResetAsync）。
        /// </summary>
        protected override async Task ProcessLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    // ── Step Init: 初始化模组（启动或复位后首次执行）─────────
                    case PickPlaceStep.Init:
                        _logger.Info($"[{StationName}] 正在初始化龙门模组...");
                        if (!await _gantry.InitializeAsync(token))
                            throw new Exception($"[{StationName}] 龙门模组初始化失败，工站无法启动！");
                        _logger.Success($"[{StationName}] 初始化完成，进入工艺循环 ▶");
                        _currentStep = PickPlaceStep.WaitMaterial;
                        break;

                    // ── Step WaitMaterial: 等待上游物料到位 ──────────────────
                    case PickPlaceStep.WaitMaterial:
                        // ════════ 暂停检查点 ① ════════
                        _pauseEvent.Wait(token);

                        _cycleCount++;
                        _logger.Info($"[{StationName}] ══ Cycle #{_cycleCount} 开始 ══");

                        if (CurrentMode == OperationMode.DryRun)
                        {
                            _logger.Info($"[{StationName}] [DryRun][1/5] 跳过真实物料等待，模拟已到位");
                            await Task.Delay(200, token);
                        }
                        else
                        {
                            _logger.Info($"[{StationName}] [1/5] 等待上游物料到位...");
                            await WaitForMaterialAsync(token);
                        }

                        _currentStep = PickPlaceStep.Pick;
                        break;

                    // ── Step Pick: 执行取料 ───────────────────────────────────
                    case PickPlaceStep.Pick:
                        // ════════ 暂停检查点 ② ════════
                        _pauseEvent.Wait(token);

                        _logger.Info($"[{StationName}] [2/5] 执行取料动作...");
                        await _gantry.PickAsync(token);
                        _currentStep = PickPlaceStep.WaitSlotEmpty;
                        break;

                    // ── Step WaitSlotEmpty: 等待工作台槽位空闲（★ 流水线协同）
                    case PickPlaceStep.WaitSlotEmpty:
                        // ════════ 暂停检查点 ③ ════════
                        _pauseEvent.Wait(token);

                        _logger.Info($"[{StationName}] [3/5] 等待工作台槽位空闲...");
                        await _sync.WaitAsync(WorkstationSignals.SlotEmpty, token);
                        _currentStep = PickPlaceStep.Place;
                        break;

                    // ── Step Place: 执行放料 + 通知点胶工站（★ 流水线协同）────
                    case PickPlaceStep.Place:
                        _logger.Info($"[{StationName}] [4/5] 执行放料动作（放入工作台）...");
                        await _gantry.PlaceAsync(token);

                        _sync.Release(WorkstationSignals.ProductReady);
                        _logger.Info($"[{StationName}] [5/5] 已通知点胶工站：产品到位");
                        _currentStep = PickPlaceStep.NotifyDownstream;
                        break;

                    // ── Step NotifyDownstream: 通知下游 ──────────────────────
                    case PickPlaceStep.NotifyDownstream:
                        await NotifyDownstreamAsync(token);
                        _logger.Success($"[{StationName}] ══ Cycle #{_cycleCount} 完成 ══\n");

                        // 节拍间隙
                        await Task.Delay(100, token);
                        _currentStep = PickPlaceStep.WaitMaterial;
                        break;
                }
            }
        }

        // ── 物理复位（覆写基类）─────────────────────────────────────────────

        /// <summary>
        /// 硬件复位 + 智能步序回跳：
        ///   1. 调用 _gantry.ResetAsync() 清除硬件报警
        ///   2. 调用 _gantry.InitializeAsync() 重新回原点
        ///   3. 根据故障时所在步序决定恢复入口（见类注释中的策略表）
        ///   4. 调用 ResetAlarm() 将工站状态机推回 Idle
        /// </summary>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            _logger.Warn($"[{StationName}] 开始物理复位，故障步序: {_currentStep}");

            // 1. 硬件复位：清除底层报警（轴卡、IO 卡等）
            await _gantry.ResetAsync(token);

            // 2. 重新初始化（回原点、关真空），使硬件回到安全初始状态
            if (!await _gantry.InitializeAsync(token))
                _logger.Warn($"[{StationName}] 复位后初始化未完全成功，请检查硬件！");

            // 3. 智能回跳：根据故障发生的步序决定下一次 ProcessLoopAsync 从哪里进入
            if (_currentStep >= PickPlaceStep.Place)
            {
                // 放料阶段（或之后）发生故障：放料动作已完成或吸盘已空，
                // 安全地从下一个物料开始。
                _currentStep = PickPlaceStep.WaitMaterial;
                _logger.Info($"[{StationName}] 步序回跳 → WaitMaterial（跳过取料）");
            }
            else if (_currentStep >= PickPlaceStep.Pick)
            {
                // 取料阶段（到等待槽位之间）发生故障：硬件已回原点，吸盘已空，
                // 重新尝试取料。
                _currentStep = PickPlaceStep.Pick;
                _logger.Info($"[{StationName}] 步序回跳 → Pick（重试取料）");
            }
            else
            {
                // 初始化阶段或等待物料前发生故障：从头初始化。
                _currentStep = PickPlaceStep.Init;
                _logger.Info($"[{StationName}] 步序回跳 → Init（重新初始化）");
            }

            // 4. 工站状态机：Alarm → Idle
            ResetAlarm();
            _logger.Success($"[{StationName}] 物理复位完成，就绪。");
        }

        // ── 辅助方法 ────────────────────────────────────────────────────────

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

        // ── 报警处理 ────────────────────────────────────────────────────────

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
