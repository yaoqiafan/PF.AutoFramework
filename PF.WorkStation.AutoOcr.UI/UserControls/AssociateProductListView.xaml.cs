using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    public partial class AssociateProductListView : UserControl
    {
        // 1. 定义依赖属性
        /// <summary>
        /// AssociatesProperty
        /// </summary>
        public static readonly DependencyProperty AssociatesProperty =
            DependencyProperty.Register(
                nameof(Associates),
                typeof(List<string>),
                typeof(AssociateProductListView),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnAssociatesChanged));
        /// <summary>
        /// 成员
        /// </summary>

        public List<string> Associates
        {
            get => (List<string>)GetValue(AssociatesProperty);
            set => SetValue(AssociatesProperty, value);
        }

        // 用于 UI 绑定的内部可观察集合
        /// <summary>
        /// 获取或设置 InternalList
        /// </summary>
        public ObservableCollection<StringWrapper> InternalList { get; } = new ObservableCollection<StringWrapper>();

        private bool _isSyncing = false;
        /// <summary>
        /// AssociateProductListView 构造函数
        /// </summary>

        public AssociateProductListView()
        {
            InitializeComponent();
        }

        // 外部 ViewModel 数据变化时，同步到内部 UI 列表
        private static void OnAssociatesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AssociateProductListView control)
            {
                control.SyncToInternalList();
            }
        }

        private void SyncToInternalList()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            InternalList.Clear();
            if (Associates != null)
            {
                foreach (var item in Associates)
                {
                    var wrapper = new StringWrapper { Value = item };
                    wrapper.PropertyChanged += Wrapper_PropertyChanged;
                    InternalList.Add(wrapper);
                }
            }

            _isSyncing = false;
        }

        // UI 发生增/删/改时，打包全新 List 触发外部绑定的 SetProperty
        private void SyncToAssociates()
        {
            if (_isSyncing) return;
            _isSyncing = true;

            // 创建新的 List 实例，确保外部框架的 SetProperty 引用比较能侦测到变化
            var newList = InternalList.Select(x => x.Value).ToList();
            SetCurrentValue(AssociatesProperty, newList);

            _isSyncing = false;
        }

        private void Wrapper_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(StringWrapper.Value))
            {
                // TextBox 焦点丢失时会触发此事件，随后同步给外层
                SyncToAssociates();
            }
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            var newItem = new StringWrapper { Value = "New_Recipe" };
            newItem.PropertyChanged += Wrapper_PropertyChanged;
            InternalList.Add(newItem);

            // 滚动到最新添加的项
            ItemListView.ScrollIntoView(newItem);
            SyncToAssociates();
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ItemListView.SelectedItem is StringWrapper selectedItem)
            {
                selectedItem.PropertyChanged -= Wrapper_PropertyChanged;
                InternalList.Remove(selectedItem);
                SyncToAssociates();
            }
        }
    }

    /// <summary>
    /// 字符串包装类，支持双向绑定与 UI 通知
    /// </summary>
    public class StringWrapper : INotifyPropertyChanged
    {
        private string _value;
        /// <summary>
        /// 成员
        /// </summary>
        public string Value
        {
            get => _value;
            set
            {
                if (_value != value)
                {
                    _value = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Value)));
                }
            }
        }
        /// <summary>
        /// PropertyChanged
        /// </summary>

        public event PropertyChangedEventHandler PropertyChanged;
    }
}