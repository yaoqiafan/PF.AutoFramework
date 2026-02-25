using PF.Core.Attributes;
using PF.Core.Constants;
using System.Windows.Controls;

namespace PF.Modules.HardwareDebug.Views
{
    [ModuleNavigation(NavigationConstants.Views.AxisDebugView, "轴调试",
        GroupName = "硬件调试",
        GroupOrder = 10,
        GroupIcon = "/PF.UI.Resources;component/Images/PNG/6.png",
        Icon = "SettingIcon",
        Order = 1)]
    public partial class AxisDebugView : UserControl
    {
        public AxisDebugView()
        {
            InitializeComponent();
        }
    }
}
