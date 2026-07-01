using PF.Core.Enums.FileTransfer;

namespace PF.Core.Events.FileTransfer;

/// <summary>通道整体状态变化事件参数</summary>
public sealed class ChannelStateChangedEventArgs : EventArgs
{
    /// <summary>变化前状态</summary>
    public required FileTransferStatus OldStatus { get; init; }

    /// <summary>变化后状态</summary>
    public required FileTransferStatus NewStatus { get; init; }
}
