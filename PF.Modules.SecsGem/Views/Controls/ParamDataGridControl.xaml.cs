using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace PF.Modules.SecsGem.Views.Controls
{
    /// <summary>
    /// 自定义参数数据表格控件。
    /// 封装了一个标准化的 DataGrid，提供默认的“Code”、“描述”和“备注”列，
    /// 并支持通过 <see cref="ExtraColumns"/> 属性在 XAML 中动态插入额外的业务自定义列。
    /// </summary>
    public partial class ParamDataGridControl : UserControl
    {
        /// <summary>
        /// 初始化 <see cref="ParamDataGridControl"/> 类的新实例。
        /// </summary>
        public ParamDataGridControl()
        {
            InitializeComponent();

            // 订阅 Loaded 事件，以确保在控件加载到可视化树时动态构建 DataGrid 的列
            Loaded += OnLoaded;
        }

        // ── ItemsSource ────────────────────────────────────────────────────────

        /// <summary>
        /// 标识 <see cref="ItemsSource"/> 依赖属性。
        /// </summary>
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(object),
                typeof(ParamDataGridControl), new PropertyMetadata(null));

        /// <summary>
        /// 获取或设置用于生成 DataGrid 内容的集合。
        /// 通常绑定到一个 ObservableCollection。
        /// </summary>
        public object ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        // ── SelectedItem ───────────────────────────────────────────────────────

        /// <summary>
        /// 标识 <see cref="SelectedItem"/> 依赖属性。
        /// </summary>
        public static readonly DependencyProperty SelectedItemProperty =
            DependencyProperty.Register(nameof(SelectedItem), typeof(object),
                typeof(ParamDataGridControl),
                new FrameworkPropertyMetadata(null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)); // 默认为双向绑定，方便 ViewModel 获取选中项

        /// <summary>
        /// 获取或设置 DataGrid 中当前选中的项。
        /// 支持双向绑定，以便在 ViewModel 中实时跟踪用户的选择。
        /// </summary>
        public object SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        // ── AddCommand ─────────────────────────────────────────────────────────

        /// <summary>
        /// 标识 <see cref="AddCommand"/> 依赖属性。
        /// </summary>
        public static readonly DependencyProperty AddCommandProperty =
            DependencyProperty.Register(nameof(AddCommand), typeof(ICommand),
                typeof(ParamDataGridControl), new PropertyMetadata(null));

        /// <summary>
        /// 获取或设置用于执行“添加”操作的命令。
        /// 可在 XAML 中将此控件的内部添加按钮（如果有）绑定到此命令。
        /// </summary>
        public ICommand AddCommand
        {
            get => (ICommand)GetValue(AddCommandProperty);
            set => SetValue(AddCommandProperty, value);
        }

        // ── DeleteCommand ──────────────────────────────────────────────────────

        /// <summary>
        /// 标识 <see cref="DeleteCommand"/> 依赖属性。
        /// </summary>
        public static readonly DependencyProperty DeleteCommandProperty =
            DependencyProperty.Register(nameof(DeleteCommand), typeof(ICommand),
                typeof(ParamDataGridControl), new PropertyMetadata(null));

        /// <summary>
        /// 获取或设置用于执行“删除”操作的命令。
        /// 可在 XAML 中将此控件的内部删除按钮（如果有）绑定到此命令。
        /// </summary>
        public ICommand DeleteCommand
        {
            get => (ICommand)GetValue(DeleteCommandProperty);
            set => SetValue(DeleteCommandProperty, value);
        }

        // ── ExtraColumns (CLR collection, populated by XAML content) ──────────

        /// <summary>
        /// 获取用于存放额外列的集合。
        /// 这是一个普通的 CLR 集合，支持在 XAML 中通过属性元素语法进行填充。
        /// 动态添加的列将被插入在“Code”、“Description”之后，“Comment”之前。
        /// </summary>
        /// <example>
        /// 在 XAML 中的使用方式如下：
        /// <code>
        /// &lt;ctrl:ParamDataGridControl.ExtraColumns&gt;
        ///     &lt;DataGridTextColumn Header="类型" Binding="{Binding DataType}" /&gt;
        /// &lt;/ctrl:ParamDataGridControl.ExtraColumns&gt;
        /// </code>
        /// </example>
        public Collection<DataGridColumn> ExtraColumns { get; } = new();

        // ── Column construction ────────────────────────────────────────────────

        /// <summary>
        /// 处理控件的 <see cref="FrameworkElement.Loaded"/> 事件。
        /// 在此方法中动态生成和排列内部 DataGrid (PART_Grid) 的列。
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 清空现有的列，防止在 TabControl 等容器中来回切换时触发多次 Loaded 导致列重复
            PART_Grid.Columns.Clear();

            // 1. 添加固定的首列：Code (代码)
            PART_Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "Code",
                Width = DataGridLength.Auto, // 宽度自适应内容
                // UpdateSourceTrigger 设为 LostFocus 以优化性能，在单元格失去焦点时才更新源
                Binding = new Binding("Code") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
            });

            // 2. 添加固定的第二列：描述 (Description)
            PART_Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "描述",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), // 占据剩余空间的 1 份
                MinWidth = 80, // 设置最小宽度防止被过度挤压
                Binding = new Binding("Description") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
            });

            // 3. 遍历并插入来自 XAML 声明的额外列 (ExtraColumns)
            foreach (var col in ExtraColumns)
            {
                PART_Grid.Columns.Add(col);
            }

            // 4. 添加固定的尾列：备注 (Comment)
            PART_Grid.Columns.Add(new DataGridTextColumn
            {
                Header = "备注",
                Width = new DataGridLength(1, DataGridLengthUnitType.Star), // 同样占据剩余空间的 1 份
                MinWidth = 60,
                Binding = new Binding("Comment") { UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }
            });
        }
    }
}