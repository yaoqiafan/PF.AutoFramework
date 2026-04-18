using log4net.Appender;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PF.Core.Attributes;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 【工位1】晶圆上料模组 (Wafer Feeding Mechanism)
    /// 
    /// <para>物理架构：</para>
    /// 包含一个控制料盒整体升降的 Z 轴（用于层级对准与高速硬件扫描）、一个控制前挡板的 X 轴（用于适配不同尺寸料盒），
    /// 以及底部的光电传感器组（用于料盒到位检测与尺寸防呆）。
    /// 
    /// <para>软件职责：</para>
    /// 封装三轴与 IO 的底层联动逻辑，屏蔽硬件底层的复杂性，向上层工站状态机提供基于“业务语境”的原子操作接口。
    /// 采用硬件延迟加载策略（Proxy 模式），确保系统在 <see cref="InternalInitializeAsync(CancellationToken)"/> 阶段
    /// 完整装载底层驱动后才建立设备句柄。
    /// </summary>
    /// <remarks>
    /// 💡 【标准工艺调用时序】
    /// 
    /// 本模组继承自 <see cref="BaseMechanism"/>，上层主控在调度时应遵循以下安全时序：
    /// 
    /// 1. 状态复位：<see cref="InitializeFeedingStateAsync(CancellationToken)"/> -> 机构退回安全待机避让位。
    /// 2. 尺寸识别：<see cref="GetWaferBoxSizeAsync(CancellationToken)"/> -> 底部传感器交叉验证尺寸（8寸/12寸/异常）。
    /// 3. 配方加载：<see cref="SwitchProductionStateAsync(E_WafeSize, CancellationToken)"/> -> 载入尺寸对应的间距参数并推演生成全层坐标阵列。
    ///    ⚠️ 关键：必须执行此步骤，否则后续寻层与定位将抛出 <see cref="NullReferenceException"/>。
    /// 4. 寻层扫描：前置判断 <see cref="CanMoveZAxesAsync(CancellationToken)"/> 后，调用 <see cref="SearchLayerAsync(int, int, int, int, CancellationToken)"/>。
    /// 5. 数据过滤：调用 <see cref="AnalyzeAndFilterMappingData(Dictionary{int, List{double}})"/>，将扫描原始数据剔除斜片、假触发，生成有效层级映射。
    /// 6. 定位取料：调用 <see cref="SwitchToLayerAsync(int, CancellationToken)"/> 到达目标层级后，交由外部机械手拉料。
    /// </remarks>
    [MechanismUI("工位1上晶圆模组", "Workstation1FeedingModelDebugView", 1)]
    public class WorkStation1FeedingModule : BaseMechanism
    {
        #region Enums (轴关键点位枚举)

        /// <summary>
        /// 定义Z轴（升降轴）的关键示教点位。
        /// 对应底层点位表配置，需确保名称在配置工具中完全一致。
        /// </summary>
        public enum ZAxisPoint
        {
            /// <summary>扫描结束位置_8寸</summary>
            扫描结束位置_8寸,
            /// <summary>扫描起始位置_8寸</summary>
            扫描起始位置_8寸,
            /// <summary>扫描结束位置_12寸</summary>
            扫描结束位置_12寸,
            /// <summary>扫描起始位置_12寸</summary>
            扫描起始位置_12寸,
            /// <summary>待机位</summary>
            待机位,            // 默认安全高度，通常在机械最高点以避让机械手
            /// <summary>层1取料位_8寸</summary>
            层1取料位_8寸,    // 8寸料盒第1层晶圆的绝对坐标基准点（用于阵列推演取料点）
            /// <summary>层1取料位_12寸</summary>
            层1取料位_12寸,   // 12寸料盒第1层晶圆的绝对坐标基准点（用于阵列推演取料点）
            /// <summary>层1扫描点位_8寸</summary>
            层1扫描点位_8寸,  // 8寸料盒第一层扫描基准点（用于阵列推演比对点）
            /// <summary>层1扫描点位_12寸</summary>
            层1扫描点位_12寸, // 12寸料盒第一层扫描基准点（用于阵列推演比对点）
        }

        /// <summary>
        /// 定义X轴（挡料/调宽轴）的关键示教点位
        /// </summary>
        public enum XAxisPoint
        {
            /// <summary>待机位</summary>
            待机位,            // 挡料机构退回的安全位置，不干涉上下料
            /// <summary>挡料位</summary>
            挡料位,            // 适应晶圆盒宽度的挡料位置，防止拉料时料盒位移
        }

        #endregion

        #region Fields & Properties (硬件实例与配方变量)

        // ── 底层硬件实例（延迟加载） ──
        private IAxis _zAxis;      // 工位1上料Z轴：控制料盒整体升降，实现层级对准与扫描
        private IAxis _xAxis;      // 工位1挡料X轴：控制前挡料机构位置（适应8/12寸料盒差异）
        private IIOController _io; // EtherCat IO 模块：负责读取底座传感器状态与控制气缸

        // ── 生产配方状态变量 ──

        /// <summary>
        /// 当前正在生产的晶圆尺寸，由底部传感器物理识别后动态赋值。
        /// 决定后续调用的参数体系（如层距、最大层数、基准点位）。
        /// </summary>
        private E_WafeSize _currentWaferSize = E_WafeSize._8寸;

        /// <summary>
        /// 当前料盒的理论最大层数。
        /// 标准半导体料盒通常为13层或25层，作为阵列推演的最大循环边界。
        /// </summary>
        private int _maxLayerCount = 13;

        /// <summary>
        /// 晶圆层间距 (Pitch)。
        /// 由云端或本地配方系统下发，是 <see cref="ArrayZAxisMaterialPickingPosition"/> 方法进行等差坐标推演的核心步长。
        /// </summary>
        private double LayerPitch = 0;

        // ── 轴阵列点位缓存集合 ──

        /// <summary>8寸料盒：推演出的每一层实际水平拉料的 Z 轴绝对坐标字典 (Key=层索引, Value=坐标属性)</summary>
        public readonly ConcurrentDictionary<int, AxisPoint> PickingPosition_8;
        /// <summary>12寸料盒：推演出的每一层实际水平拉料的 Z 轴绝对坐标字典</summary>
        public readonly ConcurrentDictionary<int, AxisPoint> PickingPosition_12;
        /// <summary>8寸料盒：推演出的用于和寻层锁存数据比对的标准槽位理论坐标</summary>
        public readonly ConcurrentDictionary<int, AxisPoint> ScanPosition_8;
        /// <summary>12寸料盒：推演出的用于和寻层锁存数据比对的标准槽位理论坐标</summary>
        public readonly ConcurrentDictionary<int, AxisPoint> ScanPosition_12;

        // ── 公开硬件绑定属性 (供 ViewModel/UI 调试面板使用) ──
        /// <summary>获取Z轴实例</summary>
        public IAxis ZAxis => _zAxis;
        /// <summary>获取X轴实例</summary>
        public IAxis XAxis => _xAxis;
        /// <summary>获取IO控制器实例</summary>
        public IIOController IO => _io;

        #endregion

        #region Constructor & Framework Hooks (构造与生命周期)

        /// <summary>
        /// 实例化晶圆上料模组
        /// </summary>
        public WorkStation1FeedingModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger)
            : base(E_Mechanisms.工位1上晶圆模组.ToString(), hardwareManagerService, paramService, logger)
        {
            PickingPosition_8 = new ConcurrentDictionary<int, AxisPoint>();
            PickingPosition_12 = new ConcurrentDictionary<int, AxisPoint>();
            ScanPosition_8 = new ConcurrentDictionary<int, AxisPoint>();
            ScanPosition_12 = new ConcurrentDictionary<int, AxisPoint>();
        }

        /// <summary>
        /// 模组初始化核心逻辑：延迟解析硬件、注册报警聚合防线、连接/使能/回零
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例
            _zAxis = HardwareManagerService?.GetDevice(E_AxisName.工位1上料Z轴.ToString()) as IAxis;
            if (_zAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Z轴 '{E_AxisName.工位1上料Z轴}'，请确认硬件配置。");
                return false;
            }

            _xAxis = HardwareManagerService?.GetDevice(E_AxisName.工位1挡料X轴.ToString()) as IAxis;
            if (_xAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到X轴 '{E_AxisName.工位1挡料X轴}'，请确认硬件配置。");
                return false;
            }

            _io = HardwareManagerService?.GetDevice("IO_Collectorll") as IIOController;
            if (_io == null)
            {
                _logger.Error($"[{MechanismName}] 未找到IO模块 'IO_Collectorll'，请确认硬件配置。");
                return false;
            }

            // ② 注册报警聚合：单一硬件故障自动拉停整个 Mechanism
            RegisterHardwareDevice(_zAxis as IHardwareDevice);
            RegisterHardwareDevice(_xAxis as IHardwareDevice);
            RegisterHardwareDevice(_io as IHardwareDevice);

            // 确认所需点位在配置中是否完备
            await ConfirmEunmPoints();

            // ③ 建立底层物理通信连接
            if (!await _zAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            if (!await _xAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] X轴连接失败"); return false; }
            if (!await _io.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] IO模块连接失败"); return false; }

            // ④ 伺服上电使能 (Servo On)
            if (!await _zAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }

            // ⑤ 异常待处理：回原点 (Home) 前需检查传感器确认物料是否处于安全位置，防止硬碰撞。
            // if (!await _zAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Z轴回零失败"); return false; }
            // if (!await _xAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] X轴回零失败"); return false; }

            return true;
        }

        /// <summary>
        /// 模组急停/停止钩子：安全阻断运动
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            if (_zAxis != null) await _zAxis.StopAsync();
            if (_xAxis != null) await _xAxis.StopAsync();
        }

        #endregion

        #region Core Business Process (核心业务指令流)

        /// <summary>
        /// 0. 初始化上料状态：将 Z轴 与 X轴 插补至待机位，准备迎接人工或AGV放料
        /// </summary>
        public async Task<bool> InitializeFeedingStateAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 初始化上料状态...");

            // 并发控制指令：多轴插补运动提升设备初始化节拍 (Cycle Time)
            if (await MoveMultiAxesToPointsAsync(new[] {
                    (_xAxis, nameof(XAxisPoint.挡料位)),
                    (_zAxis, nameof(ZAxisPoint.待机位))
                }, token: token))
            {
                _logger.Success($"[{MechanismName}] 上料状态初始化完成。");
                return true;
            }
            else
            {
                _logger.Warn($"[{MechanismName}] 上料状态初始化失败。");
                return false;
            }
        }

        /// <summary>
        /// 1. 识别晶圆料盒尺寸 (防呆设计)
        /// 通过底部光电传感器的组合状态来交叉验证判定 8寸 / 12寸 / 放置异常。
        /// </summary>
        public async Task<E_WafeSize> GetWaferBoxSizeAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 检测晶圆料盒尺寸...");

            // 变量解析：读取三个特征物理传感器的布尔值
            bool iscom = _io.ReadInput(E_InPutName.上晶圆左料盒公用到位检测) == true;   // 公共底座是否受压（是否有料盒）
            bool is8inch = _io.ReadInput(E_InPutName.上晶圆左8寸料盒到位检测) == true;  // 8寸料盒特征感应位
            bool is12inch = _io.ReadInput(E_InPutName.上晶圆左12寸料盒到位检测) == true;// 12寸料盒特征感应位

            if (iscom)
            {
                // 逻辑分支：互斥判定尺寸
                if (is8inch && !is12inch)
                {
                    _logger.Success($"[{MechanismName}] 识别到 8寸 晶圆料盒。");
                    _currentWaferSize = E_WafeSize._8寸;
                    return E_WafeSize._8寸;
                }
                else if (is12inch && !is8inch)
                {
                    _logger.Success($"[{MechanismName}] 识别到 12寸 晶圆料盒。");
                    _currentWaferSize = E_WafeSize._12寸;
                    return E_WafeSize._12寸;
                }
                else
                {
                    // 异常分支：两个都亮或都不亮，代表料盒倾斜、放歪或传感器损坏（防反、防错位）
                    throw new Exception($"[{MechanismName}] 料盒尺寸识别异常（8寸={is8inch}, 12寸={is12inch}），请检查传感器或料盒放置是否倾斜。");
                }
            }
            else
            {
                throw new Exception($"[{MechanismName}] 晶圆料盒公用底座未检测到物体，请检查料盒是否正确放入。");
            }
        }

        /// <summary>
        /// 2. 切换生产状态：拉取对应尺寸配方，计算全层坐标阵列
        /// </summary>
        /// <param name="waferSize">目标生产的晶圆尺寸</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool> SwitchProductionStateAsync(E_WafeSize waferSize, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 切换生产状态为 [{waferSize}]...");
            _currentWaferSize = waferSize;

            // 根据尺寸动态提取对应的工艺参数（层距），并重算坐标系
            if (waferSize == E_WafeSize._8寸)
            {
                LayerPitch = await ParamService.GetParamAsync<double>(E_Params.LayerPitch_8.ToString());
                await ArrayZAxisMaterialPickingPosition(E_WafeSize._8寸);
            }
            else if (waferSize == E_WafeSize._12寸)
            {
                LayerPitch = await ParamService.GetParamAsync<double>(E_Params.LayerPitch_12.ToString());
                await ArrayZAxisMaterialPickingPosition(E_WafeSize._12寸);
            }

            _logger.Success($"[{MechanismName}] 生产状态已切换为 [{waferSize}]。");
            return true;
        }

        /// <summary>
        /// 5. 切换目标层：驱动 Z轴 精准定位到指定的层绝对坐标
        /// </summary>
        /// <param name="targetLayer">目标层索引 (0 代表第1层)</param>
        /// <param name="token">取消令牌</param>
        public async Task<bool> SwitchToLayerAsync(int targetLayer, CancellationToken token = default)
        {
            CheckReady();

            // 边界守卫：防止越界访问引发集合异常
            if (targetLayer < 0 || targetLayer >= _maxLayerCount)
            {
                _logger.Error($"[{MechanismName}] 目标层数 {targetLayer} 超出有效范围 (0 ~ {_maxLayerCount - 1})。");
                return false;
            }

            // 执行核心防撞互锁检查
            if (!await CanMoveZAxesAsync(token)) return false;

            _logger.Info($"[{MechanismName}] 准备切换至第 {targetLayer + 1} 层...");

            // 从缓存字典中提取由 ArrayZAxisMaterialPickingPosition 算好的特定层取料点位
            var targetPoint = await GetZAxisMaterialPickingPosition(targetLayer, _currentWaferSize);
            if (targetPoint == null)
            {
                _logger.Error($"[{MechanismName}] 未找到第 {targetLayer + 1} 层的阵列点位，可能未执行生产状态切换。");
                return false;
            }

            // 驱动 Z 轴进行绝对位置运动，并复用示教点内的运动曲线参数 (Speed/Acc/Dec)
            bool moveResult = await MoveAbsAndWaitAsync(_zAxis, targetPoint.TargetPosition, _zAxis.Param.Vel, _zAxis.Param.Acc, _zAxis.Param.Dec, 0.08, token: token);
            if (moveResult)
            {
                _logger.Success($"[{MechanismName}] 成功切换至第 {targetLayer + 1} 层位置。");
            }
            return moveResult;
        }

        #endregion

        #region Safety Interlocks (安全互锁守卫)

        /// <summary>
        /// Z轴安全互锁：判断升降条件，防止顶翻料盒或切割机械手
        /// </summary>
        public async Task<bool> CanMoveZAxesAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized || _zAxis.HasAlarm)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：模组未初始化或处于报警状态。");
                return false;
            }

            // 【刚性互锁】料盒必须完全落座。如果料盒悬空，Z轴强行升降会将其顶翻引发碎片。
            if (_io.ReadInput(E_InPutName.上晶圆左料盒公用到位检测) != true)
            {
                _logger.Warn($"[{MechanismName}] Z轴运动检查失败：料盒未到位，禁止升降以免倾覆。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// X轴安全互锁：判断挡料条件，防止挤碎晶圆
        /// </summary>
        public async Task<bool> CanMoveXAxesAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized || _xAxis.HasAlarm)
            {
                _logger.Warn($"[{MechanismName}] X轴运动检查失败：模组未初始化或处于报警状态。");
                return false;
            }

            // 【刚性互锁】晶圆夹爪闭合时（可能夹着晶圆），X挡板伸缩会导致机械干涉撞击
            if (_io.ReadInput(E_InPutName.晶圆夹爪左气缸张开) != true)
            {
                _logger.Warn($"[{MechanismName}] X轴运动检查失败：夹爪未张开，禁止X轴运动以防夹碎晶圆。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 拉料安全互锁：检查是否允许外部机构执行水平抽料动作
        /// </summary>
        public async Task<bool> CanPullOutMaterialAsync(CancellationToken token = default)
        {
            await Task.CompletedTask;

            if (!IsInitialized)
            {
                _logger.Warn($"[{MechanismName}] 拉料检查失败：模组未初始化。");
                return false;
            }
            // 预留区：后续补充夹爪闭合状态、料片真空吸附检测等逻辑...
            return true;
        }

        #endregion

        #region Wafer Mapping & Data Processing (硬件扫描与错层过滤)

        /// <summary>
        /// 4. 硬件级双通道锁存扫描 (High-Speed Position Capture)
        /// 控制Z轴匀速运动，底层运动板卡通过双传感器硬中断高速记录晶圆触发的 Z 轴物理坐标，
        /// 确保扫描精度不受到上位机软件轮询周期与通讯延迟的影响。
        /// </summary>
        public async Task<Dictionary<int, List<double>>> SearchLayerAsync(
            int latchNo1 = 0, int inputPort1 = 0,
            int latchNo2 = 1, int inputPort2 = 1,
            CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 开始执行Z轴双传感器高速锁存寻层扫描...");

            // 结果集变量：使用字典按照锁存通道号独立存放两侧传感器的原始触发高度集合
            var resultMap = new Dictionary<int, List<double>>
            {
                { latchNo1, new List<double>() },
                { latchNo2, new List<double>() }
            };

            try
            {
                // Step 1: 解析并移动至扫描物理起点（一般为 Z 轴最下方的负极限附近）
                var start = _currentWaferSize == E_WafeSize._12寸 ? nameof(ZAxisPoint.扫描起始位置_12寸) : nameof(ZAxisPoint.扫描起始位置_8寸);
                _logger.Info($"[{MechanismName}] 正在移动至扫描起点（负极限）...");
                if (!await _zAxis.MoveToPointAsync(start, token: token)) throw new Exception($"移动到 {start} 位置触发失败");
                if (!await WaitAxisMoveDoneAsync(_zAxis, token: token)) throw new Exception($"移动到 {start} 位置超时");

                // Step 2: 根据料盒尺寸映射实际传感器硬件端口，并向底层运动板卡下发高速锁存配置 (Latch Mode)
                _logger.Info($"[{MechanismName}] 正在配置硬件锁存参数 (通道 {latchNo1} 和 通道 {latchNo2})...");
                inputPort2 = _currentWaferSize == E_WafeSize._12寸 ? (int)E_InPutName.上晶圆左错层12寸检测 : (int)E_InPutName.上晶圆左错层8寸检测;

                // latch1Set / latch2Set: 底层板卡命令执行的回执，确保开启中断模式成功
                bool latch1Set = await _zAxis.SetLatchMode(LatchNo: latchNo1, InPutPort: inputPort1, LtcMode: 1, LtcLogic: 1, Filter: 1.0, LatchSource: 0, token: token);
                bool latch2Set = await _zAxis.SetLatchMode(LatchNo: latchNo2, InPutPort: inputPort2, LtcMode: 1, LtcLogic: 1, Filter: 1.0, LatchSource: 0, token: token);

                if (!latch1Set || !latch2Set) throw new Exception("底层运动控制卡位置锁存(Position Capture)配置失败！");

                // Step 3: 开始由下至上匀速扫测，触发传感器记录
                _logger.Info($"[{MechanismName}] 开始向上匀速扫描...");
                var end = _currentWaferSize == E_WafeSize._12寸 ? nameof(ZAxisPoint.扫描结束位置_12寸) : nameof(ZAxisPoint.扫描结束位置_8寸);
                if (!await _zAxis.MoveToPointAsync(end, token: token)) throw new Exception($"移动到 {end} 位置触发失败");
                if (!await WaitAxisMoveDoneAsync(_zAxis, token: token)) throw new Exception($"移动到 {end} 位置超时");

                // Step 4: 运动结束后，遍历双通道，读取底层板卡 FIFO 缓存的坐标数据并封装进 resultMap
                int[] latchChannels = { latchNo1, latchNo2 }; // 锁存通道ID数组
                foreach (var latchId in latchChannels)
                {
                    // latchCount: 当前通道在整个扫描过程中发生的触发总次数
                    int latchCount = await _zAxis.GetLatchNumber(latchId, token);
                    _logger.Info($"[{MechanismName}] 锁存通道 {latchId} 共捕获到 {latchCount} 个信号点。");

                    for (int i = 0; i < latchCount; i++)
                    {
                        double? pos = await _zAxis.GetLatchPos(latchId, token);
                        if (pos.HasValue) resultMap[latchId].Add(pos.Value);
                        else _logger.Warn($"[{MechanismName}] 通道 {latchId} 读取第 {i + 1} 个位置失败。");
                    }
                }

                _logger.Success($"[{MechanismName}] 扫描完毕。通道1识别 {resultMap[latchNo1].Count} 层，通道2识别 {resultMap[latchNo2].Count} 层。");

                // 本地化存根，用于复盘分析传感器的稳定性和跳动差
                SavePoint($"D://ScanPoint//{DateTime.Now.Year}//{DateTime.Now.Month}//{DateTime.Now.Day}//{DateTime.Now:yyyyMMddHHmmss}.xlsx", resultMap);

                return resultMap;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{MechanismName}] 扫描发生异常: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 4.1 寻层防呆算法 (Cross-slot / Double-wafer Validation)
        /// 融合双传感器原始数据，与生成的理论阵列高度对比。严格剔除斜放晶圆、单边假触发及叠片故障。
        /// </summary>
        /// <param name="rawMappingData"> <see cref="SearchLayerAsync"/> 返回的双传感器原始锁存数据集</param>
        public async Task<Dictionary<int, double>> AnalyzeAndFilterMappingData(Dictionary<int, List<double>> rawMappingData)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 开始执行寻层数据的过滤与防呆验证...");

            // validWafers: 过滤分析后的有效结果集 (Key: 逻辑层数索引 0-12, Value: 该层实际供后续取料的绝对高度)
            var validWafers = new Dictionary<int, double>();

            // 提取基于配方生成的理论参考点集合
            var ScanPositions = _currentWaferSize == E_WafeSize._8寸 ? ScanPosition_8 : ScanPosition_12; // 理论扫描参考高度
            var pickpoints = _currentWaferSize == E_WafeSize._8寸 ? PickingPosition_8 : PickingPosition_12; // 理论取料参考高度

            if (ScanPositions == null || ScanPositions.Count == 0)
                throw new Exception("理论层坐标未初始化，请先执行 SwitchProductionStateAsync 切换生产状态！");

            if (rawMappingData.Keys.Count < 2)
                throw new Exception("寻层计算失败，传感器原始数据缺失");

            // 剥离双通道独立数据
            var sensor1Data = rawMappingData[rawMappingData.Keys.ElementAt(0)]; // 左侧传感器原始数据列表
            var sensor2Data = rawMappingData[rawMappingData.Keys.ElementAt(1)]; // 右侧传感器原始数据列表

            // [防呆1：总体数量宏观比对] 左右识别总数不应相差超过1，否则意味着大面积的漏片或假信号
            if (Math.Abs(sensor1Data.Count - sensor2Data.Count) > 1)
            {
                throw new Exception($"[{MechanismName}] 识别数量差异过大(S1:{sensor1Data.Count}, S2:{sensor2Data.Count})，疑似斜片或传感器失效！");
            }

            // slotMatchTolerance: 槽位匹配容差，设定为层距(Pitch)的40%，严防坐标漂移导致匹配到上下相邻槽位
            double slotMatchTolerance = LayerPitch * 0.4;

            // SameLayerMaximum: 同一层晶圆两侧传感器捕捉高度的最大允许高度差。超出代表晶圆放斜了(Cross-slot)
            int SameLayerMaximum = _currentWaferSize == E_WafeSize._8寸
                ? await ParamService.GetParamAsync<int>(E_Params.SameLayerMaximum_8.ToString())
                : await ParamService.GetParamAsync<int>(E_Params.SameLayerMaximum_12.ToString());

            // [防呆2：斜片验证与双端数据融合]
            List<double> mergedRawPositions = new List<double>(); // 融合后的单片晶圆中心绝对高度集合
            foreach (var z1 in sensor1Data)
            {
                // closestZ2: 在 Sensor2 的集合中，寻找距离 z1 最近的那个高度值
                var closestZ2 = sensor2Data.OrderBy(z2 => Math.Abs(z2 - z1)).FirstOrDefault();

                if (closestZ2 != 0 && Math.Abs(z1 - closestZ2) <= SameLayerMaximum)
                {
                    // 高度差在阈值内，视为同一片平整晶圆，取两端高度的算术平均值作为真实的物理中心
                    mergedRawPositions.Add((z1 + closestZ2) / 2.0);
                    sensor2Data.Remove(closestZ2); // 匹配成功后移出待选池
                }
                else
                {
                    throw new Exception("Mapping 失败：检测到严重斜片(Cross-slot)异常！");
                }
            }

            // [防呆3 & 防呆4：重叠片与脱靶验证] 将融合后的实际坐标与理论槽位执行映射绑定
            foreach (var rawZ in mergedRawPositions)
            {
                double actualZ = rawZ; // 当前遍历分析的这片晶圆的实际融合高度
                bool matched = false;  // 标记该实际高度是否成功归属到某个标准理论层

                for (int layerIndex = 0; layerIndex < _maxLayerCount; layerIndex++)
                {
                    if (ScanPositions.TryGetValue(layerIndex, out var theoreticalPoint) &&
                        pickpoints.TryGetValue(layerIndex, out var pickpos))
                    {
                        double theoreticalZ = theoreticalPoint.TargetPosition; // 该层的标准理论高度

                        // 如果实际高度落在了理论高度的容差范围内（±40%层距）
                        if (Math.Abs(actualZ - theoreticalZ) <= Math.Abs(slotMatchTolerance))
                        {
                            // 防呆3：如果该逻辑层已经有绑定数据了，说明同一个物理槽位塞进了两片晶圆
                            if (validWafers.ContainsKey(layerIndex))
                            {
                                throw new Exception($"Mapping 失败：第 {layerIndex + 1} 层发生重叠片(Double-wafer)异常！");
                            }
                            // 绑定成功，存入对应的取料高度
                            validWafers.Add(layerIndex, pickpos.TargetPosition);
                            matched = true;
                            break; // 跳出内循环，处理下一片
                        }
                    }
                }

                // 防呆4：这片晶圆悬在半空，无法落入任何标准层的容差区间
                if (!matched)
                {
                    throw new Exception($"Mapping 失败：检测到晶圆 Z:{actualZ} 严重偏离标准槽位(可能未插到底)！");
                }
            }

            _logger.Success($"[{MechanismName}] 数据过滤完成，实际有效晶圆共 {validWafers.Count} 片。");
            return validWafers;
        }

        /// <summary>
        /// 私有辅助方法：利用 NPOI 将锁存生成的坐标原始数据写入本地 Excel 表格，方便算法工程师溯源。
        /// </summary>
        private void SavePoint(string FilePath, Dictionary<int, List<double>> point)
        {
            FileInfo file = new FileInfo(FilePath);
            if (!Directory.Exists(file.DirectoryName)) Directory.CreateDirectory(file.DirectoryName);

            using (XSSFWorkbook wk = new XSSFWorkbook())
            {
                int count = 0;
                ISheet sheet = wk.CreateSheet("point");
                foreach (var item in point)
                {
                    for (int i = 0; i < item.Value?.Count; i++)
                    {
                        if (i == 0) sheet.CreateRow(count).CreateCell(i).SetCellValue(item.Value[i]);
                        else sheet.GetRow(count).CreateCell(i).SetCellValue(item.Value[i]);
                    }
                    count++;
                }
                using (FileStream fs = new FileStream(FilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite))
                {
                    wk.Write(fs);
                }
            }
        }

        #endregion

        #region Mathematical & Array Calculations (几何坐标推演阵列)

        /// <summary>
        /// 验证当前轴关联的枚举示教点是否在底层数据表中完整配置，避免引发 <see cref="NullReferenceException"/>。
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (_zAxis != null) EnsurePointsExist<ZAxisPoint>(_zAxis);
            if (_xAxis != null) EnsurePointsExist<XAxisPoint>(_xAxis);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 核心阵列推演算法：提取第一层（底层）基准示教点，根据动态配方中的 LayerPitch 推算整盒晶圆（取料及扫描）的所有理论坐标系。
        /// <para>业务价值：极大降低了调机成本。工程师只需示教第1层的位置，其余24层交由算法推演。</para>
        /// </summary>
        public async Task<Dictionary<int, AxisPoint>> ArrayZAxisMaterialPickingPosition(E_WafeSize wafeSize)
        {
            // 路由变量：决定计算后的数据存入哪一对尺寸缓存字典
            var dictToFill = wafeSize == E_WafeSize._8寸 ? PickingPosition_8 : PickingPosition_12;
            var dictScanToFill = wafeSize == E_WafeSize._8寸 ? ScanPosition_8 : ScanPosition_12;

            dictScanToFill.Clear();
            dictToFill.Clear();
            var resultDict = new Dictionary<int, AxisPoint>(); // 局部返回结果集

            // 依据枚举名称动态提取基准点配置实体
            string basePointName = wafeSize == E_WafeSize._8寸 ? nameof(ZAxisPoint.层1取料位_8寸) : nameof(ZAxisPoint.层1取料位_12寸);
            string basescanPointName = wafeSize == E_WafeSize._8寸 ? nameof(ZAxisPoint.层1扫描点位_8寸) : nameof(ZAxisPoint.层1扫描点位_12寸);

            var basePoint = _zAxis.PointTable.FirstOrDefault(p => p.Name == basePointName); // 提取取料基准属性
            var basescanPoint = _zAxis.PointTable.FirstOrDefault(p => p.Name == basescanPointName); // 提取扫描基准属性

            if (basePoint == null || basescanPoint == null)
            {
                _logger.Error($"[{MechanismName}] 阵列计算失败：底层硬件配置中找不到指定的基准点位。");
                return resultDict;
            }

            // 执行等差推演：由于 Z 轴通常自下而上为正，故向上的槽位物理高度 = 基准坐标 - (N * 层距)
            for (int i = 0; i < _maxLayerCount; i++)
            {
                // 构造当前层的扫描比对属性
                var scanPoint = new AxisPoint
                {
                    Name = $"第{i + 1}层取料位",
                    TargetPosition = basescanPoint.TargetPosition - (i * LayerPitch), // 核心计算逻辑
                    Speed = basescanPoint.Speed,
                    Acc = basescanPoint.Acc,
                    Dec = basescanPoint.Dec,
                    STime = basescanPoint.STime
                };

                // 构造当前层的实际拉料属性
                var pickPoint = new AxisPoint
                {
                    Name = $"第{i + 1}层扫描位",
                    TargetPosition = basePoint.TargetPosition - (i * LayerPitch), // 核心计算逻辑
                    Speed = basePoint.Speed,
                    Acc = basePoint.Acc,
                    Dec = basePoint.Dec,
                    STime = basePoint.STime
                };

                dictToFill.TryAdd(i, pickPoint);
                dictScanToFill.TryAdd(i, scanPoint);
                resultDict.Add(i, scanPoint);
            }

            _logger.Info($"[{MechanismName}] [{wafeSize}] 阵列点位计算完毕，共生成 {_maxLayerCount} 层坐标。");
            await Task.CompletedTask;
            return resultDict;
        }

        /// <summary>
        /// 缓存读取：根据传入层数提取由 <see cref="ArrayZAxisMaterialPickingPosition"/> 计算好的目标点位信息
        /// </summary>
        public async Task<AxisPoint> GetZAxisMaterialPickingPosition(int index, E_WafeSize wafeSize)
        {
            await Task.CompletedTask;

            var dictToSearch = wafeSize == E_WafeSize._8寸 ? PickingPosition_8 : PickingPosition_12;

            if (dictToSearch.TryGetValue(index, out var point))
            {
                return point;
            }

            _logger.Error($"[{MechanismName}] 获取层点位失败：找不到尺寸 [{wafeSize}] 的第 {index + 1} 层点位数据。");
            return null;
        }

        #endregion
    }
}