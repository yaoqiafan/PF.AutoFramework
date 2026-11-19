using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Communication.Serial;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.LightController.HikCom
{


    /// <summary>
    /// 海康光源控制器实现
    /// </summary>
    public class HikComLightController : BaseLightController
    {

        /// <summary>
        /// 构建海康光源控制器
        /// </summary>
        /// <param name="serial"></param>
        /// <param name="deviceId"></param>
        /// <param name="deviceName"></param>
        /// <param name="isSimulated"></param>
        /// <param name="logger"></param>
        /// <param name="channelCount">通道数，默认 4</param>
        public HikComLightController(ISerialCommunication serial, string deviceId, string deviceName, bool isSimulated, ILogService logger, int channelCount = 4) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger, channelCount: channelCount)
        {

            lightSerial = serial;

        }

        /// <summary>
        /// 串口数据到达：追加到应答缓冲而不是覆盖。串口一条应答可能分几次 DataReceived 到达
        /// （如 "SA05" + "00#"），覆盖写法只留下最后一段，读回的亮度就是错的。
        /// </summary>
        private void LightSerial_DataReceived(object? sender, Core.Events.DataReceivedEventArgs e)
        {
            lock (_rxLock)
            {
                _rxBuffer.Append(Encoding.UTF8.GetString(e.Data));
            }
            lightEvent.Set();
        }

        private readonly object _rxLock = new();
        private readonly StringBuilder _rxBuffer = new();

        /// <summary>收到首段应答后，若暂未见帧尾 '#'，再等后续分段的静默窗口（毫秒）。</summary>
        private const int FragmentQuietMs = 50;

        private string SnapshotReply()
        {
            lock (_rxLock) return _rxBuffer.ToString();
        }

        private readonly ISerialCommunication lightSerial;

        private ManualResetEventSlim lightEvent = new ManualResetEventSlim(false);


        /// <summary>单条指令等待应答的超时（毫秒）。</summary>
        private const int ReplyTimeoutMs = 3000;

        /// <summary>
        /// 串口事务锁。应答是靠 <see cref="lightEvent"/> + <see cref="_rxBuffer"/> 这一对共享字段收的，
        /// 一条链路上同时只能有一笔问答在途——否则并发的读与写会抢走对方的应答
        /// （调试页一边读回亮度、一边拖滑块下发，就是必现场景）。
        /// </summary>
        private readonly SemaphoreSlim _txGate = new(1, 1);

        /// <summary>串口名称。本实现的串口由外部注入的 ISerialCommunication 持有，故返回空串。</summary>
        public override string ComName => "";

        /// <summary>IP 地址。串口光源不适用，返回空串。</summary>
        public override string IPAdress => "";

        /// <summary>端口号。串口光源不适用，返回 0。</summary>
        public override int Port => 0;

        /// <summary>
        /// 设置指定通道的光源亮度。指令格式 S{通道字母}{4 位亮度}#，通道号取值 1~4。
        /// </summary>
        /// <param name="Channel">通道号（1~4）</param>
        /// <param name="LightValue">光源亮度值</param>
        /// <param name="token">取消令牌</param>
        public override async Task SetLightValue(int Channel, int LightValue, CancellationToken token = default)
        {
            if (IsSimulated)
            {
                return;
            }

            // 下发失败不外抛：本方法在工站流程里被直接 await，抛出会把整条产线带停。
            // 失败信息落 Error 日志（原先是 Debug，等于看不见）。
            try
            {
                EnsureReady(Channel);
                await SendAndWaitAsync($"S{(char)('A' + Channel - 1)}{LightValue:D4}#", token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                HardwareLogger.Error($"{DeviceName} 光源控制器设置通道{Channel}亮度失败：{ex.Message}", ex);
            }
        }

        /// <summary>
        /// 读取指定通道的当前光源亮度。指令格式 S{通道字母}#，通道号取值 1~4。
        /// </summary>
        /// <param name="Channel">通道号（1~4）</param>
        /// <param name="token">取消令牌</param>
        /// <returns>设备返回的亮度值</returns>
        /// <remarks>
        /// 与 <see cref="SetLightValue"/> 不同，读取失败时【抛出异常】而不是返回 0：
        /// 返回 0 的话，"读到亮度确实是 0" 与 "根本没读到" 在调用方看来完全一样，
        /// 界面会把滑块悄悄归零、误导现场以为光源已关。
        /// </remarks>
        public override async Task<int> GetLightValue(int Channel, CancellationToken token = default)
        {
            if (IsSimulated)
            {
                return 0;
            }

            EnsureReady(Channel);

            string send = $"S{(char)('A' + Channel - 1)}#";
            string reply = await SendAndWaitAsync(send, token);

            if (!TryParseBrightness(reply, out int value))
            {
                throw new InvalidOperationException(
                    $"{DeviceName} 光源控制器读取亮度失败：应答无法解析，发送数据{send}，接收数据{reply}");
            }

            return value;
        }

        /// <summary>下发前的通道号与链路状态校验。</summary>
        private void EnsureReady(int Channel)
        {
            if (Channel < 1 || Channel > ChannelCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(Channel), $"{DeviceName}光源控制器通道数输入错误，有效范围 1~{ChannelCount}");
            }
            if (lightSerial.Status != ClientStatus.Connected)
            {
                throw new InvalidOperationException($"{DeviceName}光源控制器未连接");
            }
        }

        /// <summary>
        /// 发一条指令并等回一条应答，整笔事务串行化。
        /// </summary>
        /// <remarks>
        /// 等待写法有意不用"一个任务等事件 + 一个任务睡 3 秒，谁先到算谁"：那样一笔事务要占两个
        /// 线程池线程，而且超时那一路胜出后，等事件的那个任务并不会消失——它继续挂在 lightEvent 上，
        /// 等下一笔应答到达时被唤醒，于是把别人的应答吃掉。这里用
        /// <see cref="ManualResetEventSlim.Wait(int, CancellationToken)"/> 一次覆盖超时与取消，
        /// 只占一个线程且不留孤儿等待。
        /// </remarks>
        private async Task<string> SendAndWaitAsync(string send, CancellationToken token)
        {
            await _txGate.WaitAsync(token);
            try
            {
                // 发送前清掉缓冲：上一笔超时后才迟到的应答不能算到这一笔头上
                lock (_rxLock)
                {
                    _rxBuffer.Clear();
                    lightEvent.Reset();
                }

                await lightSerial.SendStringAsync(send);

                string reply = await Task.Run(() =>
                {
                    if (!lightEvent.Wait(ReplyTimeoutMs, token))
                    {
                        throw new TimeoutException(
                            $"{DeviceName} 光源控制器接收数据超时（{ReplyTimeoutMs}ms），发送数据为{send}");
                    }

                    // 首段已到：见到帧尾 '#' 即完整；否则只要静默窗口内还有新分段到达就继续收，
                    // 窗口内再无数据则按已收内容作为整条应答（兼容不带 '#' 的应答格式）
                    while (true)
                    {
                        lock (_rxLock)
                        {
                            if (_rxBuffer.ToString().Contains('#')) break;
                            lightEvent.Reset();
                        }
                        if (!lightEvent.Wait(FragmentQuietMs, token)) break;
                    }
                    return SnapshotReply();
                }, token);

                HardwareLogger.Debug($"{DeviceName} 光源控制器 发送数据{send} ,接收数据{reply}");
                return reply;
            }
            finally
            {
                _txGate.Release();
            }
        }

        /// <summary>
        /// 从应答里取出亮度数值。
        /// </summary>
        /// <remarks>
        /// 设备应答是带帧的（形如 SA0500#，后面还可能跟 CR/LF），直接对整串做 int.TryParse 必然失败，
        /// 读取在真机上会恒不成功。故按"取应答中的数字段"解析，兼容裸数字与带帧两种回复。
        /// </remarks>
        private static bool TryParseBrightness(string reply, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(reply))
            {
                return false;
            }

            string digits = new string(reply.Where(char.IsDigit).ToArray());
            return digits.Length > 0 && int.TryParse(digits, out value);
        }

        /// <summary>
        /// 内部连接实现：打开串口并订阅数据接收事件。
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            if (!await lightSerial.OpenAsync())
            {
                return false;
            }
            // 先减后加：BaseDevice 连接失败会重试，且断开后可再次连接，
            // 只加不减会让同一个回调挂多份，一条应答被重复写入 receivestr。
            lightSerial.DataReceived -= LightSerial_DataReceived;
            lightSerial.DataReceived += LightSerial_DataReceived;
            return true;
        }



        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected override async Task InternalDisconnectAsync()
        {
            lightSerial.DataReceived -= LightSerial_DataReceived;
            await lightSerial.CloseAsync();
        }

        /// <summary>
        /// 内部复位实现
        /// </summary>
        protected override Task InternalResetAsync(CancellationToken token)
            => Task.CompletedTask;


        /// <summary>
        /// 内部健康检查实现。
        /// 连接类报警：串口断开→报警，重连→防抖后自动消除。
        /// <see cref="BaseDevice.UpdateAutoClearableHealth"/> 第一个参数是"是否故障"，
        /// 必须传"未连接"，传"已连接"会整个反过来——连着时报警、断了反而消警。
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool faulted = lightSerial.Status != ClientStatus.Connected;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.LightControllerError,
                    $"光源控制器[{DeviceName}]串口 连接中断");
            }
            return Task.CompletedTask;
        }

    }
}
