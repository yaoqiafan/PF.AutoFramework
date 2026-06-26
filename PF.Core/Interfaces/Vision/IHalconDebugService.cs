namespace PF.Core.Interfaces.Vision;

/// <summary>
/// HALCON HDevEngine 调试服务（Level 2）。
/// 启用后 HDevelop 可通过调试服务器实时接管正在运行的 .hdev 过程，实现断点调试。
/// 调试引擎（Debug 模式）按需拉起，与生产引擎独立。
/// </summary>
public interface IHalconDebugService
{
    /// <summary>调试服务器当前是否已启动</summary>
    bool IsDebugServerActive { get; }

    /// <summary>当前活跃的调试端口（未启动时为 0）</summary>
    int ActivePort { get; }

    /// <summary>当前连接密码（未设置时为空字符串）</summary>
    string Password { get; }

    /// <summary>
    /// 在 Debug 引擎上启动调试服务器，绑定到指定端口。
    /// 同时禁用 JIT 编译（调试必须），设置密码和等待连接属性，并调用 StartDebugServer()。
    /// </summary>
    Task<bool> EnableDebugServerAsync(int port = 9999, string password = "",
        CancellationToken cancellationToken = default);

    /// <summary>停止调试服务器，恢复 JIT 编译（不释放 Debug 引擎）</summary>
    Task DisableDebugServerAsync(CancellationToken cancellationToken = default);

    /// <summary>返回过程目录中可用的 .hdev 文件名列表（不含扩展名）</summary>
    IReadOnlyList<string> GetAvailableProcedures();

    /// <summary>强制从缓存卸载指定过程，下次执行时从磁盘重新加载</summary>
    Task ForceReloadAsync(string procedureName, CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析 .hdev 文件，返回过程的输入/输出参数签名。
    /// 返回 null 表示文件不存在或无法解析。
    /// </summary>
    Task<ProcedureSignature?> GetProcedureSignatureAsync(string procedureName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用指定参数执行一次过程（测试执行）。
    /// 图像输入通过文件路径传入，在 Worker 线程上加载；控制量字符串由实现层自动解析为数值或字符串。
    /// <paramref name="iconicObjects"/> 用于传入预计算的 HObject（如 ROI Region），
    /// 与 <paramref name="iconicFilePaths"/> 并行注入，同名时 iconicObjects 优先。
    /// 执行结果包含所有控制量输出和图标量输出。
    /// </summary>
    Task<IVisionResult> RunTestAsync(
        string procedureName,
        IReadOnlyDictionary<string, string>  controlInputs,
        IReadOnlyDictionary<string, string>  iconicFilePaths,
        IReadOnlyDictionary<string, object?> iconicObjects,
        CancellationToken ct = default);

    /// <summary>
    /// 启动 HDevelop 并附加到当前调试服务器（Level 2）。
    /// 传入过程文件名时 HDevelop 自动加载该文件；追加 -external_proc_path 以解析过程互调。
    /// <paramref name="runImmediately"/> 为 true 时追加 -run，打开后立即执行。
    /// 须先调用 <see cref="EnableDebugServerAsync"/> 启动服务器，否则返回 false。
    /// </summary>
    bool LaunchHDevelop(string? procedureName = null, bool runImmediately = false);
}
