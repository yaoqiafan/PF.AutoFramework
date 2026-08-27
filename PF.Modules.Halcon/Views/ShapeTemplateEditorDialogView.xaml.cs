using PF.Modules.Halcon.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace PF.Modules.Halcon.Views;

/// <summary>
/// ShapeTemplateEditorDialogView.xaml 的交互逻辑
/// </summary>
public partial class ShapeTemplateEditorDialogView : UserControl
{
    /// <summary>ShapeTemplateEditorDialogView 构造函数</summary>
    public ShapeTemplateEditorDialogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 宿主 Window 默认 SizeToContent=WidthAndHeight，对可调整大小的弹窗需改为 Manual
        // （照抄 RoiEditorDialogView 的做法——ROI 编辑器需要足够的画布空间）
        if (Window.GetWindow(this) is Window win)
        {
            win.MaxWidth      = double.PositiveInfinity;
            win.MaxHeight     = double.PositiveInfinity;
            win.SizeToContent = SizeToContent.Manual;
            win.Width         = 1100;
            win.Height        = 720;
            win.MinWidth      = 700;
            win.MinHeight     = 500;
        }

        if (DataContext is ShapeTemplateEditorDialogViewModel vm)
            vm.SetEditor(RoiEditor);
    }
}
