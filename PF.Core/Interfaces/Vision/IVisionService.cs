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
    /// </summary>
    Task<IVisionResult> ExecuteAsync(VisionRequest request, CancellationToken cancellationToken = default);

    // ── 管线执行 ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 按顺序执行多步管线，步骤间通过 VisionContext 自动传递 HObject。
    /// 整个管线在同一 Worker 线程内完成，HALCON 对象不跨线程。
    /// <param name="pipeline">管线定义（通常由 VisionPipelineLoader 从 JSON 反序列化）</param>
    /// <param name="externalInputs"></param>
    ///  <param name="cancellationToken"></param>
    /// 外部注入的初始值（如相机实时图像），在步骤 inputs 中用 "$__ext__.keyName" 引用
    /// </summary>

    Task<IVisionResult> ExecutePipelineAsync(
        VisionPipelineDefinition        pipeline,
        Dictionary<string, object?>?    externalInputs    = null,
        CancellationToken               cancellationToken = default);

    // ── 事件 ──────────────────────────────────────────────────────────────────

    /// <summary>过程执行完成后触发（成功或失败均触发，管线每步完成也触发）</summary>
    event EventHandler<IVisionResult> ProcedureExecuted;

    /// <summary>过程目录下 .hdev 文件发生变化时触发（FileSystemWatcher 驱动）</summary>
    event EventHandler<string> ProcedureDirectoryChanged;
}
