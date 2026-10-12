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

    /// <summary>
    /// 本次执行是否在过程入口暂停、等 HDevelop 连接后再继续（默认 false）。
    /// <para>只有显式的"调试执行"（HALCON 调试页）才置 true，且只在 Debug 引擎、调试服务器已启动时生效。
    /// 其余调用即使调试服务器开着也直接执行——否则服务器一开，Debug 引擎上的每一次执行（业务里的本地试跑、
    /// 预处理等）都会卡在入口等一个不会来的连接。HDevelop 已连上并下了断点时，普通执行照样会停在断点上。</para>
    /// </summary>
    public bool WaitForDebugConnection { get; init; }
}
