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

    /// <summary>收到的完整数据，仅 <see cref="TransferDirection.Received"/> 时有值</summary>
    public byte[]? Data { get; init; }

    /// <summary>传输结果汇总</summary>
    public required FileTransferResult Result { get; init; }
}
