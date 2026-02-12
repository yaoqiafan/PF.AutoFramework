using System.Windows;

namespace PF.Application.Shell.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : PF.UI.Controls.Window
    {

        public MainWindow()
        {

            InitializeComponent();
        }


        private void ButtonSkins_OnClick(object sender, RoutedEventArgs e)
        {
            //Button button = e.OriginalSource as Button;
            //if (e.OriginalSource is Button)
            //{
            //    var Params = _containerProvider.Resolve<IParams>().GetParam<GlobalSettingsDTO>(ParamType.Base);
            //    PopupConfig.IsOpen = false;
            //    if (button.Tag.Equals(Params.Skin.ToString()))
            //    {
            //        return;
            //    }


            //    Params.Skin = (CommonConfig.Core.Dtos.SkinType)Enum.Parse(typeof(CommonConfig.Core.Dtos.SkinType), button.Tag.ToString());
            //    ((App)Application.Current).UpdateSkin(button.Tag.ToString());
            //    Params.Save();
            //}
        }

        private void ButtonConfig_OnClick(object sender, RoutedEventArgs e) => PopupConfig.IsOpen = true;




        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {

            //this.Hide();
            //e.Cancel = true;
        }

    }
}
