using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Modules.Debug.Models;
using PF.Modules.Debug.ViewModels;
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

namespace PF.Modules.Debug.Views
{
    [ModuleNavigation(NavigationConstants.Views.HardwareDebugView, "设备综合调试", GroupName = "系统调试", Icon = "DebugIcon", Order = 4, GroupIcon = "/PF.UI.Resources;component/Images/PNG/4.png")]
    public partial class HardwareDebugView : UserControl
    {
        public HardwareDebugView() { InitializeComponent(); }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is HardwareDebugViewModel vm)
                vm.SelectedNode = e.NewValue as DebugTreeNode;
        }
    }
}
