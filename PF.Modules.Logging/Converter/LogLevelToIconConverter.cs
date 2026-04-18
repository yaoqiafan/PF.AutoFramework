using PF.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Logging.Converter
{
    /// <summary>
    /// 日志级别到符号转换器
    /// </summary>
    public class LogLevelToSymbolConverter : IValueConverter
    {
        /// <summary>将日志级别转换为对应符号</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Debug => "🔍",
                    LogLevel.Info => "ℹ️",
                    LogLevel.Success => "✅",
                    LogLevel.Warn => "⚠️",
                    LogLevel.Error => "❌",
                    LogLevel.Fatal => "💀",
                    _ => "📝"
                };
            }
            return "📝";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


}
