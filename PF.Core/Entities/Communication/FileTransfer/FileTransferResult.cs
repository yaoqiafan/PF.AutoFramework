using PF.Core.Enums.FileTransfer;

namespace PF.Core.Entities.Communication.FileTransfer;

/// <summary>一次传输（发送或接收）完成后的结果汇总</summary>
public sealed class FileTransferResult
{
    /// <summary>是否成功</summary>
    public bool Success { get; init; }

    /// <summary>对应的传输标识</summary>
    public Guid TransferId { get; init; }

    /// <summary>耗时</summary>
    public TimeSpan Elapsed { get; init; }

    /// <summary>平均吞吐（MB/s）</summary>
    public double ThroughputMBps { get; init; }

    /// <summary>各 Lane 实际传输的字节数，键为 LaneId，用于诊断哪条链路偏慢</summary>
    public IReadOnlyDictionary<int, long> BytesPerLane { get; init; } = new Dictionary<int, long>();

    /// <summary>失败原因，成功时为 <see cref="FileTransferFailureReason.None"/></summary>
    public FileTransferFailureReason FailureReason { get; init; } = FileTransferFailureReason.None;

    /// <summary>失败详情描述</summary>
    public string? ErrorMessage { get; init; }
}
