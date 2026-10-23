using PF.Core.Attributes;
using PF.Core.Constants;
using System.Windows.Controls;

namespace PF.Modules.Identity.Views
{
    /// <summary>异常权限配置视图：按权限等级配置报警码的手动清除/复位等级</summary>
    [ModuleNavigation(NavigationConstants.Views.AlarmPermissionView, "异常权限配置",
        GroupName = "权限管控", Order = 4,
        Icon = "ShieldAlertOutline")]
    public partial class AlarmPermissionView : UserControl
    {
        /// <summary>初始化异常权限配置视图</summary>
        public AlarmPermissionView()
        {
            InitializeComponent();
        }
    }
}
