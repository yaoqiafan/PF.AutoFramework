using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Services.Hardware;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;


namespace PF.WorkStation.AutoOcr.Mechanisms
{



    [MechanismUI("检测模组", "WorkStationDetectionModuleDebugView", 1)]
    public class WorkStationDetectionModule : BaseMechanism
    {

        #region 轴点位定义
        public enum XAxisPoint
        {
            待机位 = 0,
        }
        public enum YAxisPoint
        {
            待机位 = 0,
        }
        public enum ZAxisPoint
        {
            待机位 = 0,
        }

        #endregion 轴点位定义

        private IAxis XAxis;

        private IAxis YAxis;

        private IAxis ZAxis;

        private IIntelligentCamera Camera;


        private OCRRecipeParam _1StationRecipe;

        private OCRRecipeParam _2StationRecipe;



        private double XVel;

        private double XAcc;

        private double XDec;




        private double YVel;
        private double YAcc;
        private double YDec;

        private double ZVel;
        private double ZAcc;
        private double ZDec;


        public WorkStationDetectionModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger) : base(E_Mechanisms.OCR识别模组.ToString(), hardwareManagerService, paramService, logger)
        {

        }

        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例：通过硬件管理器及配置名称查找具体硬件对象
            XAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            if (XAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到X轴 '{E_AxisName.视觉X轴}'，请确认硬件配置。");
                return false;
            }
            YAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            if (YAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Y轴 '{E_AxisName.视觉Y轴}'，请确认硬件配置。");
                return false;
            }
            ZAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;

            if (ZAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Z轴 '{E_AxisName.视觉Z轴}'，请确认硬件配置。");
                return false;
            }
            Camera = HardwareManagerService?.GetDevice(E_Camera.OCR相机.ToString()) as IIntelligentCamera;
            if (Camera == null)
            {
                _logger.Error($"[{MechanismName}] 未找到相机 '{E_Camera.OCR相机}'，请确认硬件配置。");
                return false;
            }
            // ② 将硬件注册到当前模组：这样当单一轴/IO发生报警时，整个模组的状态会自动同步为 Alarm
            RegisterHardwareDevice(XAxis as IHardwareDevice);
            RegisterHardwareDevice(YAxis as IHardwareDevice);
            RegisterHardwareDevice(ZAxis as IHardwareDevice);
            RegisterHardwareDevice(Camera as IHardwareDevice);

            // 确认点位枚举在轴配置中都已示教/创建
            await ConfirmEunmPoints();
            // ③ 并行/串行连接所有硬件通信
            if (!await XAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] X轴连接失败"); return false; }
            if (!await YAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Y轴连接失败"); return false; }
            if (!await ZAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            // ④ 使能伺服电机（Power On）
            if (!await XAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] X轴使能失败"); return false; }
            if (!await YAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Y轴使能失败"); return false; }
            if (!await ZAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }


            // ⑤ 执行回原点操作，建立机械绝对坐标系
            if (!await ZAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Z轴回零失败"); return false; }
            if (!await WaitHomeDoneAsync(ZAxis, await ParamService.GetParamAsync<int>(E_Params.AxisHomeTimeout.ToString()), token)) { _logger.Error($"[{MechanismName}] Z轴回零超时"); return false; }
            if (!await XAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] X轴回零失败"); return false; }
            if (!await YAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Y轴回零失败"); return false; }
            if (!await WaitHomeDoneAsync(XAxis, await ParamService.GetParamAsync<int>(E_Params.AxisHomeTimeout.ToString()), token)) { _logger.Error($"[{MechanismName}] X轴回零超时"); return false; }
            if (!await WaitHomeDoneAsync(YAxis, await ParamService.GetParamAsync<int>(E_Params.AxisHomeTimeout.ToString()), token)) { _logger.Error($"[{MechanismName}] Y轴回零超时"); return false; }


            //电机使能
            if (!await XAxis.EnableAsync(token))
            {
                return false;
            }

            if (!await YAxis.EnableAsync(token))
            {
                return false;
            }

            if (!await ZAxis.EnableAsync(token))
            {
                return false;
            }
            if (!await ZAxis.HomeAsync(token))
            {
                return false;
            }


            return true;
        }







        /// <summary>
        /// 模组急停/停止钩子：安全停止所有轴运动
        /// </summary>
        /// <returns></returns>
        protected override async Task InternalStopAsync()
        {
            if (XAxis != null) await XAxis.StopAsync();
            if (YAxis != null) await YAxis.StopAsync();
            if (ZAxis != null) await ZAxis.StopAsync();
        }




        #region 业务逻辑
        /**************
         * ①移动到待机位置
         * ②移动到工位1
         * ③移动到工位2
         * ④触发OCR检测
         * ⑤判断去哪一个检测工位（工位1还是工位2）
         * ⑥检测完成触发指令返回结果
         * ***************/


        /// <summary>
        /// OCR检测模组移动到待机位置：Z轴先行，确保在XY轴移动过程中不会发生碰撞
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> MoveInitial(CancellationToken token = default)
        {
            CheckReady();
            if (!await MoveToPointAndWaitAsync(ZAxis, nameof(ZAxisPoint.待机位), token: token))
            {
                _logger.Error($"[{MechanismName}] Z轴移动到待机位失败");
                return false;
            }
            if (!await MoveMultiAxesToPointsAsync(new[] {(XAxis, nameof(XAxisPoint.待机位)),
                (YAxis, nameof(YAxisPoint.待机位))  }, token: token))
            {
                _logger.Error($"[{MechanismName}] XY轴移动到待机位失败");
                return false;
            }
            _logger.Info($"[{MechanismName}] 移动到待机位成功");
            return true;
        }




        /// <summary>
        /// OCR模组移动到工位1：先移动XY轴到工位1位置，再根据工位1的OCR配方切换相机设置，最后等待轴运动完成
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>

        public async Task<bool> MoveToStation1(CancellationToken token = default)
        {
            CheckReady();
            //先移动XY
            if (!await XAxis.MoveAbsoluteAsync(_1StationRecipe._1PosX, XVel, XAcc, XDec, 0.08, token) || !await YAxis.MoveAbsoluteAsync(_1StationRecipe._1PosY, YVel, YAcc, YDec, 0.1, token))
            {
                _logger.Error($"[{MechanismName}] 移动到工位1失败");
                return false;
            }
            if (IsChangedOcrCamera())
            {
                if (!await Camera.ChangeProgram(_1StationRecipe.OCRRecipeName))
                {
                    _logger.Error($"[{MechanismName}] 切换到工位1的OCR配方失败");
                    return false;
                }
            }
            if (!await WaitAxisMoveDoneAsync(XAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token) || !await WaitAxisMoveDoneAsync(YAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token))
            {
                _logger.Error($"[{MechanismName}] XY轴移动到工位1超时");
                return false;
            }
            return true;
        }



        /// <summary>
        /// OCR模组移动到工位2：先移动XY轴到工位2位置，再根据工位2的OCR配方切换相机设置，最后等待轴运动完成
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<bool> MoveToStation2(CancellationToken token = default)
        {
            CheckReady();
            //先移动XY
            if (!await XAxis.MoveAbsoluteAsync(_2StationRecipe._2PosX, XVel, XAcc, XDec, 0.08, token) || !await YAxis.MoveAbsoluteAsync(_2StationRecipe._2PosY, YVel, YAcc, YDec, 0.1, token))
            {
                _logger.Error($"[{MechanismName}] 移动到工位2失败");
                return false;
            }
            if (IsChangedOcrCamera())
            {
                if (!await Camera.ChangeProgram(_2StationRecipe.OCRRecipeName))
                {
                    _logger.Error($"[{MechanismName}] 切换到工位2的OCR配方失败");
                    return false;
                }
            }
            if (!await WaitAxisMoveDoneAsync(XAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token) || !await WaitAxisMoveDoneAsync(YAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token))
            {
                _logger.Error($"[{MechanismName}] XY轴移动到工位2超时");
                return false;
            }
            return true;
        }



        /// <summary>
        /// OCR相机触发
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public async Task<string> CameraTigger(CancellationToken token)
        {
            return await Camera.Tigger(token);
        }




        /// <summary>
        /// 判断那个工位需要检测，移动到相应工位
        /// </summary>
        /// <param name="token"></param>
        /// <returns>1：工位1  2：工位2   -1：异常推出</returns>
        public async Task<int> WaitMoveStation(CancellationToken token)
        {
            return 1;
        }


        #endregion 业务逻辑



        #region 内部辅助验证方法

        /// <summary>
        /// 验证当前轴的枚举点位是否在底层配置中全部存在
        /// 防止因为漏建点位导致程序在运行中引发空引用异常（NullReferenceException）
        /// </summary>
        public async Task ConfirmEunmPoints()
        {
            if (XAxis != null) EnsurePointsExist<XAxisPoint>(XAxis);
            if (YAxis != null) EnsurePointsExist<YAxisPoint>(YAxis);
            if (ZAxis != null) EnsurePointsExist<ZAxisPoint>(ZAxis);
            await Task.CompletedTask; // 满足 async 签名要求
        }


        /// <summary>
        /// 判断是否需要切换OCR视觉配方：如果工位1和工位2使用了不同的OCR配方，则需要在两者之间切换相机设置
        /// </summary>
        /// <returns></returns>
        private bool IsChangedOcrCamera()
        {
            return _1StationRecipe.OCRRecipeName != _2StationRecipe.OCRRecipeName;
        }


        #endregion 内部辅助验证方法

    }
}
