using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Debug.Converters
{
    /// <summary>null → Visible，非null → Collapsed（用于"无数据时显示占位提示"场景）</summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>将 null 转为 Visible，非 null 转为 Collapsed</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
            value == null ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>不支持反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
