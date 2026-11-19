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
                EnsureChannel(Channel);
                int ret = CtsAPI.SetDigitalValue(CtsAPI.Rs232Mode, Channel, LightValue, controllerHandle);
                if (ret == CtsAPI.SUCCESS)
                {
                    return Task.CompletedTask;
                }
                else
                {
                    throw new Exception($"设置康视达光源控制器亮度失败，Channel：{Channel}，LightValue：{LightValue}，返回码：{ret}");
                }
            }
            catch (Exception ex)
            {
                // 下发失败不外抛（与海康串口光源一致：工站流程直接 await，抛出会带停产线），
                // 但必须记 Error——原先记 Debug，现场日志里看不到
                HardwareLogger.Error($"[{DeviceName}] {ex.Message}", ex);
                return Task.CompletedTask;
            }
        }

        /// <summary>
        /// 下发前确认串口已打开。通道号不在此校验：SDK 的 ChannelIndex 起始值（0/1）未经上机确认，
        /// 误判会把原本能用的通道挡掉，越界交给 SDK 返回码报告。
        /// </summary>
        private void EnsureChannel(int channel)
        {
            if (controllerHandle <= 0)
                throw new InvalidOperationException($"{DeviceName} 光源控制器串口未打开");
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
                HardwareLogger.Error($"[{DeviceName}] {ex.Message}", ex);
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
                if (controllerHandle <= 0)
                {
                    return Task.CompletedTask;
                }
                int RT3 = CtsAPI.ReleaseSerialPort(controllerHandle);
                // 无论释放成功与否句柄都作废：留着旧值会让健康检查一直判定"已连接"
                controllerHandle = 0;
                if (RT3 != CtsAPI.SUCCESS)
                {
                    throw new Exception($"关闭康视达光源控制器失败，错误码：{RT3}");
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                HardwareLogger.Warn($"[{DeviceName}] {ex.Message}", ex);
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
