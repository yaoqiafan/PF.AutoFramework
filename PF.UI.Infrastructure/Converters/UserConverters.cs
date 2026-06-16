using PF.Core.Enums;
using System.Globalization;
using System.Windows.Data;

namespace PF.UI.Infrastructure.Converters
{
    /// <summary>用户等级转背景色转换器</summary>
    [ValueConversion(typeof(UserLevel), typeof(string))]
    public class UserBackGroundConnver : IValueConverter
    {
        /// <summary>将用户等级转换为背景色</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = string.Empty;
            UserLevel root = (UserLevel)value;
            switch (root)
            {
                case UserLevel.Null:        str = "#FF808080"; break;
                case UserLevel.Operator:    str = "#FFFFA500"; break;
                case UserLevel.Engineer:    str = "#FF0000FF"; break;
                case UserLevel.Administrator: str = "#FF83b768"; break;
                case UserLevel.SuperUser:   str = "#FFEB0058"; break;
            }
            return str;
        }

        /// <summary>不支持反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// <summary>用户等级转文本转换器</summary>
    [ValueConversion(typeof(UserLevel), typeof(string))]
    public class UserTextConnver : IValueConverter
    {
        /// <summary>将用户等级转换为中文文本</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = string.Empty;
            UserLevel root = (UserLevel)value;
            switch (root)
            {
                case UserLevel.Null:          str = "未登录";   break;
                case UserLevel.Operator:      str = "操作员";   break;
                case UserLevel.Engineer:      str = "工程师";   break;
                case UserLevel.Administrator: str = "管理员";   break;
                case UserLevel.SuperUser:     str = "超级用户"; break;
            }
            return str;
        }

        /// <summary>不支持反向转换</summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
