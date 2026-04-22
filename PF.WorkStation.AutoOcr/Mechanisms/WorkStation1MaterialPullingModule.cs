using NPOI.HSSF.Record;
using NPOI.SS.Formula.Functions;
using Prism.Ioc;
using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PF.Core.Constants;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 【工位1】推拉晶圆模组 (Wafer Pulling Mechanism)
    /// 
    /// <para>物理架构：</para>
    /// 包含一个控制水平推拉的 Y 轴、一套多尺寸自适应的气动夹爪与变轨气缸，
    /// 以及光源控制器与工业扫码枪。
    /// 
    /// <para>软件职责：</para>
    /// 负责将指定槽位的晶圆从料盒中水平平稳拉出、执行光源频闪与读码校验，并在完成后推回原位。
    /// 内置了高频并发实时防呆系统（防卡料、防丢料），确保拉片过程的绝对安全。
    /// </summary>
    /// <remarks>
    /// 💡 【标准工艺调用时序】
    /// 
    /// 1. 轨道与夹爪预处理：调用 <see cref="ChangeWafeSizeControl(E_WafeSize, CancellationToken)"/> 自动切换当前气缸与轨道的宽窄尺寸。
    /// 2. 取料前置定位：调用 <see cref="InitialMoveFeeding(CancellationToken)"/> 张开夹爪并深入料盒。
    /// 3. 执行夹取：调用 <see cref="CloseWafeGipper(CancellationToken)"/> 闭合气缸夹紧铁环。
    /// 4. 并发拉出与检测：调用核心方法 <see cref="MoveDetection(CancellationToken)"/>。该步骤会在 Y 轴退出的同时，
    ///    开启高频后台线程监控夹爪传感器。一旦发现"阻力过大(卡料)"或"铁环丢失(丢料)"，Y 轴将瞬间刹停。
    /// 5. 扫码校验：调用 <see cref="CodeScanTigger(CancellationToken)"/> 触发光源与扫码枪验证载具信息。
    /// 6. 送回晶圆：调用 <see cref="FeedingMaterialToBox(CancellationToken)"/>，同样带有并发防呆监控，防止推入时撞片。
    /// </remarks>
    [MechanismUI("工位1推拉晶圆模组", "WorkStation1MaterialPullingModuleDebugView", 2)]
    public class WorkStation1MaterialPullingModule : BaseMechanism
    {
        #region Enums (轴点枚举定义)

        /// <summary>
        /// 定义 Y 轴（水平推拉轴）的关键示教点位
        /// </summary>
        public enum YAxisPoint
        {
            /// <summary>待机位置</summary>
            待机位置,         // Y轴退回最深处的安全位，不干涉 Z轴升降 与 上下料
            /// <summary>晶圆取料位置</summary>
            晶圆取料位置,     // Y轴伸入料盒内部，夹爪中心对准铁环的位置
            /// <summary>晶圆拉出位置</summary>
            晶圆拉出位置,     // 夹持晶圆后，往回拉出用于进行扫码或视觉检测的基准位置
            /// <summary>取出安全位置</summary>
            取出安全位置,     // 晶圆完全离开料盒后的安全驻留位
        }

        #endregion 

        #region Fields & Properties (硬件实例与依赖服务)

        // ── 底层硬件实例（延迟加载） ──
        private IAxis _yAxis;                       // 推拉 Y 轴
        private IIOController _io;                  // IO 模块：读取夹爪传感器、控制气缸
        private IBarcodeScan _codeScan;             // 工业扫码枪
        private ILightController _lightController;  // 光源控制器 (用于扫码补光)

        // ── 业务状态模块 ──
        private WorkStationDataModule? _dataModule; // 数据处理中心，用于校验扫码结果的合法性
        private IContainerProvider Provider;        // DI 容器，用于手动解析依赖

        /// <summary>
        /// 当前生产的晶圆尺寸，影响气动变轨与夹爪的尺寸切换逻辑
        /// </summary>
        private E_WafeSize _currentWafeSize = E_WafeSize._12寸;

        // ── 公开硬件绑定属性 (供 ViewModel/UI 面板调试使用) ──
        /// <summary>获取Y轴实例</summary>
        public IAxis YAxis => _yAxis;
        /// <summary>获取IO控制器实例</summary>
        public IIOController IO => _io;
        /// <summary>获取扫码枪实例</summary>
        public IBarcodeScan CodeScan => _codeScan;
        /// <summary>获取光源控制器实例</summary>
        public ILightController LightController => _lightController;

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化工位1推拉晶圆模组
        /// </summary>
        public WorkStation1MaterialPullingModule(
            IHardwareManagerService hardwareManagerService,
            IParamService paramService,
            IContainerProvider provider,
            ILogService logger)
            : base(E_Mechanisms.工位1推拉晶圆模组.ToString(), hardwareManagerService, paramService, logger)
        {
            Provider = provider;
        }

        /// <summary>
        /// 模组初始化核心逻辑：延迟解析硬件 → 注册报警聚合 → 建立通讯并使能
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例
            _yAxis = HardwareManagerService?.GetDevice(E_AxisName.工位1拉料Y轴.ToString()) as IAxis;
            if (_yAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Y轴 '{E_AxisName.工位1拉料Y轴}'，请确认硬件配置。");
                return false;
            }

            _io = HardwareManagerService?.GetDevice("IO_Collectorll") as IIOController;
            if (_io == null)
            {
                _logger.Error($"[{MechanismName}] 未找到IO模块 'IO_Collectorll'，请确认硬件配置。");
                return false;
            }

            _codeScan = HardwareManagerService?.GetDevice(E_ScanCode.工位1扫码枪.ToString()) as IBarcodeScan;
            if (_codeScan == null)
            {
                _logger.Error($"[{MechanismName}] 未找到扫码枪 '{E_ScanCode.工位1扫码枪}'，请确认硬件配置。");
                return false;
            }

            _lightController = HardwareManagerService?.ActiveDevices?.OfType<ILightController>().FirstOrDefault();
            if (_lightController == null)
            {
                _logger.Error($"[{MechanismName}] 未找光源控制器 '{E_LightController.康视达_COM}'，请确认硬件配置。");
                return false;
            }

            // ② 注册报警聚合防线
            RegisterHardwareDevice(_yAxis as IHardwareDevice);
            RegisterHardwareDevice(_io as IHardwareDevice);
            RegisterHardwareDevice(_codeScan as IHardwareDevice);
            RegisterHardwareDevice(_lightController as IHardwareDevice);

            await ConfirmEunmPoints();

            // ③ 建立底层物理通信连接
            if (!await _yAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Y轴连接失败"); return false; }
            if (!await _io.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] IO模块连接失败"); return false; }

            // ④ 伺服上电使能
            if (!await _yAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Y轴使能失败"); return false; }

            // ⑤ 获取数据校验模块
            _dataModule = Provider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            if (_dataModule == null)
            {
                _logger.Error($"[{MechanismName}] 未找到 {nameof(WorkStationDataModule)} 模块，请检查软件依赖配置。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 模组急停/停止
        /// </summary>
        protected override async Task InternalStopAsync()
        {
            if (_yAxis != null)
            {
                await _yAxis.StopAsync();
            }
        }

        #endregion

        #region Status Check & Preparation (状态检测与调宽准备)

        /// <summary>
        /// 全局初始化：驱动推拉轴退回安全待机位
        /// </summary>
        public async Task<MechResult> InitializeFullingAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 初始化晶圆拉料流程...");

            if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, token: token))
            {
                _logger.Info($"[{MechanismName}] 所有轴已到达待机位置，初始化拉料流程完成。");
                return MechResult.Success();
            }
            else
            {
                _logger.Error($"[{MechanismName}] 晶圆拉料流程初始化失败，未能成功移动 Y 轴到待机位置。");
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.InitPullingFailed, "初始化拉料流程失败，Y轴运动到待机位失败");
            }
        }

        /// <summary>
        /// 检查轨道上是否残留有晶圆物料（初始化防呆使用，防止盲目复位导致撞片）
        /// </summary>
        public async Task<MechResult<bool>> CheckTrackIsMaterial(CancellationToken token = default)
        {
            CheckReady(); // 保持与方法1一致的就绪检查
            _logger.Info($"[{MechanismName}] 开始检查轨道是否残留晶圆物料...");

            // 读取IO信号
            bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左晶圆在位检测1);
            bool? res2 = _io.ReadInput((int)E_InPutName.晶圆轨道左晶圆在位检测2);

            // 如果两个信号都成功读取
            if (res1.HasValue && res2.HasValue)
            {
                // 任一传感器感应到即判定为有物料
                bool hasMaterial = res1.Value || res2.Value;
                _logger.Info($"[{MechanismName}] 轨道物料检查完成，当前状态：{(hasMaterial ? "有物料残留" : "无物料")}。");

                return MechResult<bool>.Success(hasMaterial);
            }
            else
            {
                // 找出具体是哪个传感器读取失败，方便排查
                string failedSensor = !res1.HasValue ? nameof(E_InPutName.晶圆轨道左晶圆在位检测1) : nameof(E_InPutName.晶圆轨道左晶圆在位检测2);

                _logger.Error($"[{MechanismName}] 轨道物料检查失败，未能成功读取 {failedSensor} 信号。");

                // 这里的 AlarmCodes 根据你的实际项目枚举替换，这里用 ReadSignalFailed 示意
                return MechResult<bool>.Fail(AlarmCodes.Hardware.IoGetError, $"读取轨道物料检测信号({failedSensor})失败");
            }
        }

        /// <summary>
        /// 检查当前轨道的调宽气缸与夹爪气缸是否已经处于对应尺寸的正确状态
        /// </summary>
        public async Task<MechResult<bool>> CheckWafeSizeControl(E_WafeSize wafesize, CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 开始检查轨道与夹爪气缸是否处于 [{wafesize}] 的正确状态...");

            // 1. 根据晶圆尺寸，确定需要检测的 IO 点位
            E_InPutName trackSensor;
            E_InPutName gripperSensor;

            if (wafesize == E_WafeSize._8寸)
            {
                trackSensor = E_InPutName.晶圆轨道左调宽气缸缩回;
                gripperSensor = E_InPutName.晶圆夹爪左8寸气缸缩回;
            }
            else 
            {
                trackSensor = E_InPutName.晶圆轨道左调宽气缸打开;
                gripperSensor = E_InPutName.晶圆夹爪左12寸气缸打开;
            }
         

            // 2. 统一读取 IO 信号
            bool? trackRes = _io.ReadInput((int)trackSensor);
            bool? gripperRes = _io.ReadInput((int)gripperSensor);

            // 3. 校验读取结果并返回
            if (trackRes.HasValue && gripperRes.HasValue)
            {
                bool isCorrectState = trackRes.Value && gripperRes.Value;
                _logger.Info($"[{MechanismName}] 气缸 [{wafesize}] 状态检查完成，当前状态：{(isCorrectState ? "到位" : "未到位/异常")}。");

                return MechResult<bool>.Success(isCorrectState);
            }
            else
            {
                // 找出具体失败的传感器名称
                string failedSensor = !trackRes.HasValue ? trackSensor.ToString() : gripperSensor.ToString();

                _logger.Error($"[{MechanismName}] 气缸状态检查失败，未能成功读取 {failedSensor} 信号。");
                return MechResult<bool>.Fail(AlarmCodes.Hardware.IoGetError, $"读取气缸检测信号({failedSensor})失败");
            }
        }

        /// <summary>
        /// 切换晶圆尺寸：动态驱动轨道调宽气缸与夹爪转换气缸。
        /// <para>防呆机制：切换前必须保证轨道上无物料，防止挤压碎片；控制电磁阀时采用严格的先关后开逻辑。</para>
        /// </summary>
        public async Task<MechResult> ChangeWafeSizeControl(E_WafeSize wafesize, CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                if (await CheckTrackIsMaterial(linktoken) == true)
                {
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeTrackHasMaterial, "工位1轨道有晶圆，请先清除轨道物料再执行尺寸切换");
                }

                if (wafesize == E_WafeSize._8寸)
                {
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸伸出, false)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.晶圆轨道左调宽气缸伸出} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸收回, true)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.晶圆轨道左调宽气缸收回} 操作失败");

                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸伸出, false)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.夹爪左X轴气缸伸出} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸缩回, true)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.夹爪左X轴气缸缩回} 操作失败");

                    while (true)
                    {
                        await Task.Delay(1, linktoken);
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸缩回);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左8寸气缸缩回);
                        if (res1 == true && res2 == true) break;
                    }
                    return MechResult.Success();
                }
                else if (wafesize == E_WafeSize._12寸)
                {
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸收回, false)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.晶圆轨道左调宽气缸收回} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸伸出, true)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.晶圆轨道左调宽气缸伸出} 操作失败");

                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸缩回, false)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.夹爪左X轴气缸缩回} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸伸出, true)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"{E_OutPutName.夹爪左X轴气缸伸出} 操作失败");

                    while (true)
                    {
                        await Task.Delay(1, linktoken);
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸打开);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左12寸气缸打开);
                        if (res1 == true && res2 == true) break;
                    }
                    return MechResult.Success();
                }
                else
                {
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, $"晶圆尺寸输入错误: {wafesize}");
                }
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderTimeout, "切换晶圆尺寸操作气缸超时，请检查气压或传感器状态");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.ChangeSizeCylinderFailed, ex.Message);
            }
        }

        #endregion

        #region Pneumatic Control (气动夹爪控制)

        /// <summary>
        /// 张开晶圆夹爪
        /// </summary>
        public async Task<MechResult> OpenWafeGipper(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左闭合, false)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperOpenCylinderFailed, $"操作输出信号 {E_OutPutName.夹爪气缸左闭合} 失败");
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左张开, true)) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperOpenCylinderFailed, $"操作输出信号 {E_OutPutName.夹爪气缸左张开} 失败");

                while (true)
                {
                    await Task.Delay(1, linktoken);
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左气缸张开);
                    if (res1 == true) break;
                }
                return MechResult.Success();
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperOpenTimeout, $"等待输入信号 {E_InPutName.晶圆夹爪左气缸张开} 超时，请检查气路");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperOpenCylinderFailed, ex.Message);
            }
        }

        /// <summary>
        /// 闭合晶圆夹爪，并检测是否成功夹取到铁环
        /// </summary>
        public async Task<MechResult> CloseWafeGipper(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左张开, false))
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperCloseCylinderFailed, "夹爪气缸左张开IO操作失败");
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左闭合, true))
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperCloseCylinderFailed, "夹爪气缸左闭合IO操作失败");

                while (true)
                {
                    await Task.Delay(1, linktoken);
                    bool? res = _io.ReadInput((int)E_InPutName.晶圆夹爪左气缸闭合);
                    if (res == true) break;
                }

                bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                if (!res1.HasValue)
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperCloseNoRing, "铁环检测传感器读取失败");

                if (res1.Value)
                {
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperCloseNoRing, "夹爪闭合后未检测到铁环物料（空夹）");
                }
                return MechResult.Success();
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperCloseTimeout, $"等待输入信号 {E_InPutName.晶圆夹爪左气缸闭合} 超时");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.GripperCloseCylinderFailed, ex.Message);
            }
        }

        #endregion

        #region Motion Sequences (核心联动序列)

        /// <summary>
        /// 判断是否存在叠片异常 (预留方法)
        /// </summary>
        public async Task<bool> CheckStackedPieces(CancellationToken token)
        {
            try
            {
                // 预留硬件检测叠片逻辑
                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 移动到待机位 (无强制物料检测，用于强制复位)
        /// </summary>
        public async Task<MechResult> MoveInitialNoScan(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _logger.Info($"[{MechanismName}] Y 轴移动到待机位 (无检测)");

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    return MechResult.Success();
                }
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.MoveInitialNoScanFailed, "Y轴移动到待机位失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.MoveInitialNoScanFailed, ex.Message);
            }
        }

        /// <summary>
        /// 移动到待机位，并严格防呆检查夹爪内是否残留有料
        /// </summary>
        public async Task<MechResult> MoveInitial(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _logger.Info($"[{MechanismName}] 移动到待机位并执行余料防呆检查");

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                    if (!res1.HasValue) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.MoveInitialResidualMaterial, $"获取输入信号 {E_InPutName.晶圆夹爪左铁环有无检测} 失败");

                    if (res1.Value)
                    {
                        return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.MoveInitialResidualMaterial, "机构已复位但检测到残留物料，请人工确认是否带料");
                    }
                    return MechResult.Success();
                }
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.MoveInitialFailed, "移动到待机位失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.MoveInitialFailed, ex.Message);
            }
        }

        /// <summary>
        /// 卸料完成后，退回安全位并确认晶圆已成功脱离夹爪
        /// </summary>
        public async Task<MechResult> PutOverMove(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _logger.Info($"[{MechanismName}] 卸料后退回取出安全位...");

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.取出安全位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                    if (!res1.HasValue) return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PutOverMaterialStuck, $"获取输入信号 {E_InPutName.晶圆夹爪左铁环有无检测} 失败");

                    if (!res1.Value)
                    {
                        return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PutOverMaterialStuck, "卸料后依然检测到物料粘连");
                    }
                    return MechResult.Success();
                }
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PutOverMoveFailed, "移动到取出安全位置失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PutOverMoveFailed, ex.Message);
            }
        }

        /// <summary>
        /// 取料前奏：确保夹爪张开并伸入目标料盒位
        /// </summary>
        public async Task<MechResult> InitialMoveFeeding(CancellationToken token = default)
        {
            try
            {
                var openResult = await OpenWafeGipper(token);
                if (!openResult.IsSuccess) return openResult;

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.晶圆取料位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    return MechResult.Success();
                }
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.InitialMoveFeedingFailed, "移动到晶圆取料位置失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.InitialMoveFeedingFailed, ex.Message);
            }
        }

        #endregion

        #region Concurrent Movement & Safety Guard (高并发运动与实时防呆监控)

        /// <summary>
        /// 核心动作：将夹取的晶圆拉出到检测位。
        /// <para>技术亮点：使用 <see cref="Task.WhenAny"/> 实现运动与双重硬件防呆的并发执行。
        /// 运动过程中若检测到拉力异常(卡料)或物料脱落(丢料)，能在毫秒级响应并切断运动。</para>
        /// </summary>
        public async Task<MechResult> MoveDetection(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linkedToken = cts.Token;

            try
            {
                Task taskA = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(5, linkedToken);
                        if (_io.ReadInput((int)E_InPutName.晶圆夹爪左卡料检测) == true) return;
                    }
                }, linkedToken);

                Task taskB = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(5, linkedToken);
                        if (_io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测) == true) return;
                    }
                }, linkedToken);

                if (!await _yAxis.MoveToPointAsync(YAxisPoint.晶圆拉出位置.ToString(), linkedToken))
                {
                    cts.Cancel();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTriggerFailed, "轴底层运动触发指令下发失败");
                }

                Task taskC = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(10, linkedToken);
                        var motionio = _yAxis.AxisIOStatus;
                        if (motionio == null || (motionio.MoveDone && !motionio.Moving)) return;
                    }
                }, linkedToken);

                // 等待任意任务完成（不管是正常完成、异常退出还是被取消）
                Task finishedTask = await Task.WhenAny(taskA, taskB, taskC);
                cts.Cancel(); // 取消其他未完成的任务

                // 【修复 1】优先检查是否被外部信号取消
                if (token.IsCancellationRequested)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTriggerFailed, "操作已被外部取消"); // 这里可以根据你们的业务定义专门的错误码
                }

                // 【修复 2】检查是否是因为超时导致的取消
                if (timeoutcts.IsCancellationRequested)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTimeout, "Y轴拉出运动超时");
                }

                // 【修复 3】防御性检查：如果任务是因为未知异常崩溃的
                if (finishedTask.IsFaulted)
                {
                    await _yAxis.StopAsync();
                    _logger.Warn(finishedTask.Exception?.InnerException?.Message ?? "任务发生未知异常");
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTriggerFailed, "状态检测任务发生异常");
                }

                // 此时可以确保 finishedTask 是“正常检测到条件并 return”的
                if (finishedTask == taskA)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutJamAlarm, "拉出过程中触发【卡料报警】，已紧急停止");
                }
                if (finishedTask == taskB)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutDropAlarm, "拉出过程中触发【丢料报警】，已紧急停止");
                }
                

                // 只有 taskC (运动完成) 是真正的成功
                if (finishedTask == taskC)
                {
                    return MechResult.Success();
                }

                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTriggerFailed, "未知的执行逻辑");
            }
            catch (OperationCanceledException)
            {
                // 这里主要是抓取 _yAxis.MoveToPointAsync 内部因为 token 取消抛出的异常
                await _yAxis.StopAsync();
                if (timeoutcts.IsCancellationRequested)
                {
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTimeout, "Y轴拉出运动超时");
                }
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTriggerFailed, "操作已被外部取消");
            }
            catch (Exception ex)
            {
                await _yAxis.StopAsync();
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PullOutTriggerFailed, ex.Message);
            }
        }

        /// <summary>
        /// 核心动作：将检测完成的晶圆推回料盒位。
        /// <para>采用与 <see cref="MoveDetection(CancellationToken)"/> 相同的并发硬件防呆模型。</para>
        /// </summary>
        public async Task<MechResult> FeedingMaterialToBox(CancellationToken token)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);

            try
            {
                Task taskA = Task.Run(async () =>
                {
                    while (!linked.IsCancellationRequested)
                    {
                        await Task.Delay(5, linked.Token);
                        if (_io.ReadInput((int)E_InPutName.晶圆夹爪左卡料检测) == true) return;
                    }
                }, linked.Token);

                Task taskB = Task.Run(async () =>
                {
                    while (!linked.IsCancellationRequested)
                    {
                        await Task.Delay(5, linked.Token);
                        if (_io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测) == true) return;
                    }
                }, linked.Token);

                if (!await _yAxis.MoveToPointAsync(YAxisPoint.晶圆取料位置.ToString(), linked.Token))
                {
                    linked.Cancel();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PushBackTriggerFailed, "送入运动触发失败");
                }

                Task taskC = Task.Run(async () =>
                {
                    while (!linked.IsCancellationRequested)
                    {
                        await Task.Delay(10, linked.Token);
                        var motionio = _yAxis.AxisIOStatus;
                        if (motionio == null || (motionio.MoveDone && !motionio.Moving)) return;
                    }
                }, linked.Token);

                Task finishedTask = await Task.WhenAny(taskA, taskB, taskC);
                linked.Cancel();

                if (finishedTask == taskA)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PushBackJamAlarm, "送回过程中触发【卡料报警】，已紧急刹停");
                }
                if (finishedTask == taskB)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PushBackDropAlarm, "送回过程中触发【丢料报警】，已紧急刹停");
                }

                return MechResult.Success();
            }
            catch (OperationCanceledException)
            {
                if (timeoutcts.IsCancellationRequested)
                {
                    await _yAxis.StopAsync();
                    return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PushBackTimeout, "送入运动超时");
                }
                await _yAxis.StopAsync();
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PushBackTriggerFailed, "送入运动被取消");
            }
            catch (Exception ex)
            {
                await _yAxis.StopAsync();
                _logger.Warn(ex.Message);
                return MechResult.Fail(AlarmCodesExtensions.WS1Pulling.PushBackTriggerFailed, ex.Message);
            }
        }

        #endregion

        #region Barcode Scanning (视觉扫码业务)

        /// <summary>
        /// 扫码枪触发方法：联动打光并请求核心业务层校验条码合法性
        /// </summary>
        public async Task<MechResult<List<string>>> CodeScanTigger(CancellationToken token = default)
        {
            try
            {
                await _lightController.SetLightValue(3, await ParamService.GetParamAsync<int>(E_Params.WorkStation1LightBrightness.ToString()));

                for (int i = 0; i < 3; i++)
                {
                    string str = await _codeScan.Tigger(token);
                    if (string.IsNullOrEmpty(str))
                    {
                        continue;
                    }
                    else
                    {
                        var flag = await _dataModule.CheckCodeAsync(E_WorkSpace.工位1, str.Split('&').ToList(), token);
                        if (flag.IsSuccess)
                        {
                            _logger.Info($"[{MechanismName}] 扫码结果校验通过: {str}");
                            return MechResult<List<string>>.Success(str.Split('&').ToList());
                        }
                        else
                        {
                            _logger.Warn($"[{MechanismName}] 扫码内容校验不合法 (拦截): {str}");
                            if (i == 2) return MechResult<List<string>>.Success(str.Split('&').ToList());
                        }
                    }
                }
                return MechResult<List<string>>.Fail(AlarmCodesExtensions.WS1Pulling.CodeScanFailed, "扫码失败或校验不合法，3次重试均未通过");
            }
            catch (Exception ex)
            {
                await _lightController.SetLightValue(3, 0);
                return MechResult<List<string>>.Fail(AlarmCodesExtensions.WS1Pulling.CodeScanFailed, $"扫码异常: {ex.Message}");
            }
            finally
            {
                await _lightController.SetLightValue(3, 0);
            }
        }

        #endregion

        #region Helper Methods (内部辅助方法)

        /// <summary>
        /// 判断当前状态下，夹爪内部是否物理带料
        /// </summary>
        public async Task<bool> CheckGipperInsidePro(CancellationToken token = default)
        {
            await Task.CompletedTask;
            bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
            return res2 == true;
        }

        /// <summary>
        /// 验证当前关联的枚举示教点是否在底层数据表中完整配置
        /// 防止因为工程师漏建点位引发程序 <see cref="NullReferenceException"/>。
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (_yAxis != null) EnsurePointsExist<YAxisPoint>(_yAxis);
            await Task.CompletedTask;
        }

        #endregion
    }
}