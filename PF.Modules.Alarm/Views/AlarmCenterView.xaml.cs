using PF.Core.Attributes;
using System.Windows.Controls;

namespace PF.Modules.Alarm.Views
{
    /// <summary>
    /// 报警中心视图。
    /// [ModuleNavigation] 特性使其自动挂载到左侧导航菜单的"系统监控"分组。
    /// </summary>
    [ModuleNavigation(
        NavigationConstantsAlarm.AlarmCenterView,
        "异常中心",
        groupName: "系统监控",
        GroupOrder = 80,
        Order = 10,
        Icon = "Bell")]
    public partial class AlarmCenterView : UserControl
    {
        public AlarmCenterView()
        {
            InitializeComponent();
        }
    }
}
