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
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 宿主 Window 默认 SizeToContent=WidthAndHeight，对可调整大小的弹窗需改为 Manual
        if (Window.GetWindow(this) is Window win)
        {
            win.SizeToContent = SizeToContent.Manual;
            win.MinWidth  = 700;
            win.MinHeight = 500;
        }

        if (DataContext is RoiEditorDialogViewModel vm)
            vm.SetEditor(RoiEditor);
    }
}
