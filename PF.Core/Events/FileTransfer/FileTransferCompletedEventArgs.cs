using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums;

namespace PF.Core.Events.FileTransfer;

/// <summary>传输完成事件参数（发送完成、接收完成均触发，用 <see cref="Direction"/> 区分）</summary>
public sealed class FileTransferCompletedEventArgs : EventArgs
{
    /// <summary>该次传输的元数据</summary>
    public required FileTransferMetadata Metadata { get; init; }

    /// <summary>传输方向</summary>
    public required TransferDirection Direction { get; init; }

    /// <summary>
    /// 收到的完整数据。仅 <see cref="TransferDirection.Received"/> 且数据量不超过
    /// FileTransferOptions.InMemoryReceiveThresholdBytes（内存重组模式）时有值，与 <see cref="FilePath"/> 二选一
    /// </summary>
    public byte[]? Data { get; init; }

    /// <summary>
    /// 落盘临时文件的完整路径。仅 <see cref="TransferDirection.Received"/> 且数据量超过内存重组阈值
    /// （落盘模式）时有值，与 <see cref="Data"/> 二选一；文件由消费方使用完毕后负责删除
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>传输结果汇总</summary>
    public required FileTransferResult Result { get; init; }
}
