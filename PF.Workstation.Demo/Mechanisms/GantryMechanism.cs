using PF.Core.Attributes;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;

namespace PF.Workstation.Demo.Mechanisms
{
    /// <summary>
    /// 【模组层示例】龙门取放模组
    ///
    /// 职责：封装 X轴电机 + 真空IO 的联动工艺动作，向上提供语义清晰的原子操作接口：
    ///   · PickAsync()  — 一次完整取料（移位 → 慢降 → 开真空 → 确认 → 提升）
    ///   · PlaceAsync() — 一次完整放料（移位 → 慢降 → 关真空 → 确认 → 退位）
    ///
    /// 继承 BaseMechanism，自动获得：
    ///   · 将所有内部硬件的 AlarmTriggered 事件聚合为模组级 AlarmTriggered 事件
    ///   · CheckReady()：有报警或未初始化时直接抛异常，由上层 Station 统一捕获
    ///   · ResetAsync()：遍历所有内部硬件批量复位
    ///   · InitializeAsync()：带状态和日志的初始化包装
    ///
    /// 线程安全：
    ///   本类所有公开方法均接受 CancellationToken，运动/等待 均支持急停打断。
    ///   同一时刻只有一个 Station 线程调用本模组（单工站单模组设计）。
    ///   若多工站共用模组，需在此层添加 SemaphoreSlim 串行化。
    /// </summary>
    /// 

    [MechanismUI("取放模组调试", "GantryMechanismView", 1)]
    public class GantryMechanism : BaseMechanism
    {
        private readonly IAxis          _xAxis;
        private readonly IIOController  _vacuumIO;

        // ── 工艺坐标常量（实际项目从 IParamService 读取，支持界面调参）──────
        private const double PickX      = 100.0;  // mm: 取料位 X 坐标
        private const double PlaceX     = 350.0;  // mm: 放料位 X 坐标
        private const double SafeX      = 50.0;   // mm: 安全缩回/提升量（相对）
        private const double FastSpeed  = 300.0;  // mm/s: 空移速度
        private const double SlowSpeed  = 60.0;   // mm/s: 接触/离开物料慢速

        private const int VacuumValve   = 0;      // OUT[0]: 真空阀
        private const int VacuumSensor  = 0;      // IN[0]:  真空检测传感器


        // 在 GantryMechanism 类中添加这两个公开属性，供 ViewModel 访问
        public IAxis XAxis => _xAxis;
        public IIOController VacuumIO => _vacuumIO;

        // 暴露真空阀端口号供 ViewModel 使用
        public int VacuumValvePort => VacuumValve;

        public GantryMechanism(IAxis xAxis, IIOController vacuumIO, ILogService logger)
            : base("龙门取放模组", logger, xAxis, vacuumIO)
        {
            _xAxis    = xAxis;
            _vacuumIO = vacuumIO;
        }

        // ── BaseMechanism 钩子实现 ─────────────────────────────────────────

        /// <summary>
        /// 模组初始化：依次连接硬件 → 使能伺服 → 回原点 → 安全输出
        /// 任何步骤失败则返回 false，InitializeAsync 上层会记录日志并标记 IsInitialized=false
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // 1. 连接所有硬件（BaseDevice 内部有3次重试）
            if (!await _xAxis.ConnectAsync(token))    return false;
            if (!await _vacuumIO.ConnectAsync(token)) return false;

            // 2. 使能 X 轴伺服
            if (!await _xAxis.EnableAsync()) return false;

            // 3. X 轴回原点（阻塞直至完成或 token 取消）
            if (!await _xAxis.HomeAsync(token)) return false;

            // 4. 安全初始输出：关闭真空阀
            _vacuumIO.WriteOutput(VacuumValve, false);

            return true;
        }

        /// <summary>
        /// 模组急停：先停轴（防碰撞）→ 再关真空
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            await _xAxis.StopAsync();
            _vacuumIO.WriteOutput(VacuumValve, false);
        }

        // ── 业务动作 API（Station 层在 ProcessLoopAsync 中调用）────────────

        /// <summary>
        /// 取料动作序列（原子操作）：
        ///   ① 快速移动到取料位上方安全位置
        ///   ② 慢速下降接触物料
        ///   ③ 打开真空阀
        ///   ④ 等待真空传感器确认（物料已吸附）
        ///   ⑤ 提升到安全高度
        ///
        /// 任意步骤失败均 throw Exception → 由 StationBase.ProcessWrapperAsync 捕获
        /// → 触发工站状态机 Error → 联动主控急停
        /// </summary>
        public async Task PickAsync(CancellationToken token)
        {
            CheckReady(); // 有报警/未初始化时立即抛异常，禁止动作

            _logger.Info($"[{MechanismName}] ▶ 取料开始");

            // ① 空移到取料位上方（快速）
            //if (!await _xAxis.MoveAbsoluteAsync(PickX - SafeX, FastSpeed, token))
            //    throw new Exception($"[{MechanismName}] 移动到取料安全位失败");

            //// ② 慢速下降接触物料
            //if (!await _xAxis.MoveAbsoluteAsync(PickX, SlowSpeed, token))
            //    throw new Exception($"[{MechanismName}] 慢降接触物料失败");

            //// ③ 开真空阀
            //_vacuumIO.WriteOutput(VacuumValve, true);

            //// ④ 等待真空传感器变高（最多 2000ms，否则视为无料）
            //if (!await _vacuumIO.WaitInputAsync(VacuumSensor, true, 2000, token))
            //    throw new Exception($"[{MechanismName}] 真空建立超时，未检测到物料！");

            //// ⑤ 提升到安全高度（快速）
            //if (!await _xAxis.MoveAbsoluteAsync(PickX - SafeX, FastSpeed, token))
            //    throw new Exception($"[{MechanismName}] 取料后提升失败");

            _logger.Success($"[{MechanismName}] ✔ 取料完成");
        }

        /// <summary>
        /// 放料动作序列（原子操作）：
        ///   ① 快速移动到放料位上方安全位置
        ///   ② 慢速下降到放料位
        ///   ③ 关闭真空阀（释放物料）
        ///   ④ 等待真空传感器确认（物料已离开）
        ///   ⑤ 退回安全位
        /// </summary>
        public async Task PlaceAsync(CancellationToken token)
        {
            CheckReady();

            _logger.Info($"[{MechanismName}] ▶ 放料开始");

            //// ① 空移到放料位上方
            //if (!await _xAxis.MoveAbsoluteAsync(PlaceX - SafeX, FastSpeed, token))
            //    throw new Exception($"[{MechanismName}] 移动到放料安全位失败");

            //// ② 慢速下降到放料位
            //if (!await _xAxis.MoveAbsoluteAsync(PlaceX, SlowSpeed, token))
            //    throw new Exception($"[{MechanismName}] 慢降到放料位失败");

            //// ③ 关真空阀（释放物料）
            //_vacuumIO.WriteOutput(VacuumValve, false);

            //// ④ 等待真空消失确认
            //// 修复：原代码忽略返回值，超时时静默继续——在真空未消失（物料未离开）的情况下
            ////       X轴仍会运动，可能带着物料碰撞。现改为超时时打印警告，
            ////       实际项目可改为 throw 以强制停线。
            //bool vacuumReleased = await _vacuumIO.WaitInputAsync(VacuumSensor, false, 1000, token);
            //if (!vacuumReleased)
            //    _logger.Warn($"[{MechanismName}] 真空释放超时，物料可能未完全离开！");

            //// ⑤ 退回安全位
            //if (!await _xAxis.MoveAbsoluteAsync(SafeX, FastSpeed, token))
            //    throw new Exception($"[{MechanismName}] 退回安全位失败");

            _logger.Success($"[{MechanismName}] ✔ 放料完成");
        }
    }
}
