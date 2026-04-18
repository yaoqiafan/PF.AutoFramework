using PF.Core.Enums;
using PF.Core.Events;
using System;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Communication.TCP
{
    /// <summary>
    /// TCP客户端接口
    /// </summary>
    public interface IClient : IDisposable
    {
        /// <summary>客户端ID</summary>
        string ClientId { get; }
        /// <summary>连接状态</summary>
        ClientStatus Status { get; }
        /// <summary>服务器IP地址</summary>
        string ServerIp { get; }
        /// <summary>服务器端口号</summary>
        int ServerPort { get; }
        /// <summary>本地端点</summary>
        string LocalEndPoint { get; }
        /// <summary>远程端点</summary>
        string RemoteEndPoint { get; }
        /// <summary>连接时间</summary>
        DateTime ConnectTime { get; }

        /// <summary>连接成功事件</summary>
        event EventHandler<ClientConnectedEventArgs> Connected;
        /// <summary>断开连接事件</summary>
        event EventHandler<ClientDisconnectedEventArgs> Disconnected;
        /// <summary>数据接收事件</summary>
        event EventHandler<DataReceivedEventArgs> DataReceived;
        /// <summary>错误发生事件</summary>
        event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>异步连接到服务器</summary>
        Task<bool> ConnectAsync(string serverIp, int serverPort, bool IsAsync = true);
        /// <summary>异步发送数据</summary>
        Task<bool> SendAsync(byte[] data);
        /// <summary>异步断开连接</summary>
        Task DisconnectAsync();
        /// <summary>异步重连</summary>
        Task ReconnectAsync();

        /// <summary>发送数据并等待返回结果（带超时）</summary>
        Task<byte[]> WaitSentReceiveDataAsync(byte[] data, int timeoutMs);

        /// <summary>在固定时间窗口内接收所有到达的数据</summary>
        Task<byte[]> ReceiveAllDataInTimeWindowAsync(int timeWindowMs);
    }
}
