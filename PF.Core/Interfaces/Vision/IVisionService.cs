using PF.Core.Entities.Vision;

namespace PF.Core.Interfaces.Vision;

/// <summary>
/// 视觉引擎服务契约（零 Halcon 依赖）。
/// 图像数据以 object 装箱传递，结果以 <see cref="IVisionResult"/> 返回。
/// 实现层（PF.Vision.Halcon）使用 HDevEngine 运行时加载 .hdev 算子文件。
/// </summary>
public interface IVisionService
{
    // ── 过程目录查询 ──────────────────────────────────────────────────────────

    /// <summary>获取过程目录中扫描到的所有 .hdev 文件名（不含路径和扩展名）</summary>
    IReadOnlyList<string> GetAvailableProcedures();

    /// <summary>获取当前已加载到内存缓存的过程名称快照</summary>
    IReadOnlyList<string> GetLoadedProcedures();

    // ── 过程加载管理 ──────────────────────────────────────────────────────────

    /// <summary>预加载指定过程到内存缓存（首次执行时也会自动加载）</summary>
    Task<bool> LoadProcedureAsync(string procedureName, CancellationToken cancellationToken = default);

    /// <summary>卸载指定过程并释放其占用的内存</summary>
    Task UnloadProcedureAsync(string procedureName, CancellationToken cancellationToken = default);

    // ── 单步执行 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 异步执行视觉过程。线程安全：内部通过 Channel 委托给单一 Worker 线程串行执行。
    /// 超时（<see cref="VisionRequest.Timeout"/>）覆盖排队 + 执行全过程，超时返回失败结果；
    /// 通过 <paramref name="cancellationToken"/> 主动取消则抛出 OperationCanceledException。
    /// <para>
    /// 所有权：返回结果 IconicOutputs 中的图标量（装箱 HObject）归调用方所有，
    /// 使用完毕必须由调用方释放。
    /// </para>
    /// </summary>
    Task<IVisionResult> ExecuteAsync(VisionRequest request, CancellationToken cancellationToken = default);

    // ── 管线执行 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 按顺序执行多步管线，步骤间通过上下文黑板自动传递 HObject。
    /// 整个管线在同一 Worker 线程内完成，HALCON 对象不跨线程。
    /// <para>
    /// 所有权：返回结果 IconicOutputs 中的图标量归调用方所有，使用完毕必须由调用方释放；
    /// <paramref name="externalInputs"/> 中的图标量所有权仍归调用方（实现层存副本）。
    /// </para>
    /// </summary>
    /// <param name="pipeline">管线定义（通常由 VisionPipelineLoader 从 JSON 反序列化）</param>
    /// <param name="externalInputs">外部注入的初始值（如相机实时图像），在步骤 inputs 中用 "$__ext__.keyName" 引用</param>
    /// <param name="cancellationToken">取消令牌（主动取消时抛出 OperationCanceledException）</param>
    Task<IVisionResult> ExecutePipelineAsync(
        VisionPipelineDefinition        pipeline,
        Dictionary<string, object?>?    externalInputs    = null,
        CancellationToken               cancellationToken = default);

    // ── 事件 ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// 过程执行完成后触发（成功或失败均触发，管线每步完成也触发）。
    /// 在引擎 Worker 线程上同步回调。
    /// <para>
    /// 所有权：事件参数中的图标量（装箱 HObject）仅在回调期间有效，
    /// 订阅者若需保留必须自行克隆（如 HOperatorSet.CopyObj），严禁保存原引用或释放它。
    /// </para>
    /// </summary>
    event EventHandler<IVisionResult> ProcedureExecuted;

    /// <summary>过程目录下 .hdev 文件发生变化时触发（FileSystemWatcher 驱动）</summary>
    event EventHandler<string> ProcedureDirectoryChanged;
}
