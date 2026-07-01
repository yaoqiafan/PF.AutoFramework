namespace PF.Core.Enums.FileTransfer;

/// <summary>
/// 传输通道整体状态，由各条 Lane 的连接状态聚合而来。
/// </summary>
public enum FileTransferStatus
{
    /// <summary>已停止（未调用 StartAsync 或已 StopAsync）</summary>
    Stopped,

    /// <summary>正在启动（监听器绑定 / 首次连接中）</summary>
    Starting,

    /// <summary>Server 角色下已启动但尚无客户端连接</summary>
    WaitingForPeer,

    /// <summary>全部配置的 Lane 均已连接</summary>
    Connected,

    /// <summary>部分 Lane 断开，但仍有 Lane 存活，可降级传输</summary>
    Degraded,

    /// <summary>全部 Lane 均已断开</summary>
    Faulted
}
