using System;
using System.Globalization;
using System.Windows.Data;

namespace PF.UI.Infrastructure.Tools.Converter;

public class Object2BooleanReConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
