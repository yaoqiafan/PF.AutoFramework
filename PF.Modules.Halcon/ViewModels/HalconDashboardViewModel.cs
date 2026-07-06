using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System.Windows;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// 视觉调试仪表盘 ViewModel：驱动内部区域在「过程调试」和「管线运行」之间导航。
/// 选项切换时 HalconDebugViewModel 的 OnNavigatedTo/From 钩子自动启停调试服务器。
/// </summary>
public class HalconDashboardViewModel : RegionViewModelBase
{
    private string _selectedView = string.Empty;

    public bool IsDebugSelected    => _selectedView == HalconNavigationConstants.Views.HalconDebug;
    public bool IsPipelineSelected => _selectedView == HalconNavigationConstants.Views.PipelineRunner;

    public DelegateCommand NavToDebugCommand    { get; }
    public DelegateCommand NavToPipelineCommand { get; }

    public HalconDashboardViewModel() : base()
    {
        NavToDebugCommand    = new DelegateCommand(() => Navigate(HalconNavigationConstants.Views.HalconDebug));
        NavToPipelineCommand = new DelegateCommand(() => Navigate(HalconNavigationConstants.Views.PipelineRunner));
    }

    public override bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public override void OnNavigatedTo(NavigationContext navigationContext)
    {
        base.OnNavigatedTo(navigationContext);

        // 首次进入时默认导航到过程调试；延迟一帧确保内部 Region 已完成注册
        if (string.IsNullOrEmpty(_selectedView))
        {
            Application.Current?.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Loaded,
                () => Navigate(HalconNavigationConstants.Views.HalconDebug));
        }
    }

    private void Navigate(string viewName)
    {
        _selectedView = viewName;
        RaisePropertyChanged(nameof(IsDebugSelected));
        RaisePropertyChanged(nameof(IsPipelineSelected));
        RegionManager.RequestNavigate(
            HalconNavigationConstants.Regions.HalconContentRegion, viewName);
    }
}
