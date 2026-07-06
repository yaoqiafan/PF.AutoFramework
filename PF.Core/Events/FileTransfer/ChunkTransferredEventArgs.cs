using PF.Core.Enums;

namespace PF.Core.Events.FileTransfer;

/// <summary>
/// 单个分片收/发事件参数，仅用于诊断调试。
/// 一次大文件传输可能对应成百上千个分片，默认不订阅，需要通道显式开启
/// <c>EnableChunkLevelDiagnostics</c> 才会触发，避免事件风暴。
/// </summary>
public sealed class ChunkTransferredEventArgs : EventArgs
{
    /// <summary>对应的传输标识</summary>
    public required Guid TransferId { get; init; }

    /// <summary>所在 Lane 编号</summary>
    public required int LaneId { get; init; }

    /// <summary>该分片在原始数据中的偏移</summary>
    public required long ChunkOffset { get; init; }

    /// <summary>该分片长度</summary>
    public required int ChunkLength { get; init; }

    /// <summary>方向</summary>
    public required TransferDirection Direction { get; init; }
}
