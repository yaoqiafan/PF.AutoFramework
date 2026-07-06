using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Modules.Halcon.Controls;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// ROI 编辑弹窗 ViewModel。
/// 打开时通过 DialogParameters 传入：
///   "FilePath" (string?)   — 背景图路径，可空
///   "Rois"    (IReadOnlyList&lt;VisionRoiConfig&gt;?) — 已有 ROI，可空
/// 关闭时（OK）返回：
///   "Rois"    (IReadOnlyList&lt;VisionRoiConfig&gt;) — 用户编辑后的 ROI
/// </summary>
public class RoiEditorDialogViewModel : PFDialogViewModelBase
{
    private HalconRoiEditor? _editor;

    public RoiEditorDialogViewModel()
    {
        Title = "ROI 绘制";

        ConfirmCommand = new DelegateCommand(() =>
        {
            var rois = _editor?.GetCurrentRois() ?? [];
            var p = new DialogParameters { { "Rois", rois } };
            RequestClose.Invoke(new DialogResult(ButtonResult.OK) { Parameters = p });
        });

        CancelCommand = new DelegateCommand(() =>
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel)));
    }

    /// <summary>由 code-behind Loaded 注入编辑器控件</summary>
    public void SetEditor(HalconRoiEditor editor)
    {
        _editor = editor;

        // 如果已有初始参数（在 SetEditor 调用前 OnDialogOpened 先执行），立即应用
        if (_pendingFilePath is not null || _pendingRois is not null)
            ApplyPending();
    }

    // ── 延迟参数（控件可能在 OnDialogOpened 之后才 Loaded）────────────────────

    private string? _pendingFilePath;
    private IReadOnlyList<VisionRoiConfig>? _pendingRois;

    public override void OnDialogOpened(IDialogParameters parameters)
    {
        base.OnDialogOpened(parameters);
        _pendingFilePath = parameters.GetValue<string>("FilePath");
        _pendingRois     = parameters.GetValue<IReadOnlyList<VisionRoiConfig>>("Rois");

        if (_editor is not null)
            ApplyPending();
    }

    private void ApplyPending()
    {
        if (_editor is null) return;

        if (_pendingRois is { Count: > 0 })
            _editor.LoadRois(_pendingRois);

        if (!string.IsNullOrEmpty(_pendingFilePath))
        {
            try
            {
                HOperatorSet.ReadImage(out HObject image, _pendingFilePath);
                _editor.LoadImage(image);   // DisplayImage 接管所有权，此处不 Dispose
            }
            catch { }
        }

        _pendingFilePath = null;
        _pendingRois     = null;
    }
}
