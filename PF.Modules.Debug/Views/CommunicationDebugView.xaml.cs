using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Modules.Debug.Models;
using PF.Modules.Debug.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Debug.Views
{
    [ModuleNavigation(NavigationConstants.Views.CommunicationDebugView, "通讯综合调试", GroupName = "系统调试", Icon = "Lan", Order = 0)]
    public partial class CommunicationDebugView : UserControl
    {
        /// <summary>初始化通讯调试视图</summary>
        public CommunicationDebugView(IRegionManager regionManager)
        {
            // 在初始化 XAML 之前移除残留的嵌套 Region，避免重复注册冲突（同 HardwareDebugView 的既有做法）
            string regionName = NavigationConstants.Regions.CommunicationViewRegion;
            if (regionManager.Regions.ContainsRegionWithName(regionName))
            {
                regionManager.Regions.Remove(regionName);
            }

            InitializeComponent();
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is CommunicationDebugViewModel vm)
            {
                var node = e.NewValue as DebugTreeNode;
                vm.SelectedNode = node;

                if (node?.Payload != null && vm.NavigateToDebugCommand.CanExecute(node.Payload))
                {
                    vm.NavigateToDebugCommand.Execute(node.Payload);
                }
            }
        }
    }
}
