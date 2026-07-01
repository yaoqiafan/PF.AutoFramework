namespace PF.Core.Enums.FileTransfer;

/// <summary>
/// 大文件传输通道角色。只影响连接建立方式（监听 or 主动连接），
/// 连接建立后双方均可收发，角色不代表数据流向。
/// </summary>
public enum FileTransferRole
{
    /// <summary>被动监听，等待对端连接</summary>
    Server,

    /// <summary>主动连接到对端</summary>
    Client
}
