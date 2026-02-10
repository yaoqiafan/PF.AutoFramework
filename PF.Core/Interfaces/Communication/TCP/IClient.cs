using PF.Core.Enums;
using PF.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Communication.TCP
{
    public interface IClient : IDisposable
    {
        string ClientId { get; }
        ClientStatus Status { get; }
        string ServerIp { get; }
        int ServerPort { get; }
        string LocalEndPoint { get; }
        string RemoteEndPoint { get; }
        DateTime ConnectTime { get; }

        event EventHandler<ClientConnectedEventArgs> Connected;
        event EventHandler<ClientDisconnectedEventArgs> Disconnected;
        event EventHandler<DataReceivedEventArgs> DataReceived;
        event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        Task<bool> ConnectAsync(string serverIp, int serverPort);
        Task<bool> SendAsync(byte[] data);
        Task DisconnectAsync();
        Task ReconnectAsync();
    }
}
