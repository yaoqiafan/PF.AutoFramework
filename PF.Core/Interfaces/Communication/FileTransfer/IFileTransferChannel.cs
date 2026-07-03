using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums.FileTransfer;
using PF.Core.Events.FileTransfer;

namespace PF.Core.Interfaces.Communication.FileTransfer;

/// <summary>
/// 两台设备之间的大字节数组传输通道。角色（Server/Client）与链路（Lane）数量均由
/// <see cref="FileTransferOptions"/> 配置，同一套接口两端通用，只需切换 Role 即可对调
/// 监听/连接行为。通道只负责搬运 <c>byte[]</c>，不关心内容语义。
/// </summary>
public interface IFileTransferChannel : IDisposable
{
    /// <summary>通道名称，便于日志/诊断区分多个通道实例</summary>
    string ChannelName { get; }

    /// <summary>本端角色</summary>
    FileTransferRole Role { get; }

    /// <summary>通道整体状态，由各 Lane 状态聚合</summary>
    FileTransferStatus Status { get; }

    /// <summary>配置的链路列表</summary>
    IReadOnlyList<FileTransferLinkEndpoint> Links { get; }

    /// <summary>各条 Lane 的实时状态快照</summary>
    IReadOnlyList<LaneStatus> LaneStatuses { get; }

    /// <summary>是否启用分片级诊断事件（<see cref="ChunkTransferred"/>），默认 false，调试联调时按需开启</summary>
    bool EnableChunkLevelDiagnostics { get; set; }

    /// <summary>通道整体状态发生变化时触发</summary>
    event EventHandler<ChannelStateChangedEventArgs> StateChanged;

    /// <summary>单条 Lane 连接状态发生变化时触发（连接/断开/开始重连）</summary>
    event EventHandler<LaneStatusChangedEventArgs> LaneStatusChanged;

    /// <summary>断线后重连成功时触发（比单看 StateChanged 更明确地标记"恢复"这一时刻）</summary>
    event EventHandler<LaneStatusChangedEventArgs> LaneReconnected;

    /// <summary>传输进度更新时触发</summary>
    event EventHandler<FileTransferProgressEventArgs> TransferProgress;

    /// <summary>传输完成时触发（发送完成、接收完成均触发，用 Direction 区分）</summary>
    event EventHandler<FileTransferCompletedEventArgs> TransferCompleted;

    /// <summary>传输失败时触发</summary>
    event EventHandler<FileTransferFailedEventArgs> TransferFailed;

    /// <summary>单个分片收/发时触发，仅诊断用途，需 <see cref="EnableChunkLevelDiagnostics"/> 为 true 才会触发</summary>
    event EventHandler<ChunkTransferredEventArgs>? ChunkTransferred;

    /// <summary>
    /// 启动通道：Server 角色开始监听配置的各 Lane 端口；Client 角色开始连接对端。
    /// 启动后台连接守护循环，断线后持续自动重连，不因单次连接失败而放弃。
    /// </summary>
    Task<bool> StartAsync(CancellationToken token = default);

    /// <summary>停止通道，断开所有 Lane 连接并释放监听资源</summary>
    Task StopAsync();

    /// <summary>
    /// 发送一段字节数组。内部按配置自动切分并轮询分配到各条 Lane 并行发送，
    /// 全部到齐并通过校验后返回结果。同一时刻只允许一次在途发送，
    /// 上一次尚未完成时调用会立即返回 <see cref="FileTransferFailureReason.Busy"/>。
    /// </summary>
    Task<FileTransferResult> SendAsync(byte[] data, FileTransferMetadata metadata, CancellationToken token = default);

    /// <summary>
    /// <see cref="SendAsync(byte[], FileTransferMetadata, CancellationToken)"/> 的零拷贝版本：
    /// 调用方可传入池化/复用缓冲区的切片，避免为发送而额外物化一份 byte[]。
    /// 缓冲区在本方法返回前不得被复用或归还。
    /// </summary>
    Task<FileTransferResult> SendAsync(ReadOnlyMemory<byte> data, FileTransferMetadata metadata, CancellationToken token = default);

    /// <summary>
    /// 直接发送磁盘上的文件：按分片边读边发，全程内存占用只有「Lane 数 × 发送流水线深度 × 分片大小」量级，
    /// 与文件大小无关。启用 VerifyFinalHash 时会先顺序读一遍文件计算整体哈希（协议要求 Begin 帧先携带哈希），
    /// 再读第二遍发送。
    /// </summary>
    Task<FileTransferResult> SendFileAsync(string filePath, FileTransferMetadata metadata, CancellationToken token = default);
}
