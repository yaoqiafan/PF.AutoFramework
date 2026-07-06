using PF.Core.Entities.Communication.FileTransfer;

namespace PF.Core.Events.FileTransfer;

/// <summary>单条 Lane 状态变化事件参数（连接/断开/重连尝试/重连成功）</summary>
public sealed class LaneStatusChangedEventArgs : EventArgs
{
    /// <summary>变化后的 Lane 状态快照</summary>
    public required LaneStatus Status { get; init; }
}
