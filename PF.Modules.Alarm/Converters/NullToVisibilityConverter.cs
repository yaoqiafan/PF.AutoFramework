using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Alarm.Converters
{
    /// <summary>null → Collapsed，非null → Visible</summary>
    public sealed class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value == null ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotImplementedException();
    }
}
