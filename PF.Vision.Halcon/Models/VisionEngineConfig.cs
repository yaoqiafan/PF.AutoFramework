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
    /// 本引擎是否<b>允许</b>在过程入口暂停等待 HDevelop 连接。仅 Debug 引擎启用，Production/Offline 为 false。
    /// 允许不等于每次都等：只有请求显式要求（<c>VisionRequest.WaitForDebugConnection</c>，调试页的"调试执行"）
    /// 且调试服务器已启动时才真的暂停。
    /// </summary>
    public bool WaitForDebugConnection { get; init; } = false;

    /// <summary>生产引擎：30s 超时，容量 100，标准日志</summary>
    public static VisionEngineConfig Production { get; } =
        new(TimeSpan.FromSeconds(30), 100, false);

    /// <summary>调试引擎：无超时限制，容量 10，逐步详细日志，允许"调试执行"在入口等待 HDevelop 接入</summary>
    public static VisionEngineConfig Debug { get; } =
        new(Timeout.InfiniteTimeSpan, 10, true) { WaitForDebugConnection = true };

    /// <summary>离线引擎：120s 超时，容量 500，标准日志</summary>
    public static VisionEngineConfig Offline { get; } =
        new(TimeSpan.FromSeconds(120), 500, false);
}
