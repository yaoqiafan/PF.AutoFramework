using PF.Core.Attributes;
using Prism.Navigation.Regions;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

/// <summary>
/// 视觉仪表盘视图：左侧过程列表 + 右侧 Region（承载 ProcedureDebugView）。
/// [ModuleNavigation] 使其自动注册到侧边栏菜单。
/// </summary>
[ModuleNavigation(
    HalconNavigationConstants.Views.Dashboard,
    "视觉调试",
    groupName: "视觉系统",
    GroupOrder = 50,
    Order = 1,
    Icon = "VisionIcon")]
public partial class HalconDashboardView : UserControl
{
    public HalconDashboardView(IRegionManager regionManager)
    {
        // 防止 Region 重复注册（页面重新导航时）
        var regionName = HalconNavigationConstants.Regions.HalconContentRegion;
        if (regionManager.Regions.ContainsRegionWithName(regionName))
            regionManager.Regions.Remove(regionName);

        InitializeComponent();
    }
}
