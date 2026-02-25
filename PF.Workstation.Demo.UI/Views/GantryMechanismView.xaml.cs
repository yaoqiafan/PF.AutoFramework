using PF.Core.Attributes;
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

namespace PF.Workstation.Demo.UI.Views
{
    /// <summary>
    /// GantryMechanismView.xaml 的交互逻辑
    /// </summary>

    [MechanismUI("取放模组调试", "PickPlaceDebugView", 1)]
    public partial class GantryMechanismView : UserControl
    {
        public GantryMechanismView()
        {
            InitializeComponent();
        }
    }
}
