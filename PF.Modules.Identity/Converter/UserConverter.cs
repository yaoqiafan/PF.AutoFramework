using PF.Core.Entities.Identity;
using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Identity.Converter
{
    [ValueConversion(typeof(UserLevel), typeof(string))]
    public class UserBackGroundConnver : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = string.Empty;
            UserLevel root = (UserLevel)value;
            switch (root)
            {
                case UserLevel.Null:
                    str = "#FF808080";
                    break;
                case UserLevel.Operator:
                    str = "#FFFFA500";
                    break;
                case UserLevel.Engineer:
                    str = "#FF0000FF";
                    break;
                case UserLevel.Administrator:
                    str = "#FF83b768";
                    break;
                case UserLevel.SuperUser:
                    str = "#FFEB0058";
                    break;
            }
            return str;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    [ValueConversion(typeof(UserLevel), typeof(string))]
    public class UserTextConnver : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            string str = string.Empty;
            UserLevel root = (UserLevel)value;
            switch (root)
            {
                case UserLevel.Null:
                    str = "未登录";
                    break;
                case UserLevel.Operator:
                    str = "操作员";
                    break;
                case UserLevel.Engineer:
                    str = "工程师";
                    break;
                case UserLevel.Administrator:
                    str = "管理员";
                    break;
                case UserLevel.SuperUser:
                    str = "超级用户";
                    break;
            }
            return str;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }



    [ValueConversion(typeof(UserLevel), typeof(string))]
    public class UserVisibilityConnver : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            Visibility str = Visibility.Collapsed;
            if (value != null)
            {
                UserLevel root = ((UserInfo)value).Root;
                switch (root)
                {
                    case UserLevel.SuperUser:
                        str = Visibility.Visible;
                        break;
                }
            }

            return str;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
