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
        public PagePermissionView()
        {
            InitializeComponent();
        }
    }

    public class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// 是否反转逻辑。
        /// 默认 (False): Null/空 -> Collapsed, 非 Null -> Visible
        /// 反转 (True): Null/空 -> Visible, 非 Null -> Collapsed
        /// </summary>
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // 判断是否为 null 或者 空字符串
            bool isNullOrEmpty = value == null || (value is string str && string.IsNullOrWhiteSpace(str));

            // 根据 Invert 属性决定最终的可见状态
            bool isVisible = Invert ? isNullOrEmpty : !isNullOrEmpty;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("NullToVisibilityConverter 不支持反向转换。");
        }
    }
}
