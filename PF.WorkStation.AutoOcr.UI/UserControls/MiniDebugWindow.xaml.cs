using System;
using System.Windows;
using System.Windows.Input;

namespace PF.WorkStation.AutoOcr.UI.UserControls
{
    public partial class MiniDebugWindow : Window
    {
        public Action? OnClosed { get; set; }

        public MiniDebugWindow()
        {
            InitializeComponent();
            var area = SystemParameters.WorkArea;
            Left = area.Width - 396;
            Top = 50;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            OnClosed?.Invoke();
            Close();
        }
    }
}
