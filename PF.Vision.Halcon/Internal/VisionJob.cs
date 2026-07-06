using HalconDotNet;
using PF.Core.Entities.Vision;
using PF.Core.Interfaces.Vision;

namespace PF.Vision.Halcon.Internal;

/// <summary>
/// Channel Worker 的作业单元，支持三种模式：
/// <list type="bullet">
/// <item>单步 — 设置 <see cref="Request"/>，<see cref="Pipeline"/> 为 null</item>
/// <item>管线 — 设置 <see cref="Pipeline"/>，<see cref="Request"/> 为 null</item>
/// <item>元操作 — 设置 <see cref="EngineAction"/>，直接在 Worker 线程操作 HDevEngine</item>
/// </list>
/// </summary>
internal sealed class VisionJob
{
    // ── 单步模式 ──────────────────────────────────────────────────────────────

    /// <summary>单步作业请求（管线/元操作模式下为 null）</summary>
    public VisionRequest? Request { get; init; }

    // ── 管线模式 ──────────────────────────────────────────────────────────────

    /// <summary>管线定义（单步/元操作模式下为 null）</summary>
    public VisionPipelineDefinition? Pipeline { get; init; }

    /// <summary>管线外部注入值，在步骤 inputs 中用 "$__ext__.key" 引用</summary>
    public Dictionary<string, object?>? ExternalInputs { get; init; }

    // ── 元操作模式（调试端口设置等引擎级配置） ────────────────────────────────

    /// <summary>
    /// 在 Worker 线程上对 HDevEngine 执行的元操作（非 null 时为元操作模式）。
    /// 返回 true 表示成功；抛出异常则通过 <see cref="ActionCompletion"/> 传播。
    /// </summary>
    public Func<HDevEngine, bool>? EngineAction { get; init; }

    /// <summary>元操作结果通道（元操作模式下使用，其他模式为 null）</summary>
    public TaskCompletionSource<bool>? ActionCompletion { get; init; }

    // ── 公共 ──────────────────────────────────────────────────────────────────

    /// <summary>视觉作业完成通道（单步和管线模式使用，元操作模式为 null）</summary>
    public TaskCompletionSource<IVisionResult>? Completion { get; init; }
    public required CancellationToken           CancellationToken { get; init; }

    /// <summary>true = 管线作业</summary>
    public bool IsPipeline    => Pipeline     is not null;

    /// <summary>true = 引擎元操作</summary>
    public bool IsEngineAction => EngineAction is not null;

    // ── 遗弃标记 ──────────────────────────────────────────────────────────────

    private int _abandoned;

    /// <summary>
    /// 由提交方在超时/取消后调用：调用方已不再等待结果，
    /// Worker 完成执行后须自行释放结果中的非托管资源（HObject），不再触发事件。
    /// </summary>
    public void Abandon() => Interlocked.Exchange(ref _abandoned, 1);

    /// <summary>true = 调用方已放弃等待此作业</summary>
    public bool IsAbandoned => Volatile.Read(ref _abandoned) == 1;
}
