using PF.Modules.SecsGem.ViewModels;
using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.SecsGem.Views
{
    /// <summary>
    /// SecsGemDebugView 的交互逻辑
    /// </summary>
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
                vm.TransactionLogs.CollectionChanged += OnTransactionLogsChanged;
            }
        }

        private void OnTransactionLogsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (DataContext is not SecsGemDebugViewModel vm || !vm.AutoScrollLog) return;
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
                vm.SelectCommandLeafCommand.Execute(leaf);
                e.Handled = true;
            }
        }
    }
}
