using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Communication.Serial;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static NPOI.HSSF.Util.HSSFColor;

namespace PF.Infrastructure.Hardware.LightController.HikCom
{


    /// <summary>
    /// 海康光源控制器实现
    /// </summary>
    internal class HikComLightController : BaseLightController
    {

        /// <summary>
        /// 构建海康光源控制器
        /// </summary>
        /// <param name="serial"></param>
        /// <param name="deviceId"></param>
        /// <param name="deviceName"></param>
        /// <param name="isSimulated"></param>
        /// <param name="logger"></param>
        public HikComLightController(ISerialCommunication serial, string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {

            lightSerial = serial;

        }

        private void LightSerial_DataReceived(object? sender, Core.Events.DataReceivedEventArgs e)
        {
            receivestr = Encoding.UTF8.GetString(e.Data);
            lightEvent.Set();
        }

        private readonly ISerialCommunication lightSerial;

        private ManualResetEventSlim lightEvent = new ManualResetEventSlim(false);

        private string receivestr = string.Empty;

        public override string ComName => "";

        public override string IPAdress => "";

        public override int Port => 0;

        public override async Task SetLightValue(int Channel, int LightValue, CancellationToken token = default)
        {
            if (IsSimulated)
            {
                return;
            }
            try
            {
                if (Channel < 1 || Channel > 4)
                {
                    throw new Exception($"{this.DeviceName}光源控制器通道数输入错误");
                }
                if (lightSerial.Status != Core.Enums.ClientStatus.Connected)
                {
                    throw new Exception($"{this.DeviceName}光源控制器未连接");
                }
                string send = $"S{(char)('A' + Channel - 1)}{LightValue.ToString("D4")}#";
                lightEvent.Reset();
                await lightSerial.SendStringAsync(send);
                Task a = Task.Run(() => lightEvent.Wait(), token);
                Task b = Task.Run(() => Thread.Sleep(3000), token);
                Task result = await Task.WhenAny(a, b);
                if (result.Equals(a))
                {
                    HardwareLogger.Debug($"{this.DeviceName} 光源控制器 发送数据{send} ,接收数据{receivestr}");
                    return;
                }
                else
                {
                    HardwareLogger.Debug($"{this.DeviceName} 光源控制器 接收数据超时，发送数据为{send}");
                }

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return;
            }
        }


        public override async Task <int > GetLightValue(int Channel,CancellationToken token =default )
        {
            if (IsSimulated)
            {
                return 0;
            }

            try
            {
                if (Channel < 1 || Channel > 4)
                {
                    throw new Exception($"{this.DeviceName}光源控制器通道数输入错误");
                }
                if (lightSerial.Status != Core.Enums.ClientStatus.Connected)
                {
                    throw new Exception($"{this.DeviceName}光源控制器未连接");
                }
                string send = $"S{(char)('A' + Channel - 1)}#";
                lightEvent.Reset();
                await lightSerial.SendStringAsync(send);
                Task a = Task.Run(() => lightEvent.Wait(), token);
                Task b = Task.Run(() => Thread.Sleep(3000), token);
                Task result = await Task.WhenAny(a, b);
                if (result.Equals(a))
                {
                    HardwareLogger.Debug($"{this.DeviceName} 光源控制器 发送数据{send} ,接收数据{receivestr}");
                    if (int .TryParse (receivestr,out int value ))
                    {
                        return value;
                    }

                    throw new Exception($"{this.DeviceName} 光源控制器读取光源亮度失败，发送数据{send} ,接收数据{receivestr}");
                }
                else
                {
                  throw new Exception  ($"{this.DeviceName} 光源控制器 接收数据超时，发送数据为{send}");
                }

            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return 0;
            }

        }

        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            if (!await lightSerial.OpenAsync())
            {
                return false;
            }
            lightSerial.DataReceived += LightSerial_DataReceived;
            return true;
        }



        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected override async Task InternalDisconnectAsync()
        {
            await lightSerial.CloseAsync();
        }

        /// <summary>
        /// 内部复位实现
        /// </summary>
        protected override Task InternalResetAsync(CancellationToken token)
            => Task.CompletedTask;


        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool trigOk = lightSerial.Status == ClientStatus.Connected;
                UpdateAutoClearableHealth(trigOk, AlarmCodes.Hardware.LightControllerError,
                    $"光源控制器[{DeviceName}]串口 连接中断");
            }
            return Task.CompletedTask;
        }

    }
}
