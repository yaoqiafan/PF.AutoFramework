using HalconDotNet;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Vision;
using PF.Vision.Halcon.Internal;
using System.Diagnostics;
using System.Xml.Linq;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// HALCON 调试服务实现（Level 2）。
/// <para>
/// 启用流程：禁用 JIT → 设置端口/密码/等待属性 → StartDebugServer()。
/// 每次过程调用由 <see cref="HalconVisionService"/> 自动调用 SetWaitForDebugConnection(true)，
/// 使过程在入口暂停，等待 HDevelop 连接后再继续。
/// 所有引擎操作通过 Channel 路由到 Worker 线程，保证 HDevEngine 线程安全。
/// </para>
/// </summary>
public sealed class HalconDebugService : IHalconDebugService
{
    private readonly IVisionContextManager _contextManager;
    private readonly ILogService           _logger;

    // 过程目录直接持有：签名解析、过程枚举、HDevelop 启动参数都只需要这个字符串，
    // 不得为了取它而 GetOrCreate(EngineMode.Debug)——那会在生产机上凭空拉起一个
    // HDevEngine + 专用系统线程 + FileSystemWatcher，且永不使用
    private readonly string _procedureDirectory;

    private int    _activePort;
    private string _password = string.Empty;

    // 唯一真相在引擎上：调用方若 ReleaseAsync(EngineMode.Debug) 释放了引擎，
    // 调试服务器随之消失，这里必须跟着回落，不能靠本地 _isActive 字段自说自话
    public string ProcedureDirectory   => _procedureDirectory;

    public bool   IsDebugServerActive => TryGetDebugEngine()?.DebugServerStarted == true;
    public int    ActivePort          => IsDebugServerActive ? _activePort : 0;
    public string Password            => IsDebugServerActive ? _password : string.Empty;

    public HalconDebugService(IVisionContextManager contextManager, ILogService logger,
                              string procedureDirectory)
    {
        _contextManager = contextManager;
        _logger         = logger;
        // 与 HalconVisionService 一致地规范化：LaunchHDevelop 会把该路径拼进 HDevelop.exe 的
        // -external_proc_path 命令行，HALCON C 层不保证处理双斜杠/混合斜杠
        _procedureDirectory = Path.GetFullPath(procedureDirectory);
    }

    public async Task<bool> EnableDebugServerAsync(int port = 9999, string password = "",
        CancellationToken cancellationToken = default)
    {
        var engine = GetDebugEngine();

        var ok = await engine.ScheduleEngineActionAsync(e =>
        {
            // 调试必须禁用 JIT，否则 HDevelop 无法接管断点
            e.SetEngineAttribute("execute_procedures_jit_compiled", "false");
            e.SetEngineAttribute("debug_port",                new HTuple(port));
            e.SetEngineAttribute("debug_password",            new HTuple(password));
            e.SetEngineAttribute("debug_wait_for_connection", "true");
            e.StartDebugServer();
            return true;
        }, cancellationToken);

        if (ok)
        {
            _activePort = port;
            _password   = password;
            // 最后置位：IsDebugServerActive 以此为准，端口/密码须先就位
            engine.DebugServerStarted = true;
            _logger.Info(
                $"[Vision] 调试服务器已启动，端口: {port}，密码: {(string.IsNullOrEmpty(password) ? "（无）" : "***")}",
                LogCategories.Vision);
        }
        return ok;
    }

    public async Task DisableDebugServerAsync(CancellationToken cancellationToken = default)
    {
        // 引擎已被释放时同样直接返回：没有服务器可停，也不该为此重新拉起引擎
        if (TryGetDebugEngine() is not { DebugServerStarted: true } engine) return;

        await engine.ScheduleEngineActionAsync(e =>
        {
            e.StopDebugServer();
            // 恢复 JIT 编译，恢复生产性能
            e.SetEngineAttribute("execute_procedures_jit_compiled", "true");
            return true;
        }, cancellationToken);

        engine.DebugServerStarted = false;
        _activePort = 0;
        _password   = string.Empty;
        _logger.Info("[Vision] 调试服务器已停止，JIT 编译已恢复", LogCategories.Vision);
    }

    public IReadOnlyList<string> GetAvailableProcedures()
        => HdevProcedureCatalog.Scan(_procedureDirectory);

    public Task ForceReloadAsync(string procedureName, CancellationToken cancellationToken = default)
        => GetDebugEngine().UnloadProcedureAsync(procedureName, cancellationToken);

    public Task<IVisionResult> RunTestAsync(
        string procedureName,
        IReadOnlyDictionary<string, string>  controlInputs,
        IReadOnlyDictionary<string, string>  iconicFilePaths,
        IReadOnlyDictionary<string, object?> iconicObjects,
        CancellationToken ct = default)
    {
        var engine  = GetDebugEngine();
        var request = new VisionRequest
        {
            ProcedureName   = procedureName,
            Timeout         = Timeout.InfiniteTimeSpan,   // 调试会话无超时，等待 HDevelop 接入
            // 跳过空字符串：空值不等于"未填写"，传给 HALCON 会引发类型错误
            ControlInputs   = controlInputs
                .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Value))
                .ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value),
            IconicFilePaths = iconicFilePaths.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
            // 预计算 HObject（ROI Region 等），同名时优先级高于 FilePath
            IconicInputs    = iconicObjects.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
        };
        return engine.ExecuteAsync(request, ct);
    }

    public async Task<ProcedureSignature?> GetProcedureSignatureAsync(string procedureName,
        CancellationToken cancellationToken = default)
    {
        var filePath = HdevProcedureCatalog.FindProcedureFile(_procedureDirectory, procedureName);

        if (filePath is null) return null;

        return await Task.Run(() => ParseHdevSignature(procedureName, filePath), cancellationToken);
    }

    public bool LaunchHDevelop(string? procedureName = null, bool runImmediately = false)
    {
        if (!IsDebugServerActive)
        {
            _logger.Warn("[Vision] 未启动调试服务器，请先启动 Level 2 调试服务器", LogCategories.Vision);
            return false;
        }

        // HDevelop 已在运行则复用现有窗口，不重复启动
        if (Process.GetProcessesByName("HDevelop").Length > 0)
        {
            _logger.Info("[Vision] HDevelop 已在运行，跳过启动新实例", LogCategories.Vision);
            return true;
        }

        var procDir = _procedureDirectory;
        var args    = new System.Text.StringBuilder();

        if (!string.IsNullOrEmpty(procedureName))
        {
            var procFile = HdevProcedureCatalog.FindProcedureFile(procDir, procedureName);
            if (procFile is not null)
                args.Append($"\"{procFile}\" ");
            else
                _logger.Warn($"[Vision] 过程文件不存在，仅打开 HDevelop: {procedureName}", LogCategories.Vision);
        }

        args.Append($"-external_proc_path:\"{procDir}\"");
        if (runImmediately)
            args.Append(" -run");

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName        = "HDevelop.exe",
                Arguments       = args.ToString(),
                UseShellExecute = true,
            });
            _logger.Info(
                $"[Vision] HDevelop 已启动 | 过程: {procedureName ?? "（无）"} | 参数: {args}",
                LogCategories.Vision);
            return true;
        }
        catch (Exception ex)
        {
            _logger.Warn(
                $"[Vision] HDevelop 启动失败: {ex.Message}（请确认 HALCON 已安装且 HDevelop.exe 在 PATH 中）",
                LogCategories.Vision);
            return false;
        }
    }

    // ── 私有 ─────────────────────────────────────────────────────────────────

    /// <summary>取已拉起的 Debug 引擎；未拉起时返回 null（不创建）。</summary>
    private HalconVisionService? TryGetDebugEngine()
        => _contextManager.TryGet(EngineMode.Debug) as HalconVisionService;

    private HalconVisionService GetDebugEngine()
    {
        var svc = _contextManager.GetOrCreate(EngineMode.Debug);
        return svc as HalconVisionService
               ?? throw new InvalidOperationException("Debug 引擎实例类型不是 HalconVisionService");
    }

    /// <summary>
    /// 解析 .hdev 文件（XML 格式），提取过程接口的输入/输出参数。
    /// 支持经典格式（ic/oc/ii/oi 子元素）和新格式（par_types 列表）。
    /// </summary>
    private static ProcedureSignature? ParseHdevSignature(string name, string filePath)
    {
        try
        {
            var doc  = XDocument.Load(filePath);
            var proc = doc.Descendants("procedure")
                          .FirstOrDefault(p => (string?)p.Attribute("name") == name)
                       ?? doc.Descendants("procedure").FirstOrDefault();

            if (proc == null) return null;

            var iface = proc.Element("interface");
            if (iface == null) return null;

            // 经典格式：
            //   ic = input control,  oc = output control
            //   io = input iconic,   oo = output iconic   ← HALCON 实际使用的标签名
            bool hasClassicFormat = iface.Element("ic") != null || iface.Element("io") != null
                                    || iface.Element("oc") != null || iface.Element("oo") != null;
            if (hasClassicFormat)
            {
                var inputs = new List<ProcedureParam>();
                inputs.AddRange(ParamsFromElement(iface.Element("io"), ProcedureParamKind.Iconic));
                inputs.AddRange(ParamsFromElement(iface.Element("ic"), ProcedureParamKind.Control));

                var outputs = new List<ProcedureParam>();
                outputs.AddRange(ParamsFromElement(iface.Element("oo"), ProcedureParamKind.Iconic));
                outputs.AddRange(ParamsFromElement(iface.Element("oc"), ProcedureParamKind.Control));

                return new ProcedureSignature(name, inputs, outputs);
            }

            // 新格式：<par_types> 列表，每个 <par> 有 io / value_type 属性
            var parTypes = iface.Element("par_types");
            if (parTypes != null)
            {
                var inputs = parTypes.Elements("par")
                    .Where(p => (string?)p.Attribute("io") == "input")
                    .Select(p => new ProcedureParam(
                        (string?)p.Attribute("name") ?? "",
                        (string?)p.Attribute("value_type") == "iconic"
                            ? ProcedureParamKind.Iconic
                            : ProcedureParamKind.Control))
                    .ToList();

                var outputs = parTypes.Elements("par")
                    .Where(p => (string?)p.Attribute("io") == "output")
                    .Select(p => new ProcedureParam(
                        (string?)p.Attribute("name") ?? "",
                        (string?)p.Attribute("value_type") == "iconic"
                            ? ProcedureParamKind.Iconic
                            : ProcedureParamKind.Control))
                    .ToList();

                return new ProcedureSignature(name, inputs, outputs);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static IEnumerable<ProcedureParam> ParamsFromElement(XElement? el, ProcedureParamKind kind)
    {
        if (el == null) yield break;
        foreach (var par in el.Elements("par"))
        {
            var n = (string?)par.Attribute("name");
            if (!string.IsNullOrEmpty(n))
                yield return new ProcedureParam(n, kind);
        }
    }
}
