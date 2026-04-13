using PF.Core.Enums;
using PF.Core.Events;
using System;
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

        Task<bool> ConnectAsync(string serverIp, int serverPort, bool IsAsync = true);
        Task<bool> SendAsync(byte[] data);
        Task DisconnectAsync();
        Task ReconnectAsync();

        // 新增：发送数据并等待一次返回结果（带超时）
        Task<byte[]> WaitSentReceiveDataAsync(byte[] data, int timeoutMs);

        // 新增：在固定的时间窗口内，接收期间到达的所有数据
        Task<byte[]> ReceiveAllDataInTimeWindowAsync(int timeWindowMs);
    }
}