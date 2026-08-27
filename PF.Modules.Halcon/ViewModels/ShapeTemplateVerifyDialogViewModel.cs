using HalconDotNet;
using PF.Modules.Halcon.Controls;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Models;
using PF.Vision.Halcon.Services;
using Prism.Commands;
using System.Collections.ObjectModel;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// ROI 形状模板验证弹窗 ViewModel——框架侧独立、可复用能力，范围就是"拿一张图验证一个已存在的
/// 模板"，不建模板也不改模板（那是 <see cref="ShapeTemplateEditorDialogViewModel"/> 的事）。
///
/// <para>打开时通过 DialogParameters 传：<c>"TemplateName"</c>（string，必需）——验证哪个模板；
/// <c>"ImagePath"</c>（string，可选）——预填的验证图，调用方（比如某个程式调试向导）手上已经有
/// 一张图时直接注入，省得用户重选。不传就是空白，弹窗里自己"选图"（框架没有相机接口，只能选
/// 文件）。跟建模板弹窗不同：这里的图片来源按钮**始终可见**，不因为注入就隐藏——验证场景本来
/// 就需要随时换图测试鲁棒性。</para>
/// </summary>
public class ShapeTemplateVerifyDialogViewModel : PFDialogViewModelBase
{
    private HalconImageViewer? _imageViewer;
    private ShapeTemplateHandle? _templateHandle;
    private HObject? _verifyImage;

    private string? _verifyImagePath;
    /// <summary>用来验证的那张图的路径。</summary>
    public string? VerifyImagePath
    {
        get => _verifyImagePath;
        private set
        {
            if (!SetProperty(ref _verifyImagePath, value)) return;
            RaisePropertyChanged(nameof(VerifyImageName));
            FindMatchCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>验证图文件名，界面上显示这个就够了。</summary>
    public string VerifyImageName => string.IsNullOrEmpty(_verifyImagePath)
        ? "（未选择图片）" : System.IO.Path.GetFileName(_verifyImagePath);

    private string _statusMessage = "选一张图，点「查找匹配」。";
    /// <summary>操作反馈文字。</summary>
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private ShapeMatchOptions _matchOptions = new();
    /// <summary>查找匹配参数，绑 pf:PropertyGrid 直接编辑；每次打开弹窗独立一份，不做持久化。</summary>
    public ShapeMatchOptions MatchOptions { get => _matchOptions; private set => SetProperty(ref _matchOptions, value); }

    /// <summary>本次查找到的匹配结果列表。</summary>
    public ObservableCollection<ShapeMatchResult> MatchResults { get; } = [];

    private ShapeMatchResult? _selectedMatchResult;
    /// <summary>选中的匹配结果——选中即在图上叠加显示对应命中轮廓。</summary>
    public ShapeMatchResult? SelectedMatchResult
    {
        get => _selectedMatchResult;
        set
        {
            if (!SetProperty(ref _selectedMatchResult, value)) return;
            RedrawMatchOverlay();
        }
    }

    /// <summary>选一张图用来验证。</summary>
    public DelegateCommand SelectImageCommand { get; }

    /// <summary>在当前图上查找模板。</summary>
    public DelegateCommand FindMatchCommand { get; }

    /// <summary>ROI 形状模板验证弹窗构造函数。</summary>
    public ShapeTemplateVerifyDialogViewModel()
    {
        Title = "ROI 形状模板验证";

        SelectImageCommand = new DelegateCommand(ExecuteSelectImage);

        FindMatchCommand = new DelegateCommand(ExecuteFindMatch,
            () => _templateHandle != null && _verifyImage != null);

        CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        ConfirmCommand = CancelCommand;   // 只读验证，没有"确定"要提交，关闭即可
    }

    /// <summary>由 code-behind Loaded 时注入图像查看控件（HALCON 显示不走 WPF 绑定）。</summary>
    public void SetImageViewer(HalconImageViewer viewer)
    {
        _imageViewer = viewer;
        if (_verifyImage != null) _imageViewer.DisplayImage(_verifyImage);
    }

    /// <inheritdoc/>
    public override void OnDialogOpened(IDialogParameters parameters)
    {
        base.OnDialogOpened(parameters);

        var templateName = parameters.GetValue<string>("TemplateName");
        if (string.IsNullOrWhiteSpace(templateName))
        {
            StatusMessage = "没有指定要验证的模板名字。";
            return;
        }

        try
        {
            _templateHandle = ShapeTemplateService.LoadTemplate(templateName);
            Title = $"验证模板 [{templateName}]";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载模板 [{templateName}] 失败：{ex.Message}";
            return;
        }

        var imagePath = parameters.GetValue<string>("ImagePath");
        if (!string.IsNullOrWhiteSpace(imagePath))
            LoadVerifyImage(imagePath);

        FindMatchCommand.RaiseCanExecuteChanged();
    }

    /// <inheritdoc/>
    public override void OnDialogClosed()
    {
        _templateHandle?.Dispose();
        _templateHandle = null;
        _verifyImage?.Dispose();
        _verifyImage = null;
    }

    private void ExecuteSelectImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "选择用来验证的图",
            Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        LoadVerifyImage(dlg.FileName);
    }

    private void LoadVerifyImage(string path)
    {
        try
        {
            HOperatorSet.ReadImage(out HObject image, path);
            _verifyImage?.Dispose();
            _verifyImage    = image;
            VerifyImagePath = path;

            MatchResults.Clear();
            SelectedMatchResult = null;
            StatusMessage = $"已加载 {System.IO.Path.GetFileName(path)}，点「查找匹配」。";

            _imageViewer?.DisplayImage(image);
        }
        catch (Exception ex)
        {
            StatusMessage = $"图像加载失败：{ex.Message}";
        }
    }

    private void ExecuteFindMatch()
    {
        if (_templateHandle == null || _verifyImage == null) return;

        try
        {
            var results = ShapeTemplateService.FindMatches(_verifyImage, _templateHandle, MatchOptions);
            MatchResults.Clear();
            foreach (var r in results) MatchResults.Add(r);

            StatusMessage = results.Count == 0
                ? "没找到匹配——可以放宽 MatchOptions 里的 MinScore/AngleExtent 再试。"
                : $"找到 {results.Count} 个匹配，选中一条查看叠加轮廓。";

            SelectedMatchResult = MatchResults.FirstOrDefault();
        }
        catch (Exception ex)
        {
            MatchResults.Clear();
            StatusMessage = $"查找匹配失败：{ex.Message}";
        }
    }

    /// <summary>选中匹配结果变化时，在图上重画命中轮廓。</summary>
    private void RedrawMatchOverlay()
    {
        if (_imageViewer == null) return;

        _imageViewer.ClearOverlays();
        if (_templateHandle == null || _selectedMatchResult is not { } match) return;

        using var contour = ShapeTemplateService.GetMatchedContour(_templateHandle, match);
        if (contour.IsInitialized())
            _imageViewer.DisplayOverlay(contour, "lime green", 2);
    }
}
