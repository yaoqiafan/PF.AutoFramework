using PF.Core.Attributes;
using PF.Core.Constants;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PF.WorkStation.AutoOcr.UI.Views
{
    [ModuleNavigation(nameof(OcrRecipeManageView), "配方", GroupIcon = "/PF.UI.Resources;component/Images/PNG/10.png",
       GroupName = "配方管理", Order = 1, GroupOrder = 3,
       Icon = "NailGeometry"
     )]
    public partial class OcrRecipeManageView : UserControl
    {
        public OcrRecipeManageView()
        {
            InitializeComponent();
        }
    }





    [ValueConversion(typeof(List<string>), typeof(string))]
    public class StringListToStringConverter : IValueConverter
    {
        /// <summary>
        /// 默认的分隔符，如果不传参数，默认用逗号加空格分隔
        /// </summary>
        public string Separator { get; set; } = ", ";

        // List<string> -> string (用于UI显示)
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is IEnumerable<string> list)
            {
                // 如果 XAML 中传了 ConverterParameter，优先使用参数作为分隔符
                string sep = parameter as string ?? Separator;
                return string.Join(sep, list);
            }
            return string.Empty;
        }

        // string -> List<string> (用于UI修改后回传给ViewModel，比如绑定在TextBox上时)
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                string sep = parameter as string ?? Separator;

                // 将字符串按分隔符切分，去除两端空格并忽略空项
                return str.Split(new[] { sep }, StringSplitOptions.RemoveEmptyEntries)
                          .Select(s => s.Trim())
                          .ToList();
            }
            return new List<string>();
        }
    }

}
