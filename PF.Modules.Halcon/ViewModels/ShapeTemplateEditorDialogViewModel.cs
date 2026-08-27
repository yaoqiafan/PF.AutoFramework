using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Modules.Halcon.Controls;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Internal;
using PF.Vision.Halcon.Services;
using Prism.Commands;
using System.Windows;

namespace PF.Modules.Halcon.ViewModels;

/// <summary>
/// ROI 形状模板编辑弹窗 ViewModel——框架侧独立、可复用能力，范围到"建立模板并按名字存盘"为止。
/// "建立模板"（<c>CreateShapeModel</c>）不是单独一步，合并进了 <see cref="SaveCommand"/>：
/// 画好 ROI、填好名字，点一次"保存并关闭"就把建模型和写盘一起做了，不用先点"建立模板"
/// 再填名字保存这两步。
///
/// <para><b>"建新的" 跟 "改已有的" 是两条互斥的打开路径，调用方一开始就得选好，不要都传：</b>
/// 传 <c>"ImagePath"</c>（string）——建新模板，用调用方指定的这张图当参考图，隐藏"选参考图"
/// 按钮（图片锁定）；传 <c>"LoadTemplateName"</c>（string）——编辑一个已存在的模板，直接按名字读
/// 它自己存的参考图 + ROI 灌回编辑器，跟调用方手上有没有别的图无关。两者都不传就是空白开局，
/// 弹窗里"选参考图"/"加载模板文件"两个按钮都在，用户自己选。画 ROI 复用
/// <see cref="HalconRoiEditor"/> 控件本体，不重新发明交互。</para>
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

    /// <summary>
    /// 本次编辑会话最初是按哪个名字加载的（<see cref="ApplyEditSession"/> 设置）——建新模板时
    /// 一直是 null。非 null 时说明是"改已有模板"，<see cref="TemplateName"/> 锁定不可改
    /// （见 <see cref="CanEditTemplateName"/>），保存时必然跟这个名字相同，不用做唯一性校验；
    /// null（新建模板）时如果填的名字撞了已有模板的名字，才需要提示用户确认覆盖。
    /// </summary>
    private string? _loadedTemplateName;

    /// <summary>
    /// 模板名字输入框是否可编辑——只有"建新模板"（<see cref="_loadedTemplateName"/> 为 null）
    /// 才可以填/改名字；"改已有模板"时名字锁定为加载进来时的那个，不允许改名另存，
    /// 避免改名之后实际保存的到底是"新模板"还是"覆盖原模板"这件事变得含糊。
    /// </summary>
    public bool CanEditTemplateName => _loadedTemplateName == null;

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

    private string _statusMessage = "选参考图，画 ROI，填个名字后点「保存并关闭」。";
    /// <summary>操作反馈文字。</summary>
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    /// <summary>选参考图（仅在未通过参数注入时可见/可用）。</summary>
    public DelegateCommand SelectReferenceImageCommand { get; }

    /// <summary>用当前画好的 ROI 建立形状模板并按名字存盘、关闭弹窗。</summary>
    public DelegateCommand SaveCommand { get; }

    /// <summary>弹文件选择框选一个已存在的模板包（.roipk），把当初的参考图 + ROI 灌回编辑器，供微调。</summary>
    public DelegateCommand LoadTemplateFileCommand { get; }

    /// <summary>ROI 形状模板编辑弹窗构造函数。</summary>
    public ShapeTemplateEditorDialogViewModel()
    {
        Title = "ROI 形状模板编辑";

        SelectReferenceImageCommand = new DelegateCommand(ExecuteSelectReferenceImage);

        SaveCommand = new DelegateCommand(ExecuteSave,
            () => _referenceImage != null && !string.IsNullOrWhiteSpace(TemplateName));

        LoadTemplateFileCommand = new DelegateCommand(ExecuteLoadTemplateFile);

        ConfirmCommand = SaveCommand;
        CancelCommand  = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
    }

    /// <summary>
    /// 由 code-behind Loaded 时注入 ROI 编辑器控件（HALCON 显示不走 WPF 绑定）。
    /// <c>OnDialogOpened</c> 可能先于本方法执行（Prism 弹窗生命周期里视图 <c>Loaded</c> 晚于
    /// <c>IDialogAware.OnDialogOpened</c>），所以参考图和 ROI 都得在这里补一次"追上"——不能假设
    /// <see cref="ApplyEditSession"/>/<see cref="LoadReferenceImage"/> 调用时 <see cref="_editor"/>
    /// 已经就绪。
    /// </summary>
    public void SetEditor(HalconRoiEditor editor)
    {
        _editor = editor;
        if (_referenceImage != null) _editor.LoadImage(_referenceImage);
        if (_currentRois != null) _editor.LoadRois(_currentRois);
    }

    /// <inheritdoc/>
    public override void OnDialogOpened(IDialogParameters parameters)
    {
        base.OnDialogOpened(parameters);

        // "编辑已有模板" 和 "用指定图建新模板" 是互斥的两条路径，前者优先——
        // 调用方理论上只会传其中一个，万一都传了，"编辑已有" 更明确，不该被裸图覆盖。
        var loadName = parameters.GetValue<string>("LoadTemplateName");
        if (!string.IsNullOrWhiteSpace(loadName))
        {
            ApplyEditSessionByName(loadName);
            return;
        }

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
            StatusMessage = $"已加载 {System.IO.Path.GetFileName(path)}，画 ROI、填个名字后保存。";

            _editor?.LoadImage(image);
            SaveCommand.RaiseCanExecuteChanged();
        }
        catch (Exception ex)
        {
            StatusMessage = $"参考图加载失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 建模型（原「建立模板」按钮的逻辑，现在合并进保存这一步，不再单独点一次）+ 两项前置校验，
    /// 任何一项不过就不落盘：①合法性——名字最终会拼进文件路径（<see cref="ShapeTemplateService"/>
    /// 类注释里的 <c>TemplateDirectory + name + ".roipk"</c>），含 Windows 文件名非法字符时
    /// <c>ZipFile.CreateFromDirectory</c> 会直接抛异常，不如提前挡住给出明确提示；②唯一性——同名
    /// 会整体覆盖已有模板包，属于破坏性操作，除非正是当初加载进来微调的那个模板（名字锁定，见
    /// <see cref="CanEditTemplateName"/>，二者天然相同），否则要弹确认，不能用户填错名字就
    /// 悄悄覆盖了别人的模板。
    /// </summary>
    private async void ExecuteSave()
    {
        if (_referenceImage == null || _editor == null || string.IsNullOrWhiteSpace(TemplateName)) return;

        var rois = _editor.GetCurrentRois();
        if (rois.Count == 0)
        {
            StatusMessage = "还没画任何 ROI——模板至少需要一个「包含」区域。";
            return;
        }

        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        if (TemplateName.IndexOfAny(invalidChars) >= 0)
        {
            StatusMessage = "模板名字不能包含文件名非法字符（如 \\ / : * ? \" < > |）。";
            return;
        }

        bool isSameAsLoaded = string.Equals(TemplateName, _loadedTemplateName, StringComparison.OrdinalIgnoreCase);
        if (!isSameAsLoaded && ShapeTemplateService.GetAvailableTemplateNames()
                .Any(n => string.Equals(n, TemplateName, StringComparison.OrdinalIgnoreCase)))
        {
            var result = await MessageService.ShowMessageAsync(
                $"模板名称 [{TemplateName}] 已存在，保存会整体覆盖原有模板，确认要覆盖吗？",
                "名称已存在", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != ButtonResult.Yes) return;
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

            ShapeTemplateService.SaveTemplate(_templateHandle, _referenceImage, rois, TemplateName);
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
            ApplyEditSession(session, System.IO.Path.GetFileNameWithoutExtension(dlg.FileName));
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载模板文件失败：{ex.Message}";
        }
    }

    /// <summary>
    /// <see cref="OnDialogOpened"/> 收到 <c>"LoadTemplateName"</c> 参数时的入口——按名字（走
    /// <see cref="ShapeTemplateService.TemplateDirectory"/> 约定）而不是裸路径加载，跟弹窗里
    /// "加载模板文件"按钮（<see cref="ExecuteLoadTemplateFile"/>，弹文件选择框选路径）是同一件事
    /// 的两种触发方式，共用 <see cref="ApplyEditSession"/>。
    /// </summary>
    private void ApplyEditSessionByName(string name)
    {
        try
        {
            var session = ShapeTemplateService.LoadTemplateForEdit(name);
            ApplyEditSession(session, name);
        }
        catch (Exception ex)
        {
            StatusMessage = $"加载模板 [{name}] 失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 把"编辑已有模板"读出来的会话（参考图 + ROI）灌进编辑器，替换掉当前状态——
    /// 不管是弹文件选择框选的，还是打开弹窗时按名字自动加载的，都走这一个方法。
    /// </summary>
    private void ApplyEditSession(ShapeTemplateEditSession session, string name)
    {
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

        _loadedTemplateName = name;   // 先锁定名字可编辑状态，再回填名字框——顺序不影响结果，但更贴近语义
        RaisePropertyChanged(nameof(CanEditTemplateName));
        TemplateName  = name;         // 自动回填名字框（此时已锁定不可改），微调完直接覆盖保存同一个模板
        SaveCommand.RaiseCanExecuteChanged();

        StatusMessage = $"已加载模板 [{name}] 用于微调，改完 ROI 后直接保存（名称已锁定，不可修改）。";
    }
}
