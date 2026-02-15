using PF.UI.Shared.Data;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PF.UI.Controls
{
    public class SideMenu : HeaderedSimpleItemsControl
    {
        private SideMenuItem _selectedItem;
        private SideMenuItem _selectedHeader;
        private bool _isItemSelected;

        // =========================================================================
        // 新增：SelectedItem 依赖属性，支持双向绑定
        // =========================================================================
        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            nameof(SelectedItem), typeof(object), typeof(SideMenu),
            new FrameworkPropertyMetadata(default(object), FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedItemChanged));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        private static void OnSelectedItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (SideMenu)d;
            if (e.NewValue == null)
            {
                ctl.ClearAllSelection();          // 外部设为 null 时清除所有选中
            }
            else
            {
                ctl.SyncUISelection(e.NewValue);  // 外部设置非空值时同步 UI 选中状态
            }
        }

        // 清除所有选中项（包括 Header 和 Item）
        private void ClearAllSelection()
        {
            if (_selectedItem != null)
            {
                _selectedItem.IsSelected = false;
                _selectedItem = null;
            }
            if (_selectedHeader != null)
            {
                _selectedHeader.IsSelected = false;
                _selectedHeader = null;
            }
            _isItemSelected = false;
        }

        // 根据外部传入的数据对象，在视觉树中查找对应的 SideMenuItem 并设为选中
        private void SyncUISelection(object data)
        {
            if (data == null || ItemsHost == null) return;

            var target = FindSideMenuItemByData(ItemsHost.Children, data);
            if (target != null)
            {
                ClearAllSelection();               // 先取消当前选中
                target.IsSelected = true;           // 设置新选中（会触发 SideMenuItem 内部的面板展开等逻辑）
                if (target.Role == SideMenuItemRole.Item)
                    _selectedItem = target;
                else
                    _selectedHeader = target;
            }
        }

        // 递归查找 DataContext 或自身与指定数据匹配的 SideMenuItem
        private SideMenuItem FindSideMenuItemByData(UIElementCollection container, object data)
        {
            if (container == null) return null;
            foreach (var child in container.OfType<SideMenuItem>())
            {
                if (child.DataContext == data || child == data)
                    return child;

                if (child.ItemsHost != null)
                {
                    var found = FindSideMenuItemByData(child.ItemsHost.Children, data);
                    if (found != null)
                        return found;
                }
            }
            return null;
        }
        // =========================================================================

        public SideMenu()
        {
            AddHandler(SideMenuItem.SelectedEvent, new RoutedEventHandler(SideMenuItemSelected));
            Loaded += (s, e) => Init();
        }

        protected override void Refresh()
        {
            base.Refresh();
            Init();
        }

        private void Init()
        {
            if (ItemsHost == null) return;
            OnExpandModeChanged(ExpandMode);
        }

        // -------------------------------------------------------------------------
        // 核心：菜单项点击处理（保留原完整逻辑，仅增加 SelectedItem 更新）
        // -------------------------------------------------------------------------
        private void SideMenuItemSelected(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is SideMenuItem item)
            {
                object itemData = item.DataContext ?? item;   // 获取项对应的业务数据

                if (item.Role == SideMenuItemRole.Item)       // 叶子项选中
                {
                    _isItemSelected = true;

                    // 如果当前项已是选中项且 SelectedItem 不为 null（未被外部重置），则忽略重复点击
                    if (Equals(item, _selectedItem) && SelectedItem != null)
                        return;

                    if (_selectedItem != null)
                        _selectedItem.IsSelected = false;

                    _selectedItem = item;
                    _selectedItem.IsSelected = true;

                    // 【新增】更新绑定属性，通知 ViewModel
                    SelectedItem = itemData;

                    RaiseSelectionChanged(e.OriginalSource);
                }
                else                                            // Header 项选中
                {
                    // ---------- 以下为原有 Header 处理逻辑，仅添加了无子项时的 SelectedItem 赋值 ----------
                    if (!Equals(item, _selectedHeader))
                    {
                        if (_selectedHeader != null)
                        {
                            if (ExpandMode == ExpandMode.Freedom && item.ItemsHost != null && item.ItemsHost.IsVisible && !_isItemSelected)
                            {
                                item.IsSelected = false;
                                SwitchPanelArea(item);
                                return;
                            }

                            _selectedHeader.IsSelected = false;
                            if (ExpandMode != ExpandMode.Freedom)
                            {
                                SwitchPanelArea(_selectedHeader);
                            }
                        }

                        _selectedHeader = item;
                        _selectedHeader.IsSelected = true;
                        SwitchPanelArea(_selectedHeader);
                    }
                    else if (ExpandMode == ExpandMode.Freedom && !_isItemSelected)
                    {
                        _selectedHeader.IsSelected = false;
                        SwitchPanelArea(_selectedHeader);
                        _selectedHeader = null;
                    }

                    if (_isItemSelected)
                    {
                        _isItemSelected = false;
                    }
                    else if (_selectedHeader != null)
                    {
                        if (AutoSelect)
                        {
                            if (_selectedItem != null)
                            {
                                _selectedItem.IsSelected = false;
                                _selectedItem = null;
                            }
                            _selectedHeader.SelectDefaultItem();  // 自动选择默认子项，会触发子项的 SelectedEvent
                        }
                        _isItemSelected = false;
                    }

                    // 【新增】如果 Header 本身没有子项，则将其视为可选中项，更新 SelectedItem
                    if (!item.HasItems)
                    {
                        SelectedItem = itemData;
                        RaiseSelectionChanged(e.OriginalSource);
                    }
                    // ---------- Header 处理结束 ----------
                }
            }
        }

        private void RaiseSelectionChanged(object info)
        {
            RaiseEvent(new FunctionEventArgs<object>(SelectionChangedEvent, this)
            {
                Info = info
            });
        }

        // -------------------------------------------------------------------------
        // 面板切换辅助方法
        // -------------------------------------------------------------------------
        private void SwitchPanelArea(SideMenuItem oldItem)
        {
            switch (ExpandMode)
            {
                case ExpandMode.ShowAll:
                    return;
                case ExpandMode.ShowOne:
                case ExpandMode.Freedom:
                case ExpandMode.Accordion:
                    oldItem.SwitchPanelArea(oldItem.IsSelected);
                    break;
            }
        }

        // -------------------------------------------------------------------------
        // 容器生成与识别
        // -------------------------------------------------------------------------
        protected override DependencyObject GetContainerForItemOverride() => new SideMenuItem();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is SideMenuItem;

        // -------------------------------------------------------------------------
        // 依赖属性：AutoSelect, ExpandMode, PanelAreaLength
        // -------------------------------------------------------------------------
        public static readonly DependencyProperty AutoSelectProperty = DependencyProperty.Register(
            nameof(AutoSelect), typeof(bool), typeof(SideMenu), new PropertyMetadata(ValueBoxes.TrueBox));

        public bool AutoSelect
        {
            get => (bool)GetValue(AutoSelectProperty);
            set => SetValue(AutoSelectProperty, ValueBoxes.BooleanBox(value));
        }

        public static readonly DependencyProperty ExpandModeProperty = DependencyProperty.Register(
     nameof(ExpandMode), typeof(ExpandMode), typeof(SideMenu),
     new FrameworkPropertyMetadata(
         default(ExpandMode),
         FrameworkPropertyMetadataOptions.Inherits, // 核心：允许属性值向下级元素继承
         OnExpandModeChanged));

        public ExpandMode ExpandMode
        {
            get => (ExpandMode)GetValue(ExpandModeProperty);
            set => SetValue(ExpandModeProperty, value);
        }

        private static void OnExpandModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctl = (SideMenu)d;
            if (ctl.ItemsHost == null) return;
            ctl.OnExpandModeChanged((ExpandMode)e.NewValue);
        }

        // 【保留原有复杂逻辑】当 ExpandMode 变为 ShowOne 时，确保只有一个顶级项展开，并同步内部选中状态
        private void OnExpandModeChanged(ExpandMode mode)
        {
            if (mode == ExpandMode.ShowAll)
            {
                ShowAll();
            }
            else if (mode == ExpandMode.ShowOne)
            {
                SideMenuItem sideMenuItemSelected = null;
                foreach (var sideMenuItem in ItemsHost.Children.OfType<SideMenuItem>())
                {
                    if (sideMenuItemSelected != null)
                    {
                        sideMenuItem.IsSelected = false;
                        if (sideMenuItem.ItemsHost != null)
                        {
                            foreach (var sub in sideMenuItem.ItemsHost.Children.OfType<SideMenuItem>())
                                sub.IsSelected = false;
                        }
                    }
                    else if (sideMenuItem.IsSelected)
                    {
                        switch (sideMenuItem.Role)
                        {
                            case SideMenuItemRole.Header:
                                _selectedHeader = sideMenuItem;
                                break;
                            case SideMenuItemRole.Item:
                                _selectedItem = sideMenuItem;
                                break;
                        }
                        ShowSelectedOne(sideMenuItem);
                        sideMenuItemSelected = sideMenuItem;

                        if (sideMenuItem.ItemsHost != null)
                        {
                            foreach (var sub in sideMenuItem.ItemsHost.Children.OfType<SideMenuItem>())
                            {
                                if (_selectedItem != null)
                                    sub.IsSelected = false;
                                else if (sub.IsSelected)
                                    _selectedItem = sub;
                            }
                        }
                    }
                }
            }
        }

        private void ShowAll()
        {
            foreach (var item in ItemsHost.Children.OfType<SideMenuItem>())
                item.SwitchPanelArea(true);
        }

        private void ShowSelectedOne(SideMenuItem item)
        {
            foreach (var other in ItemsHost.Children.OfType<SideMenuItem>())
                other.SwitchPanelArea(Equals(other, item));
        }

        public static readonly DependencyProperty PanelAreaLengthProperty = DependencyProperty.Register(
            nameof(PanelAreaLength), typeof(double), typeof(SideMenu), new PropertyMetadata(double.NaN));

        public double PanelAreaLength
        {
            get => (double)GetValue(PanelAreaLengthProperty);
            set => SetValue(PanelAreaLengthProperty, value);
        }

        // -------------------------------------------------------------------------
        // 路由事件：SelectionChanged
        // -------------------------------------------------------------------------
        public static readonly RoutedEvent SelectionChangedEvent = EventManager.RegisterRoutedEvent(
            "SelectionChanged", RoutingStrategy.Bubble, typeof(EventHandler<FunctionEventArgs<object>>), typeof(SideMenu));

        public event EventHandler<FunctionEventArgs<object>> SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }



        protected override void OnItemsSourceChanged(IEnumerable oldValue, IEnumerable newValue)
        {
            base.OnItemsSourceChanged(oldValue, newValue);
            _selectedHeader = null;
            _selectedItem = null;
            _isItemSelected = false;
        }  
        
        protected override void OnItemsChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(sender, e);

            // 当触发清空重置（如注销时清理菜单），清空内部状态指针
            if (e.Action == NotifyCollectionChangedAction.Reset ||
                e.Action == NotifyCollectionChangedAction.Remove)
            {
                _selectedHeader = null;
                _selectedItem = null;
                _isItemSelected = false;

                // 如果有 SelectedItem 依赖属性，也将其置空
                if (SelectedItem != null)
                {
                    SetCurrentValue(SelectedItemProperty, null);
                }
            }
        }

        // 在 SideMenu.cs 中重写 UpdateItems 方法
        protected override void UpdateItems()
        {
            base.UpdateItems();

            // 清空缓存防死锁
            _selectedHeader = null;
            _selectedItem = null;
            _isItemSelected = false;

            // 使用 DispatcherPriority.Loaded 延迟执行
            // 确保这段代码在 WPF 把 SideMenuItem 完全生成并挂载到界面后才运行
            this.Dispatcher.InvokeAsync(() =>
            {
                if (this.ItemsHost != null)
                {
                    OnExpandModeChanged(this.ExpandMode);
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }
}