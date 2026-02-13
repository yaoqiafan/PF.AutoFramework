using PF.Core.Attributes;
using PF.Core.Constants;
using System;
using System.Collections.Generic;
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

namespace PF.Modules.Parameter.Views
{
   
    [ModuleNavigation(NavigationConstants.Views.ParameterView, "运行参数", GroupName = "设备参数设置", Order = 1)]
    public partial class ParameterView : UserControl
    {
        public ParameterView()
        {
            InitializeComponent();
        }
    }
}
