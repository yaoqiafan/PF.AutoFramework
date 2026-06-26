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
            win.MaxWidth      = double.PositiveInfinity;   // 解除 PFDialogBaseWindow 默认的 MaxWidth=1080 限制
            win.MaxHeight     = double.PositiveInfinity;
            win.SizeToContent = SizeToContent.Manual;
            win.Width         = 1200;
            win.Height        = 750;
            win.MinWidth      = 700;
            win.MinHeight     = 500;
        }

        if (DataContext is RoiEditorDialogViewModel vm)
            vm.SetEditor(RoiEditor);
    }
}
