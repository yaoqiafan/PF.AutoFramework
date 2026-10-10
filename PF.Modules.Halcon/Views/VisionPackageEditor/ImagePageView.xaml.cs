using System.Windows.Controls;
using PF.Modules.Halcon.ViewModels.VisionPackageEditor;

namespace PF.Modules.Halcon.Views.VisionPackageEditor;

/// <summary>
/// 原图页。同类型页面之间切换时 WPF 会复用同一个视图实例、只换 DataContext（不触发 Loaded/Unloaded），
/// 所以除了 Loaded/Unloaded 还要在 DataContextChanged 里换绑查看器。
/// </summary>
public partial class ImagePageView : UserControl
{
    /// <summary>构造。</summary>
    public ImagePageView()
    {
        InitializeComponent();
        Loaded   += (_, _) => (DataContext as ImagePageViewModel)?.AttachViewer(Viewer);
        Unloaded += (_, _) => (DataContext as ImagePageViewModel)?.DetachViewer();
        DataContextChanged += (_, e) =>
        {
            (e.OldValue as ImagePageViewModel)?.DetachViewer();
            if (IsLoaded) (e.NewValue as ImagePageViewModel)?.AttachViewer(Viewer);
        };
    }
}
