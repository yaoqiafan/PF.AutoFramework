using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using PF.UI.Shared.Data;


namespace PF.UI.Controls;

[TemplatePart(Name = ElementSearchBox, Type = typeof(System.Windows.Controls.TextBox))]
public class SearchComboBox : ListBox
{
    private const string ElementSearchBox = "PART_SearchBox";

    private System.Windows.Controls.TextBox _searchBox;

    private bool _isInternalAction;

    public static readonly DependencyProperty MaxDropDownHeightProperty =
        System.Windows.Controls.ComboBox.MaxDropDownHeightProperty.AddOwner(typeof(SearchComboBox),
            new FrameworkPropertyMetadata(SystemParameters.PrimaryScreenHeight / 3));

    [Bindable(true), Category("Layout")]
    [TypeConverter(typeof(LengthConverter))]
    public double MaxDropDownHeight
    {
        get => (double) GetValue(MaxDropDownHeightProperty);
        set => SetValue(MaxDropDownHeightProperty, value);
    }

    public static readonly DependencyProperty IsDropDownOpenProperty = DependencyProperty.Register(
        nameof(IsDropDownOpen), typeof(bool), typeof(SearchComboBox),
        new PropertyMetadata(ValueBoxes.FalseBox, OnIsDropDownOpenChanged));

    private static void OnIsDropDownOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (SearchComboBox) d;

        if (!(bool) e.NewValue)
        {
            ctl.Dispatcher.BeginInvoke(new Action(() =>
            {
                Mouse.Capture(null);
            }), DispatcherPriority.Send);

            ctl._isInternalAction = true;
            ctl.SetCurrentValue(SearchTextProperty, string.Empty);
            ctl._isInternalAction = false;
        }
        else
        {
            ctl.Dispatcher.BeginInvoke(new Action(() =>
            {
                ctl._searchBox?.Focus();
            }), DispatcherPriority.Input);
        }
    }

    public bool IsDropDownOpen
    {
        get => (bool) GetValue(IsDropDownOpenProperty);
        set => SetValue(IsDropDownOpenProperty, ValueBoxes.BooleanBox(value));
    }

    public static readonly DependencyProperty SearchTextProperty = DependencyProperty.Register(
        nameof(SearchText), typeof(string), typeof(SearchComboBox),
        new PropertyMetadata(default(string), OnSearchTextChanged));

    private static void OnSearchTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var ctl = (SearchComboBox) d;
        if (!ctl._isInternalAction)
        {
            ctl.RefreshFilter();
        }
    }

    public string SearchText
    {
        get => (string) GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public static readonly DependencyProperty SelectedDisplayTextProperty = DependencyProperty.Register(
        nameof(SelectedDisplayText), typeof(string), typeof(SearchComboBox),
        new PropertyMetadata(default(string)));

    public string SelectedDisplayText
    {
        get => (string) GetValue(SelectedDisplayTextProperty);
        private set => SetValue(SelectedDisplayTextProperty, value);
    }

    /// <summary>
    ///     自定义搜索委托，接受当前项集合和关键词，返回匹配的项
    /// </summary>
    public Func<IEnumerable<object>, string, IEnumerable<object>> SearchFunc { get; set; }

    public SearchComboBox()
    {
        CommandBindings.Add(new CommandBinding(ControlCommands.Clear, (s, e) =>
        {
            SetCurrentValue(SelectedValueProperty, null);
            SetCurrentValue(SelectedItemProperty, null);
            SetCurrentValue(SelectedIndexProperty, -1);
            SelectedDisplayText = null;
        }));
    }

    public override void OnApplyTemplate()
    {
        if (_searchBox != null)
        {
            _searchBox.TextChanged -= SearchBox_TextChanged;
        }

        base.OnApplyTemplate();

        _searchBox = GetTemplateChild(ElementSearchBox) as System.Windows.Controls.TextBox;

        if (_searchBox != null)
        {
            _searchBox.TextChanged += SearchBox_TextChanged;
        }
    }

    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);

        UpdateSelectedDisplay();

        if (SelectedItem != null)
        {
            SetCurrentValue(IsDropDownOpenProperty, ValueBoxes.FalseBox);
        }
    }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is SearchComboBoxItem;

    protected override DependencyObject GetContainerForItemOverride() => new SearchComboBoxItem();

    protected override void OnDisplayMemberPathChanged(string oldDisplayMemberPath, string newDisplayMemberPath)
    {
        base.OnDisplayMemberPathChanged(oldDisplayMemberPath, newDisplayMemberPath);
        UpdateSelectedDisplay();
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var text = _searchBox?.Text ?? string.Empty;
        _isInternalAction = true;
        SetCurrentValue(SearchTextProperty, text);
        _isInternalAction = false;
        RefreshFilter();
    }

    private void RefreshFilter()
    {
        var searchText = SearchText;

        Predicate<object> filter = null;

        if (!string.IsNullOrEmpty(searchText))
        {
            filter = item =>
            {
                if (SearchFunc != null)
                {
                    var allItems = new List<object>();
                    foreach (var i in Items) allItems.Add(i);
                    foreach (var matched in SearchFunc(allItems, searchText))
                    {
                        if (ReferenceEquals(matched, item) || Equals(matched, item))
                            return true;
                    }
                    return false;
                }

                var displayText = GetItemDisplayText(item);
                return displayText != null &&
                       displayText.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
            };
        }

        if (ItemsSource != null)
        {
            var view = CollectionViewSource.GetDefaultView(ItemsSource);
            view.Filter = filter;
        }
        else
        {
            Items.Filter = filter;
        }
    }

    private string GetItemDisplayText(object item)
    {
        if (item == null) return null;

        if (!string.IsNullOrEmpty(DisplayMemberPath))
        {
            var prop = item.GetType().GetProperty(DisplayMemberPath);
            return prop?.GetValue(item)?.ToString();
        }

        return item is SearchComboBoxItem scbItem ? scbItem.Content?.ToString() : item.ToString();
    }

    private void UpdateSelectedDisplay()
    {
        SelectedDisplayText = GetItemDisplayText(SelectedItem);
    }
}
