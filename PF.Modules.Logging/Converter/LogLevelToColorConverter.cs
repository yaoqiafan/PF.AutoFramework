using PF.Core.Enums;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.Modules.Logging.Converter
{
    /// <summary>
    /// 日志级别到颜色转换器
    /// </summary>
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => new SolidColorBrush(Color.FromRgb(158, 158, 158)),
                    LogLevel.Info => new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                    LogLevel.Success => new SolidColorBrush(Color.FromRgb(76, 175, 80)),
                    LogLevel.Warn => new SolidColorBrush(Color.FromRgb(255, 152, 0)),
                    LogLevel.Error => new SolidColorBrush(Color.FromRgb(244, 67, 54)),
                    LogLevel.Fatal => new SolidColorBrush(Color.FromRgb(183, 28, 28)),
                    _ => Brushes.Gray
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
