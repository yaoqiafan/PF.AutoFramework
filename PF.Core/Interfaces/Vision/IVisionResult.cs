namespace PF.Core.Interfaces.Vision;

/// <summary>
/// 视觉过程执行结果契约。
/// 控制量和图标量均以 object 装箱传递，UI 层（PF.Modules.Halcon）负责强转为 HTuple / HObject。
/// </summary>
public interface IVisionResult
{
    /// <summary>执行是否成功</summary>
    bool Success { get; }

    /// <summary>失败时的错误信息（成功时为 null）</summary>
    string? ErrorMessage { get; }

    /// <summary>执行耗时</summary>
    TimeSpan ElapsedTime { get; }

    /// <summary>执行的过程名称</summary>
    string ProcedureName { get; }

    /// <summary>
    /// 控制量输出（数值 / 字符串类型变量）。
    /// Key 为 .hdev 中的变量名；Value 为装箱的 HTuple，由 UI 层强转后使用。
    /// </summary>
    IReadOnlyDictionary<string, object?> ControlOutputs { get; }

    /// <summary>
    /// 图标量输出（图像 / 区域 / 轮廓类型变量）。
    /// Key 为 .hdev 中的变量名；Value 为装箱的 HObject，由 UI 层强转后显示。
    /// <para>
    /// 所有权：HObject 为非托管 HALCON 内存。通过 ExecuteAsync / ExecutePipelineAsync
    /// 返回值拿到的结果归调用方所有，用毕必须释放；
    /// 通过 ProcedureExecuted 事件拿到的结果仅在回调期间有效，保留必须自行克隆。
    /// </para>
    /// </summary>
    IReadOnlyDictionary<string, object?> IconicOutputs { get; }
}
