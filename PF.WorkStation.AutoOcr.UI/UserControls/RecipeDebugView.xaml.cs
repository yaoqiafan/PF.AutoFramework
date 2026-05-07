using System.Windows;
using System.Windows.Controls;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    /// <summary>
    /// RecipeDebugView.xaml 的交互逻辑
    /// </summary>
    public partial class RecipeDebugView : UserControl
    {
        private bool _minimizedByDeactivation;

        /// <summary>
        /// RecipeDebugView 构造函数
        /// </summary>
        public RecipeDebugView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Deactivated += OnApplicationDeactivated;
            Application.Current.Activated += OnApplicationActivated;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            Application.Current.Deactivated -= OnApplicationDeactivated;
            Application.Current.Activated -= OnApplicationActivated;
        }

        private void OnApplicationDeactivated(object? sender, EventArgs e)
        {
            var host = Window.GetWindow(this);
            if (host == null || host.WindowState == WindowState.Minimized) return;
            _minimizedByDeactivation = true;
            host.WindowState = WindowState.Minimized;
        }

        private void OnApplicationActivated(object? sender, EventArgs e)
        {
            if (!_minimizedByDeactivation) return;
            _minimizedByDeactivation = false;
            var host = Window.GetWindow(this);
            if (host != null)
                host.WindowState = WindowState.Normal;
        }
    }
}
