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
                return false;
            }
            YAxis = HardwareManagerService.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            ZAxis = HardwareManagerService.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;
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
