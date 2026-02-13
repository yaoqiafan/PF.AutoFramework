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

namespace PF.Modules.Logging.Views
{
    [ModuleNavigation(NavigationConstants.Views.LogManagementView, "日志查询",
       GroupName = "历史表单", GroupOrder = 3, Order = 1,
       GroupIcon = "/PF.UI.Resources;component/Images/PNG/7.png", // 父节点 PNG 图标
       Icon = "RunInfoIcon")] 
   
    public partial class LogManagementView : UserControl
    {
        public LogManagementView()
        {
            InitializeComponent();
        }
    }
}
