using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.Application.Base.Converters
{
    /// <summary>将设备连接状态（bool）转换为"已连接"/"未连接"文本。</summary>
    [ValueConversion(typeof(bool), typeof(string))]
    public class DeviceConnectedTextConverter : IValueConverter
    {
        /// <summary>将布尔值转换为中文连接状态文本。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? "已连接" : "未连接";

        /// <summary>不支持反向转换。</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>将设备连接状态（bool）转换为对应颜色画刷（绿色/灰色）。</summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public class DeviceConnectedBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush _connected = MakeBrush(0x02, 0xad, 0x8b);
        private static readonly SolidColorBrush _disconnected = MakeBrush(0x75, 0x75, 0x75);

        /// <summary>将布尔值转换为连接状态颜色画刷。</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? _connected : _disconnected;

        /// <summary>不支持反向转换。</summary>
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
