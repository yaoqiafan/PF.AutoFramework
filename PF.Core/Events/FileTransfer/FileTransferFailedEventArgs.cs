using PF.Core.Enums.FileTransfer;

namespace PF.Core.Events.FileTransfer;

/// <summary>传输失败事件参数</summary>
public sealed class FileTransferFailedEventArgs : EventArgs
{
    /// <summary>对应的传输标识</summary>
    public required Guid TransferId { get; init; }

    /// <summary>失败原因分类</summary>
    public required FileTransferFailureReason Reason { get; init; }

    /// <summary>详情描述</summary>
    public string? Message { get; init; }
}
