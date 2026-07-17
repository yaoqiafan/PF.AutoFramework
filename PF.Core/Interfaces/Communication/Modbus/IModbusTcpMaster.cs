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

    /// <summary>
    /// 连接意外断开（IO 异常、帧失步、半帧超时废弃连接）或连接失败（含首次）后是否自动重连，默认 true。
    /// 主动 <see cref="DisconnectAsync"/> 不触发自动重连。
    /// </summary>
    bool AutoReconnect { get; set; }

    /// <summary>自动重连尝试间隔（毫秒），必须为正数，默认 5000</summary>
    int ReconnectIntervalMs { get; set; }

    /// <summary>
    /// MBAP 事务号是否逐请求递增，默认 true。设为 false（固定模式）时每次请求使用 <see cref="FixedTransactionId"/>
    /// 指定的固定值、计数器不再前进——用于兼容个别按固定事务号应答的网关/从站，或与抓包比对。注意：固定事务号
    /// 会削弱靠事务号识别迟到/串包响应的能力，仅建议调试场景使用。响应校验始终按本次请求实际发出的事务号匹配。
    /// </summary>
    bool TransactionIdAutoIncrement { get; set; }

    /// <summary>
    /// TransactionIdAutoIncrement 为 false（固定模式）时每次请求使用的事务号，默认 0；递增模式下本属性被忽略。
    /// 取值范围 0~65535。用于在不递增时明确指定发送事务号（而非复用计数器残留值）。
    /// </summary>
    ushort FixedTransactionId { get; set; }

    /// <summary>将 MBAP 事务号计数器重置为 0（递增模式下，下一笔请求从 1 开始）。计数器为本实例独立，不影响其他连接实例。</summary>
    void ResetTransactionId();

    /// <summary>连接成功事件</summary>
    event EventHandler<ClientConnectedEventArgs>? Connected;

    /// <summary>断开连接事件</summary>
    event EventHandler<ClientDisconnectedEventArgs>? Disconnected;

    /// <summary>错误发生事件（含请求超时、MBAP 头校验失败等协议层错误）</summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>每次请求/响应事务完成（无论成功/失败）时触发，携带完整原始报文，供调试查看</summary>
    event EventHandler<ModbusFrameExchangedEventArgs>? FrameExchanged;

    /// <summary>
    /// 异步连接到 Modbus TCP 从站。token 可取消连接过程——目标不可达时
    /// 无 token 的连接尝试会按 OS 默认超时阻塞（Windows 约 21s），外部编排无法打断。
    /// </summary>
    Task<bool> ConnectAsync(string serverIp, int serverPort, CancellationToken token = default);

    /// <summary>异步断开连接</summary>
    Task DisconnectAsync();
}
