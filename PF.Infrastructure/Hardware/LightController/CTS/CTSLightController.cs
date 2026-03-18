using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.LightController.CTS
{
    public class CTSLightController : BaseLightController
    {



        public CTSLightController(string Com, string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {

            this.ComName = Com;

        }

        public override string ComName { get; }

        public override string IPAdress { get; }

        public override int Port { get; }

        public override Task SetLightValue(int Channel, int LightValue)
        {
            try
            {
                if (CtsAPI.SetDigitalValue(CtsAPI.Rs232Mode, Channel, LightValue, controllerHandle) == CtsAPI.SUCCESS)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    throw new Exception($"设置康视达光源控制器亮度失败，Channel：{Channel}，LightValue：{LightValue}");
                }
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.CompletedTask;
            }
        }
        private Int64 controllerHandle = 0;
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            try
            {
                if (ComName.Length < 4)
                {
                    throw new Exception($"ComName {ComName} is invalid");
                }
                if (!int.TryParse(ComName.Substring(3, ComName.Length - 3), out int ComNumber))
                {
                    throw new Exception($"ComName {ComName} is invalid");
                }
                int RET = CtsAPI.CreateSerialPort(ComNumber, ref controllerHandle);
                if (RET == CtsAPI.SUCCESS)
                {
                    return Task.FromResult(true);
                }
                else
                {
                    throw new Exception($"打开康视达光源控制器失败，错误码：{RET}");
                }

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return Task.FromResult(false);
            }

        }

        protected override Task InternalDisconnectAsync()
        {
            try
            {
                int RT3 = CtsAPI.ReleaseSerialPort(controllerHandle);
                if (RT3 == CtsAPI.SUCCESS)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    throw new Exception($"关闭康视达光源控制器失败，错误码：{RT3}");
                }
            }
            catch (Exception ex)
            {
                return Task.CompletedTask;
            }
        }

        protected override Task InternalResetAsync(CancellationToken token)
        {

            return Task.CompletedTask;
        }
    }
}
