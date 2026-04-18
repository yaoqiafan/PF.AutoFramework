using System;
using System.Globalization;
using System.Windows.Data;

namespace PF.Modules.Identity.Converter
{
    /// <summary>
    /// 系统内置账号保护转换器
    ///
    /// 用途：绑定到删除按钮的 IsEnabled，当用户名属于系统内置的不可删除账号时返回 false，
    ///   使删除按钮自动变灰，防止误操作删除超级管理员等关键账号。
    ///
    /// 受保护账号列表（不区分大小写）：
    ///   · SuperUser  — DefaultParameters 中定义的默认超级管理员
    ///   · System     — UserInfo.SystemUser 内置系统账号
    ///   · admin      — 通用内置管理员别名
    /// </summary>
    [ValueConversion(typeof(string), typeof(bool))]
    public class SystemUserToBoolConverter : IValueConverter
    {
        // 不可删除的系统内置账号名（不区分大小写）
        private static readonly string[] ProtectedUsers =
        {
            "operator",
            "engineer",
            "administrator",
            "superuser",
            "system",
            "admin",
        };

        /// <summary>
        /// 当用户名属于受保护账号时返回 false（禁用按钮），否则返回 true（启用按钮）。
        /// </summary>
        /// <summary>判断用户名是否属于受保护账号</summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string userName) return true;

            foreach (var name in ProtectedUsers)
            {
                if (string.Equals(userName, name, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return true;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException($"{nameof(SystemUserToBoolConverter)} 不支持反向转换。");
    }
}
