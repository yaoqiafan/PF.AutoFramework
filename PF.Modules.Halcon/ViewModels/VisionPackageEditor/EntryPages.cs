using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Modules.Halcon.Controls;
using PF.Vision.Halcon.Internal;
using PF.Vision.Halcon.Models;
using PF.Vision.Halcon.Packaging;
using PF.Vision.Halcon.Services;
using Prism.Commands;
using Prism.Ioc;
using Prism.Mvvm;

namespace PF.Modules.Halcon.ViewModels.VisionPackageEditor;

/// <summary>编辑页回调编辑器主窗口的能力：忙碌遮罩下跑后台操作、弹确认框、导入原图、删除条目。</summary>
public interface IPackageEditorHost
{
    /// <summary>编辑会话。</summary>
    VisionPackageSession Session { get; }

    /// <summary>布局。</summary>
    VisionPackageLayout Layout { get; }

    /// <summary>在后台线程执行 <paramref name="work"/>，期间显示忙碌遮罩；出错弹提示。返回是否成功。</summary>
    Task<bool> RunBusyAsync(string text, Action work);

    /// <summary>弹提示/确认框。</summary>
    Task<ButtonResult> AskAsync(string message, MessageBoxButton buttons = MessageBoxButton.OKCancel,
                                MessageBoxImage image = MessageBoxImage.Question);

    /// <summary>底部状态栏提示。</summary>
    void ShowStatus(string message);

    /// <summary>给原图条目导入图片（含与当前图比对、替换确认）。</summary>
    Task ImportImageAsync(string imageEntryId, string path);

    /// <summary>删除条目（含下游影响提示）。</summary>
    Task RemoveEntryAsync(string entryId);
}

/// <summary>右侧编辑页的公共部分：条目标题、状态、原因、删除。</summary>
public abstract class EntryPageViewModel : BindableBase
{
    /// <summary>编辑器主窗口。</summary>
    protected IPackageEditorHost Host { get; }

    /// <summary>编辑会话。</summary>
    protected VisionPackageSession Session => Host.Session;

    /// <summary>条目 id。</summary>
    public string EntryId { get; }

    /// <summary>布局里的定义；多余条目为 null。</summary>
    public LayoutEntry? Definition { get; }

    private PackageEntryState _state = null!;
    /// <summary>当前状态。</summary>
    public PackageEntryState State { get => _state; private set => SetProperty(ref _state, value); }

    /// <summary>删除这个条目的数据。</summary>
    public DelegateCommand RemoveCommand { get; }

    /// <summary>构造。</summary>
    protected EntryPageViewModel(IPackageEditorHost host, string entryId)
    {
        Host = host;
        EntryId = entryId;
        Definition = host.Layout.Find(entryId);
        RemoveCommand = new DelegateCommand(async () => await Host.RemoveEntryAsync(EntryId), () => State?.HasContent == true);
        _state = Session.GetState(entryId);
    }

    /// <summary>条目类型的中文名。</summary>
    public string KindLabel => State.Kind switch
    {
        BuiltInKinds.Image      => "原图",
        BuiltInKinds.Roi        => "ROI",
        BuiltInKinds.ShapeModel => "形状模型（程序生成）",
        BuiltInKinds.Data       => "参数",
        BuiltInKinds.File       => "附件",
        _                       => State.Kind,
    };

    /// <summary>状态文字。</summary>
    public string StatusText => State.Status switch
    {
        EntryStatus.Ok      => "正常",
        EntryStatus.Stale   => "需处理",
        EntryStatus.Invalid => "无效",
        EntryStatus.Extra   => "多余（布局中已删除）",
        _                   => State.Required ? "未建立" : "未建立（可选）",
    };

    /// <summary>状态级别（ok / warn / error / none）。</summary>
    public string StatusLevel => State.Status switch
    {
        EntryStatus.Ok      => "ok",
        EntryStatus.Stale   => "warn",
        EntryStatus.Missing => State.Required ? "error" : "none",
        _                   => "error",
    };

    /// <summary>非正常时的原因。</summary>
    public string ReasonsText => State.Status == EntryStatus.Ok ? string.Empty : string.Join("\n", State.Reasons);

    /// <summary>编辑器在会话变化后调用：刷新状态与页面内容。</summary>
    public void Refresh()
    {
        State = Session.GetState(EntryId);
        RaisePropertyChanged(nameof(KindLabel));
        RaisePropertyChanged(nameof(StatusText));
        RaisePropertyChanged(nameof(StatusLevel));
        RaisePropertyChanged(nameof(ReasonsText));
        RemoveCommand.RaiseCanExecuteChanged();
        OnRefreshed();
    }

    /// <summary>状态刷新后，页面自己的内容刷新。</summary>
    protected virtual void OnRefreshed() { }

    /// <summary>页面上有没应用到会话的编辑（切走/保存/关闭前提示）。</summary>
    public virtual bool HasUnappliedChanges => false;

    /// <summary>把未应用的编辑应用到会话（出错抛 <see cref="VisionPackageException"/>）。</summary>
    public virtual void ApplyPending() { }

    /// <summary>放弃未应用的编辑。</summary>
    public virtual void DiscardPending() { }

    /// <summary>页面被切走或编辑器关闭。</summary>
    public virtual void OnClosed() { }

    /// <summary>JSON 文本，用来比较"编辑中的对象"和会话里的有没有差别。</summary>
    protected static string Json(object? o) => o == null ? "null" : JsonSerializer.Serialize(o, o.GetType());
}

// ── 原图 ────────────────────────────────────────────────────────────────────

/// <summary>原图页：预览、尺寸/来源/存储方式、导入。</summary>
public sealed class ImagePageViewModel : EntryPageViewModel
{
    private HalconImageViewer? _viewer;
    private string? _renderedHash;

    /// <summary>构造。</summary>
    public ImagePageViewModel(IPackageEditorHost host, string entryId) : base(host, entryId)
    {
        ImportCommand = new DelegateCommand(ExecuteImport);
        UpdateInfo();
    }

    /// <summary>导入/替换原图。</summary>
    public DelegateCommand ImportCommand { get; }

    private string _info = string.Empty;
    /// <summary>原图信息。</summary>
    public string Info { get => _info; private set => SetProperty(ref _info, value); }

    /// <summary>由页面 code-behind 注入查看器。</summary>
    public void AttachViewer(HalconImageViewer viewer)
    {
        _viewer = viewer;
        viewer.WindowInitialized += OnWindowInitialized;
        Render();
    }

    /// <summary>页面卸载时解除。</summary>
    public void DetachViewer()
    {
        if (_viewer != null) _viewer.WindowInitialized -= OnWindowInitialized;
        _viewer = null;
        _renderedHash = null;
    }

    private void OnWindowInitialized(object? sender, EventArgs e) => Render();

    private void Render()
    {
        if (_viewer == null) return;
        _renderedHash = State.ContentHash;
        var img = State.HasContent ? Session.GetImage(EntryId) : null;
        if (img == null) _viewer.Clear();
        else _viewer.DisplayImage(img);   // 查看器接管拷贝的所有权
    }

    /// <inheritdoc/>
    protected override void OnRefreshed()
    {
        UpdateInfo();
        if (_renderedHash != State.ContentHash) Render();
    }

    private void UpdateInfo()
    {
        var a = Session.GetAttributes(EntryId);
        var opts = Definition?.Options as ImageEntryOptions;
        string storage = (opts?.Storage ?? ImageStorage.Png) switch
        {
            ImageStorage.Png      => "无损 PNG",
            ImageStorage.Original => "原文件原样保存",
            ImageStorage.Tiff     => "无损 TIFF",
            ImageStorage.Bmp      => "BMP",
            ImageStorage.Jpeg     => $"JPEG（有损，质量 {opts!.JpegQuality}）",
            _                     => "?",
        };
        Info = State.HasContent && a != null
            ? $"来源：{a["sourceName"]}\n尺寸：{a["width"]} × {a["height"]}，{a["channels"]} 通道，{a["pixelType"]}\n存储方式：{storage}"
            : $"还没有导入原图。\n存储方式：{storage}";
    }

    private async void ExecuteImport()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = $"导入 [{State.Title}]",
            Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        await Host.ImportImageAsync(EntryId, dlg.FileName);
    }

    /// <inheritdoc/>
    public override void OnClosed() => DetachViewer();
}

// ── ROI ─────────────────────────────────────────────────────────────────────

/// <summary>ROI 页：在依赖的原图上画 ROI（复用 <see cref="HalconRoiEditor"/>），应用 / 撤销 / 换图后确认位置。</summary>
public sealed class RoiPageViewModel : EntryPageViewModel
{
    private HalconRoiEditor? _editor;
    private string? _loadedImageHash;
    private string? _loadedRoiHash;
    private string _baseline = "[]";

    /// <summary>依赖的原图条目 id。</summary>
    public string ImageId { get; }

    /// <summary>构造。</summary>
    public RoiPageViewModel(IPackageEditorHost host, string entryId) : base(host, entryId)
    {
        ImageId = Definition!.DependsOn[RoiRole];
        ApplyCommand   = new DelegateCommand(ExecuteApply, () => HasImage);
        ConfirmCommand = new DelegateCommand(ExecuteConfirm, () => State.Status == EntryStatus.Stale);
        RevertCommand  = new DelegateCommand(() => LoadRois());
    }

    private const string RoiRole = "image";

    /// <summary>把编辑器里的 ROI 应用到会话。</summary>
    public DelegateCommand ApplyCommand { get; }

    /// <summary>换图后确认 ROI 位置仍然正确。</summary>
    public DelegateCommand ConfirmCommand { get; }

    /// <summary>撤销未应用的修改。</summary>
    public DelegateCommand RevertCommand { get; }

    /// <summary>依赖的原图是否已导入。</summary>
    public bool HasImage => Session.GetState(ImageId).HasContent;

    /// <summary>提示文字。</summary>
    public string Hint => HasImage
        ? $"在 [{Session.GetState(ImageId).Title}] 上画 ROI，画好后点「应用 ROI」。坐标为原图坐标。"
        : $"请先导入 [{Session.GetState(ImageId).Title}]。";

    /// <summary>由页面 code-behind 注入编辑器。</summary>
    public void AttachEditor(HalconRoiEditor editor)
    {
        _editor = editor;
        editor.ImageViewer.WindowInitialized += OnWindowInitialized;
        LoadImage();
        LoadRois();
    }

    /// <summary>页面卸载时解除。</summary>
    public void DetachEditor()
    {
        if (_editor != null) _editor.ImageViewer.WindowInitialized -= OnWindowInitialized;
        _editor = null;
    }

    private void OnWindowInitialized(object? sender, EventArgs e)
    {
        // 首次显示时窗口可能还没建好、底图没画上；窗口（重新）建好后补一次
        if (_editor != null && HasImage && Session.GetImage(ImageId) is { } img) _editor.LoadImage(img);
    }

    private void LoadImage()
    {
        if (_editor == null) return;
        _loadedImageHash = Session.GetState(ImageId).ContentHash;
        if (HasImage && Session.GetImage(ImageId) is { } img) _editor.LoadImage(img);
        else _editor.ImageViewer.Clear();
    }

    private void LoadRois()
    {
        if (_editor == null) return;
        var rois = Clone(Session.GetRois(EntryId));
        _editor.LoadRois(rois);
        _baseline = Json(rois);
        _loadedRoiHash = State.ContentHash;
    }

    /// <inheritdoc/>
    protected override void OnRefreshed()
    {
        RaisePropertyChanged(nameof(HasImage));
        RaisePropertyChanged(nameof(Hint));
        ApplyCommand.RaiseCanExecuteChanged();
        ConfirmCommand.RaiseCanExecuteChanged();
        if (_editor == null) return;
        if (_loadedImageHash != Session.GetState(ImageId).ContentHash) LoadImage();
        if (_loadedRoiHash != State.ContentHash && !HasUnappliedChanges) LoadRois();
    }

    /// <inheritdoc/>
    public override bool HasUnappliedChanges => _editor != null && Json(_editor.GetCurrentRois()) != _baseline;

    /// <inheritdoc/>
    public override void ApplyPending()
    {
        if (_editor == null) return;
        var rois = Clone(_editor.GetCurrentRois());
        Session.SetRois(EntryId, rois);
        _baseline = Json(rois);
        _loadedRoiHash = Session.GetState(EntryId).ContentHash;
    }

    /// <inheritdoc/>
    public override void DiscardPending() => LoadRois();

    private void ExecuteApply()
    {
        try { ApplyPending(); Host.ShowStatus($"[{State.Title}] 已应用。"); }
        catch (VisionPackageException ex) { _ = Host.AskAsync(ex.Message, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private void ExecuteConfirm()
    {
        try { Session.ConfirmRoi(EntryId); Host.ShowStatus($"[{State.Title}] 位置已确认。"); }
        catch (VisionPackageException ex) { _ = Host.AskAsync(ex.Message, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }

    private static List<VisionRoiConfig> Clone(IReadOnlyList<VisionRoiConfig> rois)
        => JsonSerializer.Deserialize<List<VisionRoiConfig>>(JsonSerializer.Serialize(rois)) ?? [];

    /// <inheritdoc/>
    public override void OnClosed() => DetachEditor();
}

// ── 形状模型 ────────────────────────────────────────────────────────────────

/// <summary>形状模型页：建模参数、生成、在原图（或另选的图）上试找并叠加轮廓。</summary>
public sealed class ShapeModelPageViewModel : EntryPageViewModel
{
    private HalconImageViewer? _viewer;
    private HObject? _testImage;          // 另选的试找图（null = 用包里的原图）
    private HObject? _contours;           // 试找命中的轮廓
    private string _baseline;

    /// <summary>依赖的原图条目 id。</summary>
    public string ImageId { get; }

    /// <summary>依赖的 ROI 条目 id。</summary>
    public string RoiId { get; }

    /// <summary>构造。</summary>
    public ShapeModelPageViewModel(IPackageEditorHost host, string entryId) : base(host, entryId)
    {
        ImageId = Definition!.DependsOn["image"];
        RoiId   = Definition.DependsOn["roi"];
        _options = Session.GetOptions(EntryId);
        _baseline = Json(_options);

        ApplyOptionsCommand  = new DelegateCommand(ExecuteApplyOptions);
        RevertCommand        = new DelegateCommand(DiscardPending);
        GenerateCommand      = new DelegateCommand(ExecuteGenerate);
        FindCommand          = new DelegateCommand(ExecuteFind, () => State.HasContent);
        SelectTestImageCommand = new DelegateCommand(ExecuteSelectTestImage);
        UsePackageImageCommand = new DelegateCommand(() => { SetTestImage(null, null); Render(); });
    }

    private ShapeTemplateCreateOptions _options;
    /// <summary>建模参数（PropertyGrid 直接编辑，改完点「应用」或「生成」）。</summary>
    public ShapeTemplateCreateOptions Options { get => _options; private set => SetProperty(ref _options, value); }

    /// <summary>试找参数。</summary>
    public ShapeMatchOptions MatchOptions { get; } = new() { MinScore = 0.5 };

    /// <summary>试找结果。</summary>
    public ObservableCollection<ShapeMatchResult> Results { get; } = [];

    private string _testImageName = "包里的原图";
    /// <summary>试找用图的名字。</summary>
    public string TestImageName { get => _testImageName; private set => SetProperty(ref _testImageName, value); }

    /// <summary>说明文字。</summary>
    public string Info => $"由 [{Session.GetState(ImageId).Title}] + [{Session.GetState(RoiId).Title}] 按下方建模参数生成，不能手工编辑。";

    /// <summary>应用建模参数（模型变为需重新生成）。</summary>
    public DelegateCommand ApplyOptionsCommand { get; }

    /// <summary>撤销未应用的参数修改。</summary>
    public DelegateCommand RevertCommand { get; }

    /// <summary>生成 / 重新生成。</summary>
    public DelegateCommand GenerateCommand { get; }

    /// <summary>试找。</summary>
    public DelegateCommand FindCommand { get; }

    /// <summary>另选一张图试找。</summary>
    public DelegateCommand SelectTestImageCommand { get; }

    /// <summary>改回用包里的原图试找。</summary>
    public DelegateCommand UsePackageImageCommand { get; }

    /// <summary>由页面 code-behind 注入查看器。</summary>
    public void AttachViewer(HalconImageViewer viewer)
    {
        _viewer = viewer;
        viewer.WindowInitialized += OnWindowInitialized;
        Render();
    }

    /// <summary>页面卸载时解除。</summary>
    public void DetachViewer()
    {
        if (_viewer != null) _viewer.WindowInitialized -= OnWindowInitialized;
        _viewer = null;
    }

    private void OnWindowInitialized(object? sender, EventArgs e) => Render();

    private void Render()
    {
        if (_viewer == null) return;
        HObject? img = null;
        if (_testImage != null) HOperatorSet.CopyObj(_testImage, out img, 1, -1);
        else if (Session.GetState(ImageId).HasContent) img = Session.GetImage(ImageId);
        if (img == null) { _viewer.Clear(); return; }
        _viewer.DisplayImage(img);

        if (_testImage == null && Session.GetRois(RoiId) is { Count: > 0 } rois)
        {
            using HObject region = RoiRegionBuilder.Build(rois);
            _viewer.DisplayOverlay(region, "blue", 1);
        }
        if (_contours != null) _viewer.DisplayOverlay(_contours, "lime green", 2);
    }

    /// <inheritdoc/>
    protected override void OnRefreshed()
    {
        RaisePropertyChanged(nameof(Info));
        FindCommand.RaiseCanExecuteChanged();
        if (!HasUnappliedChanges)
        {
            Options = Session.GetOptions(EntryId);
            _baseline = Json(Options);
        }
    }

    /// <inheritdoc/>
    public override bool HasUnappliedChanges => Json(Options) != _baseline;

    /// <inheritdoc/>
    public override void ApplyPending()
    {
        Session.SetOptions(EntryId, Options);
        _baseline = Json(Options);
    }

    /// <inheritdoc/>
    public override void DiscardPending()
    {
        Options = Session.GetOptions(EntryId);
        _baseline = Json(Options);
    }

    private void ExecuteApplyOptions()
    {
        ApplyPending();
        Host.ShowStatus("建模参数已应用，保存时会自动重新生成模型。");
    }

    private async void ExecuteGenerate()
    {
        if (HasUnappliedChanges) ApplyPending();
        if (await Host.RunBusyAsync("正在生成形状模型…", () => Session.Regenerate(EntryId)))
        {
            ClearMatches();
            Host.ShowStatus("形状模型已生成。");
            Render();
        }
    }

    private async void ExecuteFind()
    {
        HObject? image = null;
        List<ShapeMatchResult> found = [];
        HObject? contours = null;
        bool ok = await Host.RunBusyAsync("正在试找…", () =>
        {
            using var model = Session.LoadShapeModel(EntryId)
                              ?? throw new VisionPackageException("形状模型还没有生成。");
            if (_testImage != null) HOperatorSet.CopyObj(_testImage, out image, 1, -1);
            else image = Session.GetImage(ImageId) ?? throw new VisionPackageException("原图还没有导入。");
            found = ShapeTemplateService.FindMatches(image, model, MatchOptions).ToList();
            HOperatorSet.GenEmptyObj(out contours);
            foreach (var m in found)
            {
                using var c = ShapeTemplateService.GetMatchedContour(model, m);
                HOperatorSet.ConcatObj(contours, c, out HObject joined);
                contours.Dispose();
                contours = joined;
            }
        });
        image?.Dispose();
        if (!ok) { contours?.Dispose(); return; }

        ClearMatches();
        _contours = contours;
        foreach (var m in found) Results.Add(m);
        Host.ShowStatus(found.Count == 0 ? "没有找到匹配。" : $"找到 {found.Count} 个匹配。");
        Render();
    }

    private void ExecuteSelectTestImage()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "选择试找用的图",
            Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            HOperatorSet.ReadImage(out HObject img, dlg.FileName);
            SetTestImage(img, Path.GetFileName(dlg.FileName));
            Render();
        }
        catch (HalconException ex)
        {
            _ = Host.AskAsync($"无法读取图片：{ex.GetErrorMessage()}", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void SetTestImage(HObject? img, string? name)
    {
        _testImage?.Dispose();
        _testImage = img;
        TestImageName = name ?? "包里的原图";
        ClearMatches();
    }

    private void ClearMatches()
    {
        Results.Clear();
        _contours?.Dispose();
        _contours = null;
    }

    /// <inheritdoc/>
    public override void OnClosed()
    {
        DetachViewer();
        ClearMatches();
        _testImage?.Dispose();
        _testImage = null;
    }
}

// ── Data<T> ─────────────────────────────────────────────────────────────────

/// <summary>参数页：按布局声明的类型用 PropertyGrid 编辑，应用 / 撤销。</summary>
public sealed class DataPageViewModel : EntryPageViewModel
{
    private string _baseline;
    private string? _loadedHash;

    /// <summary>构造。</summary>
    public DataPageViewModel(IPackageEditorHost host, string entryId) : base(host, entryId)
    {
        _data = Session.GetData(EntryId);
        _baseline = Json(_data);
        _loadedHash = State.ContentHash;
        ApplyCommand  = new DelegateCommand(ExecuteApply);
        RevertCommand = new DelegateCommand(DiscardPending);
    }

    private object _data;
    /// <summary>编辑中的参数对象。</summary>
    public object Data { get => _data; private set => SetProperty(ref _data, value); }

    /// <summary>数据类型名。</summary>
    public string TypeName => (Definition?.Options as DataEntryOptions)?.DataType.Name ?? "?";

    /// <summary>应用到会话。</summary>
    public DelegateCommand ApplyCommand { get; }

    /// <summary>撤销未应用的修改。</summary>
    public DelegateCommand RevertCommand { get; }

    /// <inheritdoc/>
    protected override void OnRefreshed()
    {
        if (_loadedHash != State.ContentHash && !HasUnappliedChanges) DiscardPending();
    }

    /// <inheritdoc/>
    public override bool HasUnappliedChanges => Json(Data) != _baseline;

    /// <inheritdoc/>
    public override void ApplyPending()
    {
        Session.SetData(EntryId, Data);
        _baseline = Json(Data);
        _loadedHash = Session.GetState(EntryId).ContentHash;
    }

    /// <inheritdoc/>
    public override void DiscardPending()
    {
        Data = Session.GetData(EntryId);
        _baseline = Json(Data);
        _loadedHash = State.ContentHash;
    }

    private void ExecuteApply()
    {
        try { ApplyPending(); Host.ShowStatus($"[{State.Title}] 已应用。"); }
        catch (VisionPackageException ex) { _ = Host.AskAsync(ex.Message, MessageBoxButton.OK, MessageBoxImage.Warning); }
    }
}

// ── 附件 ────────────────────────────────────────────────────────────────────

/// <summary>附件页：导入、导出、文件信息。</summary>
public sealed class FilePageViewModel : EntryPageViewModel
{
    /// <summary>构造。</summary>
    public FilePageViewModel(IPackageEditorHost host, string entryId) : base(host, entryId)
    {
        ImportCommand = new DelegateCommand(ExecuteImport);
        ExportCommand = new DelegateCommand(ExecuteExport, () => State.HasContent);
        UpdateInfo();
    }

    /// <summary>导入附件。</summary>
    public DelegateCommand ImportCommand { get; }

    /// <summary>导出附件。</summary>
    public DelegateCommand ExportCommand { get; }

    private string _info = string.Empty;
    /// <summary>文件信息。</summary>
    public string Info { get => _info; private set => SetProperty(ref _info, value); }

    private string[] Extensions => (Definition?.Options as FileEntryOptions)?.Extensions ?? [];

    /// <inheritdoc/>
    protected override void OnRefreshed()
    {
        ExportCommand.RaiseCanExecuteChanged();
        UpdateInfo();
    }

    private void UpdateInfo()
    {
        var a = Session.GetAttributes(EntryId);
        string allow = Extensions.Length == 0 ? "不限" : string.Join("、", Extensions);
        Info = State.HasContent && a != null
            ? $"文件：{a["fileName"]}\n大小：{(long)a["size"]! / 1024.0:F1} KB\n允许类型：{allow}"
            : $"还没有导入。\n允许类型：{allow}";
    }

    private async void ExecuteImport()
    {
        string filter = Extensions.Length == 0
            ? "所有文件|*.*"
            : $"允许的文件|{string.Join(";", Extensions.Select(e => "*" + e))}|所有文件|*.*";
        var dlg = new Microsoft.Win32.OpenFileDialog { Title = $"导入 [{State.Title}]", Filter = filter };
        if (dlg.ShowDialog() != true) return;
        string path = dlg.FileName;
        if (await Host.RunBusyAsync("正在导入附件…", () => Session.SetFile(EntryId, path)))
            Host.ShowStatus($"[{State.Title}] 已导入。");
    }

    private void ExecuteExport()
    {
        string? src = Session.GetFilePath(EntryId);
        if (src == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog { Title = $"导出 [{State.Title}]", FileName = Path.GetFileName(src) };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.Copy(src, dlg.FileName, overwrite: true);
            Host.ShowStatus($"已导出到 {dlg.FileName}");
        }
        catch (Exception ex)
        {
            _ = Host.AskAsync($"导出失败：{ex.Message}", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}

// ── 只读信息 / 自定义类型 ────────────────────────────────────────────────────

/// <summary>只读信息页：多余条目、或自定义类型没有编辑页时显示。</summary>
public sealed class InfoPageViewModel : EntryPageViewModel
{
    /// <summary>构造。</summary>
    public InfoPageViewModel(IPackageEditorHost host, string entryId, string? message = null) : base(host, entryId)
        => Message = message ?? (State.Status == EntryStatus.Extra
            ? "这个条目在当前布局里已经没有了，删除后才能保存。"
            : "这个条目类型没有编辑页。");

    /// <summary>说明。</summary>
    public string Message { get; }
}

/// <summary>自定义条目类型的编辑页：按 <see cref="IPackageEntryKind.EditorViewName"/> 从容器解析视图。</summary>
public sealed class CustomPageViewModel : EntryPageViewModel
{
    private readonly IVisionPackageEntryEditor? _editor;

    /// <summary>构造。</summary>
    public CustomPageViewModel(IPackageEditorHost host, string entryId, string viewName) : base(host, entryId)
    {
        try
        {
            View = ContainerLocator.Container.Resolve<object>(viewName);
            _editor = View as IVisionPackageEntryEditor
                      ?? (View as FrameworkElement)?.DataContext as IVisionPackageEntryEditor;
            _editor?.Attach(Session, EntryId);
        }
        catch (Exception ex)
        {
            View = $"编辑页 \"{viewName}\" 加载失败：{ex.Message}";
        }
    }

    /// <summary>编辑页视图（或加载失败时的说明文字）。</summary>
    public object View { get; }

    /// <inheritdoc/>
    public override void OnClosed() => _editor?.Detach();
}
