using log4net.Repository.Hierarchy;
using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Services.Hardware;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace PF.WorkStation.AutoOcr.Mechanisms
{



    [MechanismUI("检测模组", "Test", 1)]
    public class WorkStationDetectionModule : BaseMechanism
    {

        private IAxis XAxis;

        private IAxis YAxis;

        private IAxis ZAxis;

        public WorkStationDetectionModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger) : base(E_Mechanisms.OCR识别模组.ToString(), hardwareManagerService, paramService, logger)
        {

        }

        protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
        {

            XAxis = HardwareManagerService?.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            if (XAxis == null)
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉X轴}未注册", Core.Enums.LogLevel.Error);
                return false;
            }
            YAxis = HardwareManagerService.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            if (YAxis == null)
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉Y轴}未注册", Core.Enums.LogLevel.Error);
                return false;
            }
            ZAxis = HardwareManagerService.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;
            if (ZAxis == null)
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉Z轴}未注册", Core.Enums.LogLevel.Error);
                return false;
            }
            RegisterHardwareDevice(XAxis as IHardwareDevice);
            RegisterHardwareDevice(YAxis as IHardwareDevice);
            RegisterHardwareDevice(ZAxis as IHardwareDevice);

            //电机使能
            if (!await XAxis.EnableAsync(token))
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉X轴}使能失败", Core.Enums.LogLevel.Error);
                return false;
            }

            if (!await YAxis.EnableAsync(token))
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉Y轴}使能失败", Core.Enums.LogLevel.Error);
                return false;
            }

            if (!await ZAxis.EnableAsync(token))
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉Z轴}使能失败", Core.Enums.LogLevel.Error);
                return false;
            }
            if (!await ZAxis.HomeAsync(token))
            {
                _logger.ShowUiMessage($"{E_AxisName.视觉Z轴}回零失败", Core.Enums.LogLevel.Error);
                return false;
            }

            if (!await WaitHomeDone(ZAxis, token))
            {
                return false;
            }

            if (!await XAxis.HomeAsync(token) || !await YAxis.HomeAsync(token))
            {
                return false;
            }
            if (!await WaitHomeDone(XAxis, token) || !await WaitHomeDone(YAxis, token))
            {
                return false;
            }
            return true;
        }


        private async Task<bool> WaitHomeDone(IAxis axis, CancellationToken token)
        {
            try
            {
                Task a = Task.Run(async () =>
                {
                    while (true)
                    {
                        await Task.Delay(10, token);
                        if (axis.AxisIOStatus.MoveDone && !axis.AxisIOStatus.Moving)
                        {
                            return;
                        }
                    }
                });
                Task b = Task.Delay(await ParamService.GetParamAsync<int>(E_TimeOut.电机回零超时.ToString()), token);
                Task result = await Task.WhenAny(a, b);
                if (result.Equals(a))
                {
                    return true;
                }
                await axis.StopAsync(token);
                return false;
            }
            catch (Exception ex)
            {
                await axis.StopAsync(token);
                return false;
            }
        }





        protected override Task InternalStopAsync()
        {
            throw new NotImplementedException();
        }




        #region 业务逻辑
        /**************
         * ①移动到待机位置
         * ②移动到工位1
         * ③移动到工位2
         * ④触发OCR检测
         * 
         * 
         * ***************/




        #endregion 业务逻辑

    }
}
