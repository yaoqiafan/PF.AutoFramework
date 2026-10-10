using System.Windows.Controls;
using PF.Modules.Halcon.ViewModels.VisionPackageEditor;

namespace PF.Modules.Halcon.Views.VisionPackageEditor;

/// <summary>ROI 页（换绑规则同 <see cref="ImagePageView"/>）。</summary>
public partial class RoiPageView : UserControl
{
    /// <summary>构造。</summary>
    public RoiPageView()
    {
        InitializeComponent();
        Loaded   += (_, _) => (DataContext as RoiPageViewModel)?.AttachEditor(Editor);
        Unloaded += (_, _) => (DataContext as RoiPageViewModel)?.DetachEditor();
        DataContextChanged += (_, e) =>
        {
            (e.OldValue as RoiPageViewModel)?.DetachEditor();
            if (IsLoaded) (e.NewValue as RoiPageViewModel)?.AttachEditor(Editor);
        };
    }
}
