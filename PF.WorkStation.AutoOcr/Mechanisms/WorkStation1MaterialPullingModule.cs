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
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

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
    ///    开启高频后台线程监控夹爪传感器。一旦发现“阻力过大(卡料)”或“铁环丢失(丢料)”，Y 轴将瞬间刹停。
    /// 5. 扫码校验：调用 <see cref="CodeScanTigger(CancellationToken)"/> 触发光源与扫码枪验证载具信息。
    /// 6. 送回晶圆：调用 <see cref="FeedingMaterialToBox(CancellationToken)"/>，同样带有并发防呆监控，防止推入时撞片。
    /// </remarks>
    [MechanismUI("工位1推拉晶圆模组", "WorkStation1MaterialPullingModuleDebugView", 1)]
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
        public async Task<bool> InitializeFullingAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}] 初始化晶圆拉料流程...");

            if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, token: token))
            {
                _logger.Info($"[{MechanismName}] 所有轴已到达待机位置，初始化拉料流程完成。");
                return true;
            }
            else
            {
                _logger.Error($"[{MechanismName}] 晶圆拉料流程初始化失败，未能成功移动 Y 轴到待机位置。");
                return false;
            }
        }

        /// <summary>
        /// 检查轨道上是否残留有晶圆物料（初始化防呆使用，防止盲目复位导致撞片）
        /// </summary>
        public async Task<bool?> CheckTrackIsMaterial(CancellationToken token = default)
        {
            try
            {
                bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左晶圆在位检测1);
                if (!res1.HasValue) throw new Exception($"{E_InPutName.晶圆轨道左晶圆在位检测1} 输入信号读取失败");

                bool? res2 = _io.ReadInput((int)E_InPutName.晶圆轨道左晶圆在位检测2);
                if (!res2.HasValue) throw new Exception($"{E_InPutName.晶圆轨道左晶圆在位检测2} 输入信号读取失败");

                // 任一传感器感应到即判定为有物料
                return (res1.Value || res2.Value);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return null;
            }
        }

        /// <summary>
        /// 检查当前轨道的调宽气缸与夹爪气缸是否已经处于对应尺寸的正确状态
        /// </summary>
        public async Task<bool> CheckWafeSizeControl(E_WafeSize wafesize, CancellationToken token = default)
        {
            try
            {
                if (wafesize == E_WafeSize._8寸)
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸缩回);
                    if (!res1.HasValue) throw new Exception($"{E_InPutName.晶圆轨道左调宽气缸缩回} 输入信号读取失败");

                    bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左8寸气缸缩回);
                    if (!res2.HasValue) throw new Exception($"{E_InPutName.晶圆夹爪左8寸气缸缩回} 输入信号读取失败");

                    return res1.Value && res2.Value;
                }
                else if (wafesize == E_WafeSize._12寸)
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸打开);
                    if (!res1.HasValue) throw new Exception($"{E_InPutName.晶圆轨道左调宽气缸打开} 输入信号读取失败");

                    bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左12寸气缸打开);
                    if (!res2.HasValue) throw new Exception($"{E_InPutName.晶圆夹爪左12寸气缸打开} 输入信号读取失败");

                    return res1.Value && res2.Value;
                }
                else
                {
                    throw new Exception("晶圆尺寸输入错误");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 切换晶圆尺寸：动态驱动轨道调宽气缸与夹爪转换气缸。
        /// <para>防呆机制：切换前必须保证轨道上无物料，防止挤压碎片；控制电磁阀时采用严格的先关后开逻辑。</para>
        /// </summary>
        public async Task<bool> ChangeWafeSizeControl(E_WafeSize wafesize, CancellationToken token = default)
        {
            // 配置气缸动作超时监控
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                // [物理防呆] 轨道上有物料时严禁调宽，否则会挤碎晶圆
                if (await CheckTrackIsMaterial(linktoken) == true)
                {
                    throw new Exception("工位1轨道有晶圆，请先清除轨道物料再执行尺寸切换。");
                }

                if (wafesize == E_WafeSize._8寸)
                {
                    // 先断开对立侧气路，再给目标侧供气，防止电磁阀或气缸串气形成憋气死锁
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸伸出, false)) throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸伸出} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸收回, true)) throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸收回} 操作失败");

                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸伸出, false)) throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸伸出} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸缩回, true)) throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸缩回} 操作失败");

                    // 轮询等待磁性开关信号到位
                    while (true)
                    {
                        await Task.Delay(1, linktoken);
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸缩回);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左8寸气缸缩回);
                        if (res1 == true && res2 == true) break;
                    }
                    return true;
                }
                else if (wafesize == E_WafeSize._12寸)
                {
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸收回, false)) throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸收回} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸伸出, true)) throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸伸出} 操作失败");

                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸缩回, false)) throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸缩回} 操作失败");
                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸伸出, true)) throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸伸出} 操作失败");

                    while (true)
                    {
                        await Task.Delay(1, linktoken);
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸打开);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左12寸气缸打开);
                        if (res1 == true && res2 == true) break;
                    }
                    return true;
                }
                else
                {
                    throw new Exception($"晶圆尺寸输入错误: {wafesize}");
                }
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                throw new Exception("切换晶圆尺寸操作气缸超时，请检查气压或传感器状态。");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        #endregion

        #region Pneumatic Control (气动夹爪控制)

        /// <summary>
        /// 张开晶圆夹爪
        /// </summary>
        public async Task<bool> OpenWafeGipper(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                // 先断开闭合气路，再打开张开气路
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左闭合, false)) throw new Exception($"操作输出信号 {E_OutPutName.夹爪气缸左闭合} 失败");
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左张开, true)) throw new Exception($"操作输出信号 {E_OutPutName.夹爪气缸左张开} 失败");

                while (true)
                {
                    await Task.Delay(1, linktoken);
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左气缸张开);
                    if (res1 == true) break;
                }
                return true;
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                throw new Exception($"等待输入信号 {E_InPutName.晶圆夹爪左气缸张开} 超时，请检查气路。");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 闭合晶圆夹爪，并检测是否成功夹取到铁环
        /// </summary>
        public async Task<bool> CloseWafeGipper(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左张开, false)) throw new Exception($"操作输出信号 {E_OutPutName.夹爪气缸左张开} 失败");
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左闭合, true)) throw new Exception($"操作输出信号 {E_OutPutName.夹爪气缸左闭合} 失败");

                while (true)
                {
                    await Task.Delay(1, linktoken);
                    bool? res = _io.ReadInput((int)E_InPutName.晶圆夹爪左气缸闭合);
                    if (res == true) break;
                }

                // [关键防呆] 气缸虽然闭合了，但还要检查传感器确认是否真的夹到了铁环（防空夹）
                bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                if (!res1.HasValue) throw new Exception($"获取输入信号 {E_InPutName.晶圆夹爪左铁环有无检测} 失败");

                // 注意逻辑：此传感器如果为 true，可能代表“无料”或“透光”，需根据实际接线逻辑确认
                if (res1.Value)
                {
                    throw new Exception($"{E_InPutName.晶圆夹爪左铁环有无检测} 传感器感应异常，未检测到铁环物料。");
                }
                return true;
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                throw new Exception($"等待输入信号：{E_InPutName.晶圆夹爪左气缸闭合} 超时。");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
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
        public async Task<bool> MoveInitialNoScan(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _logger.Info($"[{MechanismName}] Y 轴移动到待机位 (无检测)");

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    return true;
                }
                throw new Exception($"[{MechanismName}] 移动到待机位失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 移动到待机位，并严格防呆检查夹爪内是否残留有料
        /// </summary>
        public async Task<bool> MoveInitial(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _logger.Info($"[{MechanismName}] 移动到待机位并执行余料防呆检查");

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                    if (!res1.HasValue) throw new Exception($"获取输入信号 {E_InPutName.晶圆夹爪左铁环有无检测} 失败");

                    // 如果处于待机状态但传感器仍感应到物料，说明发生残留，需报警人工介入
                    if (res1.Value)
                    {
                        throw new Exception($"{E_InPutName.晶圆夹爪左铁环有无检测} 状态异常：机构已复位但检测到残留物料，请人工确认是否带料。");
                    }
                    return true;
                }
                throw new Exception($"[{MechanismName}] 移动到待机位失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 卸料完成后，退回安全位并确认晶圆已成功脱离夹爪
        /// </summary>
        public async Task<bool> PutOverMove(CancellationToken token = default)
        {
            try
            {
                CheckReady();
                _logger.Info($"[{MechanismName}] 卸料后退回取出安全位...");

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.取出安全位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                    if (!res1.HasValue) throw new Exception($"获取输入信号 {E_InPutName.晶圆夹爪左铁环有无检测} 失败");

                    // 退回后必须是无料状态，防止因为静电或吸附导致晶圆被意外拉回
                    if (!res1.Value)
                    {
                        throw new Exception($"{E_InPutName.晶圆夹爪左铁环有无检测} 状态异常：卸料后依然检测到物料粘连。");
                    }
                    return true;
                }
                throw new Exception($"[{MechanismName}] 移动到取出安全位置失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 取料前奏：确保夹爪张开并伸入目标料盒位
        /// </summary>
        public async Task<bool> InitialMoveFeeding(CancellationToken token = default)
        {
            try
            {
                // [防碰撞] 深入料盒前必须保证夹爪处于完全张开状态，否则会撞碎晶圆盒内的晶圆
                if (!await OpenWafeGipper(token)) return false;

                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.晶圆取料位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    return true;
                }
                throw new Exception($"[{MechanismName}] 移动到晶圆取料位置失败");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        #endregion

        #region Concurrent Movement & Safety Guard (高并发运动与实时防呆监控)

        /// <summary>
        /// 核心动作：将夹取的晶圆拉出到检测位。
        /// <para>技术亮点：使用 <see cref="Task.WhenAny"/> 实现运动与双重硬件防呆的并发执行。
        /// 运动过程中若检测到拉力异常(卡料)或物料脱落(丢料)，能在毫秒级响应并切断运动。</para>
        /// </summary>
        public async Task<bool> MoveDetection(CancellationToken token = default)
        {
            // 1. 创建联动取消源：将全局生命周期 token 与本方法的运动超时 token 绑定
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linkedToken = cts.Token;

            try
            {
                // 并发任务 A：【卡料检测】监控轨道摩擦力或干涉，防止拉断晶圆
                Task taskA = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(5, linkedToken); // 5ms 高频轮询
                        if (_io.ReadInput((int)E_InPutName.晶圆夹爪左卡料检测) == true) return;
                    }
                }, linkedToken);

                // 并发任务 B：【丢料检测】监控铁环是否在移动中意外滑脱
                Task taskB = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(5, linkedToken);
                        if (_io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测) == true) return;
                    }
                }, linkedToken);

                // 并发任务 C：【轴运动状态检测】触发底层板卡异步运动，并实时监控 IO 状态字
                if (!await _yAxis.MoveToPointAsync(YAxisPoint.晶圆拉出位置.ToString(), linkedToken))
                {
                    cts.Cancel();
                    throw new Exception($"[{MechanismName}] 轴底层运动触发指令下发失败");
                }

                Task taskC = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(10, linkedToken);
                        var motionio = _yAxis.AxisIOStatus;
                        // 当板卡反馈不再 Moving 且 MoveDone 置位时，判定到达
                        if (motionio == null || (motionio.MoveDone && !motionio.Moving)) return;
                    }
                }, linkedToken);

                // 2. 核心拦截墙：等待 A(卡料), B(丢料), C(到达) 中任意一个事件发生
                Task finishedTask = await Task.WhenAny(taskA, taskB, taskC);

                // 3. 拦截后清理：某一个任务抢跑触发后，立即取消未完成的其他后台轮询任务
                cts.Cancel();

                // 4. 判定退出的根本原因 (Root Cause)
                if (finishedTask == taskA)
                {
                    await _yAxis.StopAsync(); // 毫秒级硬刹停
                    throw new Exception($"[{MechanismName}] 运动过程中触发【卡料报警】，已紧急停止！");
                }
                if (finishedTask == taskB)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 运动过程中触发【丢料报警】，已紧急停止！");
                }

                // 只有 taskC (运动正常完成) 才会走到这里
                return true;
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                await _yAxis.StopAsync();
                throw new Exception($"[{MechanismName}] Y 轴拉出运动超时。");
            }
            catch (Exception ex)
            {
                await _yAxis.StopAsync();
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 核心动作：将检测完成的晶圆推回料盒位。
        /// <para>采用与 <see cref="MoveDetection(CancellationToken)"/> 相同的并发硬件防呆模型。</para>
        /// </summary>
        public async Task<bool> FeedingMaterialToBox(CancellationToken token)
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

                // 推回目标位变为 取料位置
                if (!await _yAxis.MoveToPointAsync(YAxisPoint.晶圆取料位置.ToString(), linked.Token))
                {
                    linked.Cancel();
                    throw new Exception($"[{MechanismName}] 送入运动触发失败");
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
                    throw new Exception($"[{MechanismName}] 送回过程中触发【卡料报警】，已紧急刹停！");
                }
                if (finishedTask == taskB)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 送回过程中触发【丢料报警】，已紧急刹停！");
                }

                return true;
            }
            catch (OperationCanceledException)
            {
                if (timeoutcts.IsCancellationRequested)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 送入运动超时");
                }
                await _yAxis.StopAsync();
                return false;
            }
            catch (Exception ex)
            {
                await _yAxis.StopAsync();
                _logger.Warn(ex.Message);
                return false;
            }
        }

        #endregion

        #region Barcode Scanning (视觉扫码业务)

        /// <summary>
        /// 扫码枪触发方法：联动打光并请求核心业务层校验条码合法性
        /// </summary>
        public async Task<List<string>> CodeScanTigger(CancellationToken token = default)
        {
            try
            {
                // 1. 点亮辅助光源，亮度取自系统配方
                await _lightController.SetLightValue(3, await ParamService.GetParamAsync<int>(E_Params.WorkStation1LightBrightness.ToString()));

                // 2. 赋予最多3次的重试机会
                for (int i = 0; i < 3; i++)
                {
                    string str = await _codeScan.Tigger(token); // 触发底层硬件解码
                    if (string.IsNullOrEmpty(str))
                    {
                        continue; // 扫码失败或超时，重试
                    }
                    else
                    {
                        // 3. 将解出的码条发送至数据中心校验 (例如：验证工单匹配度或工序有效性)
                        var flag = await _dataModule.CheckCodeAsync(E_WorkSpace.工位1, str.Split('&').ToList(), token);
                        if (flag.Item1)
                        {
                            _logger.Info($"[{MechanismName}] 扫码结果校验通过: {str}");
                            return str.Split('&').ToList();
                        }
                        else
                        {
                            _logger.Warn($"[{MechanismName}] 扫码内容校验不合法 (拦截): {str}");
                            // 如果是最后一次尝试依然不合法，也将其抛回上层供 UI 弹窗拦截
                            if (i == 2) return str.Split('&').ToList();
                        }
                    }
                }
                return null;
            }
            catch (Exception)
            {
                // 异常保底：必须关闭光源，防止长时间高亮烧损灯珠
                await _lightController.SetLightValue(3, 0);
                return null;
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