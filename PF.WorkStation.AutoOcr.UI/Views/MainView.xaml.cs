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

namespace PF.WorkStation.AutoOcr.UI.Views
{
    [ModuleNavigation(NavigationConstants.Views.MainView, "主显界面", GroupIcon = "/PF.UI.Resources;component/Images/PNG/11.png",
       GroupName = "开始", Order = 1, GroupOrder = 0,
       Icon = "NailGeometry"
     )]
    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }
    }
}
