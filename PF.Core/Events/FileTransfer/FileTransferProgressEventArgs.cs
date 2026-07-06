namespace PF.Core.Events.FileTransfer;

/// <summary>传输进度事件参数</summary>
public sealed class FileTransferProgressEventArgs : EventArgs
{
    /// <summary>对应的传输标识</summary>
    public required Guid TransferId { get; init; }

    /// <summary>已传输字节数（所有 Lane 累计）</summary>
    public required long BytesTransferred { get; init; }

    /// <summary>总字节数</summary>
    public required long TotalBytes { get; init; }

    /// <summary>完成百分比</summary>
    public double PercentComplete => TotalBytes == 0 ? 0 : (double)BytesTransferred / TotalBytes * 100;
}
