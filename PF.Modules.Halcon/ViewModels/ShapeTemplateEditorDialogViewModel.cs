using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Modules.Halcon.Controls;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Internal;
using PF.Vision.Halcon.Services;
using Prism.Commands;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// ROI 形状模板编辑弹窗 ViewModel——框架侧独立、可复用能力，范围到"建立模板并按名字存盘"为止。
///
/// <para>参考图两种来源：打开时通过 DialogParameters 传 <c>"ImagePath"</c>（此时隐藏"选参考图"
/// 按钮，图片只读展示，调用方想锁定用哪张图建模板就用这条路）；不传就显示"选参考图"按钮，用户
/// 自己选。画 ROI 复用 <see cref="HalconRoiEditor"/> 控件本体，不重新发明交互。</para>
///
/// <para>不包含"在新图上查找/预览匹配"——那是消费方自己的事，模板存盘后消费方用
/// <see cref="ShapeTemplateService.LoadTemplate"/> 按名字取，不需要重新走一遍这个弹窗的流程。</para>
///
/// <para>关闭（OK）时通过 DialogParameters 带回 <c>"Name"</c>（存盘用的模板名字），方便调用方
/// 需要的话直接拿去用（比如自动填进旁边一个"验证模板"输入框）。</para>
/// </summary>
public class ShapeTemplateEditorDialogViewModel : PFDialogViewModelBase
{
    private HalconRoiEditor? _editor;
    private HObject? _referenceImage;
    private ShapeTemplateHandle? _templateHandle;
    private bool _imageInjected;
    private string? _referenceImagePath;
    private string? _referenceImageLabelOverride;
    private IReadOnlyList<VisionRoiConfig>? _currentRois;

    /// <summary>参考图是否已通过 DialogParameters 注入——注入了就不给"选参考图"按钮，图片锁定。</summary>
    public bool ShowSelectImageButton => !_imageInjected;

    /// <summary>
    /// 参考图展示文字（只读）。正常是文件名；从已有模板包加载微调时图片来自临时解压文件，
    /// 用 <see cref="_referenceImageLabelOverride"/> 顶替成"来自模板 [名字]"这种更有意义的提示。
    /// </summary>
    public string ReferenceImageLabel
    {
        get
        {
            if (!string.IsNullOrEmpty(_referenceImageLabelOverride)) return _referenceImageLabelOverride;
            return string.IsNullOrEmpty(_referenceImagePath)
                ? "（未选择参考图）" : System.IO.Path.GetFileName(_referenceImagePath);
        }
    }

    private string _templateName = string.Empty;
    /// <summary>准备存盘的模板名字。</summary>
    public string TemplateName
    {
        get => _templateName;
        set
        {
            if (SetProperty(ref _templateName, value))
                SaveCommand.RaiseCanExecuteChanged();
        }
    }

    private string _statusMessage = "选参考图，画 ROI，再点「建立模板」。";
    /// <summary>操作反馈文字。</summary>
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    /// <summary>选参考图（仅在未通过参数注入时可见/可用）。</summary>
    public DelegateCommand SelectReferenceImageCommand { get; }

    /// <summary>用当前画好的 ROI 建立形状模板。</summary>
    public DelegateCommand BuildTemplateCommand { get; }

    /// <summary>把模板按名字存盘并关闭弹窗。</summary>
    public DelegateCommand SaveCommand { get; }

    /// <summary>弹文件选择框选一个已存在的模板包（.roipk），把当初的参考图 + ROI 灌回编辑器，供微调。</summary>
    public DelegateCommand LoadTemplateFileCommand { get; }

    /// <summary>ROI 形状模板编辑弹窗构造函数。</summary>
    public ShapeTemplateEditorDialogViewModel()
    {
        Title = "ROI 形状模板编辑";

        SelectReferenceImageCommand = new DelegateCommand(ExecuteSelectReferenceImage);

        BuildTemplateCommand = new DelegateCommand(ExecuteBuildTemplate, () => _referenceImage != null);

        SaveCommand = new DelegateCommand(ExecuteSave,
            () => _templateHandle != null && !string.IsNullOrWhiteSpace(TemplateName));

        LoadTemplateFileCommand = new DelegateCommand(ExecuteLoadTemplateFile);

        ConfirmCommand = SaveCommand;
        CancelCommand  = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
    }

    /// <summary>由 code-behind Loaded 时注入 ROI 编辑器控件（HALCON 显示不走 WPF 绑定）。</summary>
    public void SetEditor(HalconRoiEditor editor)
    {
        _editor = editor;
        if (_referenceImage != null) _editor.LoadImage(_referenceImage);
    }

    /// <inheritdoc/>
    public override void OnDialogOpened(IDialogParameters parameters)
    {
        base.OnDialogOpened(parameters);

        var imagePath = parameters.GetValue<string>("ImagePath");
        if (!string.IsNullOrWhiteSpace(imagePath))
        {
            _imageInjected = true;
            LoadReferenceImage(imagePath);
            RaisePropertyChanged(nameof(ShowSelectImageButton));
        }
    }

    /// <inheritdoc/>
    public override void OnDialogClosed()
    {
        _templateHandle?.Dispose();
        _templateHandle = null;
        _referenceImage?.Dispose();
        _referenceImage = null;
    }

    private void ExecuteSelectReferenceImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "选择参考图（用来画 ROI / 建立模板）",
            Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        LoadReferenceImage(dlg.FileName);
    }

    private void LoadReferenceImage(string path)
    {
        try
        {
            HOperatorSet.ReadImage(out HObject image, path);
            _referenceImage?.Dispose();
            _referenceImage     = image;
            _referenceImagePath = path;
            _referenceImageLabelOverride = null;
            RaisePropertyChanged(nameof(ReferenceImageLabel));
            StatusMessage = $"已加载 {System.IO.Path.GetFileName(path)}，画 ROI 后点「建立模板」。";

            _editor?.LoadImage(image);
            BuildTemplateCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"参考图加载失败：{ex.Message}";
        }
    }

    private void ExecuteBuildTemplate()
    {
        if (_referenceImage == null || _editor == null) return;

        var rois = _editor.GetCurrentRois();
        if (rois.Count == 0)
        {
            StatusMessage = "还没画任何 ROI——模板至少需要一个「包含」区域。";
            return;
        }

        try
        {
            using var region = RoiRegionBuilder.Build(rois);
            if (!region.IsInitialized())
            {
                StatusMessage = "ROI 拼接出的区域是空的，检查一下是不是全标成了「排除」。";
                return;
            }

            _templateHandle?.Dispose();
            _templateHandle = ShapeTemplateService.CreateTemplate(_referenceImage, region);
            _currentRois = rois;

            StatusMessage = "模板建立成功，填个名字保存。";
            SaveCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"建立模板失败：{ex.Message}";
        }
    }

    private void ExecuteSave()
    {
        if (_templateHandle == null || _referenceImage == null || _currentRois == null
            || string.IsNullOrWhiteSpace(TemplateName)) return;

        try
        {
            ShapeTemplateService.SaveTemplate(_templateHandle, _referenceImage, _currentRois, TemplateName);
            var p = new DialogParameters { { "Name", TemplateName } };
            RequestClose.Invoke(new DialogResult(ButtonResult.OK) { Parameters = p });
        }
        catch (Exception ex)
        {
            StatusMessage = $"保存失败：{ex.Message}";
        }
    }

    private void ExecuteLoadTemplateFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "选择模板文件（用于微调）",
            Filter = "ROI 模板包|*.roipk|所有文件|*.*",
        };
        if (!string.IsNullOrWhiteSpace(ShapeTemplateService.TemplateDirectory)
            && System.IO.Directory.Exists(ShapeTemplateService.TemplateDirectory))
            dlg.InitialDirectory = ShapeTemplateService.TemplateDirectory;

        if (dlg.ShowDialog() != true) return;

        try
        {
            var session = ShapeTemplateService.LoadTemplateForEditFromPath(dlg.FileName);
            string name = System.IO.Path.GetFileNameWithoutExtension(dlg.FileName);

            _referenceImage?.Dispose();
            _referenceImage     = session.ReferenceImage;
            _referenceImagePath = null;
            _imageInjected      = true;   // 图片来自模板包，不再显示"选参考图"按钮
            RaisePropertyChanged(nameof(ShowSelectImageButton));
            _referenceImageLabelOverride = $"（来自模板 [{name}]）";
            RaisePropertyChanged(nameof(ReferenceImageLabel));

            _editor?.LoadImage(_referenceImage);
            _editor?.LoadRois(session.Rois);
            _currentRois = session.Rois;

            _templateHandle?.Dispose();
            _templateHandle = null;
            SaveCommand.RaiseCanExecuteChanged();
            BuildTemplateCommand.RaiseCanExecuteChanged();

            TemplateName  = name;   // 自动回填名字框，微调完直接覆盖保存同一个模板
            StatusMessage = $"已加载模板 [{name}] 用于微调，改完 ROI 后先点「建立模板」再保存。";
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载模板文件失败：{ex.Message}";
        }
    }
}
