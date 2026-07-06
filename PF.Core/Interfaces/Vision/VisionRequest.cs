namespace PF.Core.Interfaces.Vision;

/// <summary>
/// 视觉过程执行请求（无 Halcon 依赖，object 装箱传递图像数据）。
/// </summary>
public class VisionRequest
{
    /// <summary>要执行的 .hdev 过程名称（不含路径和扩展名）</summary>
    public required string ProcedureName { get; init; }

    /// <summary>
    /// 输入控制量（对应 .hdev 中的 INPUT_* 控制变量）。
    /// Value 为 C# 原生类型（double / string / long 等），实现层包装为 HTuple。
    /// </summary>
    public Dictionary<string, object?> ControlInputs { get; init; } = new();

    /// <summary>
    /// 输入图标量（对应 .hdev 中的 INPUT_IMAGE、INPUT_ROI 等）。
    /// Value 为装箱的 HObject，实现层强转后传入 HDevProcedureCall。
    /// </summary>
    public Dictionary<string, object?> IconicInputs { get; init; } = new();

    /// <summary>
    /// 通过文件路径传入的图像输入（调试/测试场景）。
    /// 实现层在 Worker 线程上调用 ReadImage 加载文件，再传入 HDevProcedureCall。
    /// Key 为 .hdev 参数名，Value 为磁盘图像文件的绝对路径。
    /// </summary>
    public Dictionary<string, string> IconicFilePaths { get; init; } = new();

    /// <summary>单次执行超时（默认 30 秒）</summary>
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(30);
}
