using PF.Common.Data.Logging;
using PF.Common.Interface.Loging;
using PF.Common.Logging.ViewModels;
using Prism.Ioc;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

namespace PF.Common.Logging.Views
{
    public partial class LogListView : UserControl
    {
        private ScrollViewer _scrollViewer;
        private LogEntry _lastClickedItem;
        private object _lastClickedTime;
        private readonly IContainerProvider _containerProvider;
        private ILogService logService;

        public LogListView(IContainerProvider containerProvider)
        {
            InitializeComponent();
            _containerProvider = containerProvider;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // 添加鼠标点击事件
            LogListViewControl.AddHandler(ListViewItem.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnListViewItemMouseDown), true);
        }

        private void OnListViewItemMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 查找点击的 ListViewItem
            var item = FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject);
            if (item != null)
            {
                // 获取点击的日志项
                var logEntry = item.Content as LogEntry;
                if (logEntry != null)
                {
                    // 检查是否为双击（在短时间内点击同一项）
                    var currentTime = DateTime.Now.Ticks;
                    var lastTime = _lastClickedTime as long?;
                    var isDoubleClick = (_lastClickedItem == logEntry &&
                                        lastTime.HasValue &&
                                        (currentTime - lastTime.Value) < TimeSpan.TicksPerSecond / 2); // 500ms内视为双击

                    // 更新最后点击的信息
                    _lastClickedItem = logEntry;
                    _lastClickedTime = currentTime;

                    // 如果是双击，不处理选中逻辑（让双击事件正常处理）
                    if (isDoubleClick)
                    {
                        return;
                    }

                    // 如果是单击，切换选中状态
                    if (logEntry == LogListViewControl.SelectedItem)
                    {
                        // 取消选中
                        LogListViewControl.SelectedItem = null;
                        e.Handled = true; // 阻止事件继续传播
                    }
                    else
                    {
                        // 选中该项
                        LogListViewControl.SelectedItem = logEntry;
                        e.Handled = true; // 阻止事件继续传播
                    }
                }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 查找ScrollViewer
            _scrollViewer = FindVisualChild<ScrollViewer>(LogListViewControl);

            // 订阅滚动事件
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }

            // 订阅数据上下文变化
            DataContextChanged += OnDataContextChanged;

            logService = _containerProvider.Resolve<ILogService>();
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.OldValue is LogListViewModel oldVm)
            {
                oldVm.Unsubscribe();
            }

            if (e.NewValue is LogListViewModel newVm)
            {
                // 延迟滚动到底部
                Dispatcher.InvokeAsync(() =>
                {
                    ScrollToBottom();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (DataContext is LogListViewModel vm && vm.AutoScroll)
            {
                // 如果用户手动滚动，暂时禁用自动滚动
                if (e.VerticalChange > 0 && e.VerticalOffset != e.ExtentHeight)
                {
                    vm.AutoScroll = false;
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // 清理资源
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            }

            if (DataContext is LogListViewModel vm)
            {
                vm.Unsubscribe();
            }

            // 移除事件处理器
            LogListViewControl.RemoveHandler(ListViewItem.PreviewMouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnListViewItemMouseDown));
        }

        private void ScrollToBottom()
        {
            if (LogListViewControl.Items.Count > 0)
            {
                var lastItem = LogListViewControl.Items[LogListViewControl.Items.Count - 1];
                LogListViewControl.ScrollIntoView(lastItem);
            }
        }

        private void LogListViewControl_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LogListViewControl.SelectedItem is LogEntry logEntry)
            {
                string logText = $"[{logEntry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{logEntry.Level}] {logEntry.Message}";
                Clipboard.SetText(logText);
                logService.Info("日志内容已复制到剪贴板", "UI");
            }
        }

        private void LogListViewControl_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.C && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (DataContext is LogListViewModel vm)
                {
                    vm.CopySelectedCommand.Execute(null);
                    e.Handled = true;
                }
            }
        }

        private T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                if (child is T typedChild)
                {
                    return typedChild;
                }

                var childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            return null;
        }

        private T FindAncestor<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T typedObj)
                {
                    return typedObj;
                }
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }

     
    }

   
}