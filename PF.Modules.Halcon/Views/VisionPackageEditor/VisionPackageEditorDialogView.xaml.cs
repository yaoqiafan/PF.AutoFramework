using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PF.Modules.Halcon.ViewModels.VisionPackageEditor;

namespace PF.Modules.Halcon.Views.VisionPackageEditor;

/// <summary>视觉资产包编辑器窗口。</summary>
public partial class VisionPackageEditorDialogView : UserControl
{
    private Window? _window;

    /// <summary>构造。</summary>
    public VisionPackageEditorDialogView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is INotifyPropertyChanged o) o.PropertyChanged -= OnVmPropertyChanged;
            if (e.NewValue is INotifyPropertyChanged n) n.PropertyChanged += OnVmPropertyChanged;
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _window = Window.GetWindow(this);
        if (_window == null) return;

        // 宿主窗口默认按内容自动调整大小（SizeToContent=WidthAndHeight）且最大宽 1080：
        // 改为固定大小、不超过屏幕工作区，右侧切换不同编辑页时窗口不再跟着内容变
        var area = SystemParameters.WorkArea;
        _window.SizeToContent = SizeToContent.Manual;
        _window.MaxWidth      = area.Width;
        _window.MaxHeight     = area.Height;
        _window.MinWidth      = Math.Min(1000, area.Width);
        _window.MinHeight     = Math.Min(640, area.Height);
        _window.Width         = Math.Min(1400, area.Width * 0.95);
        _window.Height        = Math.Min(880, area.Height * 0.95);
        _window.Left          = area.Left + (area.Width - _window.Width) / 2;
        _window.Top           = area.Top + (area.Height - _window.Height) / 2;
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 兜底：任何原因让窗口回到"按内容调整大小"，切页后改回固定大小。
        // 必须推迟到当前布局结束后再改：这个通知可能发生在树控件生成节点的过程中，
        // 同步改窗口尺寸属性会触发重新布局、重入节点生成（"无法在正在进行内容生成时调用 StartAt"）
        if (e.PropertyName != nameof(VisionPackageEditorDialogViewModel.CurrentPage)) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (_window is { IsLoaded: true, SizeToContent: not SizeToContent.Manual })
                _window.SizeToContent = SizeToContent.Manual;
        });
    }

    private void OnTreeSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        // 选中变化可能发生在树控件生成/回收节点的过程中（比如关闭窗口时），切页会换掉右侧整页内容，
        // 推迟到当前布局结束后再处理，避免布局重入
        var node = e.NewValue as PackageTreeNode;
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            (DataContext as VisionPackageEditorDialogViewModel)?.OnTreeSelectionChanged(node));
    }
}
