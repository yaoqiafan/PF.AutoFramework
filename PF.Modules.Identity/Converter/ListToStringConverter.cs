using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Identity.Converter
{
    /// <summary>
    /// 将 IEnumerable&lt;string&gt; 转换为逗号分隔的单行字符串，用于 TextBlock 显示。
    /// </summary>
    [ValueConversion(typeof(IEnumerable<string>), typeof(string))]
    public class ListToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> list)
                return string.Join(", ", list);

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException($"{nameof(ListToStringConverter)} 不支持反向转换。");
    }
}
