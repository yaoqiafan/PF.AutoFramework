using PF.Modules.Halcon.Controls;
using PF.Modules.Halcon.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

public partial class HalconDebugView : UserControl
{
    public HalconDebugView()
    {
        InitializeComponent();
    }

    private void OutputImageViewer_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is HalconDebugViewModel vm)
            vm.SetImageViewer(OutputImageViewer);
    }
}
