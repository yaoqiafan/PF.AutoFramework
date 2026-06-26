using PF.Core.Attributes;
using Prism.Navigation.Regions;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

/// <summary>
/// 视觉调试根页面：顶部 Tab 条 + 内部 Prism Region（HalconContentRegion）。
/// 区域在导航到此页面时重新注册，防止多次导航导致区域重复。
/// </summary>
[ModuleNavigation(
    HalconNavigationConstants.Views.Dashboard,
    "Halcon视觉调试",
    groupName: "系统调试",
    GroupOrder = 60,
    Order = 1,
    Icon = "VisionIcon")]
public partial class HalconDashboardView : UserControl
{
    public HalconDashboardView(IRegionManager regionManager)
    {
        var regionName = HalconNavigationConstants.Regions.HalconContentRegion;
        if (regionManager.Regions.ContainsRegionWithName(regionName))
            regionManager.Regions.Remove(regionName);

        InitializeComponent();
    }
}
