using PF.Core.Enums.FileTransfer;

namespace PF.Core.Entities.Communication.FileTransfer;

/// <summary>
/// 大文件传输通道配置。只需配置 <see cref="Role"/> 和 <see cref="Links"/>，
/// 其余参数均有工业场景下经过考量的默认值。
/// </summary>
public sealed class FileTransferOptions
{
    /// <summary>本端角色</summary>
    public required FileTransferRole Role { get; init; }

    /// <summary>传输链路列表：1 条 = 单口模式，N 条 = 多口并行分片模式</summary>
    public List<FileTransferLinkEndpoint> Links { get; init; } = new();

    /// <summary>固定分片大小（字节）。为 null 时按 <see cref="TargetChunksPerLane"/> 等参数自动计算</summary>
    public int? ChunkSizeBytes { get; init; }

    /// <summary>自动计算分片大小时，期望每条 Lane 分到的分片数量</summary>
    public int TargetChunksPerLane { get; init; } = 200;

    /// <summary>自动计算分片大小的下限（字节）</summary>
    public int MinChunkSizeBytes { get; init; } = 256 * 1024;

    /// <summary>自动计算分片大小的上限（字节）</summary>
    public int MaxChunkSizeBytes { get; init; } = 8 * 1024 * 1024;

    /// <summary>Socket 收发缓冲区大小（字节）</summary>
    public int SocketBufferSizeBytes { get; init; } = 4 * 1024 * 1024;

    /// <summary>是否禁用 Nagle 算法（大块数据传输建议保持 true）</summary>
    public bool NoDelay { get; init; } = true;

    /// <summary>是否对每个分片做 CRC32 校验，尽早发现该分片的传输损坏</summary>
    public bool VerifyChunkCrc { get; init; } = true;

    /// <summary>是否对整体重组后的数据做端到端哈希校验</summary>
    public bool VerifyFinalHash { get; init; } = true;

    /// <summary>
    /// 单次传输允许的最大长度（字节），用于拒绝畸形 Begin 帧声称的异常长度，防止恶意/损坏帧撑爆磁盘或内存。
    /// 超过 <see cref="InMemoryReceiveThresholdBytes"/> 的传输在接收端直接落盘，不再受 CLR 单个数组约 2GB 上限约束。
    /// </summary>
    public long MaxTransferSizeBytes { get; init; } = 4096L * 1024 * 1024;

    /// <summary>
    /// 接收端内存重组阈值（字节）：不超过该值的传输在内存中重组，超过则直接落盘为临时文件，
    /// 内存占用与数据大小无关。该选择对消费方透明——完成事件的 OpenReadStream / SaveToFileAsync /
    /// GetBytesAsync 统一消费入口对两种形态一视同仁。
    /// </summary>
    public long InMemoryReceiveThresholdBytes { get; init; } = 16L * 1024 * 1024;

    /// <summary>
    /// 接收落盘模式的根目录。通道会在其下建立以 ChannelName 命名的子目录存放临时文件（以 TransferId 命名），
    /// 消费方经完成事件的 SaveToFileAsync 取走文件即转移所有权（推荐）；若直接经 FilePath 消费则
    /// 使用完毕后负责删除。通道启动时会清理该子目录中上次运行的残留文件。
    /// </summary>
    public string ReceiveDirectory { get; init; } = Path.Combine(Path.GetTempPath(), "PFFileTransfer");

    /// <summary>建立连接超时（毫秒）</summary>
    public int ConnectTimeoutMs { get; init; } = 5000;

    /// <summary>单次传输整体超时（毫秒），超时后清理该传输占用的缓冲区并判定失败</summary>
    public int TransferTimeoutMs { get; init; } = 10000;

    /// <summary>断线后的重连间隔（毫秒）。只要通道未 StopAsync，重连持续进行，不设次数上限</summary>
    public int ReconnectIntervalMs { get; init; } = 2000;

    /// <summary>空闲（无数据流动）时的应用层心跳发送间隔（毫秒）</summary>
    public int HeartbeatIntervalMs { get; init; } = 3000;

    /// <summary>连续收不到心跳超过此时长（毫秒）判定该 Lane 断线，建议为心跳间隔的 3 倍左右</summary>
    public int HeartbeatTimeoutMs { get; init; } = 9000;

    /// <summary>TCP Keepalive 起始探测时间（秒）</summary>
    public int TcpKeepAliveTimeSeconds { get; init; } = 5;

    /// <summary>TCP Keepalive 探测间隔（秒）</summary>
    public int TcpKeepAliveIntervalSeconds { get; init; } = 2;

    /// <summary>某条 Lane 传输中途断开时的处理策略</summary>
    public LaneFailurePolicy OnLaneFailure { get; init; } = LaneFailurePolicy.RerouteToSurvivingLanes;

    /// <summary>接收方向同时允许在途的传输数量上限，限制并发占用的重组缓冲/文件句柄/磁盘 I/O</summary>
    public int MaxConcurrentInboundTransfers { get; init; } = 2;
}
