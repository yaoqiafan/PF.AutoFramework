using HalconDotNet;
using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Vision;
using PF.Vision.Halcon.Internal;
using PF.Vision.Halcon.Models;
using System.Diagnostics;
using System.Threading.Channels;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// Halcon HDevEngine 视觉服务实现。
/// <para>
/// 线程安全策略：HDevEngine 及所有 HDevProgram / HDevProcedure / HDevProcedureCall 对象
/// 仅在 <c>_workerTask</c>（LongRunning 专用线程）上创建和访问，严禁跨线程调用。
/// 外部通过有界 Channel 投递 <see cref="VisionJob"/>，Worker 串行消费。
/// </para>
/// <para>
/// 过程加载策略：<c>new HDevProcedure(name)</c> 依赖 SetProcedurePath 的全局注册，
/// 仅适用于过程间互调，C# 端直接构造不触发文件扫描。
/// 正确做法：<c>HDevProgram(filePath)</c> 显式加载文件，
/// 再通过 <c>HDevProcedure(program, name)</c> 取得过程对象。
/// HDevProgram 必须与 HDevProcedure 同生命周期，否则过程对象失效。
/// </para>
/// </summary>
internal sealed class HalconVisionService : IVisionService, IDisposable
{
    // ── 字段 ──────────────────────────────────────────────────────────────────

    private readonly string _procedureDirectory;
    private readonly ILogService _logger;

    // Halcon 对象：仅在 _workerTask 线程访问
    private HDevEngine? _engine;

    // HDevProgram 必须与对应 HDevProcedure 同生命周期（procedure 持有 program 引用）
    private readonly Dictionary<string, HDevProgram>   _programCache   = new();
    private readonly Dictionary<string, HDevProcedure> _procedureCache = new();

    // 可用过程列表（FileSystemWatcher 维护，读写锁保护）
    private readonly List<string> _availableProcedures = new();
    private readonly ReaderWriterLockSlim _availableLock = new();

    // 已加载过程快照（仅供 GetLoadedProcedures 使用，lock 保护）
    private readonly object _loadedLock = new();
    private readonly List<string> _loadedSnapshot = new();

    // Channel：容量 100，调用方 Wait（不丢弃），单 Reader 保证串行
    private readonly Channel<VisionJob> _channel = Channel.CreateBounded<VisionJob>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });

    private readonly Task _workerTask;
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    // ── 事件 ──────────────────────────────────────────────────────────────────

    public event EventHandler<IVisionResult>? ProcedureExecuted;
    public event EventHandler<string>? ProcedureDirectoryChanged;

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public HalconVisionService(string procedureDirectory, ILogService logService)
    {
        // Path.GetFullPath 规范化路径：消除 D://foo\bar 中的双斜杠，HALCON C 层不保证处理混合斜杠
        _procedureDirectory = Path.GetFullPath(procedureDirectory);
        _logger = logService;

        if (!Directory.Exists(_procedureDirectory))
            Directory.CreateDirectory(_procedureDirectory);

        ScanAvailableProcedures();

        _watcher = CreateWatcher();

        // LongRunning：为 HDevEngine 分配专用系统线程，避免线程池复用导致的线程亲和性问题
        _workerTask = Task.Factory.StartNew(
            RunWorkerAsync,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
    }

    // ── IVisionService ────────────────────────────────────────────────────────

    public IReadOnlyList<string> GetAvailableProcedures()
    {
        _availableLock.EnterReadLock();
        try { return _availableProcedures.ToList(); }
        finally { _availableLock.ExitReadLock(); }
    }

    public IReadOnlyList<string> GetLoadedProcedures()
    {
        lock (_loadedLock) { return _loadedSnapshot.ToList(); }
    }

    public async Task<bool> LoadProcedureAsync(string procedureName, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            new VisionRequest { ProcedureName = $"__LOAD__{procedureName}", Timeout = TimeSpan.FromSeconds(10) },
            cancellationToken);
        return result.Success;
    }

    public async Task UnloadProcedureAsync(string procedureName, CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            new VisionRequest { ProcedureName = $"__UNLOAD__{procedureName}", Timeout = TimeSpan.FromSeconds(5) },
            cancellationToken);
    }

    public async Task<IVisionResult> ExecuteAsync(VisionRequest request, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<IVisionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new VisionJob { Request = request, Completion = tcs, CancellationToken = cancellationToken };

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(request.Timeout);

        await _channel.Writer.WriteAsync(job, linkedCts.Token);
        return await tcs.Task;
    }

    // ── Worker 核心（仅此方法内访问 HDevEngine / HDevProgram / HDevProcedure） ──

    private async Task RunWorkerAsync()
    {
        try
        {
            _engine = new HDevEngine();
            // SetProcedurePath 保留用于过程间互调（procedure 内部调用其他 procedure 时的查找路径）
            _engine.SetProcedurePath(_procedureDirectory);
            _logger.Info($"[Vision] HDevEngine 初始化完成，过程目录: {_procedureDirectory}", LogCategories.Vision);
        }
        catch (Exception ex)
        {
            _logger.Error("[Vision] HDevEngine 初始化失败", LogCategories.Vision, ex);
            _channel.Writer.TryComplete(ex);
            return;
        }

        await foreach (var job in _channel.Reader.ReadAllAsync())
        {
            if (job.CancellationToken.IsCancellationRequested)
            {
                job.Completion.TrySetCanceled(job.CancellationToken);
                continue;
            }

            IVisionResult result;
            var name = job.Request.ProcedureName;

            if (name.StartsWith("__LOAD__", StringComparison.Ordinal))
                result = DoLoad(name[8..]);
            else if (name.StartsWith("__UNLOAD__", StringComparison.Ordinal))
                result = DoUnload(name[10..]);
            else
                result = DoExecute(job.Request);

            job.Completion.TrySetResult(result);
            ProcedureExecuted?.Invoke(this, result);
        }

        // Channel 关闭后在 Worker 线程上释放所有 Halcon 资源（线程安全）
        DisposeAllCachedProcedures();
        _engine?.Dispose();
    }

    private IVisionResult DoLoad(string procedureName)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            if (!_procedureCache.ContainsKey(procedureName))
            {
                LoadProcedureFromFile(procedureName);
                _logger.Info($"[Vision] 过程加载成功: {procedureName}", LogCategories.Vision);
            }
            return HalconVisionResult.Succeeded(procedureName, sw.Elapsed, new(), new());
        }
        catch (HalconException hex)
        {
            _logger.Error($"[Vision] 过程加载失败: {procedureName} H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", LogCategories.Vision);
            return HalconVisionResult.Failure(procedureName, $"H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", sw.Elapsed);
        }
    }

    private IVisionResult DoUnload(string procedureName)
    {
        var sw = Stopwatch.StartNew();
        if (_procedureCache.TryGetValue(procedureName, out var proc))
        {
            proc.Dispose();
            _procedureCache.Remove(procedureName);
            UpdateLoadedSnapshot();
            _logger.Info($"[Vision] 过程卸载: {procedureName}", LogCategories.Vision);
        }
        if (_programCache.TryGetValue(procedureName, out var prog))
        {
            prog.Dispose();
            _programCache.Remove(procedureName);
        }
        return HalconVisionResult.Succeeded(procedureName, sw.Elapsed, new(), new());
    }

    private IVisionResult DoExecute(VisionRequest request)
    {
        var sw = Stopwatch.StartNew();
        HDevProcedureCall? call = null;
        try
        {
            if (!_procedureCache.TryGetValue(request.ProcedureName, out var procedure))
            {
                LoadProcedureFromFile(request.ProcedureName);
                procedure = _procedureCache[request.ProcedureName];
            }

            call = new HDevProcedureCall(procedure);

            // 设置输入控制量
            foreach (var (key, value) in request.ControlInputs)
            {
                if (value is null) continue;
                call.SetInputCtrlParamTuple(key, ObjectToHTuple(value));
            }

            // 设置输入图标量
            foreach (var (key, value) in request.IconicInputs)
            {
                if (value is HObject hobj)
                    call.SetInputIconicParamObject(key, hobj);
            }

            call.Execute();

            // 收集控制量输出（HALCON 参数索引 1-based）
            // GetOutputCtrlParamName 的异常独立捕获，防止单次失败中断整个收集
            var ctrlOut = new Dictionary<string, object?>();
            var ctrlCount = procedure.GetOutputCtrlParamCount();
            for (int i = 1; i <= ctrlCount; i++)
            {
                string? paramName = null;
                try { paramName = procedure.GetOutputCtrlParamName(i); } catch { continue; }
                try { ctrlOut[paramName] = HTupleToValue(call.GetOutputCtrlParamTuple(paramName)); }
                catch { ctrlOut[paramName] = null; }
            }

            // 收集图标量输出（HALCON 参数索引 1-based）
            var iconicOut = new Dictionary<string, object?>();
            var iconicCount = procedure.GetOutputIconicParamCount();
            for (int i = 1; i <= iconicCount; i++)
            {
                string? paramName = null;
                try { paramName = procedure.GetOutputIconicParamName(i); } catch { continue; }
                try { iconicOut[paramName] = call.GetOutputIconicParamObject(paramName); }
                catch { iconicOut[paramName] = null; }
            }

            sw.Stop();
            _logger.Info($"[Vision] 执行成功: {request.ProcedureName}，耗时: {sw.ElapsedMilliseconds} ms", LogCategories.Vision);
            return HalconVisionResult.Succeeded(request.ProcedureName, sw.Elapsed, ctrlOut, iconicOut);
        }
        catch (HalconException hex)
        {
            sw.Stop();
            _logger.Error($"[Vision] 执行失败: {request.ProcedureName} H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", LogCategories.Vision);
            return HalconVisionResult.Failure(request.ProcedureName, $"H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", sw.Elapsed);
        }
        finally
        {
            call?.Dispose();
        }
    }

    // ── 核心加载逻辑 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 从 .hdev 文件显式加载过程：HDevProgram 加载文件 → HDevProcedure(program, name) 取过程。
    /// 两者均入缓存，HDevProgram 必须与 HDevProcedure 同寿命。
    /// 文件不存在或过程名不匹配时，HALCON C 层会抛出 HalconException。
    /// </summary>
    private void LoadProcedureFromFile(string procedureName)
    {
        var filePath  = Path.Combine(_procedureDirectory, procedureName + ".hdev");
        var program   = new HDevProgram(filePath);
        var procedure = new HDevProcedure(program, procedureName);

        _programCache[procedureName]   = program;
        _procedureCache[procedureName] = procedure;
        UpdateLoadedSnapshot();
    }

    // ── 辅助 ──────────────────────────────────────────────────────────────────

    private void DisposeAllCachedProcedures()
    {
        foreach (var proc in _procedureCache.Values) proc.Dispose();
        _procedureCache.Clear();
        foreach (var prog in _programCache.Values) prog.Dispose();
        _programCache.Clear();
    }

    private static HTuple ObjectToHTuple(object value) => value switch
    {
        HTuple t    => t,
        double d    => new HTuple(d),
        float f     => new HTuple((double)f),
        int i       => new HTuple(i),
        long l      => new HTuple(l),
        bool b      => new HTuple(b ? 1 : 0),
        double[] da => new HTuple(da),
        int[] ia    => new HTuple(ia),
        // 字符串优先尝试数值解析，支持 UI 文本框直接输入数字
        string s when int.TryParse(s, out var si)
            => new HTuple(si),
        string s when double.TryParse(s, System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out var sd)
            => new HTuple(sd),
        string s    => new HTuple(s),
        _           => new HTuple(value.ToString()),
    };

    /// <summary>
    /// 将 HTuple 转为 C# 原生类型，使 ToString() 直接可读。
    /// 单元素：int / double / string；多元素：逗号分隔字符串。
    /// </summary>
    private static object HTupleToValue(HTuple t)
    {
        if (t.Length == 0) return string.Empty;
        if (t.Length == 1)
        {
            try { return (object)t.I; } catch { }
            try { return (object)t.D; } catch { }
            try { return (object)t.S; } catch { }
            return t.ToString() ?? string.Empty;
        }
        // 多元素逐个转换，失败回退到整体 ToString
        try
        {
            var parts = new string[t.Length];
            for (int j = 0; j < t.Length; j++)
            {
                HTuple elem = t[j];
                try { parts[j] = elem.I.ToString(); continue; } catch { }
                try { parts[j] = elem.D.ToString("G6"); continue; } catch { }
                try { parts[j] = elem.S; continue; } catch { }
                parts[j] = elem.ToString() ?? "";
            }
            return string.Join(", ", parts);
        }
        catch { return t.ToString() ?? string.Empty; }
    }

    private void UpdateLoadedSnapshot()
    {
        lock (_loadedLock)
        {
            _loadedSnapshot.Clear();
            _loadedSnapshot.AddRange(_procedureCache.Keys);
        }
    }

    // ── FileSystemWatcher ─────────────────────────────────────────────────────

    private void ScanAvailableProcedures()
    {
        if (!Directory.Exists(_procedureDirectory)) return;
        var names = Directory.GetFiles(_procedureDirectory, "*.hdev", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n)
            .ToList();

        _availableLock.EnterWriteLock();
        try
        {
            _availableProcedures.Clear();
            _availableProcedures.AddRange(names);
        }
        finally { _availableLock.ExitWriteLock(); }
    }

    private FileSystemWatcher CreateWatcher()
    {
        var w = new FileSystemWatcher(_procedureDirectory, "*.hdev")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        w.Created += OnWatcherChanged;
        w.Deleted += OnWatcherChanged;
        w.Renamed += OnWatcherChanged;
        return w;
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        ScanAvailableProcedures();

        var changedName = Path.GetFileNameWithoutExtension(e.Name) ?? string.Empty;

        // 文件变更时卸载旧缓存，下次执行时重新从文件加载
        if (_procedureCache.ContainsKey(changedName))
            _ = UnloadProcedureAsync(changedName);

        _logger.Info($"[Vision] 过程目录变化: {e.ChangeType} - {changedName}", LogCategories.Vision);
        ProcedureDirectoryChanged?.Invoke(this, changedName);
    }

    // ── Dispose ───────────────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();
        _availableLock.Dispose();

        // 完成 Channel，Worker 在消费完剩余 Job 后自行释放所有 Halcon 资源
        _channel.Writer.TryComplete();
    }
}
