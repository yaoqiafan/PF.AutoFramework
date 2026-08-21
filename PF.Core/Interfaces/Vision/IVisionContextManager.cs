using PF.Core.Enums;

namespace PF.Core.Interfaces.Vision;

/// <summary>
/// 三模式视觉引擎管理器。
/// 每种模式对应一个独立的 HDevEngine + Worker 线程实例，按需拉起，用完释放。
/// 三引擎可同时存在，共享同一算法目录，无硬件资源冲突（图像由外部传入）。
/// </summary>
public interface IVisionContextManager
{
    /// <summary>
    /// 按需拉起指定模式的引擎。首次调用时创建 HDevEngine + Worker 线程；
    /// 引擎已存在则直接返回，不重复创建。
    /// </summary>
    IVisionService GetOrCreate(EngineMode mode);

    /// <summary>
    /// 释放指定模式的引擎（Dispose Worker 线程 + HDevEngine），槽位置 null。
    /// 再次调用 GetOrCreate 可重新拉起。
    /// </summary>
    Task ReleaseAsync(EngineMode mode);

    /// <summary>指定模式的引擎当前是否已拉起（非 null）</summary>
    bool IsActive(EngineMode mode);

    /// <summary>
    /// 取指定模式已拉起的引擎；未拉起或已释放时返回 null。
    /// 与 <see cref="GetOrCreate"/> 的区别是<b>绝不创建</b>，供只想在引擎存在时才做事的调用方使用。
    /// </summary>
    IVisionService? TryGet(EngineMode mode);

    /// <summary>当前所有已拉起的引擎模式列表</summary>
    IReadOnlyList<EngineMode> ActiveEngines { get; }
}
