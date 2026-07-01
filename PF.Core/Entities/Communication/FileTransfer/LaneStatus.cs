namespace PF.Core.Entities.Communication.FileTransfer;

/// <summary>单条 Lane 的实时状态快照，供诊断/UI 展示使用</summary>
public sealed class LaneStatus
{
    /// <summary>Lane 编号</summary>
    public int LaneId { get; init; }

    /// <summary>当前是否已连接</summary>
    public bool IsConnected { get; init; }

    /// <summary>对端地址（已连接时有值）</summary>
    public string? RemoteEndPoint { get; init; }

    /// <summary>本次连接建立时间</summary>
    public DateTime? ConnectedTime { get; init; }

    /// <summary>累计重连尝试次数</summary>
    public int ReconnectAttempts { get; init; }

    /// <summary>累计发送字节数</summary>
    public long BytesSent { get; init; }

    /// <summary>累计接收字节数</summary>
    public long BytesReceived { get; init; }

    /// <summary>当前吞吐（MB/s），用于判断哪条 Lane 拖慢整体</summary>
    public double CurrentThroughputMBps { get; init; }

    /// <summary>最近一次收到心跳（或数据）的时间</summary>
    public DateTime? LastHeartbeatTime { get; init; }
}
