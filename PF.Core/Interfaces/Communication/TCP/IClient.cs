using PF.Core.Enums;
using PF.Core.Events;
using System;
using System.Text;
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
        /// <summary>编码方式，SendStringAsync 按此编码转换字符串，默认 Encoding.ASCII</summary>
        Encoding Encoding { get; set; }
        /// <summary>
        /// 目标服务器IP，创建实例时即可预先设置（不必等 ConnectAsync 调用），
        /// 供调用方在尚未连接时也能读到"这个客户端配置的是哪个地址"。
        /// </summary>
        string TargetServerIp { get; set; }
        /// <summary>目标服务器端口，语义同 <see cref="TargetServerIp"/></summary>
        int TargetServerPort { get; set; }

        /// <summary>
        /// 当前连接是否为后台异步接收模式（ConnectAsync 的 IsAsync=true）。
        /// 异步模式下同步收发方法（WaitSentReceive* / ReceiveAllDataInTimeWindowAsync）不可用，
        /// 依赖同步收发的上层（如相机）据此判断连接是否可用，被别处以异步模式连上时需要重连。
        /// </summary>
        bool IsAsyncReceiveMode { get; }

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
        /// <summary>异步发送字符串数据（按 Encoding 属性转换）</summary>
        Task<bool> SendStringAsync(string data);
        /// <summary>异步断开连接</summary>
        Task DisconnectAsync();
        /// <summary>异步重连</summary>
        Task ReconnectAsync();

        /// <summary>发送数据并等待返回结果（带超时）</summary>
        Task<byte[]> WaitSentReceiveDataAsync(byte[] data, int timeoutMs);

        /// <summary>
        /// 发送数据并持续接收，直到收到结束符（含）或超时。用于以分隔符结尾的 ASCII 协议
        /// （如基恩士以 \r 结尾）：TCP 不保证一次读取拿到完整报文，单次读取可能只得到半包。
        /// 超时抛出 <see cref="TimeoutException"/>；对端关闭返回空数组。仅在 IsAsync=false 时可用。
        /// </summary>
        Task<byte[]> WaitSentReceiveUntilAsync(byte[] data, byte terminator, int timeoutMs);

        /// <summary>在固定时间窗口内接收所有到达的数据</summary>
        Task<byte[]> ReceiveAllDataInTimeWindowAsync(int timeWindowMs);
    }
}
