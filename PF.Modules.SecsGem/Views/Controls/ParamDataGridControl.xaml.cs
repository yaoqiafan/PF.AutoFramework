using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PF.Modules.SecsGem.Views.Controls
{
    public partial class ParamDataGridControl : UserControl
    {
        public ParamDataGridControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        // ── ItemsSource ────────────────────────────────────────────────────────
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(object),
                typeof(ParamDataGridControl), new PropertyMetadata(null));

        public object ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // ── SelectedItem ───────────────────────────────────────────────────────
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object),
                typeof(ParamDataGridControl),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        // ── AddCommand ─────────────────────────────────────────────────────────
        public static readonly DependencyProperty AddCommandProperty =
            DependencyProperty.Register(nameof(AddCommand), typeof(ICommand),
                typeof(ParamDataGridControl), new PropertyMetadata(null));

        public ICommand AddCommand
        {
            get => (ICommand)GetValue(AddCommandProperty);
            set => SetValue(AddCommandProperty, value);
        }

        // ── DeleteCommand ──────────────────────────────────────────────────────
        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand),
                typeof(ParamDataGridControl), new PropertyMetadata(null));

        public ICommand DeleteCommand
        {
            get => (ICommand)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        // ── ExtraColumns (CLR collection, populated by XAML content) ──────────
        /// <summary>
        /// 在 Code/Description 之后、Comment 之前插入的类型专属列。
        /// 在 XAML 中通过属性元素语法填充，例如：
        /// <code>
        /// &lt;ctrl:ParamDataGridControl.ExtraColumns&gt;
        ///     &lt;DataGridTextColumn Header="类型" Binding="{Binding DataType}" /&gt;
        /// &lt;/ctrl:ParamDataGridControl.ExtraColumns&gt;
        /// </code>
        /// </summary>
        public Collection<DataGridColumn> ExtraColumns { get; } = new();

        // ── Column construction ────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            PART_Grid.Columns.Clear();

            PART_Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Code",
                Width = new DataGridLength(60),
                Binding = new Binding("Code") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
            });

            PART_Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "描述",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 80,
                Binding = new Binding("Description") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
            });

            foreach (var col in ExtraColumns)
                PART_Grid.Columns.Add(col);

            PART_Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "备注",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star),
                MinWidth = 60,
                Binding = new Binding("Comment") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
            });
        }
    }
}
