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
    /// <summary>
    /// 将 DataContext 透传进 HierarchicalDataTemplate 内部绑定（ToggleDeviceSimulationCommand）
    /// </summary>
    /// <summary>绑定代理，用于在 HierarchicalDataTemplate 中传递 DataContext</summary>
    public class BindingProxy : Freezable
    {
        protected override Freezable CreateInstanceCore() => new BindingProxy();

        /// <summary>获取或设置绑定的数据对象</summary>
        public object Data
        {
            get => GetValue(DataProperty);
            set => SetValue(DataProperty, value);
        }

        public static readonly DependencyProperty DataProperty =
            DependencyProperty.Register(nameof(Data), typeof(object), typeof(BindingProxy),
                new UIPropertyMetadata(null));
    }

    [ModuleNavigation(NavigationConstants.Views.HardwareDebugView, "设备综合调试", GroupName = "系统调试", Icon = "DebugIcon", Order = 1, GroupIcon = "/PF.UI.Resources;component/Images/PNG/4.png")]
    public partial class HardwareDebugView : UserControl
    {
        /// <summary>初始化硬件调试视图</summary>
        public HardwareDebugView(IRegionManager regionManager)
        {
            // 2. 【核心修复】在初始化 XAML 之前，检查并移除残留的嵌套 Region
            string regionName = NavigationConstants.Regions.DebugViewRegion;
            if (regionManager.Regions.ContainsRegionWithName(regionName))
            {
                regionManager.Regions.Remove(regionName);
            }

            // 3. 此时再解析 XAML 注册 Region 就不会报重复冲突的错了
            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is HardwareDebugViewModel vm)
            {
                var node = e.NewValue as DebugTreeNode;

                // 1. 更新 ViewModel 的选中节点
                vm.SelectedNode = node;

                // 2. 提取 Payload 并执行导航命令
                if (node?.Payload != null)
                {
                    // 确保命令可以执行
                    if (vm.NavigateToDebugCommand.CanExecute(node.Payload))
                    {
                        vm.NavigateToDebugCommand.Execute(node.Payload);
                    }
                }
            }
        }
    }
}
