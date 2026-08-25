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
        public CTSLightController(string Com, string deviceId, string deviceName, bool isSimulated, ILogService logger, int channelCount = 4) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger, channelCount: channelCount)
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
        public override Task SetLightValue(int Channel, int LightValue, CancellationToken token = default)
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
                // 连接类报警：串口句柄失效→报警，恢复→防抖后自动消除
                bool faulted = controllerHandle <= 0;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.LightControllerError,
                    $"康视达光源控制器句柄无效，串口[{ComName}]通讯中断");
            }
                
            return Task.CompletedTask;
        }


        /// <summary>
        /// 读取指定通道的当前光源亮度。
        /// </summary>
        /// <param name="Channel">通道号</param>
        /// <param name="token">取消令牌</param>
        /// <returns>设备返回的亮度值</returns>
        /// <remarks>
        /// 与 <see cref="SetLightValue"/> 不同，读取失败时【抛出异常】而不是返回 0：
        /// 返回 0 的话，"读到亮度确实是 0" 与 "根本没读到" 在调用方看来完全一样，
        /// 界面会把滑块悄悄归零、误导现场以为光源已关。
        /// </remarks>
        public override Task<int> GetLightValue(int Channel, CancellationToken token = default)
        {
            if (IsSimulated)
            {
                return Task.FromResult(0);
            }

            token.ThrowIfCancellationRequested();

            int value = 0;
            int ret = CtsAPI.GetDigitalValue(CtsAPI.Rs232Mode, ref value, Channel, controllerHandle);
            if (ret != CtsAPI.SUCCESS)
            {
                throw new InvalidOperationException(
                    $"获取康视达光源控制器亮度失败，Channel：{Channel}，返回码：{ret}");
            }

            return Task.FromResult(value);
        }
    }
}
