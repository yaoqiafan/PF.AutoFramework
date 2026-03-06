using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.BarcodeScan.HKRobot
{
    public class HKBarcodeScan : BaseBarcodeScan
    {
        public HKBarcodeScan(string IP, int tiggerPort, int userPort, ILogService logger) : base(deviceId: $"", deviceName: $"", isSimulated: false, logger: logger)
        {

            this.IPAdress = IP;
            this.TiggerPort = tiggerPort;
            this.UserPort = userPort;

        }

        public override string IPAdress { get; }

        public override int TiggerPort { get; }

        public override int UserPort { get; }

        /// <summary>
        /// 触发客户端
        /// </summary>
        private PF.Infrastructure.Communication.TCP.TCPClient tiggerclient;

        /// <summary>
        /// 用户权限客户端
        /// </summary>
        private PF.Infrastructure.Communication.TCP.TCPClient Userpowerclient;


        private ManualResetEventSlim TiggerEvent = new ManualResetEventSlim(false);

        private ManualResetEventSlim UserParmEvent = new ManualResetEventSlim(false);


        private string TiggerRec = string.Empty;

        public override Task<bool> ChangeUserParam(object UserInfo)
        {
            throw new NotImplementedException();
        }

        public override async Task<string> Tigger()
        {
            try
            {
                string TiggerStr = "+";
                if (await tiggerclient.SendStringAsync(TiggerStr))
                {
                    return null;
                }
                Task a = Task.Run(() => TiggerEvent.Wait());
                Task b = Task.Run(() => Thread.Sleep(2000));

            }
            catch (Exception ex)
            {
                return null;
            }








        }

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
            tiggerclient.DataReceived += Tiggerclient_DataReceived;
            Userpowerclient.DataReceived += Userpowerclient_DataReceived;

            return true;
        }

        private void Userpowerclient_DataReceived(object? sender, Core.Events.DataReceivedEventArgs e)
        {
            TiggerRec = Encoding.UTF8.GetString(e.Data);
            TiggerEvent.Set();
        }

        private void Tiggerclient_DataReceived(object? sender, Core.Events.DataReceivedEventArgs e)
        {

        }

        protected override async Task InternalDisconnectAsync()
        {
            await tiggerclient.DisconnectAsync();
            await Userpowerclient.DisconnectAsync();
        }

        protected override async Task InternalResetAsync(CancellationToken token)
        {
            await tiggerclient.ReconnectAsync();
        }
    }
}
