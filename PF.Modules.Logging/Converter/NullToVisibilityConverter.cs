using PF.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Logging.Converter
{
    /// <summary>
    /// Null到可见性转换器
    /// </summary>
    public class NullToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isNull = value == null || (value is string str && string.IsNullOrEmpty(str));
            bool visible = Invert ? isNull : !isNull;

            return visible ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    /// <summary>
    /// Null到文本转换器
    /// </summary>
    public class NullToTextConverter : IValueConverter
    {
        public string NullText { get; set; } = "空";

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
            {
                return NullText;
            }

            if (value is LogLevel level)
            {
                return level.ToString();
            }

            return value.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

}
