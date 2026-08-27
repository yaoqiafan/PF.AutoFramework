using PF.Modules.Halcon.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

/// <summary>
/// ShapeTemplateVerifyDialogView.xaml 的交互逻辑
/// </summary>
public partial class ShapeTemplateVerifyDialogView : UserControl
{
    /// <summary>ShapeTemplateVerifyDialogView 构造函数</summary>
    public ShapeTemplateVerifyDialogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 宿主 Window 默认 SizeToContent=WidthAndHeight，对可调整大小的弹窗需改为 Manual
        // （照抄 ShapeTemplateEditorDialogView 的做法）。
        if (Window.GetWindow(this) is Window win)
        {
            win.MaxWidth      = double.PositiveInfinity;
            win.MaxHeight     = double.PositiveInfinity;
            win.SizeToContent = SizeToContent.Manual;
            win.Width         = 980;
            win.Height        = 640;
            win.MinWidth      = 700;
            win.MinHeight     = 480;
        }

        if (DataContext is ShapeTemplateVerifyDialogViewModel vm)
            vm.SetImageViewer(VerifyImageViewer);
    }
}
