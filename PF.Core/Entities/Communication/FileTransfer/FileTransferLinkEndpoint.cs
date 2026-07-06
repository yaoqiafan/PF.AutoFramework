namespace PF.Core.Entities.Communication.FileTransfer;

/// <summary>
/// 一条 Lane（对应一个物理网口）的连接端点配置。
/// 两端按 <see cref="LaneId"/> 显式配对，不依赖列表顺序。
/// </summary>
public sealed class FileTransferLinkEndpoint
{
    /// <summary>Lane 编号，两端必须一致，用于配对而非按数组顺序隐式匹配</summary>
    public int LaneId { get; init; }

    /// <summary>本地绑定 IP：Server 角色为监听网口 IP；Client 角色为出口网口 IP（决定走哪个物理网口）</summary>
    public string LocalIp { get; init; } = "0.0.0.0";

    /// <summary>端口号：Server 角色为监听端口；Client 角色为对端监听端口，必须与对端同一 LaneId 配置的端口一致</summary>
    public int Port { get; init; }

    /// <summary>对端 IP，仅 Client 角色需要</summary>
    public string? RemoteIp { get; init; }
}
