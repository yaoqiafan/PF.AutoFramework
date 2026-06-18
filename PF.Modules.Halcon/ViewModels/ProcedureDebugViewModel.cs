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

    // 保存控件引用而非裸 HWindow：HWindowControlWPF 懒初始化，Loaded 时 handle 尚为 null，
    // 渲染时实时调用 HalconWindow 确保拿到有效句柄
    private HWindowControlWPF? _halconControl;

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

    public void SetHalconControl(HWindowControlWPF control) => _halconControl = control;

    /// <summary>
    /// 每次渲染前实时取句柄；若 HALCON 内部 handle 为 null（窗口尚未就绪）返回 null。
    /// </summary>
    private HWindow? GetWindow()
    {
        var win = _halconControl?.HalconWindow;
        if (win == null) return null;
        try { HOperatorSet.GetWindowExtents(win, out _, out _, out _, out _); return win; }
        catch { return null; }
    }

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

                LogService.Info($"[Vision] 控制量输出 {result.ControlOutputs.Count} 个，图标量输出 {result.IconicOutputs.Count} 个", "Vision");

                foreach (var (key, value) in result.ControlOutputs)
                    ControlOutputs.Add(new KeyValuePair<string, string>(key, value?.ToString() ?? "null"));

                RenderIconicOutputs(result.IconicOutputs);
            }
            else
            {
                StatusMessage = $"执行失败: {result.ErrorMessage}";
                LogService.Error($"[Vision] 执行失败: {result.ErrorMessage}", "Vision");
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
        var win = GetWindow();
        if (win is null)
        {
            LogService.Warn("[Vision] HalconWindow 未就绪，跳过渲染", "Vision");
            return;
        }

        try { HOperatorSet.ClearWindow(win); }
        catch (Exception ex) { LogService.Warn($"[Vision] ClearWindow 失败: {ex.Message}", "Vision"); return; }

        AdaptWindowPart(win, iconicOutputs);
        DispatchIconicObjects(win, iconicOutputs, imageFirst: true);
        DispatchIconicObjects(win, iconicOutputs, imageFirst: false);
    }

    /// <param name="imageFirst">true 只渲染 image 类对象；false 只渲染 region/XLD 类对象</param>
    private void DispatchIconicObjects(HWindow win, IReadOnlyDictionary<string, object?> iconicOutputs, bool imageFirst)
    {
        foreach (var (key, value) in iconicOutputs)
        {
            if (value is not HObject hobj || !hobj.IsInitialized())
            {
                LogService.Info($"[Vision] 跳过 {key}：非 HObject 或未初始化 (type={value?.GetType().Name ?? "null"})", "Vision");
                continue;
            }

            int cnt = 0;
            try { HOperatorSet.CountObj(hobj, out HTuple c); cnt = c.I; }
            catch (Exception ex) { LogService.Warn($"[Vision] CountObj({key}) 失败: {ex.Message}", "Vision"); continue; }
            if (cnt <= 0) { LogService.Info($"[Vision] 跳过 {key}：空对象 (count=0)", "Vision"); continue; }

            bool isImage;
            try
            {
                HOperatorSet.GetObjClass(hobj, out HTuple cls);
                isImage = cls.S == "image";
                LogService.Info($"[Vision] {key}: class={cls.S}, count={cnt}, imageFirst={imageFirst}", "Vision");
            }
            catch (Exception ex)
            {
                LogService.Warn($"[Vision] GetObjClass({key}) 失败: {ex.Message}", "Vision");
                isImage = false;
            }

            if (imageFirst != isImage) continue;

            if (!isImage)
            {
                try { HOperatorSet.SetColor(win, "red"); } catch { }
                try { HOperatorSet.SetDraw(win, "margin"); } catch { }
            }
            else
            {
                try { HOperatorSet.SetDraw(win, "fill"); } catch { }
            }

            try
            {
                HOperatorSet.DispObj(hobj, win);
                LogService.Info($"[Vision] DispObj({key}) 成功", "Vision");
            }
            catch (HalconException hex)
            {
                LogService.Warn($"[Vision] DispObj({key}) 失败: H{hex.GetErrorCode()} {hex.GetErrorMessage()}", "Vision");
            }
            catch (Exception ex)
            {
                LogService.Warn($"[Vision] DispObj({key}) 异常: {ex.Message}", "Vision");
            }
        }
    }

    private void AdaptWindowPart(HWindow win, IReadOnlyDictionary<string, object?> iconicOutputs)
    {
        // 优先用 image 尺寸设置 part
        foreach (var (_, value) in iconicOutputs)
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

        // 没有 image，用 region 最大包围盒
        int maxRow = 0, maxCol = 0;
        foreach (var (_, value) in iconicOutputs)
        {
            if (value is not HObject hobj || !hobj.IsInitialized()) continue;
            try
            {
                HOperatorSet.SmallestRectangle1(hobj,
                    out HTuple _, out HTuple _, out HTuple row2, out HTuple col2);
                if (row2.I > maxRow) maxRow = row2.I;
                if (col2.I > maxCol) maxCol = col2.I;
            }
            catch { }
        }
        if (maxRow > 0 && maxCol > 0)
        {
            try { HOperatorSet.SetPart(win, 0, 0, maxRow, maxCol); }
            catch { }
        }
    }

    private void OnClearWindow()
    {
        var win = GetWindow();
        if (win is null) return;
        try { HOperatorSet.ClearWindow(win); }
        catch { }
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
