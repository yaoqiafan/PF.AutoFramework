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

namespace PF.Modules.Identity.Views
{
    [ModuleNavigation(NavigationConstants.Dialogs.LoginView, "窗体权限更改", GroupName = "权限管控", Order = 1, IsDialog = false)]
    public partial class PagePermissionView : UserControl
    {
        public PagePermissionView()
        {
            InitializeComponent();
        }
    }
}
