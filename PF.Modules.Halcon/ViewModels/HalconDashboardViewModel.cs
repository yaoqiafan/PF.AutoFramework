using PF.Core.Entities.Vision;
using PF.Core.Enums;
using PF.Core.Interfaces.Vision;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Services;
using Prism.Commands;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// 视觉仪表盘 ViewModel：左侧双 Tab（过程 / 管线），选中后导航到对应面板。
/// </summary>
public class HalconDashboardViewModel : RegionViewModelBase
{
    private readonly IVisionService       _visionService;   // Production 引擎，提供过程列表和目录监听
    private readonly VisionPipelineLoader _pipelineLoader;

    // ── 过程 Tab ──────────────────────────────────────────────────────────────

    /// <summary>过程目录中扫描到的所有 .hdev 过程名</summary>
    public ObservableCollection<string> AvailableProcedures { get; } = new();

    private string? _selectedProcedure;
    public string? SelectedProcedure
    {
        get => _selectedProcedure;
        set
        {
            if (SetProperty(ref _selectedProcedure, value) && value is not null)
                NavigateToProcedureDebug(value);
        }
    }

    public DelegateCommand RefreshCommand { get; }

    // ── 管线 Tab ──────────────────────────────────────────────────────────────

    /// <summary>Workflows 目录中扫描到的所有管线定义</summary>
    public ObservableCollection<VisionPipelineDefinition> AvailablePipelines { get; } = new();

    private VisionPipelineDefinition? _selectedPipeline;
    public VisionPipelineDefinition? SelectedPipeline
    {
        get => _selectedPipeline;
        set
        {
            if (SetProperty(ref _selectedPipeline, value) && value is not null)
                NavigateToPipelineRunner(value);
        }
    }

    public DelegateCommand RefreshPipelinesCommand { get; }

    // ── 构造 ──────────────────────────────────────────────────────────────────

    public HalconDashboardViewModel(IVisionContextManager contextManager,
                                    VisionPipelineLoader  pipelineLoader) : base()
    {
        // Dashboard 是视觉模块入口，预拉 Production 引擎以启动 FileSystemWatcher 并获取过程列表
        _visionService  = contextManager.GetOrCreate(EngineMode.Production);
        _pipelineLoader = pipelineLoader;

        RefreshCommand         = new DelegateCommand(LoadProcedures);
        RefreshPipelinesCommand = new DelegateCommand(LoadPipelines);

        _visionService.ProcedureDirectoryChanged += OnProcedureDirectoryChanged;
        _pipelineLoader.PipelineFileChanged      += OnPipelineFileChanged;

        LoadProcedures();
        LoadPipelines();
    }

    public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

    // ── 加载 ──────────────────────────────────────────────────────────────────

    private void LoadProcedures()
    {
        AvailableProcedures.Clear();
        foreach (var p in _visionService.GetAvailableProcedures())
            AvailableProcedures.Add(p);
    }

    private void LoadPipelines()
    {
        AvailablePipelines.Clear();
        foreach (var p in _pipelineLoader.GetAll())
            AvailablePipelines.Add(p);
    }

    // ── 导航 ──────────────────────────────────────────────────────────────────

    private void NavigateToProcedureDebug(string procedureName)
    {
        var parameters = new NavigationParameters { { "ProcedureName", procedureName } };
        RegionManager.RequestNavigate(
            HalconNavigationConstants.Regions.HalconContentRegion,
            HalconNavigationConstants.Views.ProcedureDebug,
            parameters);
    }

    private void NavigateToPipelineRunner(VisionPipelineDefinition pipeline)
    {
        var parameters = new NavigationParameters { { "PipelineId", pipeline.PipelineId } };
        RegionManager.RequestNavigate(
            HalconNavigationConstants.Regions.HalconContentRegion,
            HalconNavigationConstants.Views.PipelineRunner,
            parameters);
    }

    // ── 事件 ──────────────────────────────────────────────────────────────────

    private void OnProcedureDirectoryChanged(object? sender, string e)
        => Application.Current?.Dispatcher.InvokeAsync(LoadProcedures);

    private void OnPipelineFileChanged(object? sender, string e)
        => Application.Current?.Dispatcher.InvokeAsync(LoadPipelines);

    public override void Destroy()
    {
        _visionService.ProcedureDirectoryChanged -= OnProcedureDirectoryChanged;
        _pipelineLoader.PipelineFileChanged      -= OnPipelineFileChanged;
        base.Destroy();
    }
}
