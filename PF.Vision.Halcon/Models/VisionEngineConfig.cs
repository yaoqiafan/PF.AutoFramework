namespace PF.Vision.Halcon.Models;

/// <summary>
/// 视觉引擎运行配置，按模式差异化 Channel 容量、超时和日志详细度。
/// </summary>
public sealed record VisionEngineConfig(
    TimeSpan ExecutionTimeout,
    int      ChannelCapacity,
    bool     VerboseLogging)
{
    /// <summary>
    /// 每次过程调用前是否调用 SetWaitForDebugConnection(true)，在入口处暂停等待 HDevelop 连接。
    /// 仅 Debug 引擎启用，Production/Offline 为 false。
    /// </summary>
    public bool WaitForDebugConnection { get; init; } = false;

    /// <summary>生产引擎：30s 超时，容量 100，标准日志</summary>
    public static VisionEngineConfig Production { get; } =
        new(TimeSpan.FromSeconds(30), 100, false);

    /// <summary>调试引擎：无超时限制，容量 10，逐步详细日志，每次调用等待 HDevelop 接入</summary>
    public static VisionEngineConfig Debug { get; } =
        new(Timeout.InfiniteTimeSpan, 10, true) { WaitForDebugConnection = true };

    /// <summary>离线引擎：120s 超时，容量 500，标准日志</summary>
    public static VisionEngineConfig Offline { get; } =
        new(TimeSpan.FromSeconds(120), 500, false);
}
