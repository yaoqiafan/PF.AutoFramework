using PF.Core.Attributes;
using PF.Core.Constants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PF.Modules.Parameter.Views
{

    [ModuleNavigation(NavigationConstants.Views.ParameterView_SystemConfigParam, "运行参数",
        GroupName = "设备参数设置", GroupOrder = 2, Order = 1,
        GroupIcon = "/PF.UI.Resources;component/Images/PNG/6.png", // 父节点 PNG 图标
        Icon = "SettingIcon",                                      // 子节点矢量图
        NavigationParameter = "SystemConfigParam")]                // 传递的参数名

    [ModuleNavigation(NavigationConstants.Views.ParameterView_CommonParam, "公共参数",
        GroupName = "设备参数设置", Order = 2,
        Icon = "AudioGeometry",
        NavigationParameter = "CommonParam")]

    [ModuleNavigation(NavigationConstants.Views.ParameterView_HardWareParam, "设备硬件参数",
        GroupName = "设备参数设置", Order = 3,
        Icon = "CurveIcon",
        NavigationParameter = "HardWareParam")]


    [ModuleNavigation(NavigationConstants.Views.ParameterView_UserLoginParam, "权限管理",
        GroupName = "权限管控", Order = 2,
        Icon = "NailGeometry",
        NavigationParameter = "UserLoginParam")]
    public partial class ParameterView : UserControl
    {
        public ParameterView()
        {
            InitializeComponent();
        }
    }
}
