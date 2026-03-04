using PF.Core.Attributes;
using PF.Core.Interfaces.Device.Hardware;
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
    /// 硬件获取策略（代理委托模式）：
    ///   构造函数仅注入 IHardwareManagerService 和 ILogService，不依赖任何具体设备类型。
    ///   实际硬件实例在 InternalInitializeAsync 中通过 DeviceId 延迟解析，
    ///   确保在 HardwareManagerService.LoadAndInitializeAsync() 完成后才访问设备。
    ///   更换硬件品牌时，只需在 DefaultParameters / HardwareManagerService 中修改工厂注册，
    ///   无需改动本模组代码。
    ///
    /// 线程安全：
    ///   本类所有公开方法均接受 CancellationToken，运动/等待 均支持急停打断。
    ///   同一时刻只有一个 Station 线程调用本模组（单工站单模组设计）。
    ///   若多工站共用模组，需在此层添加 SemaphoreSlim 串行化。
    /// </summary>
    [MechanismUI("取放模组调试", "GantryMechanismView", 1)]
    public class GantryMechanism : BaseMechanism
    {
        private readonly IHardwareManagerService _hwManager;

        // 硬件实例：构造后为 null，在 InternalInitializeAsync 中通过 hwManager 延迟解析
        private IAxis         _xAxis;
        private IIOController _vacuumIO;

        // ── 工艺坐标常量（实际项目从 IParamService 读取，支持界面调参）──────
        private const double PickX     = 100.0;  // mm: 取料位 X 坐标
        private const double PlaceX    = 350.0;  // mm: 放料位 X 坐标
        private const double SafeX     = 50.0;   // mm: 安全缩回/提升量（相对）
        private const double FastSpeed = 300.0;  // mm/s: 空移速度
        private const double SlowSpeed = 60.0;   // mm/s: 接触/离开物料慢速

        private const int VacuumValve  = 0;      // OUT[0]: 真空阀
        private const int VacuumSensor = 0;      // IN[0]:  真空检测传感器

        /// <summary>
        /// X 轴实例（InitializeAsync 完成后可用）。
        /// 在初始化之前为 null；ViewModel 轮询时已有 null 保护，安全访问。
        /// </summary>
        public IAxis XAxis => _xAxis;

        /// <summary>真空 IO 实例（InitializeAsync 完成后可用）</summary>
        public IIOController VacuumIO => _vacuumIO;

        /// <summary>真空阀端口号，供 ViewModel 使用</summary>
        public int VacuumValvePort => VacuumValve;

        /// <summary>
        /// 构造函数：仅注入硬件管理服务和日志服务。
        /// 不在此处获取设备实例——设备需在 LoadAndInitializeAsync 完成后才可用，
        /// 通过 InternalInitializeAsync 延迟解析。
        /// </summary>
        public GantryMechanism(IHardwareManagerService hwManager, ILogService logger)
            : base("龙门取放模组", logger)  // 无设备参数；设备将在 InternalInitializeAsync 中注册
        {
            _hwManager = hwManager ?? throw new ArgumentNullException(nameof(hwManager));
        }

        // ── BaseMechanism 钩子实现 ─────────────────────────────────────────

        /// <summary>
        /// 模组初始化：
        ///   ① 通过 IHardwareManagerService 延迟解析 X轴 和 真空IO 实例（DeviceId 与配置对应）
        ///   ② 通过 RegisterHardwareDevice 将设备注册到模组（报警聚合 + 批量复位）
        ///   ③ 连接硬件 → 使能伺服 → 回原点 → 安全输出
        ///
        /// 调用前提：HardwareManagerService.LoadAndInitializeAsync() 已完成。
        /// 任何步骤失败则返回 false，InitializeAsync 上层会记录日志并标记 IsInitialized=false。
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析：DeviceId 与 DefaultParameters.GetHardwareDefaults() 保持一致
            _xAxis = _hwManager.GetDevice("SIM_X_AXIS_0") as IAxis;
            if (_xAxis == null)
            {
                _logger.Error("[GantryMechanism] 未找到X轴 'SIM_X_AXIS_0'，请确认硬件配置或 HardwareManagerService 已完成初始化。");
                return false;
            }

            _vacuumIO = _hwManager.GetDevice("SIM_VACUUM_IO") as IIOController;
            if (_vacuumIO == null)
            {
                _logger.Error("[GantryMechanism] 未找到IO 'SIM_VACUUM_IO'，请确认硬件配置或 HardwareManagerService 已完成初始化。");
                return false;
            }

            // ② 注册到模组：报警事件聚合 + 批量复位（幂等，重复调用不会重复订阅）
            RegisterHardwareDevice(_xAxis    as IHardwareDevice);
            RegisterHardwareDevice(_vacuumIO as IHardwareDevice);

            // ③ 连接所有硬件（BaseDevice 内部有3次重试）
            if (!await _xAxis.ConnectAsync(token))    return false;
            if (!await _vacuumIO.ConnectAsync(token)) return false;

            // ④ 使能 X 轴伺服
            if (!await _xAxis.EnableAsync()) return false;

            // ⑤ X 轴回原点（阻塞直至完成或 token 取消）
            if (!await _xAxis.HomeAsync(token)) return false;

            // ⑥ 安全初始输出：关闭真空阀
            _vacuumIO.WriteOutput(VacuumValve, false);

            return true;
        }

        /// <summary>
        /// 模组急停：先停轴（防碰撞）→ 再关真空
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            if (_xAxis    != null) await _xAxis.StopAsync();
            if (_vacuumIO != null) _vacuumIO.WriteOutput(VacuumValve, false);
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
