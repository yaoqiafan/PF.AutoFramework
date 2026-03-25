using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
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

        private IAxis _xAxis;

        private IAxis _yAxis;

        private IAxis _zAxis;

        private IIntelligentCamera _camera;


        // ── 公开的硬件访问属性（主要提供给前端 ViewModel 绑定，用于调试面板的手动控制）──
        public IAxis XAxis => _xAxis;
        public IAxis YAxis => _yAxis;
        public IAxis ZAxis => _zAxis;
        public IIntelligentCamera Camera => _camera;


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

        private IContainerProvider Provider;


        public WorkStationDetectionModule(IHardwareManagerService hardwareManagerService, IParamService paramService, IContainerProvider provider, ILogService logger) : base(E_Mechanisms.OCR识别模组.ToString(), hardwareManagerService, paramService, logger)
        {
            Provider = provider;
        }


        private WorkStationDataModule _dataModule;

        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            // ① 延迟解析硬件实例：通过硬件管理器及配置名称查找具体硬件对象
            _xAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            if (_xAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到X轴 '{E_AxisName.视觉X轴}'，请确认硬件配置。");
                return false;
            }
            _yAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            if (_yAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Y轴 '{E_AxisName.视觉Y轴}'，请确认硬件配置。");
                return false;
            }
            _zAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;

            if (_zAxis == null)
            {
                _logger.Error($"[{MechanismName}] 未找到Z轴 '{E_AxisName.视觉Z轴}'，请确认硬件配置。");
                return false;
            }
            _camera = HardwareManagerService?.GetDevice(E_Camera.OCR相机.ToString()) as IIntelligentCamera;
            if (_camera == null)
            {
                _logger.Error($"[{MechanismName}] 未找到相机 '{E_Camera.OCR相机}'，请确认硬件配置。");
                return false;
            }


            _dataModule = Provider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;

            if (_dataModule == null)
            {
                _logger.Error($"[{MechanismName}] 未找到 WorkStationDataModule 模块，请检查软件。");
                return false;
            }

            // ② 将硬件注册到当前模组：这样当单一轴/IO发生报警时，整个模组的状态会自动同步为 Alarm
            RegisterHardwareDevice(_xAxis as IHardwareDevice);
            RegisterHardwareDevice(_yAxis as IHardwareDevice);
            RegisterHardwareDevice(_zAxis as IHardwareDevice);
            RegisterHardwareDevice(_camera as IHardwareDevice);

            // 确认点位枚举在轴配置中都已示教/创建
            await ConfirmEunmPoints();
            // ③ 并行/串行连接所有硬件通信
            if (!await _xAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] X轴连接失败"); return false; }
            if (!await _yAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Y轴连接失败"); return false; }
            if (!await _zAxis.ConnectAsync(token)) { _logger.Error($"[{MechanismName}] Z轴连接失败"); return false; }
            // ④ 使能伺服电机（Power On）
            if (!await _xAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] X轴使能失败"); return false; }
            if (!await _yAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Y轴使能失败"); return false; }
            if (!await _zAxis.EnableAsync()) { _logger.Error($"[{MechanismName}] Z轴使能失败"); return false; }


            // ⑤ 执行回原点操作，建立机械绝对坐标系
            if (!await _zAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Z轴回零失败"); return false; }
            if (!await WaitHomeDoneAsync(_zAxis, await ParamService.GetParamAsync<int>(E_Params.AxisHomeTimeout.ToString()), token)) { _logger.Error($"[{MechanismName}] Z轴回零超时"); return false; }
            if (!await _xAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] X轴回零失败"); return false; }
            if (!await _yAxis.HomeAsync(token)) { _logger.Error($"[{MechanismName}] Y轴回零失败"); return false; }
            if (!await WaitHomeDoneAsync(_xAxis, await ParamService.GetParamAsync<int>(E_Params.AxisHomeTimeout.ToString()), token)) { _logger.Error($"[{MechanismName}] X轴回零超时"); return false; }
            if (!await WaitHomeDoneAsync(_yAxis, await ParamService.GetParamAsync<int>(E_Params.AxisHomeTimeout.ToString()), token)) { _logger.Error($"[{MechanismName}] Y轴回零超时"); return false; }


            //电机使能
            if (!await _xAxis.EnableAsync(token))
            {
                return false;
            }

            if (!await _yAxis.EnableAsync(token))
            {
                return false;
            }

            if (!await _zAxis.EnableAsync(token))
            {
                return false;
            }
            if (!await _zAxis.HomeAsync(token))
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
            if (_xAxis != null) await _xAxis.StopAsync();
            if (_yAxis != null) await _yAxis.StopAsync();
            if (_zAxis != null) await _zAxis.StopAsync();
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
            if (!await MoveToPointAndWaitAsync(_zAxis, nameof(ZAxisPoint.待机位), token: token))
            {
                _logger.Error($"[{MechanismName}] Z轴移动到待机位失败");
                return false;
            }
            if (!await MoveMultiAxesToPointsAsync(new[] {(_xAxis, nameof(XAxisPoint.待机位)),
                (_yAxis, nameof(YAxisPoint.待机位))  }, token: token))
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
            _1StationRecipe = _dataModule.Station1ReciepParam;
            if (_1StationRecipe == null)
            {
                _logger.Error($"工位1未加载配方");
                return false;
            }
            //先移动XY
            if (!await _xAxis.MoveAbsoluteAsync(_1StationRecipe._1PosX, XVel, XAcc, XDec, 0.08, token) || !await _yAxis.MoveAbsoluteAsync(_1StationRecipe._1PosY, YVel, YAcc, YDec, 0.1, token))
            {
                _logger.Error($"[{MechanismName}] 移动到工位1失败");
                return false;
            }
            if (IsChangedOcrCamera())
            {
                if (!await _camera.ChangeProgram(_1StationRecipe.OCRRecipeName))
                {
                    _logger.Error($"[{MechanismName}] 切换到工位1的OCR配方失败");
                    return false;
                }
            }
            if (!await WaitAxisMoveDoneAsync(_xAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token) || !await WaitAxisMoveDoneAsync(_yAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token))
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
            _2StationRecipe = _dataModule.Station2ReciepParam;
            if (_2StationRecipe == null)
            {
                _logger.Error($"工位2未加载配方");
                return false;
            }
            //先移动XY
            if (!await _xAxis.MoveAbsoluteAsync(_2StationRecipe._2PosX, XVel, XAcc, XDec, 0.08, token) || !await _yAxis.MoveAbsoluteAsync(_2StationRecipe._2PosY, YVel, YAcc, YDec, 0.1, token))
            {
                _logger.Error($"[{MechanismName}] 移动到工位2失败");
                return false;
            }
            if (IsChangedOcrCamera())
            {
                if (!await _camera.ChangeProgram(_2StationRecipe.OCRRecipeName))
                {
                    _logger.Error($"[{MechanismName}] 切换到工位2的OCR配方失败");
                    return false;
                }
            }
            if (!await WaitAxisMoveDoneAsync(_xAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token) || !await WaitAxisMoveDoneAsync(_yAxis, await ParamService.GetParamAsync<int>(E_Params.AxisMoveTimeout.ToString()), token))
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
        public async Task<string> CameraTigger(CancellationToken token = default)
        {
            return await _camera.Tigger(token);
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
            if (_xAxis != null) EnsurePointsExist<XAxisPoint>(_xAxis);
            if (_yAxis != null) EnsurePointsExist<YAxisPoint>(_yAxis);
            if (_zAxis != null) EnsurePointsExist<ZAxisPoint>(_zAxis);
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
