using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.Application.Shell.Converters
{
    [ValueConversion(typeof(bool), typeof(string))]
    public class DeviceConnectedTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? "已连接" : "未连接";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    [ValueConversion(typeof(bool), typeof(Brush))]
    public class DeviceConnectedBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush _connected    = MakeBrush(0x02, 0xad, 0x8b);
        private static readonly SolidColorBrush _disconnected = MakeBrush(0x75, 0x75, 0x75);

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? _connected : _disconnected;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();

        private static SolidColorBrush MakeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }
    }
}
