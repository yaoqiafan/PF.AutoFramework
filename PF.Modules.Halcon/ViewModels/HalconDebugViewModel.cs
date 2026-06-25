using HalconDotNet;
using PF.Core.Interfaces.Vision;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Modules.Halcon.Controls;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Internal;
using Prism.Commands;
using System.Collections.ObjectModel;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// HALCON 调试集成面板 ViewModel（HDevEngine Level 2）。
/// 流程：启动调试服务器 → 填写参数 → 点击「启动 HDevelop」。
/// HDevelop 打开后通过「附加到进程」连接调试服务器，C# 端开始执行过程并在入口暂停，
/// HDevelop 接入后可断点调试，调试结束后输出控制量回填到界面。
/// </summary>
public class HalconDebugViewModel : RegionViewModelBase
{
    private readonly IHalconDebugService _debugService;

    // ── 过程文件 ──────────────────────────────────────────────────────────────

    public ObservableCollection<string> Procedures { get; } = new();

    private string? _selectedProcedure;
    public string? SelectedProcedure
    {
        get => _selectedProcedure;
        set
        {
            SetProperty(ref _selectedProcedure, value);
            RaisePropertyChanged(nameof(CanLaunchHDevelop));
            LaunchCommand.RaiseCanExecuteChanged();
            _ = LoadSignatureAsync(value);
        }
    }

    // ── 参数签名 ──────────────────────────────────────────────────────────────

    public ObservableCollection<InputIconicParamVm>  InputIconics   { get; } = new();
    public ObservableCollection<InputControlParamVm> InputControls  { get; } = new();
    public ObservableCollection<OutputCtrlParamVm>   OutputControls { get; } = new();

    private bool _hasSignature;
    public bool HasSignature
    {
        get => _hasSignature;
        private set => SetProperty(ref _hasSignature, value);
    }

    private string _outputIconicSummary = string.Empty;
    public string OutputIconicSummary
    {
        get => _outputIconicSummary;
        private set => SetProperty(ref _outputIconicSummary, value);
    }

    private bool _hasOutputIconics;
    public bool HasOutputIconics
    {
        get => _hasOutputIconics;
        private set => SetProperty(ref _hasOutputIconics, value);
    }

    private CancellationTokenSource? _signatureCts;

    // ── 调试会话状态 ──────────────────────────────────────────────────────────

    private bool _isDebugging;
    public bool IsDebugging
    {
        get => _isDebugging;
        private set
        {
            SetProperty(ref _isDebugging, value);
            RaisePropertyChanged(nameof(CanLaunchHDevelop));
            LaunchCommand.RaiseCanExecuteChanged();
            CancelDebugCommand.RaiseCanExecuteChanged();
        }
    }

    private CancellationTokenSource? _debugCts;

    // ── 调试服务器配置 ────────────────────────────────────────────────────────

    private int _port = 57786;
    public int Port
    {
        get => _port;
        set => SetProperty(ref _port, value);
    }

    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    private bool _runImmediately;
    public bool RunImmediately
    {
        get => _runImmediately;
        set => SetProperty(ref _runImmediately, value);
    }

    private bool _isActive;
    public bool IsActive
    {
        get => _isActive;
        private set
        {
            SetProperty(ref _isActive, value);
            RaisePropertyChanged(nameof(CanLaunchHDevelop));
            EnableCommand.RaiseCanExecuteChanged();
            DisableCommand.RaiseCanExecuteChanged();
            LaunchCommand.RaiseCanExecuteChanged();
        }
    }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            SetProperty(ref _isBusy, value);
            RaisePropertyChanged(nameof(IsNotBusy));
            EnableCommand.RaiseCanExecuteChanged();
            DisableCommand.RaiseCanExecuteChanged();
        }
    }

    public bool IsNotBusy => !_isBusy;

    private string _serverStatus = "调试服务器未启动";
    public string ServerStatus
    {
        get => _serverStatus;
        private set => SetProperty(ref _serverStatus, value);
    }

    /// <summary>已启动服务器 + 已选过程 + 未在调试中，供 XAML IsEnabled 双保险绑定</summary>
    public bool CanLaunchHDevelop => IsActive && SelectedProcedure is not null && !IsDebugging;

    // ── 命令 ──────────────────────────────────────────────────────────────────

    public DelegateCommand RefreshCommand     { get; }
    public DelegateCommand EnableCommand      { get; }
    public DelegateCommand DisableCommand     { get; }
    public DelegateCommand LaunchCommand      { get; }
    public DelegateCommand CancelDebugCommand { get; }

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public HalconDebugViewModel(IHalconDebugService debugService) : base()
    {
        _debugService = debugService;

        RefreshCommand = new DelegateCommand(RefreshProcedures);
        EnableCommand  = new DelegateCommand(async () => await OnEnableAsync(),
                             () => !IsActive && !IsBusy);
        DisableCommand = new DelegateCommand(async () => await OnDisableAsync(),
                             () => IsActive && !IsBusy);
        LaunchCommand  = new DelegateCommand(async () => await OnLaunchAsync(),
                             () => IsActive && SelectedProcedure is not null && !IsDebugging);
        CancelDebugCommand = new DelegateCommand(OnCancelDebug, () => IsDebugging);

        RefreshProcedures();
    }

    // ── 过程列表 ──────────────────────────────────────────────────────────────

    private void RefreshProcedures()
    {
        var current = SelectedProcedure;
        Procedures.Clear();
        foreach (var name in _debugService.GetAvailableProcedures())
            Procedures.Add(name);
        SelectedProcedure = Procedures.Contains(current!) ? current : null;
    }

    // ── 签名加载 ──────────────────────────────────────────────────────────────

    private async Task LoadSignatureAsync(string? procedureName)
    {
        _signatureCts?.Cancel();
        _signatureCts?.Dispose();
        _signatureCts = new CancellationTokenSource();
        var ct = _signatureCts.Token;

        InputIconics.Clear();
        InputControls.Clear();
        OutputControls.Clear();
        HasSignature        = false;
        HasOutputIconics    = false;
        OutputIconicSummary = string.Empty;

        if (string.IsNullOrEmpty(procedureName)) return;

        try
        {
            var sig = await _debugService.GetProcedureSignatureAsync(procedureName, ct);
            if (ct.IsCancellationRequested || sig == null) return;

            foreach (var p in sig.InputParams)
            {
                if (p.Kind == ProcedureParamKind.Iconic)
                    InputIconics.Add(new InputIconicParamVm(p.Name));
                else
                    InputControls.Add(new InputControlParamVm(p.Name));
            }

            var iconicOutNames = new List<string>();
            foreach (var p in sig.OutputParams)
            {
                if (p.Kind == ProcedureParamKind.Control)
                    OutputControls.Add(new OutputCtrlParamVm(p.Name));
                else
                    iconicOutNames.Add(p.Name);
            }

            if (iconicOutNames.Count > 0)
            {
                OutputIconicSummary = string.Join("，", iconicOutNames);
                HasOutputIconics    = true;
            }

            HasSignature = InputIconics.Count > 0 || InputControls.Count > 0 || OutputControls.Count > 0;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            LogService.Warn($"[Vision] 读取过程签名失败: {ex.Message}", "Vision");
        }
    }

    // ── 启动 HDevelop + 调试执行 ──────────────────────────────────────────────

    // ── 图像查看器注入 ────────────────────────────────────────────────────────

    private HalconImageViewer? _imageViewer;

    /// <summary>由 code-behind 在 Loaded 后调用，注入图像查看器控件引用</summary>
    public void SetImageViewer(HalconImageViewer viewer) => _imageViewer = viewer;

    // ── ROI 编辑器注入 ────────────────────────────────────────────────────────

    private HalconRoiEditor? _roiEditor;

    /// <summary>由 code-behind 在 Loaded 后调用，注入当前激活的 ROI 编辑器</summary>
    public void SetRoiEditor(HalconRoiEditor? editor) => _roiEditor = editor;

    // ── 调试执行 ──────────────────────────────────────────────────────────────

    private async Task OnLaunchAsync()
    {
        if (SelectedProcedure is null) return;

        // 文件模式的 iconic 参数必须已选择文件
        var missingFiles = InputIconics.Where(p => p.IsFileMode && string.IsNullOrEmpty(p.FilePath)).ToList();
        if (missingFiles.Count > 0)
        {
            ServerStatus = $"请先选择图像文件：{string.Join("，", missingFiles.Select(p => p.Name))}";
            return;
        }

        foreach (var p in OutputControls) p.Value = null;
        _imageViewer?.Clear();

        // 1. 启动 HDevelop（非阻塞），HDevelop 打开后通过「附加到进程」连接调试服务器
        _debugService.LaunchHDevelop(SelectedProcedure, RunImmediately);

        ServerStatus = $"HDevelop 已启动「{SelectedProcedure}」| "
                     + $"在 HDevelop 中附加到进程（端口 {Port}），C# 端过程将在入口暂停等待接入";

        // 2. C# 端执行过程，过程在入口暂停等待 HDevelop 附加后继续
        _debugCts?.Dispose();
        _debugCts   = new CancellationTokenSource();
        IsDebugging = true;

        try
        {
            var ctrlInputs  = InputControls
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .ToDictionary(p => p.Name, p => p.Value);

            var iconicPaths = InputIconics
                .Where(p => p.IsFileMode && !string.IsNullOrEmpty(p.FilePath))
                .ToDictionary(p => p.Name, p => p.FilePath!);

            // ROI 模式：从编辑器读取当前 ROI，计算 Region，直接注入为 HObject
            var iconicObjects = new Dictionary<string, object?>();
            foreach (var param in InputIconics.Where(p => p.IsRoiMode))
            {
                var rois = _roiEditor is not null
                    ? _roiEditor.GetCurrentRois()
                    : (IReadOnlyList<VisionRoiConfig>)param.CurrentRois.ToList();

                var region = RoiRegionBuilder.Build(rois);
                iconicObjects[param.Name] = region;
            }

            var result = await _debugService.RunTestAsync(
                SelectedProcedure, ctrlInputs, iconicPaths, iconicObjects, _debugCts.Token);

            // 释放临时 HObject
            foreach (var obj in iconicObjects.Values.OfType<HObject>())
                try { obj.Dispose(); } catch { }

            if (result.Success)
            {
                foreach (var outParam in OutputControls)
                {
                    outParam.Value = result.ControlOutputs.TryGetValue(outParam.Name, out var val)
                        ? val?.ToString() ?? "(null)"
                        : "(未返回)";
                }

                // 在右侧图像查看器渲染图标量输出
                if (_imageViewer is not null && result.IconicOutputs.Count > 0)
                    _imageViewer.DisplayIconics(result.IconicOutputs);

                ServerStatus = $"调试完成，耗时 {result.ElapsedTime.TotalMilliseconds:F0} ms | "
                             + $"控制量 {result.ControlOutputs.Count} 个，图标量 {result.IconicOutputs.Count} 个";
            }
            else
            {
                ServerStatus = $"过程执行失败: {result.ErrorMessage}";
            }
        }
        catch (OperationCanceledException)
        {
            ServerStatus = "调试已取消 — 请在 HDevelop 中断开连接";
        }
        catch (Exception ex)
        {
            ServerStatus = $"调试异常: {ex.Message}";
            LogService.Error("[Vision] 调试执行异常", "Vision", ex);
        }
        finally
        {
            IsDebugging = false;
        }
    }

    private void OnCancelDebug()
    {
        // CancellationToken 无法中断 HALCON native 执行；立即更新 UI，
        // 实际执行在 HDevelop 断开连接后自然结束（worker 抛异常 → finally 归还状态）
        _debugCts?.Cancel();
        IsDebugging  = false;
        ServerStatus = "已发出取消信号 — 请在 HDevelop 中停止运行或关闭 HDevelop";
    }

    // ── 调试服务器控制 ────────────────────────────────────────────────────────

    private async Task OnEnableAsync()
    {
        IsBusy       = true;
        ServerStatus = $"正在启动调试服务器（端口 {Port}）...";
        try
        {
            var ok = await _debugService.EnableDebugServerAsync(Port, Password);
            if (ok)
            {
                IsActive     = true;
                ServerStatus = $"调试服务器已启动 | 端口 {Port}";
                RefreshProcedures();
            }
            else
            {
                ServerStatus = "调试服务器启动失败，请查看日志";
            }
        }
        catch (Exception ex)
        {
            ServerStatus = $"启动失败: {ex.Message}";
            LogService.Error("[Vision] 调试服务器启动异常", "Vision", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task OnDisableAsync()
    {
        _debugCts?.Cancel();
        IsBusy       = true;
        ServerStatus = "正在停止调试服务器...";
        try
        {
            await _debugService.DisableDebugServerAsync();
            IsActive     = false;
            IsDebugging  = false;
            ServerStatus = "调试服务器已停止";
        }
        catch (Exception ex)
        {
            ServerStatus = $"停止失败: {ex.Message}";
            LogService.Error("[Vision] 调试服务器停止异常", "Vision", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── 导航生命周期 ──────────────────────────────────────────────────────────

    /// <summary>切换到调试页面时自动启动调试服务器</summary>
    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        base.OnNavigatedTo(navigationContext);
        if (!IsActive && !IsBusy)
            _ = OnEnableAsync();
    }

    /// <summary>离开调试页面时取消进行中的调试会话，并停止调试服务器</summary>
    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        base.OnNavigatedFrom(navigationContext);
        _debugCts?.Cancel();
        if (IsActive && !IsBusy)
            _ = OnDisableAsync();
    }

    public override void Destroy()
    {
        _signatureCts?.Cancel();
        _signatureCts?.Dispose();
        _debugCts?.Cancel();
        _debugCts?.Dispose();

        if (_debugService.IsDebugServerActive)
            _ = _debugService.DisableDebugServerAsync();
        base.Destroy();
    }
}
