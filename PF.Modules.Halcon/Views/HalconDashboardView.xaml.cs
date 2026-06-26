using PF.Core.Attributes;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

[ModuleNavigation(
    HalconNavigationConstants.Views.Dashboard,
    "Halcon视觉调试",
    groupName: "系统调试",
    GroupOrder = 60,
    Order = 1,
    Icon = "VisionIcon")]
public partial class HalconDashboardView : UserControl
{
    public HalconDashboardView()
    {
        InitializeComponent();
    }
}
