using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Modules.SecsGem.ViewModels;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PF.Modules.SecsGem.Views
{
    [ModuleNavigation(NavigationConstants.Views.SecsGemDebugView, "SECS/GEM 调试", GroupName = "系统调试", Icon = "DebugIcon", Order = 4, GroupIcon = "/PF.UI.Resources;component/Images/PNG/4.png")]

    public partial class SecsGemDebugView : UserControl
    {
        public SecsGemDebugView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 订阅日志集合变更以实现自动滚动
            if (DataContext is SecsGemDebugViewModel vm)
            {
                vm.Log.TransactionLogs.CollectionChanged += OnTransactionLogsChanged;
            }
        }

        private void OnTransactionLogsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is not SecsGemDebugViewModel vm || !vm.Log.AutoScrollLog) return;
            if (e.Action != NotifyCollectionChangedAction.Add) return;

            // 自动滚动到底部
            if (LogListBox.Items.Count > 0)
            {
                LogListBox.ScrollIntoView(LogListBox.Items[^1]);
            }
        }

        /// <summary>
        /// 命令树节点点击处理：将叶子节点选中事件传递给 ViewModel
        /// </summary>
        private void OnCommandTreeItemClicked(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is not SecsGemDebugViewModel vm) return;

            // 获取点击的 TreeViewItem 数据上下文
            if (sender is TreeViewItem item && item.DataContext is CommandLeafViewModel leaf)
            {
                vm.CommandBuilder.SelectCommandLeafCommand.Execute(leaf);
                e.Handled = true;
            }
        }



        /// <summary>
        /// 树视图鼠标右键按下事件处理程序
        /// 当在TreeView上按下鼠标右键时，查找并选中鼠标位置下的TreeViewItem
        /// 这样可以确保右键点击时正确的TreeViewItem被选中，以便显示正确的上下文菜单
        /// </summary>
        /// <param name="sender">事件源，TreeView控件</param>
        /// <param name="e">鼠标事件参数，包含鼠标位置、按钮状态等信息</param>
        private void TreeView_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 查找鼠标位置下的TreeViewItem
            TreeViewItem? treeViewItem = FindAncestor<TreeViewItem>((DependencyObject)e.OriginalSource);

            if (treeViewItem != null)
            {
                // 将找到的TreeViewItem设置为选中状态
                treeViewItem.IsSelected = true;

                // 标记事件已处理，防止事件继续冒泡
                e.Handled = true;
            }
        }

        /// <summary>
        /// 在可视化树中查找指定类型的祖先元素
        /// 通过遍历可视化树向上查找，直到找到指定类型的元素或到达根元素
        /// </summary>
        /// <typeparam name="T">要查找的依赖对象类型</typeparam>
        /// <param name="current">起始的依赖对象，通常是事件源</param>
        /// <returns>找到的指定类型的祖先元素，如果未找到则返回null</returns>
        private T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            // 循环向上遍历可视化树
            while (current != null)
            {
                // 检查当前元素是否为指定类型
                if (current is T ancestor)
                    return ancestor;

                // 获取当前元素的可视化父元素，继续向上查找
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }
    }
}
