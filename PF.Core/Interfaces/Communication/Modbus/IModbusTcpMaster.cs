using PF.Core.Enums;
using PF.Core.Events;

namespace PF.Core.Interfaces.Communication.Modbus;

/// <summary>
/// Modbus TCP 主站接口，结构参照 <see cref="PF.Core.Interfaces.Communication.TCP.IClient"/>。
/// </summary>
public interface IModbusTcpMaster : IModbusMaster, IDisposable
{
    /// <summary>目标从站服务端 IP</summary>
    string ServerIp { get; }

    /// <summary>目标从站服务端端口（Modbus TCP 标准端口为 502）</summary>
    int ServerPort { get; }

    /// <summary>连接状态</summary>
    ClientStatus Status { get; }

    /// <summary>连接建立时间</summary>
    DateTime ConnectTime { get; }

    /// <summary>连接成功事件</summary>
    event EventHandler<ClientConnectedEventArgs>? Connected;

    /// <summary>断开连接事件</summary>
    event EventHandler<ClientDisconnectedEventArgs>? Disconnected;

    /// <summary>错误发生事件（含请求超时、MBAP 头校验失败等协议层错误）</summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>每次请求/响应事务完成（无论成功/失败）时触发，携带完整原始报文，供调试查看</summary>
    event EventHandler<ModbusFrameExchangedEventArgs>? FrameExchanged;

    /// <summary>异步连接到 Modbus TCP 从站</summary>
    Task<bool> ConnectAsync(string serverIp, int serverPort);

    /// <summary>异步断开连接</summary>
    Task DisconnectAsync();
}
