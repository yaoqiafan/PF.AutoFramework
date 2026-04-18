using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan.HKRobot
{
    /// <summary>
    /// 海康扫码枪实现
    /// </summary>
    public class HKBarcodeScan : BaseBarcodeScan
    {
        /// <summary>
        /// 构造海康扫码枪
        /// </summary>
        public HKBarcodeScan(string IP, int tiggerPort, int userPort, int timeoutms, string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {

            this.IPAdress = IP;
            this.TiggerPort = tiggerPort;
            this.UserPort = userPort;
            this.TimeOutMs = timeoutms;

        }

        /// <summary>
        /// IP地址
        /// </summary>
        public override string IPAdress { get; }

        /// <summary>
        /// 触发端口
        /// </summary>
        public override int TiggerPort { get; }

        /// <summary>
        /// 用户端口
        /// </summary>
        public override int UserPort { get; }

        /// <summary>
        /// 超时时间（毫秒）
        /// </summary>
        public override int TimeOutMs { get; }

        /// <summary>
        /// 触发客户端
        /// </summary>
        private PF.Infrastructure.Communication.TCP.TCPClient tiggerclient=new Communication.TCP.TCPClient ();

        /// <summary>
        /// 用户权限客户端
        /// </summary>
        private PF.Infrastructure.Communication.TCP.TCPClient Userpowerclient=new Communication.TCP.TCPClient ();


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
                    Task result = Task.WhenAny(a, b);
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
                    result = Task.WhenAny(a, b);
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
                    result = Task.WhenAny(a, b);
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
                    result = Task.WhenAny(a, b);
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
        protected override async Task InternalResetAsync(CancellationToken token)
        {
            await tiggerclient.ReconnectAsync();
        }

        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (!IsSimulated)
            {
                bool trigOk = tiggerclient.Status == ClientStatus.Connected;
                bool userOk = Userpowerclient.Status == ClientStatus.Connected;
                if ((!trigOk || !userOk) && !HasAlarm)
                    RaiseAlarm(AlarmCodes.Hardware.BarcodeScannerHeartbeatTimeout,
                        $"扫码枪[{DeviceName}]TCP 连接中断（触发端口={trigOk}, 用户端口={userOk}）");
            }

            return Task.CompletedTask;
        }
    }
}
