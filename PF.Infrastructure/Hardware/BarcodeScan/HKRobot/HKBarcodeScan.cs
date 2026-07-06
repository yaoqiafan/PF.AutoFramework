using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Communication.TCP;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan.HKRobot
{
    /// <summary>
    /// 海康扫码枪实现。底层的触发通道/用户权限通道两个 TCP 连接由外部注入
    /// （通常来自 ICommunicationManagerService 按配置创建的 IClient 实例，AutoStart=false，
    /// 连接生命周期交由本类的 InternalConnectAsync/InternalDisconnectAsync 驱动，不由通讯管理器抢先连接）。
    /// </summary>
    public class HKBarcodeScan : BaseBarcodeScan
    {
        /// <summary>
        /// 构造海康扫码枪。IP/端口不再单独传入——直接读取注入客户端的 TargetServerIp/TargetServerPort，
        /// 避免和通讯实例配置出现两份数据源不一致的问题。
        /// </summary>
        /// <param name="triggerClient">触发通道 TCP 客户端（外部注入，未连接状态，已配置好 TargetServerIp/TargetServerPort）</param>
        /// <param name="userPowerClient">用户权限通道 TCP 客户端（外部注入，未连接状态）</param>
        /// <param name="timeoutms">应用协议超时时间（毫秒）——等待扫码枪响应的业务超时，与底层TCP连接本身无关，因此仍需显式传入</param>
        /// <param name="deviceId">设备唯一标识</param>
        /// <param name="deviceName">设备显示名称</param>
        /// <param name="isSimulated">是否为模拟模式</param>
        /// <param name="logger">日志服务</param>
        public HKBarcodeScan(IClient triggerClient, IClient userPowerClient, int timeoutms,
            string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            tiggerclient = triggerClient;
            Userpowerclient = userPowerClient;
            this.TimeOutMs = timeoutms;
        }

        /// <summary>
        /// IP地址（直接读取触发通道客户端的目标地址，触发/用户权限两通道理应指向同一主机）
        /// </summary>
        public override string IPAdress => tiggerclient.TargetServerIp;

        /// <summary>
        /// 触发端口（直接读取触发通道客户端的目标端口）
        /// </summary>
        public override int TiggerPort => tiggerclient.TargetServerPort;

        /// <summary>
        /// 用户端口（直接读取用户权限通道客户端的目标端口）
        /// </summary>
        public override int UserPort => Userpowerclient.TargetServerPort;

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public override int TimeOutMs { get; }

        /// <summary>
        /// 触发客户端（外部注入）
        /// </summary>
        private readonly IClient tiggerclient;

        /// <summary>
        /// 用户权限客户端（外部注入）
        /// </summary>
        private readonly IClient Userpowerclient;


        private ManualResetEventSlim TiggerEvent = new ManualResetEventSlim(false);

        private ManualResetEventSlim UserParmEvent = new ManualResetEventSlim(false);


        private string TiggerRec = string.Empty;


        private string UserParmRec = string.Empty;

        /// <summary>
        /// 切换用户参数
        /// </summary>
        public override async Task<bool> ChangeUserParam(object UserInfo, CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return true; }


                if (int.TryParse(UserInfo.ToString(), out int ID))
                {
                    UserParmEvent.Reset();
                    if (!await Userpowerclient.SendStringAsync("<Get,UserCur>"))
                    {
                        throw new Exception("海康扫码枪获取当前用户错误");
                    }
                    Task a = Task.Run(() => UserParmEvent.Wait(), token);
                    Task b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                    Task result = await Task.WhenAny(a, b);
                    if (!result.Equals(a))
                    {
                        throw new Exception("海康扫码枪获取当前用户错误");
                    }
                    if (UserParmRec.Replace(">", "").Split(',').Length == 3 && UserParmRec.Replace(">", "").Split(',')[2] == ID.ToString())
                    {
                        return true;
                    }
                    UserParmEvent.Reset();
                    if (!await Userpowerclient.SendStringAsync("<Set,Acq,0>"))
                    {
                        throw new Exception("海康扫码枪关闭流错误");
                    }
                    a = Task.Run(() => UserParmEvent.Wait(), token);
                    b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                    result = await Task.WhenAny(a, b);
                    if (!result.Equals(a))
                    {
                        throw new Exception("海康扫码枪关闭流错误");
                    }
                    if (!UserParmRec.Contains("OK"))
                    {
                        throw new Exception("海康扫码枪关闭流错误");
                    }
                    UserParmEvent.Reset();
                    if (!await Userpowerclient.SendStringAsync($"<Set,UserCur,{ID}>"))
                    {
                        throw new Exception("海康扫码枪设置用户错误");
                    }
                    a = Task.Run(() => UserParmEvent.Wait(), token);
                    b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                    result = await Task.WhenAny(a, b);
                    if (!result.Equals(a))
                    {
                        throw new Exception("海康扫码枪设置用户错误");
                    }
                    if (!UserParmRec.Contains("OK"))
                    {
                        throw new Exception("海康扫码枪设置用户错误");
                    }
                    UserParmEvent.Reset();
                    if (!await Userpowerclient.SendStringAsync($"<Set,Acq,1>"))
                    {
                        throw new Exception("海康扫码枪打开流错误");
                    }
                    a = Task.Run(() => UserParmEvent.Wait(), token);
                    b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                    result = await Task.WhenAny(a, b);
                    if (!result.Equals(a))
                    {
                        throw new Exception("海康扫码枪打开流错误");
                    }
                    if (!UserParmRec.Contains("OK"))
                    {
                        throw new Exception("海康扫码枪打开流错误");
                    }
                    await tiggerclient.ReconnectAsync();
                    return true;
                }
                else
                {
                    throw new Exception("海康扫码枪设置用户错误：设置参数错误");
                }
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return false;
            }

        }

        /// <summary>
        /// 触发扫码
        /// </summary>
        public override async Task<string> Tigger(CancellationToken token = default)
        {
            try
            {
                if (IsSimulated) { return "当前设备模拟模式中，触发测试！"; }


                TiggerEvent.Reset();
                string TiggerStr = "+";
                if (!await tiggerclient.SendStringAsync(TiggerStr))
                {
                    return null;
                }
                Task a = Task.Run(() => TiggerEvent.Wait(), token);
                Task b = Task.Run(() => Thread.Sleep(TimeOutMs), token);
                Task result = await Task.WhenAny(a, b);
                if (result.Equals(a))
                {
                    return TiggerRec;
                }
                else
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                HardwareLogger.Debug(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// 内部连接实现
        /// </summary>
        protected override async Task<bool> InternalConnectAsync(CancellationToken token)
        {
            if (!await tiggerclient.ConnectAsync(IPAdress, TiggerPort))
            {
                return false;
            }
            if (!await Userpowerclient.ConnectAsync(IPAdress, UserPort))
            {
                return false;
            }
            tiggerclient.DataReceived -= Tiggerclient_DataReceived;
            Userpowerclient.DataReceived -= Userpowerclient_DataReceived;
            tiggerclient.DataReceived += Tiggerclient_DataReceived;
            Userpowerclient.DataReceived += Userpowerclient_DataReceived;

            return true;
        }

        private void Userpowerclient_DataReceived(object? sender, Core.Events.DataReceivedEventArgs e)
        {
            UserParmRec = Encoding.UTF8.GetString(e.Data);
            UserParmEvent.Set();
        }

        private void Tiggerclient_DataReceived(object? sender, Core.Events.DataReceivedEventArgs e)
        {
            TiggerRec = Encoding.UTF8.GetString(e.Data);
            TiggerEvent.Set();
        }

        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected override async Task InternalDisconnectAsync()
        {
            await tiggerclient.DisconnectAsync();
            await Userpowerclient.DisconnectAsync();
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
                bool trigOk = tiggerclient.Status == ClientStatus.Connected;
                bool userOk = true;
                // 连接类报警：TCP 断连→报警，重连→防抖后自动消除
                bool faulted = !trigOk || !userOk;
                UpdateAutoClearableHealth(faulted, AlarmCodes.Hardware.BarcodeScannerHeartbeatTimeout,
                    $"扫码枪[{DeviceName}]TCP 连接中断（触发端口={trigOk}, 用户端口={userOk}）");
            }

            return Task.CompletedTask;
        }
    }
}
