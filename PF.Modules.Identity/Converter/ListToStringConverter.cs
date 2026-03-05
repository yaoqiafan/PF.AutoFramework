using PF.UI.Infrastructure.Navigation;
using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Identity.Converter
{
    /// <summary>
    /// 将 IEnumerable&lt;string&gt; 路由名称列表转换为中文显示名称，顿号分隔，用于 TextBlock。
    /// </summary>
    [ValueConversion(typeof(IEnumerable<string>), typeof(string))]
    public class ListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> list)
                return string.Join("、", list.Select(PermissionHelper.GetViewDisplayName));

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException($"{nameof(ListToStringConverter)} 不支持反向转换。");
    }
}
