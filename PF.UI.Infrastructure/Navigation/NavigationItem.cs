using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.Navigation
{
    /// <summary>
    /// 导航菜单项的数据模型
    /// </summary>
    public class NavigationItem : BindableBase
    {
        private string _title;
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        private string _icon;
        public string Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _viewName;
        public string ViewName
        {
            get => _viewName;
            set => SetProperty(ref _viewName, value);
        }

        private string _navigationParameter;
        public string NavigationParameter
        {
            get => _navigationParameter;
            set => SetProperty(ref _navigationParameter, value);
        }

        private bool _isDialog;
        public bool IsDialog
        {
            get => _isDialog;
            set => SetProperty(ref _isDialog, value);
        }

        private int _order;
        public int Order
        {
            get => _order;
            set => SetProperty(ref _order, value);
        }

        // 子菜单项（用于存放该分组下的具体页面）
        public ObservableCollection<NavigationItem> Children { get; set; } = new ObservableCollection<NavigationItem>();
    }
}
