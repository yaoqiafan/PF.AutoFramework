using PF.Core.Constants;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.UI.Shared.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;

namespace PF.UI.Infrastructure.Operation
{
    /// <summary>
    /// 界面操作日志全局拦截器：一次性挂载路由事件类处理器，只处理挂了 <see cref="OperationLog.KeyProperty"/>
    /// 标记的控件——未标记的控件不产生任何日志与反射开销。覆盖点击类操作（按钮/菜单/开关）与取值变化类操作
    /// （下拉选择/表格改值/文本框编辑），统一写入 <see cref="ILogService"/> 的 <see cref="LogCategories.UI"/> 分类，
    /// 是否真正记录由 <see cref="OperationLogKeyRegistry"/> 的默认值（关键操作默认启用，非关键默认不启用）
    /// 和 <see cref="OperationLogConfigStore"/> 的项目级 Excel 配置（一旦存在即以工程师改的为准）共同决定。
    /// </summary>
    public static class OperationLogInterceptor
    {
        private static bool _attached;
        private static IOperationLogSettings _settings;
        private static ILogService _logService;
        private static IUserService _userService;
        private static OperationLogConfigStore _configStore;

        /// <summary>文本框编辑前的值快照，仅内部使用</summary>
        private static readonly DependencyProperty TextSnapshotProperty =
            DependencyProperty.RegisterAttached("OperationLogTextSnapshot", typeof(string), typeof(OperationLogInterceptor));

        /// <summary>标记某个 DataGrid 实例是否已经订阅过 CellEditEnding，防止 Loaded 多次触发重复订阅</summary>
        private static readonly DependencyProperty DataGridWiredProperty =
            DependencyProperty.RegisterAttached("OperationLogDataGridWired", typeof(bool), typeof(OperationLogInterceptor));

        /// <summary>DataGrid 进入单元格编辑那一刻的旧值快照，仅内部使用</summary>
        private static readonly DependencyProperty CellEditSnapshotProperty =
            DependencyProperty.RegisterAttached("OperationLogCellEditSnapshot", typeof(object), typeof(OperationLogInterceptor));

        /// <summary>标记某个 SideMenu 实例是否已经订阅过 SelectionChanged，防止 Loaded 多次触发重复订阅</summary>
        private static readonly DependencyProperty SideMenuWiredProperty =
            DependencyProperty.RegisterAttached("OperationLogSideMenuWired", typeof(bool), typeof(OperationLogInterceptor));

        /// <summary>挂载全局路由事件类处理器，只应调用一次（重复调用自动忽略）</summary>
        public static void Attach()
        {
            if (_attached) return;
            _attached = true;

            EventManager.RegisterClassHandler(typeof(ButtonBase), ButtonBase.ClickEvent, new RoutedEventHandler(OnClick), true);
            EventManager.RegisterClassHandler(typeof(MenuItem), MenuItem.ClickEvent, new RoutedEventHandler(OnClick), true);
            EventManager.RegisterClassHandler(typeof(Selector), Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnSelectionChanged), true);
            EventManager.RegisterClassHandler(typeof(TextBoxBase), UIElement.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnTextGotFocus), true);
            EventManager.RegisterClassHandler(typeof(TextBoxBase), UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnTextLostFocus), true);
            // DataGrid.CellEditEnding 不是 RoutedEvent，RegisterClassHandler 拦不到；
            // 借用 Loaded（真正的 RoutedEvent）在每个 DataGrid 实例第一次加载时，对它订阅一次 CLR 事件。
            EventManager.RegisterClassHandler(typeof(DataGrid), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnDataGridLoaded), true);
            // pf:SideMenu 不是 Selector 派生，SelectionChanged 也是 CLR 事件——同样走 Loaded 订阅一次。
            // SideMenu 用全限定名：PF.UI.Controls 里还有同名的 TextBox/ComboBox/Window，using 整个命名空间会和
            // System.Windows.Controls 打架（CS0104）。
            EventManager.RegisterClassHandler(typeof(PF.UI.Controls.SideMenu), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnSideMenuLoaded), true);
            // 页面根（View/对话框的 UserControl、主窗口的 Window）加载时整页预写配置：
            // 注册表登记了该页全部 Key，不用等工程师把每个控件点一遍就能在 Excel 里看到完整清单。
            EventManager.RegisterClassHandler(typeof(UserControl), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnPageRootLoaded), true);
            EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent, new RoutedEventHandler(OnPageRootLoaded), true);
        }

        /// <summary>公共前置过滤：未标记控件、未挂页面名、总开关关闭、或项目级配置关闭时都不记录</summary>
        private static bool TryBegin(DependencyObject source, out string description)
        {
            var key = source == null ? null : OperationLog.GetKey(source);
            var pageName = source == null ? null : OperationLog.GetPageName(source);
            return TryBegin(key, pageName, out description);
        }

        /// <summary>
        /// Key 和页面名分别来自不同元素时用这个重载——DataGrid 改值场景：Key 挂在列上（不在可视树里，
        /// 拿不到继承的页面名），页面名要从 DataGrid 本身（在可视树里）取。
        /// </summary>
        private static bool TryBegin(string key, string pageName, out string description)
        {
            description = null;

            if (string.IsNullOrEmpty(key)) return false;
            if (string.IsNullOrEmpty(pageName)) return false;

            _settings ??= ContainerLocator.Container.Resolve<IOperationLogSettings>();
            if (!_settings.EnableOperationLog) return false;

            var registered = OperationLogKeyRegistry.TryGetEntry(pageName, key, out var registeredDescription, out var critical);
            var defaultDescription = registered ? registeredDescription : string.Empty;
            // 关键操作默认启用；非关键（或压根没登记）默认不启用，需工程师在项目级 Excel 配置里手动打开。
            var defaultEnabled = registered && critical;

            _configStore ??= new OperationLogConfigStore();
            var entry = _configStore.GetOrCreate(pageName, key, defaultDescription, defaultEnabled);
            if (!entry.Enabled) return false;

            description = entry.Description;

            _logService ??= ContainerLocator.Container.Resolve<ILogService>();
            _userService ??= ContainerLocator.Container.Resolve<IUserService>();
            return true;
        }

        private static string CurrentUserName => _userService?.CurrentUser?.UserName ?? "未知用户";

        private static void LogAction(string description) =>
            _logService.Info($"[{CurrentUserName}] {description}", LogCategories.UI);

        private static void LogValueChange(string description, object oldValue, object newValue) =>
            _logService.Info($"[{CurrentUserName}] {description}：{oldValue} → {newValue}", LogCategories.UI);

        private static void OnClick(object sender, RoutedEventArgs e)
        {
            if (!TryBegin(sender as DependencyObject, out var description)) return;

            // DataGrid 模板列里的按钮/勾选框：把所在行的标识附加到日志，否则"删除/授权"只知道动作不知道对象。
            var rowTarget = FindDataGridRowDescription(sender as DependencyObject);
            if (rowTarget == null) LogAction(description);
            else _logService.Info($"[{CurrentUserName}] {description}：{rowTarget}", LogCategories.UI);
        }

        /// <summary>
        /// 沿可视树向上找 <see cref="DataGridRow"/>，从行数据里取第一个命中的常见名称属性
        /// （Description/Title/Name/ViewName/KeyName/Code/Text）当行标识；取不到返回 null。
        /// </summary>
        private static string FindDataGridRowDescription(DependencyObject element)
        {
            var node = element;
            while (node != null && node is not DataGridRow)
                node = System.Windows.Media.VisualTreeHelper.GetParent(node);
            if (node is not DataGridRow row || row.Item == null) return null;

            if (row.Item is string s) return s;
            foreach (var propName in new[] { "Description", "Title", "Name", "ViewName", "KeyName", "Code", "Text" })
            {
                if (row.Item.GetType().GetProperty(propName)?.GetValue(row.Item) is string value && !string.IsNullOrEmpty(value))
                    return value;
            }
            return null;
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!TryBegin(sender as DependencyObject, out var description)) return;
            var oldValue = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : null;
            var newValue = e.AddedItems.Count > 0 ? e.AddedItems[0] : null;
            LogValueChange(description, oldValue, newValue);
        }

        private static void OnDataGridLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not DataGrid grid) return;
            if ((bool)grid.GetValue(DataGridWiredProperty)) return;

            grid.SetValue(DataGridWiredProperty, true);
            grid.PreparingCellForEdit += OnDataGridPreparingCellForEdit;
            grid.CellEditEnding += OnDataGridCellEditEnding;
        }

        /// <summary>进入编辑那一刻把编辑元素里的当前值存为旧值快照——模板列没有绑定路径可反射，这是唯一可靠的旧值来源。</summary>
        private static void OnDataGridPreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
        {
            if (sender is DataGrid grid)
                grid.SetValue(CellEditSnapshotProperty, GetEditValue(e.EditingElement));
        }

        private static void OnSideMenuLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is not PF.UI.Controls.SideMenu menu) return;
            if ((bool)menu.GetValue(SideMenuWiredProperty)) return;

            menu.SetValue(SideMenuWiredProperty, true);
            menu.SelectionChanged += OnSideMenuSelectionChanged;
        }

        /// <summary>
        /// 侧边栏导航切换。<see cref="PF.UI.Controls.SideMenu.SelectionChanged"/> 的参数只带新选中项（Info），
        /// 没有旧值，所以记成"动作 + 目标页面"。注意 Info 给的是 <see cref="PF.UI.Controls.SideMenuItem"/>
        /// 容器而非数据项，要从容器的 DataContext 里取 <see cref="Navigation.NavigationItem"/>；两种形态都兜住。
        /// </summary>
        private static void OnSideMenuSelectionChanged(object sender, FunctionEventArgs<object> e)
        {
            if (!TryBegin(sender as DependencyObject, out var description)) return;

            var title = DescribeMenuItem(e.Info)
                ?? DescribeMenuItem((sender as PF.UI.Controls.SideMenu)?.SelectedItem);
            if (title == null) LogAction(description);
            else _logService.Info($"[{CurrentUserName}] {description}：{title}", LogCategories.UI);
        }

        /// <summary>把菜单选中项解析成显示名：直接是数据项、还是包着一层 SideMenuItem 容器（取 DataContext）都支持；解析不了返回 null。</summary>
        private static string DescribeMenuItem(object item) => item switch
        {
            Navigation.NavigationItem nav => nav.Title,
            FrameworkElement fe when fe.DataContext is Navigation.NavigationItem dc => dc.Title,
            string s => s,
            _ => null
        };

        /// <summary>
        /// 页面根第一次加载时，把注册表里该页登记的全部键按默认值整页预写进 Excel。已存在的行不动
        /// （工程师改过的优先），一页只落一次盘。刻意不看总开关——配置清单是功能的"物料表"，
        /// 工程师可能正想在启用功能前先把各键配置好。
        /// </summary>
        private static void OnPageRootLoaded(object sender, RoutedEventArgs e)
        {
            var pageName = OperationLog.GetPageName(sender as DependencyObject);
            if (string.IsNullOrEmpty(pageName)) return;

            var entries = OperationLogKeyRegistry.GetPageEntries(pageName);
            if (entries.Count == 0) return;

            _configStore ??= new OperationLogConfigStore();
            _configStore.EnsurePage(pageName, entries.Select(en => (en.Key, en.Description, en.Critical)));
        }

        /// <summary>Key 挂在具体列上（DataGridColumn 不在可视树里，但附加属性照样能读写）；页面名从 DataGrid 本身继承拿。</summary>
        private static void OnDataGridCellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction != DataGridEditAction.Commit) return;
            if (sender is not DataGrid grid) return;

            var columnKey = OperationLog.GetKey(e.Column);
            if (!TryBegin(columnKey, OperationLog.GetPageName(grid), out var description)) return;

            // 旧值优先用 PreparingCellForEdit 存的快照（对模板列也有效）；快照缺失时退回绑定路径反射。
            var oldValue = grid.GetValue(CellEditSnapshotProperty);
            if (oldValue == null &&
                e.Column is DataGridBoundColumn boundColumn &&
                boundColumn.Binding is Binding binding &&
                !string.IsNullOrEmpty(binding.Path?.Path))
            {
                oldValue = e.Row?.Item?.GetType().GetProperty(binding.Path.Path)?.GetValue(e.Row.Item);
            }

            LogValueChange(description, oldValue, GetEditValue(e.EditingElement));
        }

        /// <summary>从单元格编辑元素里取当前值。可编辑 ComboBox（Text 绑定）SelectedItem 为空，要退回 Text。</summary>
        private static object GetEditValue(FrameworkElement editor) => editor switch
        {
            TextBox tb => tb.Text,
            ComboBox cb => cb.SelectedItem ?? (object)cb.Text,
            CheckBox chk => chk.IsChecked,
            _ => null
        };

        private static void OnTextGotFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not TextBoxBase tb) return;
            if (string.IsNullOrEmpty(OperationLog.GetKey(tb))) return;
            tb.SetValue(TextSnapshotProperty, GetText(tb));
        }

        private static void OnTextLostFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (!TryBegin(sender as DependencyObject, out var description)) return;

            var tb = (TextBoxBase)sender;
            var oldValue = (string)tb.GetValue(TextSnapshotProperty);
            var newValue = GetText(tb);
            if (oldValue == newValue) return;

            LogValueChange(description, oldValue, newValue);
        }

        private static string GetText(TextBoxBase tb) => tb switch
        {
            TextBox t => t.Text,
            RichTextBox r => new TextRange(r.Document.ContentStart, r.Document.ContentEnd).Text,
            _ => null
        };
    }
}
