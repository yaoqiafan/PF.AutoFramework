using PF.Core.Entities.Vision;
using PF.Core.Interfaces.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonValueKind = System.Text.Json.JsonValueKind;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// 从文件系统加载管线 JSON 配置，并通过 FileSystemWatcher 监控变更。
/// <para>
/// JSON 文件放置在 Devices/{DeviceName}/Workflows/ 目录下，框架自动扫描并缓存。
/// 文件变更时自动重新扫描并触发 <see cref="PipelineFileChanged"/> 事件。
/// </para>
/// </summary>
public sealed class VisionPipelineLoader : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling         = JsonCommentHandling.Skip,
        AllowTrailingCommas         = true,
        NumberHandling              = JsonNumberHandling.AllowReadingFromString,
    };

    private readonly string           _pipelineDirectory;
    private readonly ILogService      _logger;
    private readonly FileSystemWatcher _watcher;

    // pipelineId → definition，读写锁保护
    private readonly Dictionary<string, VisionPipelineDefinition> _cache = new();
    private readonly ReaderWriterLockSlim _cacheLock = new();

    /// <summary>管线 JSON 文件新增、修改或删除时触发，参数为文件名（含扩展名）</summary>
    public event EventHandler<string>? PipelineFileChanged;

    public VisionPipelineLoader(string pipelineDirectory, ILogService logger)
    {
        _pipelineDirectory = Path.GetFullPath(pipelineDirectory);
        _logger            = logger;

        if (!Directory.Exists(_pipelineDirectory))
            Directory.CreateDirectory(_pipelineDirectory);

        ScanAll();

        _watcher = new FileSystemWatcher(_pipelineDirectory, "*.json")
        {
            IncludeSubdirectories = true,
            NotifyFilter          = NotifyFilters.FileName | NotifyFilters.LastWrite,
            EnableRaisingEvents   = true,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnFileEvent;
    }

    // ── 公共 API ──────────────────────────────────────────────────────────────

    /// <summary>获取所有已扫描的管线定义快照</summary>
    public IReadOnlyList<VisionPipelineDefinition> GetAll()
    {
        _cacheLock.EnterReadLock();
        try { return [.. _cache.Values]; }
        finally { _cacheLock.ExitReadLock(); }
    }

    /// <summary>按 pipelineId 查找；找不到返回 null</summary>
    public VisionPipelineDefinition? Find(string pipelineId)
    {
        _cacheLock.EnterReadLock();
        try { return _cache.GetValueOrDefault(pipelineId); }
        finally { _cacheLock.ExitReadLock(); }
    }

    /// <summary>直接从文件路径加载，不经过缓存（用于单次临时加载）</summary>
    public static VisionPipelineDefinition LoadFromFile(string filePath)
    {
        var json = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
        var def  = JsonSerializer.Deserialize<VisionPipelineDefinition>(json, JsonOptions)
                   ?? throw new InvalidDataException($"管线 JSON 反序列化结果为空: {filePath}");

        // System.Text.Json 将 Dictionary<string, object?> 中的值反序列化为 JsonElement，
        // 而非原生 C# 类型，导致字符串 "$stepId.param" 引用无法被 ctx.Resolve 识别。
        // 在此处统一解包，确保后续管线逻辑处理的都是原生类型。
        foreach (var step in def.Steps)
        {
            var keys = step.Inputs.Keys.ToList();
            foreach (var key in keys)
                step.Inputs[key] = UnboxJsonElement(step.Inputs[key]);
        }

        return def;
    }

    private static object? UnboxJsonElement(object? value)
    {
        if (value is not JsonElement je) return value;
        return je.ValueKind switch
        {
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt64(out var l) ? (object?)l : je.GetDouble(),
            JsonValueKind.True   => true,
            JsonValueKind.False  => false,
            JsonValueKind.Null   => null,
            _                    => je.GetRawText(),
        };
    }

    // ── 内部扫描 ──────────────────────────────────────────────────────────────

    private void ScanAll()
    {
        if (!Directory.Exists(_pipelineDirectory)) return;

        var files = Directory.GetFiles(_pipelineDirectory, "*.json", SearchOption.AllDirectories);

        _cacheLock.EnterWriteLock();
        try
        {
            _cache.Clear();
            foreach (var file in files)
            {
                try
                {
                    var def = LoadFromFile(file);
                    if (string.IsNullOrEmpty(def.PipelineId))
                    {
                        _logger.Warn($"[Pipeline] 跳过：PipelineId 为空 ({file})", "Vision");
                        continue;
                    }
                    _cache[def.PipelineId] = def;
                    _logger.Info($"[Pipeline] 已加载: {def.PipelineId} — {def.Steps.Count} 步", "Vision");
                }
                catch (Exception ex)
                {
                    _logger.Error($"[Pipeline] 加载失败: {file}", "Vision", ex);
                }
            }
        }
        finally { _cacheLock.ExitWriteLock(); }
    }

    // 防抖：编辑器保存会在短时间内触发连串事件（临时文件、改名、多次 Changed），
    // 原实现每个事件都排一次全量重扫；现在仅在静默 250ms 后重扫一次
    private readonly object _rescanGate = new();
    private CancellationTokenSource? _rescanCts;
    private string _lastChangedFile = "";
    private volatile bool _disposed;

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        CancellationToken token;
        lock (_rescanGate)
        {
            _lastChangedFile = e.Name ?? "";
            _rescanCts?.Cancel();
            _rescanCts?.Dispose();
            _rescanCts = new CancellationTokenSource();
            token = _rescanCts.Token;
        }
        _ = RescanAfterDelayAsync(token);
    }

    private async Task RescanAfterDelayAsync(CancellationToken token)
    {
        // 延迟同时起到"等待文件写入完成"的作用
        try { await Task.Delay(250, token); }
        catch (OperationCanceledException) { return; }

        if (_disposed) return;

        string changed;
        lock (_rescanGate) { changed = _lastChangedFile; }

        try
        {
            ScanAll();
            PipelineFileChanged?.Invoke(this, changed);
        }
        catch (Exception ex)
        {
            _logger.Error("[Pipeline] 文件变更重扫失败", "Vision", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _watcher.EnableRaisingEvents = false;
        _watcher.Dispose();

        lock (_rescanGate)
        {
            _rescanCts?.Cancel();
            _rescanCts?.Dispose();
            _rescanCts = null;
        }

        _cacheLock.Dispose();
    }
}
