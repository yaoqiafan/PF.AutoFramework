namespace PF.Core.Enums.FileTransfer;

/// <summary>传输失败原因分类，供调用方做自动化判断（重试/报警），不需要依赖字符串匹配</summary>
public enum FileTransferFailureReason
{
    /// <summary>无失败（成功）</summary>
    None,

    /// <summary>建立连接超时</summary>
    ConnectTimeout,

    /// <summary>传输过程中连接断开</summary>
    ConnectionLost,

    /// <summary>整体传输超过 TransferTimeoutMs 未完成</summary>
    TransferTimeout,

    /// <summary>分片 CRC32 或整体 FinalHash 校验不通过</summary>
    ChecksumMismatch,

    /// <summary>接收字节数与声明的 TotalLength 不一致</summary>
    LengthMismatch,

    /// <summary>帧头非法（Magic/Version 不匹配，或字段越界）</summary>
    MalformedFrame,

    /// <summary>调用方通过 CancellationToken 主动取消</summary>
    Cancelled,

    /// <summary>通道已 Dispose</summary>
    ChannelDisposed,

    /// <summary>上一次 SendAsync 尚未完成，本次调用被拒绝</summary>
    Busy,

    /// <summary>未归类的其他错误</summary>
    Unknown
}
