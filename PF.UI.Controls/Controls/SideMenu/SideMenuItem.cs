using System;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PF.UI.Shared.Data;
using PF.UI.Shared.Tools.Extension;

namespace PF.UI.Controls;

public class SideMenuItem : HeaderedSimpleItemsControl, ISelectable, ICommandSource
{
    private bool _isMouseLeftButtonDown;

    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(SideMenuItem), new PropertyMetadata(default(object)));

    public object Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public SideMenuItem()
    {
        // 【核心修复】：彻底删掉这里的 SetBinding 代码！
        // 因为我们采用了 Inherits 继承机制，不再需要强行用 Binding 向上找爹了。
    }

    // 【核心修复】：将 AddOwner 的元数据替换为 FrameworkPropertyMetadata 并加上 Inherits
    public static readonly DependencyProperty ExpandModeProperty =
        SideMenu.ExpandModeProperty.AddOwner(typeof(SideMenuItem),
            new FrameworkPropertyMetadata(default(ExpandMode), FrameworkPropertyMetadataOptions.Inherits));

    public ExpandMode ExpandMode
    {
        get => (ExpandMode)GetValue(ExpandModeProperty);
        set => SetValue(ExpandModeProperty, value);
    }

    protected override void Refresh()
    {
        if (ItemsHost == null) return;

        ItemsHost.Children.Clear();
        foreach (var item in Items)
        {
            DependencyObject container;
            if (IsItemItsOwnContainerOverride(item))
            {
                container = item as DependencyObject;
            }
            else
            {
                container = GetContainerForItemOverride();
                PrepareContainerForItemOverride(container, item);
            }

            if (container is FrameworkElement element)
            {
                element.Style = ItemContainerStyle;
                ItemsHost.Children.Add(element);
            }
        }

        if (IsLoaded)
        {
            SwitchPanelArea(ExpandMode == ExpandMode.ShowAll || IsSelected);
        }
    }

    protected virtual void OnSelected(RoutedEventArgs e)
    {
        RaiseEvent(e);

        switch (Command)
        {
            case null:
                return;
            case RoutedCommand command:
                command.Execute(CommandParameter, CommandTarget);
                break;
            default:
                Command.Execute(CommandParameter);
                break;
        }
    }

    public static readonly RoutedEvent SelectedEvent =
        EventManager.RegisterRoutedEvent("Selected", RoutingStrategy.Bubble,
            typeof(RoutedEventHandler), typeof(SideMenuItem));

    public event RoutedEventHandler Selected
    {
        add => AddHandler(SelectedEvent, value);
        remove => RemoveHandler(SelectedEvent, value);
    }

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(SideMenuItem), new PropertyMetadata(ValueBoxes.FalseBox));

    public bool IsSelected
    {
        get => (bool) GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, ValueBoxes.BooleanBox(value));
    }

    public static readonly DependencyProperty RoleProperty = DependencyProperty.Register(
        nameof(Role), typeof(SideMenuItemRole), typeof(SideMenuItem), new PropertyMetadata(default(SideMenuItemRole)));

    public SideMenuItemRole Role
    {
        get => (SideMenuItemRole) GetValue(RoleProperty);
        set => SetValue(RoleProperty, value);
    }

    protected override DependencyObject GetContainerForItemOverride() => new SideMenuItem();

    protected override bool IsItemItsOwnContainerOverride(object item) => item is SideMenuItem;

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);

        _isMouseLeftButtonDown = false;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);

        _isMouseLeftButtonDown = true;
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);

        if (_isMouseLeftButtonDown)
        {
            IsSelected = true;
            OnSelected(new RoutedEventArgs(SelectedEvent, this));
            _isMouseLeftButtonDown = false;
        }
    }

    public void SelectDefaultItem()
    {
        if (Role == SideMenuItemRole.Header && ItemsHost.Children.Count > 0)
        {
            var item = ItemsHost.Children.OfType<SideMenuItem>().FirstOrDefault();
            if (item is { IsSelected: false })
            {
                item.OnSelected(new RoutedEventArgs(SelectedEvent, item));
            }
        }
    }

    public void SwitchPanelArea(bool isShow)
    {
        if (ItemsHost == null) return;
        if (Role == SideMenuItemRole.Header)
        {
            ItemsHost.Show(isShow);
        }
    }

    public static readonly DependencyProperty CommandProperty = DependencyProperty.Register(
        nameof(Command), typeof(ICommand), typeof(SideMenuItem), new PropertyMetadata(default(ICommand), OnCommandChanged));

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (SideMenuItem) d;
        if (e.OldValue is ICommand oldCommand)
        {
            oldCommand.CanExecuteChanged -= ctl.CanExecuteChanged;
        }
        if (e.NewValue is ICommand newCommand)
        {
            newCommand.CanExecuteChanged += ctl.CanExecuteChanged;
        }
    }

    public ICommand Command
    {
        get => (ICommand) GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public static readonly DependencyProperty CommandParameterProperty = DependencyProperty.Register(
        nameof(CommandParameter), typeof(object), typeof(SideMenuItem), new PropertyMetadata(default(object)));

    public object CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public static readonly DependencyProperty CommandTargetProperty = DependencyProperty.Register(
        nameof(CommandTarget), typeof(IInputElement), typeof(SideMenuItem), new PropertyMetadata(default(IInputElement)));

    public IInputElement CommandTarget
    {
        get => (IInputElement) GetValue(CommandTargetProperty);
        set => SetValue(CommandTargetProperty, value);
    }

    private void CanExecuteChanged(object sender, EventArgs e)
    {
        if (Command == null) return;

        IsEnabled = Command is RoutedCommand command
            ? command.CanExecute(CommandParameter, CommandTarget)
            : Command.CanExecute(CommandParameter);
    }
}
