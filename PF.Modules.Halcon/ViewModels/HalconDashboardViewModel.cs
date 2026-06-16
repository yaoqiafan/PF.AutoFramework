using PF.Core.Interfaces.Vision;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System.Collections.ObjectModel;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// 视觉仪表盘 ViewModel：维护过程列表，选中后导航到 ProcedureDebugView。
/// </summary>
public class HalconDashboardViewModel : RegionViewModelBase
{
    private readonly IVisionService _visionService;

    /// <summary>过程目录中扫描到的所有 .hdev 过程名</summary>
    public ObservableCollection<string> AvailableProcedures { get; } = new();

    private string? _selectedProcedure;
    /// <summary>当前选中的过程名（选中后自动导航到调试面板）</summary>
    public string? SelectedProcedure
    {
        get => _selectedProcedure;
        set
        {
            if (SetProperty(ref _selectedProcedure, value) && value is not null)
                NavigateToProcedureDebug(value);
        }
    }

    /// <summary>手动刷新过程列表</summary>
    public DelegateCommand RefreshCommand { get; }

    public HalconDashboardViewModel(IVisionService visionService) : base()
    {
        _visionService = visionService;
        RefreshCommand = new DelegateCommand(LoadProcedures);
        _visionService.ProcedureDirectoryChanged += OnDirectoryChanged;
        LoadProcedures();
    }

    public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

    private void LoadProcedures()
    {
        AvailableProcedures.Clear();
        foreach (var p in _visionService.GetAvailableProcedures())
            AvailableProcedures.Add(p);
    }

    private void NavigateToProcedureDebug(string procedureName)
    {
        var parameters = new NavigationParameters { { "ProcedureName", procedureName } };
        RegionManager.RequestNavigate(
            HalconNavigationConstants.Regions.HalconContentRegion,
            HalconNavigationConstants.Views.ProcedureDebug,
            parameters);
    }

    private void OnDirectoryChanged(object? sender, string e)
        => System.Windows.Application.Current?.Dispatcher.InvokeAsync(LoadProcedures);

    public override void Destroy()
    {
        _visionService.ProcedureDirectoryChanged -= OnDirectoryChanged;
        base.Destroy();
    }
}
