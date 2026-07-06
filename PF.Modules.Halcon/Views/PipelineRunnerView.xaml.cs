using HalconDotNet;
using PF.Modules.Halcon.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

public partial class PipelineRunnerView : UserControl
{
    public PipelineRunnerView()
    {
        InitializeComponent();
    }

    private void HalconWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is PipelineRunnerViewModel vm && sender is HWindowControlWPF ctrl)
            vm.SetHalconControl(ctrl);
    }
}
