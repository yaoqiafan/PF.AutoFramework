using PF.Core.Attributes;
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
    /// 【工站层示例】取放工站（步序状态机 + 运行模式解耦）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  生命周期（由 MasterController 驱动）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///   ExecuteInitializeAsync()        ← 硬件初始化（连接 / 使能 / 回原点）
    ///     Uninitialized → Initializing → Idle
    ///
    ///   Start() → ProcessLoopAsync()    ← 按模式路由至独立工艺循环
    ///     Idle → Running
    ///
    ///   ExecuteResetAsync()             ← 故障后硬件复位 + 步序智能回跳
    ///     Alarm → Idle
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  运行模式路由（ProcessLoopAsync 入口）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///   ProcessLoopAsync
    ///     ├─ Normal  → ProcessNormalLoopAsync   完整生产工艺（含真实 IO 与流水线信号量协同）
    ///     └─ DryRun  → ProcessDryRunLoopAsync   空跑验证（跳过物料等待与下游协同，仅验证轴轨迹）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  共享步序枚举（PickPlaceStep）— 两种模式共用，保证 ExecuteResetAsync 回跳一致
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///  Normal 步序流：
    ///   WaitMaterial(10) → Pick(20) → WaitSlotEmpty(30) → Place(40) → NotifyDownstream(50) ──┐
    ///   └────────────────────────────────────────────────────────────────────────────────────┘
    ///
    ///  DryRun 步序流（跳过 WaitSlotEmpty，Place 后不 Release 信号量）：
    ///   WaitMaterial(10) → Pick(20) ──→ Place(40) → NotifyDownstream(50) ──┐
    ///   └──────────────────────────────────────────────────────────────────┘
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    ///  ExecuteResetAsync 智能回跳策略（与运行模式无关，基于步序值范围判断）
    /// ═══════════════════════════════════════════════════════════════════════
    ///
    ///   步序 < Pick(20)              → WaitMaterial  （取料前发生故障，从头等待物料）
    ///   Pick(20) ≤ 步序 < Place(40) → Pick           （取料中途，重新取料；回原点后吸盘已空）
    ///   步序 ≥ Place(40)            → WaitMaterial   （放料完成或在途，下一循环取新物料）
    ///
    /// ═══════════════════════════════════════════════════════════════════════
    /// </summary>
    [StationUI("取放工站调试", "PickPlaceStationDebugView", order: 1)]
    public class PickPlaceStation : StationBase
    {
        // ── 步序枚举（显式间隔整数值，便于将来插入中间步序）─────────────────
        // Init 步序已移至 ExecuteInitializeAsync，此处仅保留运行时步序。
        private enum PickPlaceStep
        {
            WaitMaterial     = 10,  // 等待上游物料到位（Normal 等真实信号；DryRun 模拟延迟）
            Pick             = 20,  // 执行取料（两种模式均调用真实轴运动）
            WaitSlotEmpty    = 30,  // 等待工作台槽位空闲（仅 Normal；DryRun 跳过此步）
            Place            = 40,  // 执行放料（两种模式均调用真实轴运动）
            NotifyDownstream = 50,  // 通知下游
        }

        private readonly GantryMechanism _gantry;
        private readonly IStationSyncService _sync;
        private int _cycleCount;
        private PickPlaceStep _currentStep = PickPlaceStep.WaitMaterial;

        public PickPlaceStation(GantryMechanism gantry, IStationSyncService sync, ILogService logger)
            : base("取放工站", logger)
        {
            _gantry = gantry;
            _sync   = sync;

            // 订阅模组报警事件：硬件故障通过此路径驱动本工站进入 Alarm 状态
            _gantry.AlarmTriggered += OnMechanismAlarm;
        }

        // ── 硬件初始化（生命周期第一阶段）──────────────────────────────────

        /// <summary>
        /// 由 MasterController.InitializeAllAsync() 顺序调用。
        /// 驱动本工站状态机：Uninitialized → Initializing → Idle（或 Alarm）。
        /// </summary>
        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {
                _logger.Info($"[{StationName}] 正在初始化龙门模组...");
                if (!await _gantry.InitializeAsync(token))
                    throw new Exception($"[{StationName}] 龙门模组初始化失败！");
                _logger.Success($"[{StationName}] 初始化完成，就绪。");
                Fire(MachineTrigger.InitializeDone); // Initializing → Idle
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }
        }

        // ── 主入口：按运行模式路由至独立循环 ────────────────────────────────

        /// <summary>
        /// 工艺入口路由，由 StationBase.ProcessWrapperAsync 在后台线程中调用。
        /// CurrentMode 在 Idle 状态下由 MasterController.SetMode() 固定，循环期间不会改变。
        /// 硬件已在 ExecuteInitializeAsync 中完成初始化，本方法不再重复执行。
        /// </summary>
        protected override async Task ProcessLoopAsync(CancellationToken token)
        {
            if (CurrentMode == OperationMode.Normal)
                await ProcessNormalLoopAsync(token);
            else if (CurrentMode == OperationMode.DryRun)
                await ProcessDryRunLoopAsync(token);
        }

        // ── Normal 模式：完整生产工艺循环 ───────────────────────────────────

        /// <summary>
        /// 完整生产工艺大循环：
        ///   等待真实物料信号 → 取料 → 等槽位（流水线协同）→ 放料 → 通知下游
        ///
        /// · 通过 _pauseEvent.Wait(token) 在关键步序前提供暂停窗口
        /// · 通过 _sync.WaitAsync / _sync.Release 与点胶工站实现流水线节拍协同
        /// · 不含任何 DryRun 判断，逻辑简洁清晰
        /// </summary>
        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    // ── WaitMaterial: 等待上游物料到位 ───────────────────────
                    case PickPlaceStep.WaitMaterial:
                        CurrentStepDescription = "等待上游物料到位...";
                        _pauseEvent.Wait(token); // ════ 暂停检查点 ① ════

                        _cycleCount++;
                        _logger.Info($"[{StationName}] ══ Cycle #{_cycleCount} 开始 ══");
                        _logger.Info($"[{StationName}] [1/5] 等待上游物料到位...");
                        await WaitForMaterialAsync(token);
                        _currentStep = PickPlaceStep.Pick;
                        break;

                    // ── Pick: 取料 ───────────────────────────────────────────
                    case PickPlaceStep.Pick:
                        CurrentStepDescription = "正在执行取料动作...";
                        _pauseEvent.Wait(token); // ════ 暂停检查点 ② ════

                        _logger.Info($"[{StationName}] [2/5] 执行取料动作...");
                        await _gantry.PickAsync(token);
                        _currentStep = PickPlaceStep.WaitSlotEmpty;
                        break;

                    // ── WaitSlotEmpty: 等待工作台槽位空闲（★ 流水线协同）────
                    case PickPlaceStep.WaitSlotEmpty:
                        CurrentStepDescription = "等待工作台槽位空闲...";
                        _pauseEvent.Wait(token); // ════ 暂停检查点 ③ ════

                        _logger.Info($"[{StationName}] [3/5] 等待工作台槽位空闲...");
                        await _sync.WaitAsync(WorkstationSignals.SlotEmpty, token);
                        _currentStep = PickPlaceStep.Place;
                        break;

                    // ── Place: 放料 + 通知点胶工站（★ 流水线协同）──────────
                    case PickPlaceStep.Place:
                        CurrentStepDescription = "正在执行放料动作...";
                        _logger.Info($"[{StationName}] [4/5] 执行放料动作（放入工作台）...");
                        await _gantry.PlaceAsync(token);

                        _sync.Release(WorkstationSignals.ProductReady);
                        _logger.Info($"[{StationName}] [5/5] 已通知点胶工站：产品到位");
                        _currentStep = PickPlaceStep.NotifyDownstream;
                        break;

                    // ── NotifyDownstream: 通知下游 ────────────────────────────
                    case PickPlaceStep.NotifyDownstream:
                        CurrentStepDescription = "正在通知下游工站...";
                        await NotifyDownstreamAsync(token);
                        _logger.Success($"[{StationName}] ══ Cycle #{_cycleCount} 完成 ══\n");
                        await Task.Delay(100, token);
                        _currentStep = PickPlaceStep.WaitMaterial;
                        break;
                }
            }
        }

        // ── DryRun 模式：空跑验证循环 ────────────────────────────────────────

        /// <summary>
        /// 空跑验证循环：模拟物料信号 → 执行真实取/放轴运动（验证轨迹） → 跳过流水线协同。
        ///
        /// 与 Normal 的差异：
        ///   · WaitMaterial  — 短延迟模拟物料到位，不等待真实传感器 IO
        ///   · WaitSlotEmpty — 完全跳过（Pick 后直接进入 Place），无下游工站联动需求
        ///   · Place         — 执行真实放料轴运动，但不 Release(ProductReady)
        ///
        /// 共享 <see cref="PickPlaceStep"/> 枚举值，保证 ExecuteResetAsync 回跳策略对两种模式均有效。
        /// </summary>
        protected override async Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    // ── WaitMaterial: 模拟物料到位（不等真实传感器）──────────
                    case PickPlaceStep.WaitMaterial:
                        CurrentStepDescription = "[DryRun] 模拟物料到位...";
                        _pauseEvent.Wait(token); // ════ 暂停检查点 ① ════

                        _cycleCount++;
                        _logger.Info($"[{StationName}] [DryRun] ══ Cycle #{_cycleCount} 开始 ══");
                        _logger.Info($"[{StationName}] [DryRun][1/4] 模拟物料到位（200 ms）");
                        await Task.Delay(200, token);
                        _currentStep = PickPlaceStep.Pick;
                        break;

                    // ── Pick: 执行真实取料轴运动（验证轨迹）────────────────
                    case PickPlaceStep.Pick:
                        CurrentStepDescription = "[DryRun] 正在验证取料轨迹...";
                        _pauseEvent.Wait(token); // ════ 暂停检查点 ② ════

                        _logger.Info($"[{StationName}] [DryRun][2/4] 执行取料轴运动（验证轨迹）...");
                        await _gantry.PickAsync(token);
                        // 跳过 WaitSlotEmpty（值 30）：空跑时无下游联动，直接进入放料
                        _currentStep = PickPlaceStep.Place;
                        break;

                    // ── Place: 执行真实放料轴运动（验证轨迹，不通知下游）────
                    case PickPlaceStep.Place:
                        CurrentStepDescription = "[DryRun] 正在验证放料轨迹...";
                        _logger.Info($"[{StationName}] [DryRun][3/4] 执行放料轴运动（验证轨迹）...");
                        await _gantry.PlaceAsync(token);
                        // 不 Release(ProductReady)：空跑时下游点胶工站不监听此信号量
                        _currentStep = PickPlaceStep.NotifyDownstream;
                        break;

                    // ── NotifyDownstream: 节拍间隙 ────────────────────────────
                    case PickPlaceStep.NotifyDownstream:
                        CurrentStepDescription = "[DryRun] 节拍间隙...";
                        _logger.Info($"[{StationName}] [DryRun][4/4] 节拍间隙...");
                        await NotifyDownstreamAsync(token);
                        _logger.Success($"[{StationName}] [DryRun] ══ Cycle #{_cycleCount} 完成 ══\n");
                        await Task.Delay(100, token);
                        _currentStep = PickPlaceStep.WaitMaterial;
                        break;
                }
            }
        }

        // ── 物理复位（覆写基类）─────────────────────────────────────────────

        /// <summary>
        /// 硬件复位 + 智能步序回跳。
        /// 回跳判断基于 PickPlaceStep 值范围，与 CurrentMode 无关，
        /// 对 Normal 和 DryRun 模式发生的故障均适用。
        ///
        ///   步序 &lt; Pick(20)              → WaitMaterial  （取料前，重新等待物料）
        ///   Pick(20) ≤ 步序 &lt; Place(40) → Pick           （取料中，重新取料）
        ///   步序 ≥ Place(40)            → WaitMaterial   （放料后，跳过取料直接等下一批）
        /// </summary>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            _logger.Warn($"[{StationName}] 开始物理复位，故障步序: {_currentStep}");

            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                // 1. 硬件复位：清除底层报警（轴卡、IO 卡等）
                await _gantry.ResetAsync(token);

                // 2. 重新初始化（回原点、关真空），使硬件回到安全初始状态
                if (!await _gantry.InitializeAsync(token))
                    _logger.Warn($"[{StationName}] 复位后初始化未完全成功，请检查硬件！");

                // 3. 智能回跳
                if (_currentStep >= PickPlaceStep.Place)
                {
                    _currentStep = PickPlaceStep.WaitMaterial;
                    _logger.Info($"[{StationName}] 步序回跳 → WaitMaterial（放料阶段故障，跳过取料）");
                }
                else if (_currentStep >= PickPlaceStep.Pick)
                {
                    _currentStep = PickPlaceStep.Pick;
                    _logger.Info($"[{StationName}] 步序回跳 → Pick（取料阶段故障，重试取料）");
                }
                else
                {
                    _currentStep = PickPlaceStep.WaitMaterial;
                    _logger.Info($"[{StationName}] 步序回跳 → WaitMaterial（取料前故障，重新等待物料）");
                }

                // 4. 工站状态机：Resetting → Idle
                await FireAsync(MachineTrigger.ResetDone);
                _logger.Success($"[{StationName}] 物理复位完成，就绪。");
            }
            catch (Exception)
            {
                Fire(MachineTrigger.Error);  // Resetting → Alarm，确保不卡死在 Resetting
                throw;
            }
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
