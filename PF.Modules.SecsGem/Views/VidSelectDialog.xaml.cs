using PF.SecsGem.DataBase.Entities.Variable;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;

namespace PF.Modules.SecsGem.Views
{
    public partial class VidSelectDialog : Window
    {
        public VIDEntity SelectedVid { get; private set; }

        public VidSelectDialog(IEnumerable<VIDEntity> vids)
        {
            InitializeComponent();

            // 包装为带显示文本的条目
            var items = new List<VidDisplayItem>();
            foreach (var v in vids)
                items.Add(new VidDisplayItem { Entity = v, DisplayText = $"[{v.Code}]  {v.Description}  ({v.Type})" });

            VidListBox.ItemsSource = items;
            if (VidListBox.Items.Count > 0)
                VidListBox.SelectedIndex = 0;
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (VidListBox.SelectedItem is VidDisplayItem item)
            {
                SelectedVid = item.Entity;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("请先选择一个变量。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void OnListBoxDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OnOkClick(sender, e);
        }

        private class VidDisplayItem
        {
            public VIDEntity Entity { get; set; }
            public string DisplayText { get; set; }
        }
    }
}
