using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Modules.Identity.ViewModels;
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
    [ModuleNavigation(NavigationConstants.Dialogs.LoginView, "权限获取", GroupName = "权限管控", Icon = "LoginInIcon", GroupOrder = 7, Order = 1)]
    public partial class LoginView : UserControl
    {
        public LoginView(IContainerExtension containerExtension)
        {
            InitializeComponent();
            this.DataContext = containerExtension.Resolve<LoginViewModel>();
        }
    }
}
