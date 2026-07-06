using HalconDotNet;
using PF.Core.Entities.Vision;
using PF.Core.Enums;
using PF.Core.Interfaces.Vision;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Services;
using Prism.Commands;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// 管线运行面板 ViewModel：选择管线、执行、收集各步 iconic 输出并叠层渲染到 HWindow。
/// </summary>
public class PipelineRunnerViewModel : RegionViewModelBase
{
    private readonly IVisionContextManager _contextManager;
    private readonly VisionPipelineLoader  _loader;

    // 首次调用 GetEngine() 时拉起 Production 引擎并订阅事件，之后复用同一实例
    private IVisionService? _engine;

    // ── HWindow ───────────────────────────────────────────────────────────────

    private HWindowControlWPF? _halconControl;

    /// <summary>由 View code-behind 在 Loaded 事件后调用</summary>
    public void SetHalconControl(HWindowControlWPF control) => _halconControl = control;

    private HWindow? GetWindow()
    {
        var win = _halconControl?.HalconWindow;
        if (win == null) return null;
        try { HOperatorSet.GetWindowExtents(win, out _, out _, out _, out _); return win; }
        catch { return null; }
    }

    // 跨步骤累积所有 iconic 输出，按 paramName 去重（后步覆盖同名前步输出）。
    // 值为本 VM 持有的 HObject 克隆——引擎事件参数仅在回调期间有效，保留必须克隆；
    // Worker 线程（OnStepExecuted）与 UI 线程（渲染/清空）并发访问，由 _iconicsGate 保护。
    private readonly Dictionary<string, object?> _cumulativeIconics = new();
    private readonly object _iconicsGate = new();

    // ── 集合 ──────────────────────────────────────────────────────────────────

    public ObservableCollection<VisionPipelineDefinition>    Pipelines { get; } = new();
    public ObservableCollection<KeyValuePair<string, string>> Outputs  { get; } = new();
    public ObservableCollection<string>                       StepLogs { get; } = new();

    // ── 属性 ──────────────────────────────────────────────────────────────────

    private VisionPipelineDefinition? _selectedPipeline;
    public VisionPipelineDefinition? SelectedPipeline
    {
        get => _selectedPipeline;
        set => SetProperty(ref _selectedPipeline, value);
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        private set { SetProperty(ref _isRunning, value); RunCommand.RaiseCanExecuteChanged(); }
    }

    private string _status = "就绪";
    public string Status
    {
        get => _status;
        private set => SetProperty(ref _status, value);
    }

    private double _lastElapsedMs;
    public double LastElapsedMs
    {
        get => _lastElapsedMs;
        private set => SetProperty(ref _lastElapsedMs, value);
    }

    // ── 命令 ──────────────────────────────────────────────────────────────────

    public DelegateCommand RunCommand     { get; }
    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand ClearCommand   { get; }

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public PipelineRunnerViewModel(IVisionContextManager contextManager,
                                   VisionPipelineLoader  loader) : base()
    {
        _contextManager = contextManager;
        _loader         = loader;

        RunCommand     = new DelegateCommand(async () => await OnRunAsync(), () => !IsRunning);
        RefreshCommand = new DelegateCommand(LoadPipelines);
        ClearCommand   = new DelegateCommand(OnClearWindow);

        _loader.PipelineFileChanged += (_, _) =>
            Application.Current?.Dispatcher.InvokeAsync(LoadPipelines);

        LoadPipelines();
    }

    // ── 引擎懒取 ─────────────────────────────────────────────────────────────

    private IVisionService GetEngine()
    {
        if (_engine != null) return _engine;
        _engine = _contextManager.GetOrCreate(EngineMode.Production);
        _engine.ProcedureExecuted += OnStepExecuted;
        return _engine;
    }

    // ── 内部 ──────────────────────────────────────────────────────────────────

    private void LoadPipelines()
    {
        var selected = SelectedPipeline?.PipelineId;
        Pipelines.Clear();
        foreach (var p in _loader.GetAll())
            Pipelines.Add(p);

        if (selected is not null)
            SelectedPipeline = Pipelines.FirstOrDefault(p => p.PipelineId == selected);
    }

    private async Task OnRunAsync()
    {
        if (SelectedPipeline is null) return;

        IsRunning = true;
        Status    = $"运行中: {SelectedPipeline.Name}...";
        Outputs.Clear();
        StepLogs.Clear();
        DisposeCumulativeIconics();
        StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] 启动管线: {SelectedPipeline.PipelineId}");

        try
        {
            var result = await GetEngine().ExecutePipelineAsync(SelectedPipeline);

            LastElapsedMs = result.ElapsedTime.TotalMilliseconds;

            if (result.Success)
            {
                Status = $"完成（{LastElapsedMs:F0} ms）";
                foreach (var (k, v) in result.ControlOutputs)
                    Outputs.Add(new KeyValuePair<string, string>(k, v?.ToString() ?? "null"));
            }
            else
            {
                Status = $"失败: {result.ErrorMessage}";
                StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] 错误: {result.ErrorMessage}");
                LogService.Error($"[Pipeline] 执行失败: {result.ErrorMessage}", "Vision");
            }

            // 最终输出中的 HObject 所有权归本调用方；渲染用的是各步骤事件的克隆，
            // 这里直接释放最终输出，防止非托管内存泄漏
            foreach (var v in result.IconicOutputs.Values)
                (v as HObject)?.Dispose();

            // 无论成败，渲染已收集到的 iconic 输出（方便调试中间步骤）
            RenderCumulativeIconics();
        }
        catch (Exception ex)
        {
            Status = $"异常: {ex.Message}";
            StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] 异常: {ex.Message}");
            LogService.Error("[Pipeline] 发生未预期异常", "Vision", ex);
        }
        finally
        {
            IsRunning = false;
            StepLogs.Add($"[{DateTime.Now:HH:mm:ss}] 管线结束");
        }
    }

    private void OnStepExecuted(object? sender, IVisionResult stepResult)
    {
        // 追加日志
        var line = stepResult.Success
            ? $"[{DateTime.Now:HH:mm:ss}] ✓ {stepResult.ProcedureName}（{stepResult.ElapsedTime.TotalMilliseconds:F0} ms）"
            : $"[{DateTime.Now:HH:mm:ss}] ✗ {stepResult.ProcedureName}: {stepResult.ErrorMessage}";

        Application.Current?.Dispatcher.InvokeAsync(() => StepLogs.Add(line));

        // 将本步骤的 iconic 输出合并入累积字典（同名参数后步覆盖前步）。
        // 引擎契约：事件参数中的 HObject 仅在回调期间有效，保留必须克隆（句柄级复制，代价极低）；
        // 被覆盖的旧克隆就地释放。
        if (stepResult.Success)
        {
            lock (_iconicsGate)
            {
                foreach (var (k, v) in stepResult.IconicOutputs)
                {
                    object? owned = v;
                    if (v is HObject h)
                    {
                        if (!h.IsInitialized()) continue;
                        HOperatorSet.CopyObj(h, out HObject copy, 1, -1);
                        owned = copy;
                    }
                    if (_cumulativeIconics.TryGetValue(k, out var old))
                        (old as HObject)?.Dispose();
                    _cumulativeIconics[k] = owned;
                }
            }
        }
    }

    /// <summary>释放并清空累积的 iconic 克隆（本 VM 是这些 HObject 的唯一所有者）</summary>
    private void DisposeCumulativeIconics()
    {
        lock (_iconicsGate)
        {
            foreach (var v in _cumulativeIconics.Values)
                (v as HObject)?.Dispose();
            _cumulativeIconics.Clear();
        }
    }

    // ── 图像渲染 ──────────────────────────────────────────────────────────────

    private void RenderCumulativeIconics()
    {
        var win = GetWindow();
        if (win is null) return;

        // 持锁渲染：防止 Worker 线程在 OnStepExecuted 中释放被覆盖的旧克隆时，
        // UI 线程正拿着同一句柄执行 DispObj（use-after-free）
        lock (_iconicsGate)
        {
            if (_cumulativeIconics.Count == 0) return;

            try { HOperatorSet.ClearWindow(win); }
            catch (Exception ex) { LogService.Warn($"[Pipeline] ClearWindow 失败: {ex.Message}", "Vision"); return; }

            AdaptWindowPart(win);

            // 先渲染 image 类（背景），再渲染 region/XLD 类（前景）
            DispIconics(win, imageFirst: true);
            DispIconics(win, imageFirst: false);
        }
    }

    private void AdaptWindowPart(HWindow win)
    {
        foreach (var (_, value) in _cumulativeIconics)
        {
            if (value is not HObject hobj || !hobj.IsInitialized()) continue;
            try
            {
                HOperatorSet.GetObjClass(hobj, out HTuple cls);
                if (cls.S != "image") continue;
                HOperatorSet.GetImageSize(hobj, out HTuple w, out HTuple h);
                HOperatorSet.SetPart(win, 0, 0, h.I - 1, w.I - 1);
                return;
            }
            catch { }
        }
    }

    private void DispIconics(HWindow win, bool imageFirst)
    {
        foreach (var (key, value) in _cumulativeIconics)
        {
            if (value is not HObject hobj || !hobj.IsInitialized()) continue;

            int cnt = 0;
            try { HOperatorSet.CountObj(hobj, out HTuple c); cnt = c.I; }
            catch { continue; }
            if (cnt <= 0) continue;

            bool isImage;
            try { HOperatorSet.GetObjClass(hobj, out HTuple cls); isImage = cls.S == "image"; }
            catch { isImage = false; }

            if (imageFirst != isImage) continue;

            if (isImage)
            {
                try { HOperatorSet.SetDraw(win, "fill"); } catch { }
            }
            else
            {
                // 区分 Region 和 DefectRegion 的颜色
                try
                {
                    var color = key.Contains("Defect", StringComparison.OrdinalIgnoreCase)
                        ? "red" : "lime green";
                    HOperatorSet.SetColor(win, color);
                    HOperatorSet.SetDraw(win, "margin");
                    HOperatorSet.SetLineWidth(win, 2);
                }
                catch { }
            }

            try { HOperatorSet.DispObj(hobj, win); }
            catch (HalconException hex)
            {
                LogService.Warn($"[Pipeline] DispObj({key}): H{hex.GetErrorCode()} {hex.GetErrorMessage()}", "Vision");
            }
        }
    }

    private void OnClearWindow()
    {
        var win = GetWindow();
        if (win is not null)
        {
            try { HOperatorSet.ClearWindow(win); } catch { }
        }
        DisposeCumulativeIconics();
    }

    // ── 生命周期 ──────────────────────────────────────────────────────────────

    public override void Destroy()
    {
        if (_engine is not null)
            _engine.ProcedureExecuted -= OnStepExecuted;
        DisposeCumulativeIconics();
        base.Destroy();
    }
}
