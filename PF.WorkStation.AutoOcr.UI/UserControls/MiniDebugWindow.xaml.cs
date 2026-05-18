using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    /// <summary>Interaction logic for MiniDebugWindow.xaml</summary>
    public partial class MiniDebugWindow : Window
    {
        /// <summary>窗口关闭时执行的回调，用于恢复主窗口可见性。</summary>
        public Action? OnClosed { get; set; }

        /// <summary>Initializes a new instance.</summary>
        public MiniDebugWindow()
        {
            InitializeComponent();
            var area = SystemParameters.WorkArea;
            Left = area.Width - 396;
            Top = 50;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // PreviewMouseLeftButtonDown tunnels down first; skip if the actual click target is a Button
            var dep = e.OriginalSource as DependencyObject;
            while (dep != null && dep != sender as DependencyObject)
            {
                if (dep is Button) return;
                dep = VisualTreeHelper.GetParent(dep);
            }
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            OnClosed?.Invoke();
            Close();
        }
    }
}
