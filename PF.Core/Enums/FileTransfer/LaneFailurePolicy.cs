namespace PF.Core.Enums.FileTransfer;

/// <summary>某条 Lane 在传输过程中断开时的处理策略</summary>
public enum LaneFailurePolicy
{
    /// <summary>任意一条 Lane 断开，整次传输立即判失败</summary>
    FailFast,

    /// <summary>断开 Lane 尚未发出的分片改派给存活 Lane，只降速不失败（仅剩最后一条 Lane 时退化为 FailFast）</summary>
    RerouteToSurvivingLanes
}
