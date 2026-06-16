using PF.Core.Attributes;
using PF.Core.Constants;
using PF.WorkStation.AutoOcr.UI.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.WorkStation.AutoOcr.UI.Views
{
    [ModuleNavigation(NavigationConstants.Views.HomeView, "运行",
        GroupName = "开始", Order = 2,
        Icon = "NailGeometry"
      )]
    public partial class HomeView : UserControl
    {
        /// <summary>Initializes a new instance.</summary>
        public HomeView()
        {
            InitializeComponent();
        }

        private void WS1Auth1PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => (DataContext as HomeViewModel)?.SetAuth1Password(((PasswordBox)sender).Password);

        private void WS1Auth2PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => (DataContext as HomeViewModel)?.SetAuth2Password(((PasswordBox)sender).Password);

        private void WS2Auth1PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => (DataContext as HomeViewModel)?.SetAuth1Password(((PasswordBox)sender).Password);

        private void WS2Auth2PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
            => (DataContext as HomeViewModel)?.SetAuth2Password(((PasswordBox)sender).Password);

        private void AuthPasswordBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue == false)
                ((PasswordBox)sender).Clear();
        }
    }
}
