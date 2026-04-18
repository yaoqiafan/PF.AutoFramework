using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.Modules.SecsGem.ViewModels
{
    /// <summary>
    /// bool → Visibility (false → Collapsed)，与内置 BooleanToVisibilityConverter 相反
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        /// <summary>布尔值取反转可见性</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        /// <summary>反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// bool → 边框颜色（true = 红色校验错误边框，false = 正常灰色边框）
    /// </summary>
    public class BoolToBorderBrushConverter : IValueConverter
    {
        /// <summary>布尔值转边框画刷</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b && b)
                return new SolidColorBrush(Color.FromRgb(0xE5, 0x39, 0x35)); // #E53935
            return new SolidColorBrush(Color.FromRgb(0xBD, 0xBD, 0xBD)); // #BDBDBD
        }

        /// <summary>反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>
    /// string → Visibility（非空非白空格 → Visible，否则 Collapsed）
    /// </summary>
    public class StringNotEmptyToVisibilityConverter : IValueConverter
    {
        /// <summary>非空字符串转可见性</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        /// <summary>反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
