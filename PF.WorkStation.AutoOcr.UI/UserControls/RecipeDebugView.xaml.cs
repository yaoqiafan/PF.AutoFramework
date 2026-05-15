using System.Windows;
using System.Windows.Controls;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    public partial class RecipeDebugView : UserControl
    {
        private MiniDebugWindow? _miniWindow;

        public RecipeDebugView()
        {
            InitializeComponent();
        }

        private void BtnMiniMode_Click(object sender, RoutedEventArgs e)
        {
            if (_miniWindow?.IsVisible == true)
            {
                _miniWindow.Activate();
                return;
            }

            var dialogWindow = Window.GetWindow(this);
            var mainWindow = Application.Current.MainWindow;

            _miniWindow = new MiniDebugWindow { DataContext = DataContext };
            _miniWindow.OnClosed = () =>
            {
                dialogWindow?.Show();
                if (mainWindow != null)
                    mainWindow.WindowState = WindowState.Normal;
                dialogWindow?.Activate();
            };

            dialogWindow?.Hide();
            if (mainWindow != null && mainWindow != dialogWindow)
                mainWindow.WindowState = WindowState.Minimized;

            _miniWindow.Show();
        }
    }
}
