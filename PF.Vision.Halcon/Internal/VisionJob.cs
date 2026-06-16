using PF.Core.Interfaces.Vision;

namespace PF.Vision.Halcon.Internal;

/// <summary>
/// Channel Worker 的作业单元。
/// TaskCompletionSource 将异步调用方与单线程 Worker 解耦：
/// 调用方 await tcs.Task，Worker 执行完成后 TrySetResult。
/// </summary>
internal sealed class VisionJob
{
    public required VisionRequest Request { get; init; }
    public required TaskCompletionSource<IVisionResult> Completion { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
