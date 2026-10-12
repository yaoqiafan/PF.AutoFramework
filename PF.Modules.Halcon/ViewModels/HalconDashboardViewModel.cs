using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Navigation.Regions;
using System.Windows;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// 视觉调试仪表盘 ViewModel：驱动内部区域在「过程调试」和「管线运行」之间导航。
/// 内部选项切换时 HalconDebugViewModel 的 OnNavigatedTo/From 钩子自动启停调试服务器；
/// 本仪表盘整体被切走/切回时，内部页面收不到导航通知，由本类转告（OnHostDeactivated/OnHostActivated）。
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

    /// <summary>本实例依赖 IsNavigationTarget 复用，必须保留在 Region 中才可能被匹配到。</summary>
    public override bool KeepAlive => true;

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
        else
        {
            // 再次回来：本实例常驻、不会重新导航内部页面，内部的调试页收不到 OnNavigatedTo——转告它
            foreach (var vm in InnerDebugViewModels()) vm.OnHostActivated();
        }
    }

    /// <summary>
    /// 主界面切到别的页面：只有本仪表盘（常驻）收到通知，嵌在内部区域里的调试页收不到 OnNavigatedFrom——
    /// 转告它停掉调试服务器，否则服务器一直开着。
    /// </summary>
    public override void OnNavigatedFrom(NavigationContext navigationContext)
    {
        base.OnNavigatedFrom(navigationContext);
        foreach (var vm in InnerDebugViewModels()) vm.OnHostDeactivated();
    }

    /// <summary>内部区域里当前激活的过程调试页 ViewModel（没有或区域未注册时为空）。</summary>
    private IEnumerable<HalconDebugViewModel> InnerDebugViewModels()
    {
        if (RegionManager == null
            || !RegionManager.Regions.ContainsRegionWithName(HalconNavigationConstants.Regions.HalconContentRegion))
            return [];
        return RegionManager.Regions[HalconNavigationConstants.Regions.HalconContentRegion].ActiveViews
            .OfType<FrameworkElement>()
            .Select(v => v.DataContext)
            .OfType<HalconDebugViewModel>()
            .ToList();
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
