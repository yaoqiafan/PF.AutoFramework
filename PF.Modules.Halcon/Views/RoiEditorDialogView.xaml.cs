using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Modules.Halcon.Controls;
using PF.Modules.Halcon.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

public partial class RoiEditorDialogView : UserControl
{
    public RoiEditorDialogView()
    {
        InitializeComponent();
    }

    private void RoiEditor_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is RoiEditorDialogViewModel vm)
            vm.SetEditor(RoiEditor);
    }
}
