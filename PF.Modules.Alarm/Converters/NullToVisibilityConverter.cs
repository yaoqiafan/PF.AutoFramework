using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Alarm.Converters
{
    /// <summary>null → Collapsed，非null → Visible</summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        /// <summary>将 null 转为 Collapsed，非 null 转为 Visible</summary>
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value == null ? Visibility.Collapsed : Visibility.Visible;

        /// <summary>不支持反向转换</summary>
        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
