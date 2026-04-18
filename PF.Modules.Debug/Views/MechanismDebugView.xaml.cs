using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Modules.Debug.Models;
using PF.Modules.Debug.ViewModels;
using Prism.Navigation.Regions;
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
    // 注意这里：单独的侧边栏按钮“业务模组调试”
    [ModuleNavigation(NavigationConstants.Views.MechanismDebugView, "业务模组调试", GroupName = "系统调试", Icon = "DebugIcon" ,Order =2)]
    public partial class MechanismDebugView : UserControl
    {
        /// <summary>初始化模组调试视图</summary>
        public MechanismDebugView(IRegionManager regionManager)
        {
            // 2. 【核心修复】在初始化 XAML 之前，检查并移除残留的嵌套 Region
            string regionName = NavigationConstants.Regions.MechanismContentRegion;
            if (regionManager.Regions.ContainsRegionWithName(regionName))
            {
                regionManager.Regions.Remove(regionName);
            }

            // 3. 此时再解析 XAML 注册 Region 就不会报重复冲突的错了

            InitializeComponent(); 
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MechanismDebugViewModel vm)
                vm.SelectedNode = e.NewValue as DebugTreeNode;
        }
    }
}
