using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.Params;
using System;
using System.Threading.Tasks;
using PF.Core.Entities.SecsGem.Message;

namespace PF.Core.Interfaces.SecsGem
{
    public interface ISecsGemManger : IDisposable
    {
        Task<bool> InitializeAsync();
        Task<bool> ConnectAsync();
        Task DisconnectAsync();
        Task SendMessageAsync(SecsGemMessage message);
        Task<bool> WaitSendMessageAsync(SecsGemMessage message, string systemBytesHex);

        bool IsConnected { get; }

        IParams ParamsManager { get; }
        ICommandManager CommandManager { get; }
        IinternalClient SecsGemClient { get; }
        ISecsGemMessageUpdater MessageUpdater { get; }

        event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;
    }

    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public bool OldState { get; set; }
        public bool NewState { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class SecsMessageReceivedEventArgs : EventArgs
    {
        public SecsGemMessage Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
