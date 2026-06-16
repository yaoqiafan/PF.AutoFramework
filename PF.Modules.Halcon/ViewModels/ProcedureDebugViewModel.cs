using HalconDotNet;
using PF.Core.Interfaces.Vision;
using PF.Modules.Halcon.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// 算子调试面板 ViewModel：动态参数输入、执行、图像渲染和结果展示。
/// </summary>
public class ProcedureDebugViewModel : RegionViewModelBase
{
    private readonly IVisionService _visionService;

    // HWindow 由 View code-behind 在 HInitWindowCompleted 事件后注入
    private HWindow? _halconWindow;

    // ── 状态属性 ──────────────────────────────────────────────────────────────

    private string _procedureName = "未选择过程";
    public string ProcedureName
    {
        get => _procedureName;
        private set => SetProperty(ref _procedureName, value);
    }

    private bool _isExecuting;
    public bool IsExecuting
    {
        get => _isExecuting;
        private set
        {
            SetProperty(ref _isExecuting, value);
            RaisePropertyChanged(nameof(IsNotExecuting));
            ExecuteCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsNotExecuting => !_isExecuting;

    private string _statusMessage = "就绪";
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    private double _lastElapsedMs;
    public double LastElapsedMs
    {
        get => _lastElapsedMs;
        private set => SetProperty(ref _lastElapsedMs, value);
    }

    // ── 集合 ──────────────────────────────────────────────────────────────────

    /// <summary>动态输入参数列表（对应 .hdev 的 INPUT_* 控制变量）</summary>
    public ObservableCollection<VisionParameterItem> InputParameters { get; } = new();

    /// <summary>控制量输出（Key-Value 表格显示）</summary>
    public ObservableCollection<KeyValuePair<string, string>> ControlOutputs { get; } = new();

    // ── 命令 ──────────────────────────────────────────────────────────────────

    public DelegateCommand ExecuteCommand  { get; }
    public DelegateCommand ClearCommand    { get; }

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public ProcedureDebugViewModel(IVisionService visionService) : base()
    {
        _visionService = visionService;
        ExecuteCommand = new DelegateCommand(async () => await OnExecuteAsync(), () => IsNotExecuting);
        ClearCommand   = new DelegateCommand(OnClearWindow);
    }

    // ── 导航生命周期 ──────────────────────────────────────────────────────────

    public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        base.OnNavigatedTo(navigationContext);
        if (navigationContext.Parameters.TryGetValue<string>("ProcedureName", out var name) && name is not null)
        {
            ProcedureName = name;
            StatusMessage = $"已选择: {name}";
            BuildInputParameters();
        }
    }

    // ── View 注入 HWindow ─────────────────────────────────────────────────────

    /// <summary>由 ProcedureDebugView code-behind 在 HInitWindowCompleted 后调用</summary>
    public void SetHalconWindow(HWindow window) => _halconWindow = window;

    // ── 命令实现 ──────────────────────────────────────────────────────────────

    private async Task OnExecuteAsync()
    {
        IsExecuting = true;
        StatusMessage = "执行中...";
        ControlOutputs.Clear();

        try
        {
            var request = new VisionRequest
            {
                ProcedureName = ProcedureName,
                ControlInputs = InputParameters.ToDictionary(
                    p => p.Name, p => (object?)p.Value),
            };

            var result = await _visionService.ExecuteAsync(request);

            LastElapsedMs = result.ElapsedTime.TotalMilliseconds;

            if (result.Success)
            {
                StatusMessage = $"执行成功（{LastElapsedMs:F1} ms）";

                foreach (var (key, value) in result.ControlOutputs)
                    ControlOutputs.Add(new KeyValuePair<string, string>(key, value?.ToString() ?? "null"));

                RenderIconicOutputs(result.IconicOutputs);
            }
            else
            {
                StatusMessage = $"执行失败: {result.ErrorMessage}";
                LogService.Error($"[Vision] {result.ErrorMessage}", "Vision");
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"异常: {ex.Message}";
            LogService.Error("[Vision] 执行过程发生未预期异常", "Vision", ex);
        }
        finally
        {
            IsExecuting = false;
        }
    }

    private void RenderIconicOutputs(IReadOnlyDictionary<string, object?> iconicOutputs)
    {
        if (_halconWindow is null) return;
        try
        {
            HOperatorSet.ClearWindow(_halconWindow);
            foreach (var (key, value) in iconicOutputs)
            {
                if (value is not HObject hobj || !hobj.IsInitialized()) continue;

                if (key.Contains("REGION", StringComparison.OrdinalIgnoreCase))
                {
                    HOperatorSet.SetColor(_halconWindow, "red");
                    HOperatorSet.SetDraw(_halconWindow, "margin");
                }
                HOperatorSet.DispObj(hobj, _halconWindow);
            }
        }
        catch (HalconException hex)
        {
            LogService.Warn($"[Vision] 图像渲染失败: H{hex.GetErrorCode()}", "Vision");
        }
    }

    private void OnClearWindow()
    {
        if (_halconWindow is null) return;
        try { HOperatorSet.ClearWindow(_halconWindow); }
        catch { /* 窗口未就绪时静默忽略 */ }
    }

    /// <summary>
    /// 根据过程元数据反射构建动态输入参数行。
    /// 当前使用占位实现；接入 HDevProcedure 后可通过 GetInputCtrlParamCount/Name 精确获取。
    /// </summary>
    private void BuildInputParameters()
    {
        InputParameters.Clear();
        InputParameters.Add(new VisionParameterItem { Name = "INPUT_THRESHOLD", Value = "128"  });
        InputParameters.Add(new VisionParameterItem { Name = "INPUT_MIN_AREA",  Value = "1000" });
    }
}
