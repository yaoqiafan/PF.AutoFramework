using PF.Core.Attributes;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

[ModuleNavigation(
    HalconNavigationConstants.Views.HalconDebug,
    "HALCON调试",
    groupName: "视觉系统",
    GroupOrder = 50,
    Order = 2,
    Icon = "DebugIcon")]
public partial class HalconDebugView : UserControl
{
    public HalconDebugView()
    {
        InitializeComponent();
    }
}
