using NPOI.HSSF.Record;
using NPOI.SS.Formula.Functions;
using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    [MechanismUI("工位1推拉晶圆模组", "WorkStation1MaterialPullingModuleDebugView", 1)]
    public class WorkStation1MaterialPullingModule : BaseMechanism
    {
        #region 轴点枚举定义


        public enum YAxisPoint
        {
            待机位置,
            晶圆取料位置,
            晶圆拉出位置,
            取出安全位置,
        }

        #endregion 轴点枚举定义
        private IAxis _yAxis;


        public IAxis YAxis => _yAxis;


        private IIOController _io;

        public IIOController IO => _io;


        private IBarcodeScan _codeScan;


        public IBarcodeScan CodeScan => _codeScan;


        private ILightController _lightController;

        public ILightController LightController => _lightController;



        /// <summary>
        /// 当前生产晶圆尺寸
        /// </summary>
        private E_WafeSize _currentWafeSize = E_WafeSize._12寸;

        public WorkStation1MaterialPullingModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger) : base(E_Mechanisms.工位1推拉晶圆模组.ToString(), hardwareManagerService, paramService, logger)
        {


        }


        // ── BaseMechanism 框架钩子实现 ─────────────────────────────────────

        /// <summary>
        /// 模组初始化核心逻辑：延迟解析轴和IO → 注册报警聚合 → 连接/使能/回零
        /// </summary>
        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例：通过硬件管理器及配置名称查找具体硬件对象
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
            _codeScan = HardwareManagerService.GetDevice(E_ScanCode.工位1扫码枪.ToString()) as IBarcodeScan;
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

            // ② 将硬件注册到当前模组：这样当单一轴/IO发生报警时，整个模组的状态会自动同步为 Alarm
            RegisterHardwareDevice(_yAxis as IHardwareDevice);
            RegisterHardwareDevice(_io as IHardwareDevice);
            RegisterHardwareDevice(_codeScan as IHardwareDevice);
            RegisterHardwareDevice(_lightController as IHardwareDevice);

            // 确认点位枚举在轴配置中都已示教/创建
            await ConfirmEunmPoints();

            // ③ 并行/串行连接所有硬件通信
            if (!await _yAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            if (!await _io.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] IO模块连接失败"); return false; }

            // ④ 使能伺服电机（Power On）
            if (!await _yAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }

            return true;
        }

        protected override async Task InternalStopAsync()
        {
            if (_yAxis != null)
            {
                await _yAxis.StopAsync();
            }
        }



        #region 晶圆拉料模组核心业务流程方法

        public async Task<bool> InitializeFullingAsync(CancellationToken token = default)
        {
            CheckReady();
            _logger.Info($"[{MechanismName}]初始化晶圆拉料流程...");
            if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, token: token))
            {
                _logger.Info($"[{MechanismName}]所有轴已到达待机位置，晶圆拉料流程。");
                return true;
            }
            else
            {
                _logger.Error($"[{MechanismName}]晶圆拉料流程初始化失败，未能成功移动所有轴到待机位置。");
                return false;
            }
        }

        #endregion 晶圆拉料模组核心业务流程方法

        /// <summary>
        /// 判断轨道上面是否有料(初始化使用，判断轴是否需要复位)
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task<bool?> CheckTrackIsMaterial(CancellationToken token = default)
        {
            try
            {

                bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左晶圆在位检测1);
                if (!res1.HasValue)
                {
                    throw new Exception($"{E_InPutName.晶圆轨道左晶圆在位检测1}输入信号读取失败");
                }
                bool? res2 = _io.ReadInput((int)E_InPutName.晶圆轨道左晶圆在位检测2);
                if (!res2.HasValue)
                {
                    throw new Exception($"{E_InPutName.晶圆轨道左晶圆在位检测2}输入信号读取失败");
                }
                return (res1.Value || res2.Value);
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return null;
            }
        }



        /// <summary>
        /// 判断轨道调宽和夹爪是否在对应尺寸范围
        /// </summary>
        /// <param name="token">晶圆尺寸</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task<bool> CheckWafeSizeControl(E_WafeSize wafesize, CancellationToken token = default)
        {
            try
            {
                if (wafesize == E_WafeSize._8寸)
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸缩回);
                    if (!res1.HasValue)
                    {
                        throw new Exception($"{E_InPutName.晶圆轨道左调宽气缸缩回}输入信号读取失败");
                    }
                    bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左8寸气缸缩回);
                    if (!res2.HasValue)
                    {
                        throw new Exception($"{E_InPutName.晶圆夹爪左8寸气缸缩回}输入信号读取失败");
                    }
                    return res1.Value && res2.Value;
                }
                else if (wafesize == E_WafeSize._12寸)
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸打开);
                    if (!res1.HasValue)
                    {
                        throw new Exception($"{E_InPutName.晶圆轨道左调宽气缸打开}输入信号读取失败");
                    }
                    bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左12寸气缸打开);
                    if (!res2.HasValue)
                    {
                        throw new Exception($"{E_InPutName.晶圆夹爪左12寸气缸打开}输入信号读取失败");
                    }
                    return res1.Value && res2.Value;
                }
                else
                {
                    throw new Exception($"晶圆尺寸输入错误");
                }
            }
            catch (Exception ex)
            {
                return false;
            }
        }

        /// <summary>
        /// 切换晶圆尺寸（切换轨道和夹爪   切换前检查轨道上面是否有物料，有物料则无法切换）
        /// </summary>
        /// <param name="wafesize"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> ChangeWafeSizeControl(E_WafeSize wafesize, CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;
            try
            {
                if (await CheckWafeSizeControl(wafesize, linktoken))
                {
                    throw new Exception($"工位1轨道有晶圆，清除轨道物料再切换");
                }

                if (wafesize == E_WafeSize._8寸)
                {
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸伸出, false))
                    {
                        throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸伸出} 操作失败");
                    }
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸收回, true))
                    {
                        throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸收回} 操作失败");
                    }

                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸伸出, false))
                    {
                        throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸伸出} 操作失败");
                    }
                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸缩回, true))
                    {
                        throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸缩回} 操作失败");
                    }



                    while (true)
                    {
                        await Task.Delay(1, linktoken);
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸缩回);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左8寸气缸缩回);
                        if (res1.HasValue && res2.HasValue && res1.Value && res2.Value)
                        {
                            break;
                        }
                    }
                    return true;
                }
                else if (wafesize == E_WafeSize._12寸)
                {
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸收回, false))
                    {
                        throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸收回} 操作失败");
                    }
                    if (!_io.WriteOutput((int)E_OutPutName.晶圆轨道左调宽气缸伸出, true))
                    {
                        throw new Exception($"输入 {E_OutPutName.晶圆轨道左调宽气缸伸出} 操作失败");
                    }

                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸缩回, false))
                    {
                        throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸缩回} 操作失败");
                    }
                    if (!_io.WriteOutput((int)E_OutPutName.夹爪左X轴气缸伸出, true))
                    {
                        throw new Exception($"输入 {E_OutPutName.夹爪左X轴气缸伸出} 操作失败");
                    }



                    while (true)
                    {
                        await Task.Delay(1, linktoken);
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆轨道左调宽气缸打开);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左12寸气缸打开);
                        if (res1.HasValue && res2.HasValue && res1.Value && res2.Value)
                        {
                            break;
                        }
                    }

                    return true;

                }
                else
                {
                    throw new Exception($"晶圆尺寸输入错误  {wafesize}");
                }
            }
            catch (OperationCanceledException ex) when (timeoutcts.IsCancellationRequested)
            {
                throw new Exception($"切换晶圆尺寸操作气缸超时");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 打开夹爪
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task<bool> OpenWafeGipper(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左闭合, false))
                {
                    throw new Exception($"操作输出信号{E_OutPutName.夹爪气缸左闭合} 失败");
                }
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左张开, true))
                {
                    throw new Exception($"操作输出信号{E_OutPutName.夹爪气缸左张开} 失败");
                }

                while (true)
                {
                    await Task.Delay(1, linktoken);
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左气缸张开);
                    if (res1.HasValue && res1.Value)
                    {
                        break;
                    }
                }
                return true;
            }
            catch (OperationCanceledException ex) when (timeoutcts.IsCancellationRequested)
            {
                throw new Exception($"等待输入信号：{E_InPutName.晶圆夹爪左气缸闭合}  超时");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 关闭夹爪
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task<bool> CloseWafeGipper(CancellationToken token = default)
        {
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.CylinderTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linktoken = cts.Token;

            try
            {
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左张开, false))
                {
                    throw new Exception($"操作输出信号{E_OutPutName.夹爪气缸左张开} 失败");
                }
                if (!_io.WriteOutput((int)E_OutPutName.夹爪气缸左闭合, true))
                {
                    throw new Exception($"操作输出信号{E_OutPutName.夹爪气缸左闭合} 失败");
                }

                while (true)
                {
                    await Task.Delay(1, linktoken);
                    bool? res = _io.ReadInput((int)E_InPutName.晶圆夹爪左气缸闭合);
                    if (res.HasValue && res.Value)
                    {
                        break;
                    }
                }
                bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                if (!res1.HasValue)
                {
                    throw new Exception($"获取输入信号{E_InPutName.晶圆夹爪左铁环有无检测} 失败");
                }
                if (res1.Value)
                {
                    throw new Exception($"{E_InPutName.晶圆夹爪左铁环有无检测}  检测到无料");
                }
                return true;

            }
            catch (OperationCanceledException ex) when (timeoutcts.IsCancellationRequested)
            {
                throw new Exception($"等待输入信号：{E_InPutName.晶圆夹爪左气缸闭合}  超时");
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 判断是否叠片
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns>true： 正常物料，无叠片     false:叠片 </returns>
        public async Task<bool> CheckStackedPieces(CancellationToken token)
        {
            try
            {
                //bool? res = _io.ReadInput((int)E_InPutName.夹爪左叠料检测);
                //if (!res.HasValue)
                //{
                //    throw new Exception($"获取输入信号{E_InPutName.夹爪左叠料检测} 失败");
                //}
                //return res.Value;

                return true;
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 移动到待机位(不检测夹爪是否有铁环)
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> MoveInitialNoScan(CancellationToken token = default)
        {
            try
            {
                CheckReady(); // 确保模组已初始化且无报警
                _logger.Info($"[{MechanismName}] 移动到待机位");
                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    return true;
                }
                else
                {
                    throw new Exception($"[{MechanismName}] 移动到待机位失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 移动到待机位并检查夹爪是否有料
        /// </summary>
        /// <param name="token"><取消令牌/param>
        /// <returns></returns>
        public async Task<bool> MoveInitial(CancellationToken token = default)
        {
            try
            {
                CheckReady(); // 确保模组已初始化且无报警
                _logger.Info($"[{MechanismName}] 移动到待机位");
                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.待机位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                    if (!res1.HasValue)
                    {
                        throw new Exception($"获取输入信号{E_InPutName.晶圆夹爪左铁环有无检测} 失败");
                    }
                    if (res1.Value)
                    {
                        throw new Exception($"{E_InPutName.晶圆夹爪左铁环有无检测}  检测到有料，检查是否带料");
                    }
                    return true;
                }
                else
                {
                    throw new Exception($"[{MechanismName}] 移动到待机位失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }




        public async Task <bool > PutOverMove(CancellationToken token = default)
        {
            try
            {
                CheckReady(); // 确保模组已初始化且无报警
                _logger.Info($"[{MechanismName}] 移动到待机位");
                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.取出安全位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                    if (!res1.HasValue)
                    {
                        throw new Exception($"获取输入信号{E_InPutName.晶圆夹爪左铁环有无检测} 失败");
                    }
                    if (res1.Value)
                    {
                        throw new Exception($"{E_InPutName.晶圆夹爪左铁环有无检测}  检测到有料，检查是否带料");
                    }
                    return true;
                }
                else
                {
                    throw new Exception($"[{MechanismName}] 移动到待机位失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 初始位置移动到取料位去取料
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> InitialMoveFeeding(CancellationToken token = default)
        {
            try
            {
                if (!await OpenWafeGipper(token))
                {
                    return false;
                }
                await Task.Delay(1000);
                if (await MoveMultiAxesToPointsAsync(new[] { (_yAxis, nameof(YAxisPoint.晶圆取料位置)) }, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token: token))
                {
                    return true;
                }
                else
                {
                    throw new Exception($"[{MechanismName}] 移动到待机位失败");
                }
            }
            catch (Exception ex)
            {
                _logger.Warn(ex.Message);
                return false;
            }
        }


        /// <summary>
        /// 取料完成移动到检测位
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> MoveDetection(CancellationToken token = default)
        {
            // 1. 创建联动取消源：外部 token 取消或内部手动 Cancel 都会触发停止
            using var timeoutcts = new CancellationTokenSource(await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()));
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutcts.Token);
            var linkedToken = cts.Token;
            try
            {
                // 任务 A：卡料检测
                Task taskA = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(5, linkedToken); // 必须 await，建议频率不要太高(如5-10ms)
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左卡料检测);
                        if (res1 == true)
                        {
                            return;
                        }
                    }
                }, linkedToken);

                // 任务 B：丢料检测
                Task taskB = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(5, linkedToken);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                        if (res2 == true)
                        {
                            return;
                        }
                    }
                }, linkedToken);
                // 触发运动
                if (!await _yAxis.MoveToPointAsync(YAxisPoint.晶圆拉出位置.ToString(), linkedToken))
                {
                    cts.Cancel();
                    throw new Exception($"[{MechanismName}] 移动触发失败");
                }
                // 任务 C：运动完成检测
                Task taskC = Task.Run(async () =>
                {
                    while (!linkedToken.IsCancellationRequested)
                    {
                        await Task.Delay(10, linkedToken);
                        var motionio = _yAxis.AxisIOStatus;
                        if (motionio == null || (motionio.MoveDone && !motionio.Moving))
                        {
                            return;
                        }
                    }
                }, linkedToken);

                // 等待任意一个完成
                Task finishedTask = await Task.WhenAny(taskA, taskB, taskC);

                // 立即确保所有其他基于 linked 的循环感知到取消
                cts.Cancel();

                // 结果判定
                if (finishedTask == taskA)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 触发卡料报警");
                }
                if (finishedTask == taskB)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 触发丢料报警");
                }
                return true; // 只有 taskC (运动正常完成) 走到这里
            }
            catch (OperationCanceledException) when (timeoutcts.IsCancellationRequested)
            {
                await _yAxis.StopAsync();
                throw new Exception($"[{MechanismName}] 运动超时");
            }
            catch (Exception ex)
            {
                await _yAxis.StopAsync();
                _logger.Warn(ex.Message);
                // 记录日志并返回
                return false;
            }
        }



        /// <summary>
        /// 将检测完成物料送到料盒
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
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
                        await Task.Delay(5, linked.Token); // 必须 await，建议频率不要太高(如5-10ms)
                        bool? res1 = _io.ReadInput((int)E_InPutName.晶圆夹爪左卡料检测);
                        if (res1 == true)
                        {
                            return;
                        }
                    }
                }, linked.Token);

                // 任务 B：丢料检测
                Task taskB = Task.Run(async () =>
                {
                    while (!linked.IsCancellationRequested)
                    {
                        await Task.Delay(5, linked.Token);
                        bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
                        if (res2 == true)
                        {
                            return;
                        }
                    }
                }, linked.Token);

                // 触发运动
                if (!await _yAxis.MoveToPointAsync(YAxisPoint.晶圆取料位置.ToString(), linked.Token))
                {
                    linked.Cancel();
                    throw new Exception($"[{MechanismName}] 移动触发失败");
                }

                // 任务 C：运动完成检测
                Task taskC = Task.Run(async () =>
                {
                    while (!linked.IsCancellationRequested)
                    {
                        await Task.Delay(10, linked.Token);
                        var motionio = _yAxis.AxisIOStatus;
                        if (motionio == null || (motionio.MoveDone && !motionio.Moving))
                        {
                            return;
                        }
                    }
                }, linked.Token);
                // 等待任意一个完成
                Task finishedTask = await Task.WhenAny(taskA, taskB, taskC);
                linked.Cancel();
                // 立即确保所有其他基于 linked 的循环感知到取消
                if (finishedTask == taskA)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 触发卡料报警");
                }
                if (finishedTask == taskB)
                {
                    await _yAxis.StopAsync();
                    throw new Exception($"[{MechanismName}] 触发丢料报警");
                }
                return true;
            }
            catch (OperationCanceledException ex)
            {
                if (timeoutcts.IsCancellationRequested)
                {
                    throw new Exception($"[{MechanismName}] 运动超时");
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


        /// <summary>
        /// 扫码枪触发方法
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<List<string>> CodeScanTigger(CancellationToken token = default)
        {
            await _lightController.SetLightValue(3, await ParamService.GetParamAsync<int>(E_Params.WorkStation1LightBrightness.ToString()));
            string str = await _codeScan.Tigger(token);
            await _lightController.SetLightValue(3, 0);
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }
            else
            {
                return str.Split('&').ToList();
            }
        }



        /// <summary>
        /// 判断移动到待机位后夹爪内是否带料
        /// </summary>
        /// <param name="token"></param>
        /// <returns> true: 无料   false :异常</returns>
        public async Task<bool> CheckGipperInsidePro(CancellationToken token = default)
        {
            bool? res2 = _io.ReadInput((int)E_InPutName.晶圆夹爪左铁环有无检测);
            if (res2 == true)
            {
                return true;
            }
            return false;
        }



        #region 内部辅助数学/验证方法


        /// <summary>
        /// 验证当前轴的枚举点位是否在底层配置中全部存在
        /// 防止因为漏建点位导致程序在运行中引发空引用异常（NullReferenceException）
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (_yAxis != null) EnsurePointsExist<YAxisPoint>(_yAxis);
            await Task.CompletedTask; // 满足 async 签名要求
        }


        #endregion 内部辅助数学/验证方法
    }
}
