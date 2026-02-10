using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Logging.Converter
{
    /// <summary>
    /// 异常展开可见性转换器
    /// 只有当日志项包含异常且被选中时才显示异常详情
    /// </summary>
    public class ExceptionToExpanderVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
                return Visibility.Collapsed;

            var exception = values[0];
            var isSelected = values[1] as bool?;

            // 只有同时满足两个条件才显示：有异常且被选中
            return (exception != null && isSelected == true)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
