using PF.Core.Enums;
using System;
using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Identity.Converter
{
    /// <summary>
    /// UserLevel → 显示文本转换器。
    /// ConverterParameter="label" 返回中文等级名；
    /// ConverterParameter="icon"  返回头像 Emoji；
    /// 无参数默认返回中文等级名。
    /// </summary>
    public class UserLevelToDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not UserLevel level) return "未知";

            bool isIcon = parameter is string p && p == "icon";

            return (level, isIcon) switch
            {
                (UserLevel.Operator,      false) => "操作员",
                (UserLevel.Operator,      true)  => "👤",
                (UserLevel.Engineer,      false) => "工程师",
                (UserLevel.Engineer,      true)  => "⚙",
                (UserLevel.Administrator, false) => "管理员",
                (UserLevel.Administrator, true)  => "🛡",
                (UserLevel.SuperUser,     false) => "超级用户",
                (UserLevel.SuperUser,     true)  => "⭐",
                _                               => isIcon ? "？" : "未分配"
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
