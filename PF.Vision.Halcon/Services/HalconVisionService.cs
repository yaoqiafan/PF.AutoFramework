using HalconDotNet;
using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using PF.Core.Entities.Vision;
using PF.Core.Interfaces.Vision;
using PF.Vision.Halcon.Internal;
using PF.Vision.Halcon.Models;
using System.Diagnostics;
using System.Threading.Channels;
using System.Threading;

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

    private readonly string            _procedureDirectory;
    private readonly ILogService       _logger;
    private readonly VisionEngineConfig _config;

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

    // Channel：容量由 VisionEngineConfig 决定，调用方 Wait（不丢弃），单 Reader 保证串行
    private readonly Channel<VisionJob> _channel;

    private readonly Task _workerTask;
    private readonly FileSystemWatcher _watcher;
    private bool _disposed;

    // ── 事件 ──────────────────────────────────────────────────────────────────

    public event EventHandler<IVisionResult>? ProcedureExecuted;
    public event EventHandler<string>? ProcedureDirectoryChanged;

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public HalconVisionService(string procedureDirectory, ILogService logService,
                               VisionEngineConfig? config = null)
    {
        // Path.GetFullPath 规范化路径：消除 D://foo\bar 中的双斜杠，HALCON C 层不保证处理混合斜杠
        _procedureDirectory = Path.GetFullPath(procedureDirectory);
        _logger = logService;
        _config = config ?? VisionEngineConfig.Production;

        _channel = Channel.CreateBounded<VisionJob>(
            new BoundedChannelOptions(_config.ChannelCapacity)
            {
                FullMode                      = BoundedChannelFullMode.Wait,
                SingleReader                  = true,
                SingleWriter                  = false,
                AllowSynchronousContinuations = false,
            });

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

    // ── 包内访问（HalconDebugService 使用）───────────────────────────────────

    internal string ProcedureDirectory => _procedureDirectory;

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

        // Debug 引擎（无超时限制）不设置 CancelAfter，避免打断调试会话
        if (_config.ExecutionTimeout == Timeout.InfiniteTimeSpan)
        {
            await _channel.Writer.WriteAsync(job, cancellationToken);
        }
        else
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linkedCts.CancelAfter(request.Timeout);
            await _channel.Writer.WriteAsync(job, linkedCts.Token);
        }

        return await tcs.Task;
    }

    public async Task<IVisionResult> ExecutePipelineAsync(
        VisionPipelineDefinition     pipeline,
        Dictionary<string, object?>? externalInputs    = null,
        CancellationToken            cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<IVisionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new VisionJob
        {
            Pipeline          = pipeline,
            ExternalInputs    = externalInputs,
            Completion        = tcs,
            CancellationToken = cancellationToken,
        };

        await _channel.Writer.WriteAsync(job, cancellationToken);
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
            _logger.Info($"[Vision] HDevEngine 初始化完成，过程目录: {_procedureDirectory}，容量: {_config.ChannelCapacity}，详细日志: {_config.VerboseLogging}", LogCategories.Vision);
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
                if (job.IsEngineAction)
                    job.ActionCompletion?.TrySetCanceled(job.CancellationToken);
                else
                    job.Completion?.TrySetCanceled(job.CancellationToken);
                continue;
            }

            // ── 元操作（调试端口、引擎属性设置等）────────────────────────────
            if (job.IsEngineAction)
            {
                try
                {
                    var ok = job.EngineAction!(_engine!);
                    job.ActionCompletion?.TrySetResult(ok);
                }
                catch (Exception ex)
                {
                    _logger.Error($"[Vision] 引擎元操作失败: {ex.Message}", LogCategories.Vision);
                    job.ActionCompletion?.TrySetException(ex);
                }
                continue;
            }

            // ── 普通视觉作业 ──────────────────────────────────────────────────
            IVisionResult result;

            if (job.IsPipeline)
            {
                result = DoExecutePipeline(job.Pipeline!, job.ExternalInputs);
            }
            else
            {
                var name = job.Request!.ProcedureName;
                if      (name.StartsWith("__LOAD__",   StringComparison.Ordinal)) result = DoLoad(name[8..]);
                else if (name.StartsWith("__UNLOAD__", StringComparison.Ordinal)) result = DoUnload(name[10..]);
                else    result = DoExecute(job.Request);
            }

            job.Completion?.TrySetResult(result);
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
        var sw           = Stopwatch.StartNew();
        HDevProcedureCall? call = null;
        var loadedImages = new List<HObject>();
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

            // 设置输入图标量（HObject 直传）
            foreach (var (key, value) in request.IconicInputs)
            {
                if (value is HObject hobj)
                    call.SetInputIconicParamObject(key, hobj);
            }

            // 设置输入图标量（从文件路径加载，用于调试/测试执行）
            foreach (var (key, filePath) in request.IconicFilePaths)
            {
                if (string.IsNullOrEmpty(filePath)) continue;
                HOperatorSet.ReadImage(out HObject img, filePath);
                call.SetInputIconicParamObject(key, img);
                loadedImages.Add(img);
            }

            if (_config.VerboseLogging)
                _logger.Info($"[Vision][Debug] → {request.ProcedureName} | ctrl-in={request.ControlInputs.Count} iconic-in={request.IconicInputs.Count}", LogCategories.Vision);

            // Debug 模式：在过程入口暂停，等待 HDevelop 连接后再继续执行
            if (_config.WaitForDebugConnection)
                call.SetWaitForDebugConnection(true);

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
            if (_config.VerboseLogging)
                _logger.Info($"[Vision][Debug] ← {request.ProcedureName} | ctrl-out={ctrlOut.Count} iconic-out={iconicOut.Count} elapsed={sw.ElapsedMilliseconds}ms", LogCategories.Vision);
            return HalconVisionResult.Succeeded(request.ProcedureName, sw.Elapsed, ctrlOut, iconicOut);
        }
        catch (HalconException hex)
        {
            string str = hex.ToString();

            sw.Stop();
            _logger.Error($"[Vision] 执行失败: {request.ProcedureName} H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", LogCategories.Vision);
            return HalconVisionResult.Failure(request.ProcedureName, $"H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", sw.Elapsed);
        }
        finally
        {
            call?.Dispose();
            foreach (var img in loadedImages) img.Dispose();
        }
    }

    // ── 核心加载逻辑 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 从磁盘加载过程到缓存，自动按扩展名选择策略：
    ///   .hdvp → HDevProcedure(engine, name)（SetProcedurePath 已指向过程目录）；
    ///   .hdev → HDevProgram(filePath) + HDevProcedure(program, name)（程序与过程同寿命）。
    /// .hdvp 优先：同名文件同时存在时加载 .hdvp。
    /// </summary>
    private void LoadProcedureFromFile(string procedureName)
    {
        var hdvpPath = Path.Combine(_procedureDirectory, procedureName + ".hdvp");
        var hdevPath = Path.Combine(_procedureDirectory, procedureName + ".hdev");

        if (File.Exists(hdvpPath))
        {
            // 独立过程文件（.hdvp）：通过 SetProcedurePath 按名称加载，不需要 HDevProgram
            // _engine.SetProcedurePath 已在 RunWorkerAsync 中指向过程目录
            var procedure = new HDevProcedure(procedureName);
            _procedureCache[procedureName] = procedure;
        }
        else if (File.Exists(hdevPath))
        {
            // 程序文件（.hdev）：必须通过 HDevProgram 显式加载，两者须同寿命
            var program   = new HDevProgram(hdevPath);
            var procedure = new HDevProcedure(program, procedureName);
            _programCache[procedureName]   = program;
            _procedureCache[procedureName] = procedure;
        }
        else
        {
            throw new FileNotFoundException(
                $"过程文件不存在: {procedureName}（已查找 .hdvp 和 .hdev）", hdvpPath);
        }

        UpdateLoadedSnapshot();
    }

    // ── 管线执行（仅在 Worker 线程调用）────────────────────────────────────────

    private IVisionResult DoExecutePipeline(
        VisionPipelineDefinition     pipeline,
        Dictionary<string, object?>? externalInputs)
    {
        var sw  = Stopwatch.StartNew();
        var ctx = new VisionContext();
        ctx.InjectExternal(externalInputs);

        _logger.Info($"[Pipeline] 开始执行: {pipeline.PipelineId}（{pipeline.Steps.Count} 步）", LogCategories.Vision);

        foreach (var step in pipeline.Steps)
        {
            // 条件评估
            bool conditionMet;
            try   { conditionMet = ctx.EvaluateCondition(step.Condition); }
            catch (Exception ex)
            {
                _logger.Warn($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 条件解析异常: {ex.Message}，视为 true", LogCategories.Vision);
                conditionMet = true;
            }

            if (!conditionMet)
            {
                _logger.Info($"[Pipeline:{pipeline.PipelineId}] 跳过步骤 {step.Id}（条件不满足）", LogCategories.Vision);
                continue;
            }

            // 解析输入：上下文引用 → 实际值
            var resolved = step.Inputs.ToDictionary(kv => kv.Key, kv => ctx.Resolve(kv.Value));

            var request = new VisionRequest
            {
                ProcedureName = step.Procedure,
                ControlInputs = resolved
                    .Where(kv => kv.Value is not HObject)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
                IconicInputs = resolved
                    .Where(kv => kv.Value is HObject)
                    .ToDictionary(kv => kv.Key, kv => kv.Value),
            };

            var stepResult = DoExecute(request);

            // 每步完成均触发事件（UI 可监听进度）
            ProcedureExecuted?.Invoke(this, stepResult);

            if (!stepResult.Success)
            {
                if (step.SkipOnError)
                {
                    _logger.Warn($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 失败，skip_on_error=true，继续后续步骤", LogCategories.Vision);
                    continue;
                }
                sw.Stop();
                _logger.Error($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 失败，管线终止: {stepResult.ErrorMessage}", LogCategories.Vision);
                return HalconVisionResult.Failure(
                    pipeline.PipelineId,
                    $"步骤 [{step.Id}/{step.Procedure}] 失败: {stepResult.ErrorMessage}",
                    sw.Elapsed);
            }

            // 将声明的 outputs 写入上下文（控制量和图标量分别处理）
            foreach (var paramName in step.Outputs)
            {
                if (stepResult.ControlOutputs.TryGetValue(paramName, out var cv))
                    ctx.Set(step.Id, paramName, cv);
                else if (stepResult.IconicOutputs.TryGetValue(paramName, out var iv))
                    ctx.Set(step.Id, paramName, iv);
                else
                    _logger.Warn($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 声明输出 '{paramName}' 在结果中不存在", LogCategories.Vision);
            }

            _logger.Info($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 完成", LogCategories.Vision);
        }

        sw.Stop();

        // 从上下文收集管线最终输出
        var finalOutputs = ctx.Collect(pipeline.PipelineOutputs);

        _logger.Info($"[Pipeline] 完成: {pipeline.PipelineId}，耗时: {sw.ElapsedMilliseconds} ms", LogCategories.Vision);

        return HalconVisionResult.Succeeded(
            pipeline.PipelineId,
            sw.Elapsed,
            finalOutputs.Where(kv => kv.Value is not HObject)
                        .ToDictionary(kv => kv.Key, kv => kv.Value),
            finalOutputs.Where(kv => kv.Value is HObject)
                        .ToDictionary(kv => kv.Key, kv => kv.Value));
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

        // .hdvp 优先（同名时覆盖 .hdev 条目）
        var hdvp = Directory.GetFiles(_procedureDirectory, "*.hdvp", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f)!);
        var hdev = Directory.GetFiles(_procedureDirectory, "*.hdev", SearchOption.AllDirectories)
            .Select(f => Path.GetFileNameWithoutExtension(f)!);

        var names = hdvp.Concat(hdev)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
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
        // 监听所有文件，在回调中按扩展名过滤，以同时捕获 .hdev 和 .hdvp 变化
        var w = new FileSystemWatcher(_procedureDirectory, "*.*")
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents = true,
        };
        w.Created += OnWatcherChanged;
        w.Changed += OnWatcherChanged;
        w.Deleted += OnWatcherChanged;
        w.Renamed += OnWatcherChanged;
        return w;
    }

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        var ext = Path.GetExtension(e.Name);
        if (!string.Equals(ext, ".hdev", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".hdvp", StringComparison.OrdinalIgnoreCase))
            return;

        ScanAvailableProcedures();

        var changedName = Path.GetFileNameWithoutExtension(e.Name) ?? string.Empty;

        // 无论缓存里是否有该过程，都通过 Channel 投递卸载请求。
        // Worker 线程消费后会安全地从 _procedureCache / _programCache 移除，
        // 下次 Execute 时从修改后的文件重新加载。
        _ = UnloadProcedureAsync(changedName);

        _logger.Info($"[Vision] 过程目录变化: {e.ChangeType} - {changedName}", LogCategories.Vision);
        ProcedureDirectoryChanged?.Invoke(this, changedName);
    }

    // ── 引擎元操作（调试等） ──────────────────────────────────────────────────

    /// <summary>
    /// 在 Worker 线程上执行引擎级配置操作（如 SetEngineAttribute）。
    /// 操作排队执行，确保线程安全。
    /// </summary>
    internal async Task<bool> ScheduleEngineActionAsync(
        Func<HDevEngine, bool> action,
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var job = new VisionJob
        {
            EngineAction      = action,
            ActionCompletion  = tcs,
            CancellationToken = cancellationToken,
        };

        await _channel.Writer.WriteAsync(job, cancellationToken);
        return await tcs.Task;
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
