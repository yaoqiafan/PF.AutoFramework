using PF.Core.Attributes;
using PF.Core.Constants;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PF.Modules.Identity.Views
{
    [ModuleNavigation(NavigationConstants.Views.PagePermissionView, "窗体权限更改", GroupName = "权限管控", Icon = "SettingIcon", Order = 3,GroupIcon = "/PF.UI.Resources;component/Images/PNG/3.png")]
    public partial class PagePermissionView : UserControl
    {
        /// <summary>初始化页面权限视图</summary>
        public PagePermissionView()
        {
            InitializeComponent();
        }
    }

    /// <summary>Null 转可见性转换器</summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 是否反转逻辑。
        /// 默认 (False): Null/空 -> Collapsed, 非 Null -> Visible
        /// 反转 (True): Null/空 -> Visible, 非 Null -> Collapsed
        /// </summary>
        public bool Invert { get; set; }

        /// <summary>将 Null/空值转换为 Visibility</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNullOrEmpty = value == null || (value is string str && string.IsNullOrWhiteSpace(str));
            bool isVisible = Invert ? isNullOrEmpty : !isNullOrEmpty;
            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>不支持反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("NullToVisibilityConverter 不支持反向转换。");
        }
    }
}
