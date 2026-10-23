using PF.Core.Entities.Logging;
using PF.Core.Interfaces.Logging;
using PF.Modules.Logging.ViewModels;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Modules.Logging.Views
{
    /// <summary>日志列表视图</summary>
    public partial class LogListView : UserControl
    {
        // 离顶部多少像素以内视为"在顶部"（列表按时间倒序，顶部即最新）
        private const double TopTolerance = 1.0;

        private ScrollViewer _scrollViewer;
        private LogListViewModel _viewModel;
        private object _anchorItem;
        private double _anchorTop;
        private LogEntry _lastClickedItem;
        private object _lastClickedTime;
        private readonly IContainerProvider _containerProvider;
        private ILogService logService;

        /// <summary>初始化日志列表视图</summary>
        public LogListView(IContainerProvider containerProvider)
        {
            InitializeComponent();
            _containerProvider = containerProvider;
            logService = _containerProvider.Resolve<ILogService>();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;

            // 添加鼠标点击事件（挂在自身子控件上，随视图一起释放，无需在 Unloaded 中移除）
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

        // Loaded / Unloaded 成对处理：视图可能被多次卸载再加载，所有订阅都在这里挂、在 OnUnloaded 里摘
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _scrollViewer = FindVisualChild<ScrollViewer>(LogListViewControl);
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }

            _viewModel = DataContext as LogListViewModel;
            if (_viewModel != null)
            {
                _viewModel.Subscribe();
                _viewModel.LogAppended += OnLogAppended;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;

                if (_viewModel.AutoScroll)
                {
                    ScrollToTop();
                }
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
                _scrollViewer = null;
            }

            if (_viewModel != null)
            {
                _viewModel.LogAppended -= OnLogAppended;
                _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                _viewModel.Unsubscribe();
                _viewModel = null;
            }
        }

        private void OnLogAppended()
        {
            if (_viewModel == null)
                return;

            if (_viewModel.AutoScroll)
            {
                ScrollToTop();
                return;
            }

            // 不跟随时，新日志插在顶部会把正在看的内容往下挤，需要锚定当前视口顶部那一行。
            // 虚拟化下 ExtentHeight 是估算值，不能拿它的增量来补偿，改为：
            // 插入后、布局前（此时已生成的行仍停在旧位置）记下锚点行及其位置，
            // 布局完成后（Loaded 优先级低于布局所在的 Render 优先级）再找到该行，按位移差回滚。
            // 同一批次连续插入多条时只在第一条记录锚点，此时布局尚未发生，位置仍是旧值
            if (_anchorItem != null)
                return;

            if (!TryGetTopVisibleItem(out _anchorItem, out _anchorTop))
                return;

            Dispatcher.InvokeAsync(RestoreAnchor, DispatcherPriority.Loaded);
        }

        private void RestoreAnchor()
        {
            var anchorItem = _anchorItem;
            _anchorItem = null;

            if (_scrollViewer == null || anchorItem == null || _viewModel?.AutoScroll != false)
                return;

            // 锚点行被筛选掉、被挤出上限或已被虚拟化回收时取不到容器，放弃本次补偿
            if (LogListViewControl.ItemContainerGenerator.ContainerFromItem(anchorItem) is not FrameworkElement container)
                return;

            double delta = container.TranslatePoint(new Point(0, 0), _scrollViewer).Y - _anchorTop;
            if (Math.Abs(delta) > 0.5)
            {
                _scrollViewer.ScrollToVerticalOffset(_scrollViewer.VerticalOffset + delta);
            }
        }

        /// <summary>取视口内最上面一行（允许部分可见）及其相对 ScrollViewer 顶部的位置</summary>
        private bool TryGetTopVisibleItem(out object item, out double top)
        {
            item = null;
            top = 0;

            if (_scrollViewer == null)
                return false;

            var panel = FindVisualChild<VirtualizingStackPanel>(LogListViewControl);
            if (panel == null)
                return false;

            // Recycling 模式下 Children 里可能残留已回收（未关联数据项）的容器，且不保证按显示顺序排列，
            // 所以逐个过滤，取仍关联数据项、底边越过视口顶部、位置最靠上的那一个
            double bestTop = double.MaxValue;
            foreach (UIElement child in panel.Children)
            {
                if (child is not FrameworkElement element || element.Visibility != Visibility.Visible)
                    continue;

                var candidate = LogListViewControl.ItemContainerGenerator.ItemFromContainer(element);
                if (candidate == null || candidate == DependencyProperty.UnsetValue)
                    continue;

                double y = element.TranslatePoint(new Point(0, 0), _scrollViewer).Y;
                if (y + element.ActualHeight > 0 && y < bestTop)
                {
                    bestTop = y;
                    item = candidate;
                }
            }

            if (item == null)
                return false;

            top = bestTop;
            return true;
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // 用户手动勾选"自动滚动"时立即回到最新一条
            if (e.PropertyName == nameof(LogListViewModel.AutoScroll) && _viewModel?.AutoScroll == true)
            {
                ScrollToTop();
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            // 只响应滚动偏移的变化；新日志插入只改变 ExtentHeight，不影响跟随状态
            if (_viewModel == null || e.VerticalChange == 0)
                return;

            // 离开顶部即停止跟随，回到顶部即恢复跟随
            bool atTop = e.VerticalOffset <= TopTolerance;
            if (_viewModel.AutoScroll != atTop)
            {
                _viewModel.AutoScroll = atTop;
            }
        }

        private void ScrollToTop()
        {
            _scrollViewer?.ScrollToVerticalOffset(0);
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