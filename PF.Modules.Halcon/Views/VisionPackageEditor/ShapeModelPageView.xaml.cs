using System.Windows.Controls;
using PF.Modules.Halcon.ViewModels.VisionPackageEditor;

namespace PF.Modules.Halcon.Views.VisionPackageEditor;

/// <summary>形状模型页（换绑规则同 <see cref="ImagePageView"/>）。</summary>
public partial class ShapeModelPageView : UserControl
{
    /// <summary>构造。</summary>
    public ShapeModelPageView()
    {
        InitializeComponent();
        Loaded   += (_, _) => (DataContext as ShapeModelPageViewModel)?.AttachViewer(Viewer);
        Unloaded += (_, _) => (DataContext as ShapeModelPageViewModel)?.DetachViewer();
        DataContextChanged += (_, e) =>
        {
            (e.OldValue as ShapeModelPageViewModel)?.DetachViewer();
            if (IsLoaded) (e.NewValue as ShapeModelPageViewModel)?.AttachViewer(Viewer);
        };
    }
}
