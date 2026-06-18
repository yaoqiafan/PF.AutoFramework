using HalconDotNet;
using PF.Modules.Halcon.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

public partial class ProcedureDebugView : UserControl
{
    public ProcedureDebugView()
    {
        InitializeComponent();
    }

    private void HalconWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ProcedureDebugViewModel vm && sender is HWindowControlWPF ctrl)
            vm.SetHalconControl(ctrl);
    }
}
