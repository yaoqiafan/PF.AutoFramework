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

    /// <summary>
    /// 调试服务器是否已通过 <c>StartDebugServer()</c> 启动，由 HalconDebugService 维护。
    /// <para>
    /// 用于给 <see cref="VisionEngineConfig.WaitForDebugConnection"/> 加门：未启动服务器时
    /// 若仍调用 SetWaitForDebugConnection(true)，过程会在入口无限等待一个永远不会到来的
    /// HDevelop 连接，而 Debug 引擎超时为 InfiniteTimeSpan，Worker 线程将永久卡死。
    /// </para>
    /// </summary>
    internal volatile bool DebugServerStarted;

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

        // Debug 引擎（无超时限制）不设置超时，避免打断调试会话
        TimeSpan? timeout = _config.ExecutionTimeout == Timeout.InfiniteTimeSpan
            ? null
            : request.Timeout;

        return await SubmitAndAwaitAsync(job, tcs, cancellationToken, timeout, request.ProcedureName);
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

        TimeSpan? timeout = _config.ExecutionTimeout == Timeout.InfiniteTimeSpan
            ? null
            : _config.ExecutionTimeout;

        return await SubmitAndAwaitAsync(job, tcs, cancellationToken, timeout, pipeline.PipelineId);
    }

    /// <summary>
    /// 投递作业并等待结果，超时覆盖"入队 + 排队 + 执行"全过程（原实现仅覆盖入队）。
    /// 超时返回失败结果；调用方主动取消则抛出 OperationCanceledException。
    /// 放弃等待时标记 <see cref="VisionJob.Abandon"/>，Worker 执行完毕后自行释放结果资源。
    /// </summary>
    private async Task<IVisionResult> SubmitAndAwaitAsync(
        VisionJob                           job,
        TaskCompletionSource<IVisionResult> tcs,
        CancellationToken                   cancellationToken,
        TimeSpan?                           timeout,
        string                              jobName)
    {
        var sw = Stopwatch.StartNew();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } t && t != Timeout.InfiniteTimeSpan)
            linkedCts.CancelAfter(t);

        try
        {
            await _channel.Writer.WriteAsync(job, linkedCts.Token);
            return await tcs.Task.WaitAsync(linkedCts.Token);
        }
        catch (OperationCanceledException)
        {
            // 调用方不再等待：标记遗弃，Worker 负责释放该作业结果中的 HObject
            job.Abandon();

            if (cancellationToken.IsCancellationRequested)
                throw;

            sw.Stop();
            _logger.Error($"[Vision] 作业超时（{timeout?.TotalMilliseconds:F0} ms）: {jobName}", LogCategories.Vision);
            return HalconVisionResult.Failure(jobName, $"执行超时（{timeout?.TotalMilliseconds:F0} ms）", sw.Elapsed);
        }
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
            try
            {
                ProcessJob(job);
            }
            catch (Exception ex)
            {
                // 最后防线：任何未预期异常不得杀死 Worker（否则所有后续调用方将永久挂起）
                _logger.Fatal("[Vision] Worker 处理作业时发生未预期异常，已拦截以保持引擎存活", LogCategories.Vision, ex);
                if (job.IsEngineAction)
                    job.ActionCompletion?.TrySetException(ex);
                else
                    job.Completion?.TrySetResult(HalconVisionResult.Failure(
                        job.Request?.ProcedureName ?? job.Pipeline?.PipelineId ?? "unknown",
                        $"未预期异常: {ex.Message}", TimeSpan.Zero));
            }
        }

        // Channel 关闭后在 Worker 线程上释放所有 Halcon 资源（线程安全）
        DisposeAllCachedProcedures();
        _engine?.Dispose();
    }

    /// <summary>处理单个作业（仅 Worker 线程调用）</summary>
    private void ProcessJob(VisionJob job)
    {
        if (job.CancellationToken.IsCancellationRequested || job.IsAbandoned)
        {
            if (job.IsEngineAction)
                job.ActionCompletion?.TrySetCanceled(job.CancellationToken);
            else
                job.Completion?.TrySetCanceled(job.CancellationToken);
            return;
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
            return;
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

        // 调用方已超时/取消离开：结果无人消费，就地释放 HObject 防泄漏，不再触发事件
        if (job.IsAbandoned)
        {
            DisposeIconicOutputs(result);
            job.Completion?.TrySetCanceled();
            return;
        }

        // 先触发事件（订阅者仅在回调期间借用结果，保留 HObject 须自行克隆），
        // 再将结果连同其中 HObject 的所有权交给等待的调用方
        RaiseProcedureExecuted(result);
        job.Completion?.TrySetResult(result);
    }

    /// <summary>触发 ProcedureExecuted 事件并隔离订阅者异常（订阅者异常不得波及 Worker）</summary>
    private void RaiseProcedureExecuted(IVisionResult result)
    {
        var handlers = ProcedureExecuted;
        if (handlers is null) return;
        try { handlers.Invoke(this, result); }
        catch (Exception ex)
        {
            _logger.Error("[Vision] ProcedureExecuted 订阅者抛出异常", LogCategories.Vision, ex);
        }
    }

    /// <summary>释放结果中所有 HObject 图标量（结果无人消费时防止非托管内存泄漏）</summary>
    private static void DisposeIconicOutputs(IVisionResult result)
    {
        foreach (var v in result.IconicOutputs.Values)
            (v as HObject)?.Dispose();
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
        catch (Exception ex)
        {
            // FileNotFoundException（文件不存在）等非 Halcon 异常同样必须转为失败结果，不得抛出杀死 Worker
            _logger.Error($"[Vision] 过程加载失败: {procedureName}", LogCategories.Vision, ex);
            return HalconVisionResult.Failure(procedureName, ex.Message, sw.Elapsed);
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

            // Debug 模式：在过程入口暂停，等待 HDevelop 连接后再继续执行。
            // 必须同时确认调试服务器已启动，否则等待的连接永远不会到来（见 DebugServerStarted）
            if (_config.WaitForDebugConnection && DebugServerStarted)
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
            sw.Stop();
            _logger.Error($"[Vision] 执行失败: {request.ProcedureName} H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", LogCategories.Vision);
            return HalconVisionResult.Failure(request.ProcedureName, $"H{hex.GetErrorCode()}: {hex.GetErrorMessage()}", sw.Elapsed);
        }
        catch (Exception ex)
        {
            // 过程文件缺失、图像文件读取失败等非 Halcon 异常同样转为失败结果
            sw.Stop();
            _logger.Error($"[Vision] 执行失败: {request.ProcedureName}", LogCategories.Vision, ex);
            return HalconVisionResult.Failure(request.ProcedureName, ex.Message, sw.Elapsed);
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
        // ctx 持有所有步骤输出的独立副本，管线结束（含异常路径）统一释放
        using var ctx = new VisionContext();
        ctx.InjectExternal(externalInputs);

        _logger.Info($"[Pipeline] 开始执行: {pipeline.PipelineId}（{pipeline.Steps.Count} 步）", LogCategories.Vision);

        foreach (var step in pipeline.Steps)
        {
            // 条件评估：解析失败视为管线配置错误，终止执行
            // （原"视为 true 继续"会在配置写错时静默执行不该执行的步骤）
            bool conditionMet;
            try   { conditionMet = ctx.EvaluateCondition(step.Condition); }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.Error($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 条件解析失败，管线终止: \"{step.Condition}\"", LogCategories.Vision, ex);
                return HalconVisionResult.Failure(
                    pipeline.PipelineId,
                    $"步骤 [{step.Id}] 条件解析失败: {ex.Message}",
                    sw.Elapsed);
            }

            if (!conditionMet)
            {
                _logger.Info($"[Pipeline:{pipeline.PipelineId}] 跳过步骤 {step.Id}（条件不满足）", LogCategories.Vision);
                continue;
            }

            // 解析输入：上下文引用 → 实际值；引用缺失记录警告（原实现静默传 null，难以排查）
            var resolved = new Dictionary<string, object?>();
            foreach (var (key, rawValue) in step.Inputs)
            {
                var value = ctx.Resolve(rawValue, out var isRef, out var found);
                if (isRef && !found)
                    _logger.Warn($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 输入 '{key}' 引用 '{rawValue}' 在上下文中不存在，传入 null", LogCategories.Vision);
                resolved[key] = value;
            }

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

            // 先将声明的 outputs 以独立副本存入黑板，再触发事件——
            // 订阅者在回调期间只能借用 stepResult 内的 HObject（保留须自行克隆），
            // 事件返回后由本服务统一释放，防止中间步骤图像泄漏
            if (stepResult.Success)
            {
                foreach (var paramName in step.Outputs)
                {
                    if (stepResult.ControlOutputs.TryGetValue(paramName, out var cv))
                        ctx.Set(step.Id, paramName, cv);
                    else if (stepResult.IconicOutputs.TryGetValue(paramName, out var iv))
                        ctx.Set(step.Id, paramName, iv);
                    else
                        _logger.Warn($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 声明输出 '{paramName}' 在结果中不存在", LogCategories.Vision);
                }
            }

            RaiseProcedureExecuted(stepResult);
            DisposeIconicOutputs(stepResult);

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

            _logger.Info($"[Pipeline:{pipeline.PipelineId}] 步骤 {step.Id} 完成", LogCategories.Vision);
        }

        sw.Stop();

        // 收集管线最终输出：独立克隆，所有权随结果转移给等待方（等待方超时遗弃时由 Worker 释放）
        var finalOutputs = ctx.CollectOwned(pipeline.PipelineOutputs);

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
        // 字符串优先尝试数值解析（统一 InvariantCulture，避免整数/浮点在不同区域设置下行为不一致），
        // 支持 UI 文本框直接输入数字
        string s when int.TryParse(s, System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture, out var si)
            => new HTuple(si),
        string s when double.TryParse(s, System.Globalization.NumberStyles.Float,
                          System.Globalization.CultureInfo.InvariantCulture, out var sd)
            => new HTuple(sd),
        string s    => new HTuple(s),
        _           => new HTuple(value.ToString()),
    };

    /// <summary>
    /// 将 HTuple 转为 C# 原生类型，使 ToString() 直接可读。
    /// 单元素：int（超 int 范围为 long）/ double / string；多元素：逗号分隔字符串。
    /// 按 <see cref="HTuple.Type"/> 分派，不再以异常探测类型（原实现每个非 int 值都要吃 1~2 次异常）。
    /// </summary>
    private static object HTupleToValue(HTuple t)
    {
        if (t.Length == 0) return string.Empty;

        if (t.Length == 1)
        {
            return t.Type switch
            {
                // HALCON 整数内部为 64 位；在 int 范围内保持 int 装箱，超范围返回 long
                HTupleType.INTEGER or HTupleType.LONG
                    => t.L is >= int.MinValue and <= int.MaxValue ? (int)t.L : t.L,
                HTupleType.DOUBLE => t.D,
                HTupleType.STRING => t.S,
                _                 => t.ToString() ?? string.Empty,
            };
        }

        var parts = new string[t.Length];
        for (int j = 0; j < t.Length; j++)
        {
            HTuple elem = t[j];
            parts[j] = elem.Type switch
            {
                HTupleType.INTEGER or HTupleType.LONG => elem.L.ToString(),
                HTupleType.DOUBLE                     => elem.D.ToString("G6"),
                HTupleType.STRING                     => elem.S,
                _                                     => elem.ToString() ?? "",
            };
        }
        return string.Join(", ", parts);
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
        // 扫描规则与 HalconDebugService 共用，避免两侧 .hdvp/.hdev 优先级失步
        var names = HdevProcedureCatalog.Scan(_procedureDirectory);

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

    // 防抖：编辑器保存往往在数十毫秒内触发多个事件（临时文件写入、改名、多次 Changed），
    // 收集变化名并在静默 300ms 后统一处理，避免每个事件都触发全目录扫描和卸载作业
    private readonly object _watcherGate = new();
    private readonly HashSet<string> _pendingProcedureChanges = new(StringComparer.OrdinalIgnoreCase);
    private CancellationTokenSource? _watcherDebounceCts;

    private void OnWatcherChanged(object sender, FileSystemEventArgs e)
    {
        CollectChangedName(e.Name);
        // 重命名时旧名称对应的缓存同样需要失效
        if (e is RenamedEventArgs re)
            CollectChangedName(re.OldName);
    }

    private void CollectChangedName(string? fileName)
    {
        var ext = Path.GetExtension(fileName);
        if (!string.Equals(ext, ".hdev", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(ext, ".hdvp", StringComparison.OrdinalIgnoreCase))
            return;

        var name = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrEmpty(name)) return;

        CancellationToken token;
        lock (_watcherGate)
        {
            _pendingProcedureChanges.Add(name);
            _watcherDebounceCts?.Cancel();
            _watcherDebounceCts?.Dispose();
            _watcherDebounceCts = new CancellationTokenSource();
            token = _watcherDebounceCts.Token;
        }

        _ = FlushWatcherChangesAsync(token);
    }

    private async Task FlushWatcherChangesAsync(CancellationToken token)
    {
        try { await Task.Delay(300, token); }
        catch (OperationCanceledException) { return; }

        string[] changed;
        lock (_watcherGate)
        {
            changed = _pendingProcedureChanges.ToArray();
            _pendingProcedureChanges.Clear();
        }
        if (changed.Length == 0 || _disposed) return;

        try
        {
            ScanAvailableProcedures();

            foreach (var name in changed)
            {
                // 无论缓存里是否有该过程，都通过 Channel 投递卸载请求。
                // Worker 线程消费后会安全地从 _procedureCache / _programCache 移除，
                // 下次 Execute 时从修改后的文件重新加载。
                await UnloadProcedureAsync(name);

                _logger.Info($"[Vision] 过程目录变化: {name}（缓存已失效）", LogCategories.Vision);
                ProcedureDirectoryChanged?.Invoke(this, name);
            }
        }
        catch (Exception ex)
        {
            _logger.Error("[Vision] 处理过程目录变化失败", LogCategories.Vision, ex);
        }
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

        lock (_watcherGate)
        {
            _watcherDebounceCts?.Cancel();
            _watcherDebounceCts?.Dispose();
            _watcherDebounceCts = null;
        }

        _availableLock.Dispose();

        // 完成 Channel，Worker 在消费完剩余 Job 后自行释放所有 Halcon 资源
        _channel.Writer.TryComplete();
    }
}
