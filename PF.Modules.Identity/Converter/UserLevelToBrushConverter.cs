using PF.Core.Enums;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PF.Modules.Identity.Converter
{
    /// <summary>UserLevel → 品牌色画刷（用于头像背景和等级徽章）</summary>
    [ValueConversion(typeof(UserLevel), typeof(SolidColorBrush))]
    public class UserLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UserLevel level)
            {
                return level switch
                {
                    UserLevel.Operator      => new SolidColorBrush(Color.FromRgb(0x19, 0x76, 0xD2)), // 蓝
                    UserLevel.Engineer      => new SolidColorBrush(Color.FromRgb(0x00, 0x89, 0x7B)), // 绿
                    UserLevel.Administrator => new SolidColorBrush(Color.FromRgb(0x7B, 0x1F, 0xA2)), // 紫
                    UserLevel.SuperUser     => new SolidColorBrush(Color.FromRgb(0xFF, 0x8F, 0x00)), // 金
                    _                       => new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)), // 灰
                };
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
