using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using PF.UI.Infrastructure.PrismBase;
using PF.Vision.Halcon.Packaging;
using Prism.Commands;

namespace PF.Modules.Halcon.ViewModels.VisionPackageEditor;

/// <summary>
/// 视觉资产包（<c>.vpk</c>）编辑器。左侧按布局生成条目树（分组 + 条目，带状态符号），右侧按条目类型切换编辑页，
/// 底部常驻校验栏（点一条跳到对应条目），顶部工具栏导入原图 / 全部重新生成 / 保存。
///
/// <para>DialogParameters 见 <see cref="HalconNavigationConstants.Dialogs.VisionPackageEditor"/>。编辑只在会话里，
/// 点「保存」才写盘（校验不通过不能保存）；有未保存的修改时关闭会提示放弃。</para>
///
/// <para>耗时操作（打开大包、导入原图、生成模型、保存）在后台线程执行并显示忙碌遮罩；遮罩期间不读会话，
/// 结束后统一刷新，保证会话只被一个线程访问。</para>
/// </summary>
public sealed class VisionPackageEditorDialogViewModel : PFDialogViewModelBase, IPackageEditorHost
{
    private VisionPackageSession? _session;
    private VisionPackageLayout? _layout;
    private string? _packagePath;
    private bool _savedThisTime;
    private bool _closeConfirmed;
    private bool _switching;
    private readonly Dictionary<string, PackageTreeNode> _nodeById = new(StringComparer.Ordinal);

    /// <summary>构造。</summary>
    public VisionPackageEditorDialogViewModel()
    {
        Title = "视觉资产包编辑器";
        ImportImageCommand   = new DelegateCommand(ExecuteImportImage, () => _session != null && !IsBusy && ImportTargetId() != null);
        RegenerateAllCommand = new DelegateCommand(ExecuteRegenerateAll, () => _session != null && !IsBusy);
        SaveCommand          = new DelegateCommand(ExecuteSave, () => _session != null && !IsBusy);
        CancelCommand        = new DelegateCommand(ExecuteClose);
        ConfirmCommand       = CancelCommand;
    }

    // ── 绑定属性 ─────────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public VisionPackageSession Session => _session ?? throw new InvalidOperationException("编辑会话尚未打开。");

    /// <inheritdoc/>
    public VisionPackageLayout Layout => _layout ?? throw new InvalidOperationException("布局尚未指定。");

    /// <summary>条目树。</summary>
    public ObservableCollection<PackageTreeNode> Nodes { get; } = [];

    /// <summary>校验问题。</summary>
    public ObservableCollection<PackageIssue> Issues { get; } = [];

    private EntryPageViewModel? _currentPage;
    /// <summary>右侧当前编辑页。</summary>
    public EntryPageViewModel? CurrentPage { get => _currentPage; private set => SetProperty(ref _currentPage, value); }

    private PackageIssue? _selectedIssue;
    /// <summary>校验栏选中的问题：选中即跳到对应条目。</summary>
    public PackageIssue? SelectedIssue
    {
        get => _selectedIssue;
        set
        {
            if (!SetProperty(ref _selectedIssue, value) || value?.EntryId == null) return;
            if (_nodeById.TryGetValue(value.EntryId, out var node))
            {
                foreach (var g in Nodes.Where(g => g.Children.Contains(node))) g.IsExpanded = true;
                node.IsSelected = true;
            }
        }
    }

    private bool _isBusy;
    /// <summary>忙碌中（显示遮罩、禁用工具栏）。</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value)) return;
            ImportImageCommand.RaiseCanExecuteChanged();
            RegenerateAllCommand.RaiseCanExecuteChanged();
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    private string _busyText = string.Empty;
    /// <summary>忙碌遮罩上的文字。</summary>
    public string BusyText { get => _busyText; private set => SetProperty(ref _busyText, value); }

    private string _statusMessage = string.Empty;
    /// <summary>底部状态提示。</summary>
    public string StatusMessage { get => _statusMessage; private set => SetProperty(ref _statusMessage, value); }

    private string _packageName = string.Empty;
    /// <summary>包名（文件名，新建未保存时为提示文字）。</summary>
    public string PackageName { get => _packageName; private set => SetProperty(ref _packageName, value); }

    private string _layoutText = string.Empty;
    /// <summary>布局与版本。</summary>
    public string LayoutText { get => _layoutText; private set => SetProperty(ref _layoutText, value); }

    private string _revisionText = string.Empty;
    /// <summary>修订号与最后保存信息。</summary>
    public string RevisionText { get => _revisionText; private set => SetProperty(ref _revisionText, value); }

    private bool _isDirty;
    /// <summary>有未保存的修改。</summary>
    public bool IsDirty { get => _isDirty; private set => SetProperty(ref _isDirty, value); }

    private string _validationSummary = string.Empty;
    /// <summary>校验栏标题。</summary>
    public string ValidationSummary { get => _validationSummary; private set => SetProperty(ref _validationSummary, value); }

    /// <summary>导入原图（选中的是原图节点就导入它，否则导入布局里第一张原图）。</summary>
    public DelegateCommand ImportImageCommand { get; }

    /// <summary>全部重新生成派生条目。</summary>
    public DelegateCommand RegenerateAllCommand { get; }

    /// <summary>保存。</summary>
    public DelegateCommand SaveCommand { get; }

    // ── 打开 / 关闭 ──────────────────────────────────────────────────────────

    /// <inheritdoc/>
    public override async void OnDialogOpened(IDialogParameters parameters)
    {
        base.OnDialogOpened(parameters);
        _layout = parameters.GetValue<VisionPackageLayout>("Layout");
        if (_layout == null)
        {
            await AskAsync("没有指定视觉资产包的布局（DialogParameters \"Layout\"）。", MessageBoxButton.OK, MessageBoxImage.Error);
            RequestClose.Invoke(ButtonResult.Cancel);
            return;
        }

        _packagePath = parameters.GetValue<string>("PackagePath");
        string? name = parameters.GetValue<string>("PackageName");
        if (string.IsNullOrWhiteSpace(_packagePath) && !string.IsNullOrWhiteSpace(name))
            _packagePath = VisionPackage.PathOf(name);
        string? imagePath = parameters.GetValue<string>("ImagePath");

        bool exists = _packagePath != null && File.Exists(_packagePath);
        if (exists)
        {
            IsBusy = true;
            BusyText = "正在打开视觉资产包…";
            string? error = null;
            try { _session = await Task.Run(() => OpenAndWarmUp(_packagePath!, _layout)); }
            catch (Exception ex) { error = ex.Message; }
            finally { IsBusy = false; }
            if (error != null)
            {
                await AskAsync($"打开视觉资产包失败：{error}", MessageBoxButton.OK, MessageBoxImage.Error);
                RequestClose.Invoke(ButtonResult.Cancel);
                return;
            }
        }
        else
        {
            _session = VisionPackage.Create(_layout);
        }

        _session!.Changed += OnSessionChanged;
        BuildTree();
        Refresh();
        var first = _nodeById.Values.FirstOrDefault();
        if (first != null) first.IsSelected = true;

        if (!exists && !string.IsNullOrWhiteSpace(imagePath) && Layout.Entries.FirstOrDefault(e => e.Kind == BuiltInKinds.Image) is { } img)
            await ImportImageAsync(img.Id, imagePath);
        if (Session.LoadedLayoutVersion != Layout.Version)
            ShowStatus($"这个包按布局 v{Session.LoadedLayoutVersion} 建立，当前布局为 v{Layout.Version}：处理完标红的条目后保存即可升级。");
    }

    /// <summary>底部「关闭」与标题栏 × 共用的关闭流程：经 RequestClose 关闭，结果里带回包路径、修订号、是否保存过。</summary>
    private void ExecuteClose()
    {
        _closeByCommand = true;
        RequestClose.Invoke(BuildResult());
        _closeByCommand = false;   // CanCloseDialog 拒绝（取消放弃）时恢复，下次照常走这里
    }

    private bool _closeByCommand;

    /// <summary>
    /// 后台线程：打开包，并把原图先解码进会话缓存。大图（如 11000×16384）解码要近 1 秒，
    /// 若留到界面线程第一次显示原图时再做，等待遮罩期间界面会卡住一两秒，看起来像卡死。
    /// </summary>
    private static VisionPackageSession OpenAndWarmUp(string path, VisionPackageLayout layout)
    {
        var s = VisionPackage.Open(path, layout);
        try
        {
            foreach (var st in s.Entries.Where(e => e.Kind == BuiltInKinds.Image && e.HasContent && e.Status != EntryStatus.Extra))
                s.GetImage(st.Id)?.Dispose();   // GetImage 解码并缓存，拷贝立刻释放
            return s;
        }
        catch
        {
            s.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public override bool CanCloseDialog()
    {
        if (IsBusy) return false;
        if (_closeConfirmed || _session == null) return true;

        // 标题栏 × 直接关窗口时 Prism 只调本方法、拿不到结果参数：拒绝这次关闭，改走与底部「关闭」相同的流程
        if (!_closeByCommand)
        {
            Application.Current?.Dispatcher.BeginInvoke(ExecuteClose);
            return false;
        }
        bool dirty = Session.IsDirty || CurrentPage?.HasUnappliedChanges == true;
        if (dirty)
        {
            var r = MessageService.ShowMessageAsync("有未保存的修改，关闭后将全部丢弃。确定关闭？", "关闭",
                MessageBoxButton.OKCancel, MessageBoxImage.Warning).GetAwaiter().GetResult();
            if (r != ButtonResult.OK) return false;
        }
        _closeConfirmed = true;
        return true;
    }

    /// <inheritdoc/>
    public override void OnDialogClosed()
    {
        CurrentPage?.OnClosed();
        CurrentPage = null;
        if (_session != null)
        {
            _session.Changed -= OnSessionChanged;
            _session.Dispose();
            _session = null;
        }
    }

    private IDialogResult BuildResult()
    {
        var p = new DialogParameters
        {
            { "PackagePath", _packagePath ?? string.Empty },
            { "Revision", _session?.Revision ?? 0 },
            { "Saved", _savedThisTime },
        };
        return new DialogResult(_savedThisTime ? ButtonResult.OK : ButtonResult.Cancel) { Parameters = p };
    }

    // ── 树与刷新 ─────────────────────────────────────────────────────────────

    private void BuildTree()
    {
        Nodes.Clear();
        _nodeById.Clear();
        var groups = new Dictionary<string, PackageTreeNode>();
        foreach (var st in Session.Entries)
        {
            var node = new PackageTreeNode(st);
            _nodeById[st.Id] = node;
            string? group = st.Status == EntryStatus.Extra ? "多余条目" : st.Group;
            if (group == null) { Nodes.Add(node); continue; }
            if (!groups.TryGetValue(group, out var g))
            {
                groups[group] = g = new PackageTreeNode(group);
                Nodes.Add(g);
            }
            g.Children.Add(node);
        }
    }

    private void OnSessionChanged(object? sender, EventArgs e)
    {
        if (IsBusy) return;     // 后台操作期间不读会话，RunBusyAsync 结束后统一刷新
        var d = Application.Current?.Dispatcher;
        if (d == null || d.CheckAccess()) Refresh();
        else d.BeginInvoke(Refresh);
    }

    private void Refresh()
    {
        if (_session == null) return;

        var states = Session.Entries;
        if (!states.Select(s => s.Id).ToHashSet().SetEquals(_nodeById.Keys))
        {
            string? selected = CurrentPage?.EntryId;
            BuildTree();
            if (selected != null && _nodeById.TryGetValue(selected, out var n)) n.IsSelected = true;
            else
            {
                SwitchPage(null);
                if (_nodeById.Values.FirstOrDefault() is { } first) first.IsSelected = true;
            }
        }
        foreach (var s in states) _nodeById[s.Id].Update(s);

        var v = Session.Validate();
        Issues.Clear();
        foreach (var i in v.Issues) Issues.Add(i);
        int errors = v.Errors.Count();
        ValidationSummary = errors > 0 ? $"校验：{errors} 个问题，当前不能保存"
                          : v.Issues.Count > 0 ? "校验：可以保存（以下提醒保存时自动处理）"
                          : "校验：全部正常，可以保存";

        PackageName  = _packagePath != null ? Path.GetFileName(_packagePath) : "（新建，未保存）";
        Title        = $"视觉资产包编辑器 - {PackageName}";
        LayoutText   = $"布局 {Layout.Id} v{Layout.Version}" +
                       (Session.LoadedLayoutVersion != Layout.Version ? $"（包为 v{Session.LoadedLayoutVersion}，保存后升级）" : string.Empty);
        RevisionText = Session.Revision == 0
            ? "尚未保存"
            : $"修订 {Session.Revision}（{Session.SavedBy ?? "未知用户"} {Session.SavedAt:yyyy-MM-dd HH:mm} 保存）";
        IsDirty = Session.IsDirty;

        CurrentPage?.Refresh();
        ImportImageCommand.RaiseCanExecuteChanged();
    }

    /// <summary>树的选中项变化（code-behind 调用）。切走前处理当前页未应用的修改，取消时恢复原选中项。</summary>
    public async void OnTreeSelectionChanged(PackageTreeNode? node)
    {
        if (_switching || node == null || node.IsGroup || _session == null) return;
        if (node.EntryId == CurrentPage?.EntryId) return;

        var page = CurrentPage;
        if (page != null && page.HasUnappliedChanges)
        {
            _switching = true;
            try
            {
                var r = await AskAsync($"[{page.State.Title}] 有未应用的修改，是否应用？\n是 = 应用后切换；否 = 放弃修改后切换；取消 = 留在当前页。",
                                       MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (r == ButtonResult.Yes && !TryApply(page)) r = ButtonResult.Cancel;
                else if (r == ButtonResult.No) page.DiscardPending();
                if (r == ButtonResult.Cancel)
                {
                    if (_nodeById.TryGetValue(page.EntryId, out var back)) back.IsSelected = true;
                    return;
                }
            }
            finally { _switching = false; }
        }
        SwitchPage(node.EntryId);
    }

    private void SwitchPage(string? entryId)
    {
        CurrentPage?.OnClosed();
        CurrentPage = entryId == null ? null : CreatePage(entryId);
    }

    private EntryPageViewModel CreatePage(string id)
    {
        try
        {
            var st = Session.GetState(id);
            if (st.Status == EntryStatus.Extra) return new InfoPageViewModel(this, id);
            return st.Kind switch
            {
                BuiltInKinds.Image      => new ImagePageViewModel(this, id),
                BuiltInKinds.Roi        => new RoiPageViewModel(this, id),
                BuiltInKinds.ShapeModel => new ShapeModelPageViewModel(this, id),
                BuiltInKinds.Data       => new DataPageViewModel(this, id),
                BuiltInKinds.File       => new FilePageViewModel(this, id),
                _ => VisionPackage.TryGetKind(st.Kind, out var k) && k.EditorViewName is { } view
                        ? new CustomPageViewModel(this, id, view)
                        : new InfoPageViewModel(this, id),
            };
        }
        catch (VisionPackageException ex)
        {
            return new InfoPageViewModel(this, id, ex.Message);
        }
    }

    private bool TryApply(EntryPageViewModel page)
    {
        try { page.ApplyPending(); return true; }
        catch (VisionPackageException ex)
        {
            _ = AskAsync(ex.Message, MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }

    // ── 工具栏 ───────────────────────────────────────────────────────────────

    private string? ImportTargetId()
    {
        if (CurrentPage is ImagePageViewModel ip) return ip.EntryId;
        return _layout?.Entries.FirstOrDefault(e => e.Kind == BuiltInKinds.Image)?.Id;
    }

    private async void ExecuteImportImage()
    {
        string? id = ImportTargetId();
        if (id == null) return;
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = $"导入 [{Session.GetState(id).Title}]",
            Filter = "图像文件|*.png;*.bmp;*.jpg;*.jpeg;*.tif;*.tiff|所有文件|*.*",
        };
        if (dlg.ShowDialog() != true) return;
        await ImportImageAsync(id, dlg.FileName);
    }

    private async void ExecuteRegenerateAll()
    {
        if (await RunBusyAsync("正在重新生成…", () => Session.RegenerateAllStale()))
            ShowStatus("已重新生成所有需要更新的条目。");
    }

    private async void ExecuteSave()
    {
        var page = CurrentPage;
        if (page != null && page.HasUnappliedChanges)
        {
            var r = await AskAsync($"[{page.State.Title}] 有未应用的修改，是否应用后保存？\n是 = 应用后保存；否 = 不应用，直接保存；取消 = 不保存。",
                                   MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (r == ButtonResult.Cancel) return;
            if (r == ButtonResult.Yes && !TryApply(page)) return;
        }

        string? path = _packagePath;
        if (path == null)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "保存视觉资产包",
                Filter = $"视觉资产包|*{VisionPackage.Extension}",
                DefaultExt = VisionPackage.Extension,
                InitialDirectory = VisionPackage.Directory ?? string.Empty,
            };
            if (dlg.ShowDialog() != true) return;
            path = dlg.FileName;
        }

        string? user = UserService?.CurrentUser?.UserName;
        if (await RunBusyAsync("正在保存…", () => Session.Save(path, user)))
        {
            _packagePath = path;
            _savedThisTime = true;
            Refresh();
            ShowStatus($"已保存：{path}（修订 {Session.Revision}）");
        }
    }

    // ── IPackageEditorHost ───────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<bool> RunBusyAsync(string text, Action work)
    {
        if (IsBusy) return false;
        BusyText = text;
        IsBusy = true;
        string? error = null;
        try
        {
            await Task.Run(work);
        }
        catch (VisionPackageValidationException ex)
        {
            error = "校验未通过，不能保存：\n" + string.Join("\n", ex.Validation.Errors.Select(i => "· " + i));
        }
        catch (VisionPackageException ex) { error = ex.Message; }
        catch (Exception ex) { error = $"操作失败：{ex.Message}"; }
        finally
        {
            IsBusy = false;
            Refresh();
        }
        if (error != null) await AskAsync(error, MessageBoxButton.OK, MessageBoxImage.Warning);
        return error == null;
    }

    /// <inheritdoc/>
    public Task<ButtonResult> AskAsync(string message, MessageBoxButton buttons = MessageBoxButton.OKCancel,
                                       MessageBoxImage image = MessageBoxImage.Question)
        => MessageService.ShowMessageAsync(message, "视觉资产包", buttons, image);

    /// <inheritdoc/>
    public void ShowStatus(string message) => StatusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";

    /// <inheritdoc/>
    public async Task ImportImageAsync(string imageEntryId, string path)
    {
        ImportImageResult? r = null;
        if (!await RunBusyAsync("正在导入原图（大图需要几秒）…", () => r = Session.ImportImage(imageEntryId, path)) || r == null)
            return;

        if (r.Comparison == ImageImportComparison.SameFile)
        {
            ShowStatus("与当前原图是同一个文件，未做改动。");
            return;
        }
        if (r.RequiresConfirmation)
        {
            var ans = await AskAsync(r.ConfirmMessage, MessageBoxButton.OKCancel, MessageBoxImage.Warning);
            if (ans == ButtonResult.OK)
            {
                Session.ConfirmImageReplace(r);
                ShowStatus("原图已替换，请处理标为待确认 / 无效的条目。");
            }
            else
            {
                Session.CancelImageImport(r);
                ShowStatus("已放弃替换原图。");
            }
            return;
        }
        ShowStatus($"已导入原图：{Path.GetFileName(path)}");
    }

    /// <inheritdoc/>
    public async Task RemoveEntryAsync(string entryId)
    {
        var st = Session.GetState(entryId);
        var deps = Session.GetDependents(entryId);
        string msg = deps.Count > 0
            ? $"删除 [{st.Title}] 后，以下条目将失效：{string.Join("、", deps.Select(d => d.Title))}。确定删除？"
            : $"确定删除 [{st.Title}] 的数据？";
        if (await AskAsync(msg, MessageBoxButton.OKCancel, MessageBoxImage.Warning) != ButtonResult.OK) return;
        Session.Remove(entryId);
        ShowStatus($"已删除 [{st.Title}]。");
    }
}
