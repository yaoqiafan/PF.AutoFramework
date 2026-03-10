using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Carame.IntelligentCamera.Keyence
{
    public class KeyenceIntelligentCamera : BaseIntelligentCamera
    {
        public KeyenceIntelligentCamera(string IP, int port, int timeoutms, string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId: deviceId, deviceName: deviceName, isSimulated: isSimulated, logger: logger)
        {
            this.IPAdress = IP;
            this.TiggerPort = port;
            this.TimeOutMs = timeoutms;
        }

        /// <summary>
        /// 触发客户端
        /// </summary>
        private PF.Infrastructure.Communication.TCP.TCPClient tiggerclient = new Communication.TCP.TCPClient();

        private ManualResetEventSlim TiggerEvent = new ManualResetEventSlim(false);

        private string TiggerRec = string.Empty;
        public override string IPAdress { get; }

        public override int TiggerPort { get; }

        public override int TimeOutMs { get; }

        public override Task<bool> ChangeProgram(object ProgramNumber, CancellationToken token = default)
        {
            return Task.FromResult(true);
        }

        public async override Task<string> Tigger(CancellationToken token = default)
        {
            try
            {
                TiggerEvent.Reset();
                string TiggerStr = "+";
                if (await tiggerclient.SendStringAsync(TiggerStr))
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

        protected async override Task<bool> InternalConnectAsync(CancellationToken token = default)
        {
            if (!await tiggerclient.ConnectAsync(IPAdress, TiggerPort))
            {
                return false;
            }
            tiggerclient.DataReceived += Tiggerclient_DataReceived;
            return true;
        }

        private void Tiggerclient_DataReceived(object? sender, DataReceivedEventArgs e)
        {
            TiggerRec = Encoding.UTF8.GetString(e.Data);
            TiggerEvent.Set();
        }

        protected async override Task InternalDisconnectAsync()
        {
            await tiggerclient.DisconnectAsync();
        }

        protected async override Task InternalResetAsync(CancellationToken token)
        {
            await tiggerclient.ReconnectAsync();
        }
    }
}
