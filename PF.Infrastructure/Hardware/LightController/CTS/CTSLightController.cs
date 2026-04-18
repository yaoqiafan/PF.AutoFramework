using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.LightController.CTS
{
    /// <summary>
    /// 康视达光源控制器实现
    /// </summary>
    public class CTSLightController : BaseLightController
    {



        /// <summary>
        /// 构造康视达光源控制器
        /// </summary>
        public CTSLightController(string Com, string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {

            this.ComName = Com;

        }

        /// <summary>
        /// 串口名称
        /// </summary>
        public override string ComName { get; }

        /// <summary>
        /// IP地址
        /// </summary>
        public override string IPAdress { get; }

        /// <summary>
        /// 端口号
        /// </summary>
        public override int Port { get; }

        /// <summary>
        /// 设置光源亮度值
        /// </summary>
        public override Task SetLightValue(int Channel, int LightValue)
        {
            try
            {
                if (IsSimulated)
                {
                    return Task.CompletedTask;
                }
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
        /// <summary>
        /// 内部连接实现
        /// </summary>
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            try
            {
                if (IsSimulated)
                {
                    return Task.FromResult(true);
                }
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

        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected override Task InternalDisconnectAsync()
        {
            try
            {
                if (IsSimulated)
                {
                    return Task.FromResult(true);
                }
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

        /// <summary>
        /// 内部复位实现
        /// </summary>
        protected override Task InternalResetAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                if (controllerHandle <= 0 && !HasAlarm)
                    RaiseAlarm(AlarmCodes.Hardware.LightControllerError,
                        $"康视达光源控制器句柄无效，串口[{ComName}]通讯中断");
            }
                
            return Task.CompletedTask;
        }
    }
}
