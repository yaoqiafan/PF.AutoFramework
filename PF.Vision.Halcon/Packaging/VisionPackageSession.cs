using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Vision.Halcon.Models;
using PF.Vision.Halcon.Services;

namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// 视觉资产包的编辑会话：新建或打开一个包后的可编辑状态，只存在于内存 + 会话临时目录里。
/// <see cref="Save"/> 才写回 <c>.vpk</c>；不保存直接 <see cref="Dispose"/> 即放弃本次全部修改。
///
/// <para><b>依赖与状态</b>：每个条目记录生成/绘制时各依赖的指纹（<c>inputs</c>），与依赖当前的指纹比较得出
/// <see cref="EntryStatus"/>，并沿依赖向下游传播。派生条目（形状模型）过期可以 <see cref="Regenerate"/>；
/// 源条目（ROI）过期只能人工 <see cref="ConfirmRoi"/> 或重画。</para>
///
/// <para><b>保存</b>：先自动生成所有过期/未生成的派生条目，再整体校验，有任何阻止保存的问题就抛
/// <see cref="VisionPackageValidationException"/>，原文件不受影响；通过后写到同目录临时文件再替换原文件，
/// 修订号 +1。磁盘上的包因此永远是一次成功保存的完整结果。</para>
///
/// <para>非线程安全：同一会话的方法须串行调用。导入大图、生成模型、保存比较耗时，界面应放到后台线程。</para>
/// </summary>
public sealed class VisionPackageSession : IDisposable
{
    /// <summary>派生条目 <c>inputs</c> 里记录生成参数指纹用的保留键。</summary>
    private const string OptionsInputKey = "@options";

    private readonly string _work;
    private readonly Dictionary<string, SessionEntry> _entries = new(StringComparer.Ordinal);
    private readonly Dictionary<string, HObject> _imageCache = new(StringComparer.Ordinal);
    private readonly List<ImportImageResult> _pendingImports = [];
    private bool _disposed;

    /// <summary>会话使用的布局。</summary>
    public VisionPackageLayout Layout { get; }

    /// <summary>最后一次打开或保存的文件路径；新建且未保存为 null。</summary>
    public string? FilePath { get; private set; }

    /// <summary>磁盘上最后一次保存的修订号；新建为 0。</summary>
    public int Revision { get; private set; }

    /// <summary>最后一次保存的时间。</summary>
    public DateTimeOffset? SavedAt { get; private set; }

    /// <summary>最后一次保存的用户。</summary>
    public string? SavedBy { get; private set; }

    /// <summary>打开的包原来的布局版本（新建为当前布局版本）。与 <see cref="VisionPackageLayout.Version"/> 不同说明需要升级。</summary>
    public int LoadedLayoutVersion { get; private set; }

    /// <summary>有未保存的修改。</summary>
    public bool IsDirty { get; private set; }

    /// <summary>任何修改（含保存）之后触发，编辑器据此刷新树与校验栏。</summary>
    public event EventHandler? Changed;

    internal VisionPackageSession(VisionPackageLayout layout)
    {
        Layout = layout;
        LoadedLayoutVersion = layout.Version;
        _work = Path.Combine(Path.GetTempPath(), "PF.VisionPackage", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_work);
    }

    internal static VisionPackageSession OpenFile(string path, VisionPackageLayout layout)
    {
        string name = Path.GetFileNameWithoutExtension(path);
        if (!File.Exists(path)) throw new VisionPackageException($"视觉资产包文件不存在：{path}");

        var s = new VisionPackageSession(layout);
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var me = zip.GetEntry(PackageManifest.EntryName)
                     ?? throw new VisionPackageException($"[{name}] 不是视觉资产包（缺少 manifest.json）。");
            string json;
            using (var r = new StreamReader(me.Open())) json = r.ReadToEnd();
            var m = PackageManifest.FromJson(json, name);

            if (m.Layout.Id != layout.Id)
                throw new VisionPackageException($"视觉资产包 [{name}] 的布局是 {m.Layout.Id}，当前需要 {layout.Id}，不能用这个布局打开。");

            foreach (var e in m.Entries)
            {
                PackageZip.CheckRelative(e.Path, name);
                var ze = zip.GetEntry(e.Path)
                         ?? throw new VisionPackageException($"视觉资产包 [{name}] 缺少条目 [{e.Title}] 的数据文件 {e.Path}，包可能已损坏。");
                string full = s.FullPath(e.Path);
                Directory.CreateDirectory(Path.GetDirectoryName(full)!);
                ze.ExtractToFile(full, overwrite: true);
                if (PackageHash.OfFile(full) != e.ContentHash)
                    throw new VisionPackageException($"视觉资产包 [{name}] 的条目 [{e.Title}] 数据校验失败，包可能已损坏。");

                var def = layout.Find(e.Id);
                s._entries[e.Id] = new SessionEntry(e.Id, e.Kind)
                {
                    Path = e.Path,
                    ContentHash = e.ContentHash,
                    Inputs = e.Inputs ?? [],
                    Attrs = e.Attrs ?? [],
                    ManifestTitle = e.Title,
                    ManifestDependsOn = e.DependsOn ?? [],
                    KindMismatch = def != null && def.Kind != e.Kind,
                };
            }

            s.FilePath = path;
            s.Revision = m.Revision;
            s.SavedAt = m.SavedAt;
            s.SavedBy = m.SavedBy;
            s.LoadedLayoutVersion = m.Layout.Version;
            return s;
        }
        catch
        {
            s.Dispose();
            throw;
        }
    }

    // ── 状态 ─────────────────────────────────────────────────────────────────

    /// <summary>全部条目的当前状态：先按布局声明顺序，再接布局里没有的多余条目。</summary>
    public IReadOnlyList<PackageEntryState> Entries
    {
        get
        {
            var map = ComputeStates();
            var list = Layout.Entries.Select(d => map[d.Id]).ToList();
            list.AddRange(map.Values.Where(st => Layout.Find(st.Id) == null));
            return list;
        }
    }

    /// <summary>取单个条目的状态。</summary>
    public PackageEntryState GetState(string id)
        => ComputeStates().TryGetValue(id, out var st) ? st : throw new VisionPackageException($"没有条目 \"{id}\"。");

    /// <summary>条目属性的拷贝（界面显示用，比如原图尺寸、附件文件名）；没有该条目返回 null。</summary>
    public JsonObject? GetAttributes(string id)
        => _entries.GetValueOrDefault(id) is { } e ? (JsonObject)e.Attrs.DeepClone() : null;

    /// <summary>直接或间接依赖 <paramref name="id"/> 且已有数据的条目（删除/替换前提示用）。</summary>
    public IReadOnlyList<PackageEntryState> GetDependents(string id)
    {
        var result = new List<string>();
        var queue = new Queue<string>([id]);
        while (queue.Count > 0)
        {
            string cur = queue.Dequeue();
            foreach (var d in Layout.Entries.Where(d => d.DependsOn.Values.Contains(cur)))
                if (!result.Contains(d.Id)) { result.Add(d.Id); queue.Enqueue(d.Id); }
        }
        var map = ComputeStates();
        return result.Where(i => _entries.GetValueOrDefault(i)?.HasContent == true).Select(i => map[i]).ToList();
    }

    /// <summary>整包校验：列出阻止保存的错误和不阻止保存的提醒。</summary>
    public PackageValidation Validate()
    {
        var issues = new List<PackageIssue>();
        foreach (var st in Entries)
        {
            switch (st.Status)
            {
                case EntryStatus.Missing when st.Required:
                    issues.Add(new(st.Id, st.Title, IssueSeverity.Error, st.IsDerived ? "尚未生成" : "尚未建立"));
                    break;
                case EntryStatus.Invalid:
                    issues.AddRange(st.Reasons.Select(r => new PackageIssue(st.Id, st.Title, IssueSeverity.Error, r)));
                    break;
                case EntryStatus.Stale when st.IsDerived:
                    issues.Add(new(st.Id, st.Title, IssueSeverity.Warning, string.Join("；", st.Reasons) + "（保存时自动重新生成）"));
                    break;
                case EntryStatus.Stale:
                    issues.AddRange(st.Reasons.Select(r => new PackageIssue(st.Id, st.Title, IssueSeverity.Error, r)));
                    break;
                case EntryStatus.Extra:
                    issues.Add(new(st.Id, st.Title, IssueSeverity.Error, "布局中已没有此条目，请删除"));
                    break;
            }
        }
        return new PackageValidation(issues);
    }

    private Dictionary<string, PackageEntryState> ComputeStates()
    {
        var map = new Dictionary<string, PackageEntryState>(StringComparer.Ordinal);
        foreach (var def in Layout.TopologicalOrder)
            map[def.Id] = ComputeState(def, map);

        foreach (var e in _entries.Values.Where(e => Layout.Find(e.Id) == null))
            map[e.Id] = new PackageEntryState(e.Id, e.Kind, e.ManifestTitle ?? e.Id, null, false, false, e.HasContent,
                EntryStatus.Extra, ["布局中已没有此条目，请删除"], e.ManifestDependsOn, e.ContentHash);
        return map;
    }

    private PackageEntryState ComputeState(LayoutEntry def, Dictionary<string, PackageEntryState> done)
    {
        var kind = VisionPackage.GetKind(def.Kind);
        var e = _entries.GetValueOrDefault(def.Id);
        PackageEntryState Make(EntryStatus status, IReadOnlyList<string> reasons)
            => new(def.Id, def.Kind, def.Title, def.Group, def.Required, kind.IsDerived, e?.HasContent == true,
                   status, reasons, def.DependsOn, e?.ContentHash);

        if (e is { KindMismatch: true })
            return Make(EntryStatus.Invalid, [$"包里的类型是 {e.Kind}，与布局（{def.Kind}）不符，请删除后重建"]);
        if (e is not { HasContent: true })
            return Make(EntryStatus.Missing, [def.Required ? "尚未建立" : "尚未建立（可选）"]);

        var invalid = new List<string>();
        var stale = new List<string>();
        foreach (var (role, depId) in def.DependsOn)
        {
            var dep = done[depId];
            switch (dep.Status)
            {
                case EntryStatus.Missing:
                    invalid.Add($"依赖的 [{dep.Title}] 尚未建立"); continue;
                case EntryStatus.Invalid:
                case EntryStatus.Extra:
                    invalid.Add($"依赖的 [{dep.Title}] 无效"); continue;
                case EntryStatus.Stale:
                    stale.Add($"依赖的 [{dep.Title}] 待处理"); continue;
            }
            if (e.Inputs.GetValueOrDefault(role) != _entries[depId].ContentHash)
                stale.Add(kind.IsDerived ? $"需重新生成：[{dep.Title}] 已修改" : $"需确认：[{dep.Title}] 已更换");
        }

        if (invalid.Count == 0)
        {
            var ctx = new EntryContext(this, def, e);
            try { invalid.AddRange(kind.Validate(ctx)); }
            catch (Exception ex) { invalid.Add($"校验出错：{ex.Message}"); }

            if (kind.IsDerived && invalid.Count == 0)
            {
                string? fp = kind.OptionsFingerprint(ctx);
                if (fp != null && e.Inputs.GetValueOrDefault(OptionsInputKey) != fp)
                    stale.Add("需重新生成：生成参数已修改");
            }
        }

        if (invalid.Count > 0) return Make(EntryStatus.Invalid, invalid);
        if (stale.Count > 0) return Make(EntryStatus.Stale, stale);
        return Make(EntryStatus.Ok, []);
    }

    // ── 原图 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 给原图条目导入一张图片：按布局的存储方式生成存储版，并与当前图比对（<see cref="ImportImageResult.Comparison"/>）。
    /// 条目原来没有图、或没有任何已建立的条目依赖它时<b>直接生效</b>；否则返回待确认的结果，
    /// 由界面提示后调用 <see cref="ConfirmImageReplace"/> 生效或 <see cref="CancelImageImport"/> 放弃。
    /// 与当前图是同一个文件时什么都不做（<see cref="ImageImportComparison.SameFile"/>）。
    /// </summary>
    public ImportImageResult ImportImage(string id, string imagePath)
    {
        ThrowIfDisposed();
        var def = RequireDef(id, BuiltInKinds.Image);
        if (!File.Exists(imagePath)) throw new VisionPackageException($"图片文件不存在：{imagePath}");

        string sourceHash = PackageHash.OfFile(imagePath);
        var cur = _entries.GetValueOrDefault(id);
        var dependents = GetDependents(id).Select(d => d.Title).ToList();

        if (cur is { HasContent: true } && cur.Attrs.GetString("sourceHash") == sourceHash)
            return new ImportImageResult(id, ImageImportComparison.SameFile, dependents, applied: false);

        HObject image;
        try { HOperatorSet.ReadImage(out image, imagePath); }
        catch (HalconException ex) { throw new VisionPackageException($"无法读取图片 {Path.GetFileName(imagePath)}：{ex.GetErrorMessage()}"); }

        try
        {
            HOperatorSet.GetImageSize(image, out HTuple w, out HTuple h);
            HOperatorSet.CountChannels(image, out HTuple ch);
            HOperatorSet.GetImageType(image, out HTuple type);

            var opts = ImageKind.OptionsOf(def);
            var kind = VisionPackage.GetKind(def.Kind);
            string rel = kind.StoragePath(def, Path.GetFileName(imagePath));
            string pending = Path.Combine(_work, "_pending", Guid.NewGuid().ToString("N") + Path.GetExtension(rel));
            Directory.CreateDirectory(Path.GetDirectoryName(pending)!);
            ImageKind.WriteStored(image, imagePath, opts, pending);

            // 缓存用存储版的像素：无损方式下就是读入的这张图；有损（JPEG）要重新解码存储版，
            // 保证显示、画 ROI、生成模型用的都是包里那份
            if (opts.Storage == ImageStorage.Jpeg)
            {
                image.Dispose();
                HOperatorSet.ReadImage(out image, pending);
            }

            var attrs = new JsonObject
            {
                ["sourceName"] = Path.GetFileName(imagePath),
                ["sourceHash"] = sourceHash,
                ["width"] = w.I,
                ["height"] = h.I,
                ["channels"] = ch.I,
                ["pixelType"] = type.S,
                ["storage"] = opts.Storage.ToString(),
            };

            ImageImportComparison cmp;
            if (cur is not { HasContent: true }) cmp = ImageImportComparison.New;
            else
            {
                var a = cur.Attrs;
                bool sameSize = a.GetInt("width") == w.I && a.GetInt("height") == h.I
                                && a.GetInt("channels") == ch.I && a.GetString("pixelType") == type.S;
                cmp = sameSize ? ImageImportComparison.ContentChanged : ImageImportComparison.SizeChanged;
            }

            var result = new ImportImageResult(id, cmp, dependents, applied: false)
            {
                PendingPath = pending,
                PendingRelativePath = rel,
                PendingImage = image,
                PendingAttrs = attrs,
            };

            if (cmp == ImageImportComparison.New || dependents.Count == 0) ApplyImport(result);
            else _pendingImports.Add(result);
            return result;
        }
        catch
        {
            image.Dispose();
            throw;
        }
    }

    /// <summary>确认替换原图（<see cref="ImportImageResult.RequiresConfirmation"/> 为 true 时）。</summary>
    public void ConfirmImageReplace(ImportImageResult result)
    {
        ThrowIfDisposed();
        if (!_pendingImports.Remove(result))
            throw new VisionPackageException("这次导入已经生效或已放弃，不能再确认。");
        ApplyImport(result);
    }

    /// <summary>放弃一次待确认的导入。</summary>
    public void CancelImageImport(ImportImageResult result)
    {
        if (!_pendingImports.Remove(result)) return;
        result.PendingImage?.Dispose();
        TryDelete(result.PendingPath);
    }

    private void ApplyImport(ImportImageResult r)
    {
        var e = GetOrCreate(r.EntryId);
        if (e.HasContent && e.Path != r.PendingRelativePath) TryDelete(FullPath(e.Path!));

        string full = FullPath(r.PendingRelativePath!);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.Move(r.PendingPath!, full, overwrite: true);

        e.Path = r.PendingRelativePath;
        e.ContentHash = PackageHash.OfFile(full);
        e.Attrs = r.PendingAttrs!;

        if (_imageCache.Remove(r.EntryId, out var old)) old.Dispose();
        _imageCache[r.EntryId] = r.PendingImage!;
        r.PendingImage = null;
        r.Applied = true;
        Touch();
    }

    /// <summary>取原图（解码后的存储版）的一份拷贝，所有权归调用方，用完 Dispose。条目没有图时返回 null。</summary>
    public HObject? GetImage(string id)
    {
        ThrowIfDisposed();
        RequireDef(id, BuiltInKinds.Image);
        if (_entries.GetValueOrDefault(id) is not { HasContent: true }) return null;
        HOperatorSet.CopyObj(GetCachedImage(id), out HObject copy, 1, -1);
        return copy;
    }

    internal HObject GetCachedImage(string id)
    {
        if (_imageCache.TryGetValue(id, out var img)) return img;
        var e = _entries.GetValueOrDefault(id);
        if (e is not { HasContent: true }) throw new VisionPackageException($"原图 [{id}] 还没有导入。");
        HOperatorSet.ReadImage(out img, FullPath(e.Path!));
        _imageCache[id] = img;
        return img;
    }

    // ── ROI ──────────────────────────────────────────────────────────────────

    /// <summary>设置 ROI（原图坐标）。会记下当前原图的指纹与尺寸，之后原图更换时据此判断需确认还是需重画。</summary>
    public void SetRois(string id, IReadOnlyList<VisionRoiConfig> rois)
    {
        ThrowIfDisposed();
        var def = RequireDef(id, BuiltInKinds.Roi);
        string imageId = def.DependsOn[RoiKind.ImageRole];
        var img = _entries.GetValueOrDefault(imageId);
        if (img is not { HasContent: true })
            throw new VisionPackageException($"请先导入 [{Layout.Find(imageId)!.Title}]，再画 [{def.Title}]。");

        var e = GetOrCreate(id);
        string rel = VisionPackage.GetKind(def.Kind).StoragePath(def, null);
        string full = FullPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        RoiKind.Write(full, rois);

        var bbox = RoiKind.BoundingBox(rois);
        e.Path = rel;
        e.ContentHash = PackageHash.OfFile(full);
        e.Inputs = new() { [RoiKind.ImageRole] = img.ContentHash! };
        e.Attrs = new JsonObject
        {
            ["imageWidth"] = img.Attrs.GetInt("width"),
            ["imageHeight"] = img.Attrs.GetInt("height"),
            ["bbox"] = bbox == null ? null : new JsonArray(bbox.Select(v => (JsonNode)v).ToArray()),
        };
        Touch();
    }

    /// <summary>取 ROI，没有返回空列表。</summary>
    public IReadOnlyList<VisionRoiConfig> GetRois(string id)
    {
        RequireDef(id, BuiltInKinds.Roi);
        return _entries.GetValueOrDefault(id) is { HasContent: true } e ? RoiKind.Read(FullPath(e.Path!)) : [];
    }

    /// <summary>原图更换（尺寸相同）后，人工确认 ROI 位置仍然正确。尺寸变了的不能确认，只能重画（<see cref="SetRois"/>）。</summary>
    public void ConfirmRoi(string id)
    {
        ThrowIfDisposed();
        var def = RequireDef(id, BuiltInKinds.Roi);
        var e = _entries.GetValueOrDefault(id);
        if (e is not { HasContent: true }) throw new VisionPackageException($"[{def.Title}] 还没有画，无需确认。");
        var img = _entries.GetValueOrDefault(def.DependsOn[RoiKind.ImageRole]);
        if (img is not { HasContent: true }) throw new VisionPackageException($"[{def.Title}] 依赖的原图还没有导入。");
        if (img.Attrs.GetInt("width") != e.Attrs.GetInt("imageWidth") || img.Attrs.GetInt("height") != e.Attrs.GetInt("imageHeight"))
            throw new VisionPackageException($"原图尺寸已变，[{def.Title}] 不能直接确认，请重画。");
        e.Inputs[RoiKind.ImageRole] = img.ContentHash!;
        Touch();
    }

    // ── Data<T> ──────────────────────────────────────────────────────────────

    /// <summary>设置结构化参数。<typeparamref name="T"/> 须与布局声明的类型一致。</summary>
    public void SetData<T>(string id, T value) where T : class => SetData(id, (object)value);

    /// <summary>设置结构化参数（非泛型，供 PropertyGrid 等按布局类型编辑的场景）。</summary>
    public void SetData(string id, object value)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(value);
        var def = RequireDef(id, BuiltInKinds.Data);
        var type = DataKind.TypeOf(def);
        if (!type.IsInstanceOfType(value))
            throw new VisionPackageException($"[{def.Title}] 的数据类型是 {type.Name}，传入的是 {value.GetType().Name}。");

        var e = GetOrCreate(id);
        string rel = VisionPackage.GetKind(def.Kind).StoragePath(def, null);
        string full = FullPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, JsonSerializer.Serialize(value, type, KindJson.Options));
        e.Path = rel;
        e.ContentHash = PackageHash.OfFile(full);
        e.Attrs = new JsonObject { ["type"] = type.FullName };
        Touch();
    }

    /// <summary>取结构化参数（宽松反序列化）；没有数据时返回新建的默认对象。</summary>
    public T GetData<T>(string id) where T : class, new() => (T)GetData(id);

    /// <summary>取结构化参数（非泛型）；没有数据时返回布局类型的默认对象。</summary>
    public object GetData(string id)
    {
        var def = RequireDef(id, BuiltInKinds.Data);
        var type = DataKind.TypeOf(def);
        return _entries.GetValueOrDefault(id) is { HasContent: true } e
            ? DataKind.Deserialize(File.ReadAllText(FullPath(e.Path!)), type)
            : Activator.CreateInstance(type)!;
    }

    // ── 附件 ─────────────────────────────────────────────────────────────────

    /// <summary>导入附件，原样保存。</summary>
    public void SetFile(string id, string filePath)
    {
        ThrowIfDisposed();
        var def = RequireDef(id, BuiltInKinds.File);
        if (!File.Exists(filePath)) throw new VisionPackageException($"文件不存在：{filePath}");

        var e = GetOrCreate(id);
        if (e.HasContent) TryDelete(FullPath(e.Path!));
        string rel = VisionPackage.GetKind(def.Kind).StoragePath(def, Path.GetFileName(filePath));
        string full = FullPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.Copy(filePath, full, overwrite: true);
        e.Path = rel;
        e.ContentHash = PackageHash.OfFile(full);
        e.Attrs = new JsonObject { ["fileName"] = Path.GetFileName(filePath), ["size"] = new FileInfo(full).Length };
        Touch();
    }

    /// <summary>附件在会话临时目录里的路径（查看用，不要修改），没有返回 null。</summary>
    public string? GetFilePath(string id)
    {
        RequireDef(id, BuiltInKinds.File);
        return _entries.GetValueOrDefault(id) is { HasContent: true } e ? FullPath(e.Path!) : null;
    }

    // ── 派生条目 ─────────────────────────────────────────────────────────────

    /// <summary>设置形状模型的建模参数（之后模型变为需重新生成）。</summary>
    public void SetOptions(string id, ShapeTemplateCreateOptions options)
    {
        ThrowIfDisposed();
        RequireDef(id, BuiltInKinds.ShapeModel);
        ShapeModelKind.SetOptions(GetOrCreate(id).Attrs, options);
        Touch();
    }

    /// <summary>取形状模型当前的建模参数（没设置过取布局默认值），返回的是拷贝。</summary>
    public ShapeTemplateCreateOptions GetOptions(string id)
    {
        var def = RequireDef(id, BuiltInKinds.ShapeModel);
        return ShapeModelKind.GetOptions(def, _entries.GetValueOrDefault(id)?.Attrs ?? []);
    }

    /// <summary>读出会话里当前的形状模型（编辑器"试找"用），返回句柄（用完 Dispose）；还没生成时返回 null。</summary>
    public ShapeTemplateHandle? LoadShapeModel(string id)
    {
        ThrowIfDisposed();
        RequireDef(id, BuiltInKinds.ShapeModel);
        if (_entries.GetValueOrDefault(id) is not { HasContent: true } e) return null;
        HOperatorSet.ReadShapeModel(FullPath(e.Path!), out HTuple modelId);
        return new ShapeTemplateHandle(modelId);
    }

    /// <summary>
    /// 生成/重新生成一个派生条目，必要时先生成它依赖的派生条目。依赖的源条目不是 <see cref="EntryStatus.Ok"/>
    /// （未建立、无效、待确认）时抛异常说明原因。生成失败时该条目被清空并抛异常。
    /// </summary>
    public void Regenerate(string id)
    {
        ThrowIfDisposed();
        var def = Layout.Find(id) ?? throw new VisionPackageException($"布局里没有条目 \"{id}\"。");
        if (!VisionPackage.GetKind(def.Kind).IsDerived)
            throw new VisionPackageException($"[{def.Title}] 不是派生条目，不能生成。");
        RegenerateCore(def);
        Touch();
    }

    /// <summary>按依赖顺序生成所有过期或尚未生成、且依赖都正常的派生条目；依赖有问题的跳过（留给校验报告）。</summary>
    public void RegenerateAllStale()
    {
        ThrowIfDisposed();
        bool any = false;
        foreach (var def in Layout.TopologicalOrder)
        {
            if (!VisionPackage.GetKind(def.Kind).IsDerived) continue;
            var st = GetState(def.Id);
            bool depsOk = def.DependsOn.Values.All(d => GetState(d).Status == EntryStatus.Ok);
            if (depsOk && st.Status is EntryStatus.Stale or EntryStatus.Missing)
            {
                RegenerateCore(def);
                any = true;
            }
        }
        if (any) Touch();
    }

    private void RegenerateCore(LayoutEntry def)
    {
        foreach (var depId in def.DependsOn.Values)
        {
            var depDef = Layout.Find(depId)!;
            var dep = GetState(depId);
            if (VisionPackage.GetKind(depDef.Kind).IsDerived && dep.Status is EntryStatus.Stale or EntryStatus.Missing)
                RegenerateCore(depDef);
            else if (dep.Status != EntryStatus.Ok)
                throw new VisionPackageException($"不能生成 [{def.Title}]：依赖的 [{dep.Title}] {string.Join("；", dep.Reasons)}");
        }

        var kind = VisionPackage.GetKind(def.Kind);
        var e = GetOrCreate(def.Id);
        string rel = kind.StoragePath(def, null);
        string tmp = Path.Combine(_work, "_pending", Guid.NewGuid().ToString("N") + Path.GetExtension(rel));
        Directory.CreateDirectory(Path.GetDirectoryName(tmp)!);
        var ctx = new EntryContext(this, def, e, tmp);
        try
        {
            kind.Generate(ctx);
        }
        catch (Exception ex)
        {
            TryDelete(tmp);
            if (e.HasContent) TryDelete(FullPath(e.Path!));
            e.Path = null;
            e.ContentHash = null;
            Touch();
            string msg = ex is HalconException hex ? hex.GetErrorMessage() : ex.Message;
            throw new VisionPackageException($"生成 [{def.Title}] 失败：{msg}", ex);
        }

        string full = FullPath(rel);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.Move(tmp, full, overwrite: true);
        e.Path = rel;
        e.ContentHash = PackageHash.OfFile(full);
        e.Inputs = def.DependsOn.ToDictionary(kv => kv.Key, kv => _entries[kv.Value].ContentHash!);
        if (kind.OptionsFingerprint(ctx) is { } fp) e.Inputs[OptionsInputKey] = fp;
    }

    // ── 删除 ─────────────────────────────────────────────────────────────────

    /// <summary>删除条目的数据（布局条目变为未建立，多余条目彻底移除）。下游条目会变为无效，删除前可用 <see cref="GetDependents"/> 提示。</summary>
    public void Remove(string id)
    {
        ThrowIfDisposed();
        if (!_entries.Remove(id, out var e)) return;
        if (e.HasContent) TryDelete(FullPath(e.Path!));
        if (_imageCache.Remove(id, out var img)) img.Dispose();
        Touch();
    }

    // ── 保存 ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// 保存：自动生成派生条目 → 校验（不通过抛 <see cref="VisionPackageValidationException"/>，原文件不动）→
    /// 写同目录临时文件 → 替换 <paramref name="path"/>。修订号 +1，记录保存时间与 <paramref name="savedBy"/>。
    /// </summary>
    /// <param name="path">目标 <c>.vpk</c> 路径（可用 <see cref="VisionPackage.PathOf"/> 按名字拼）。</param>
    /// <param name="savedBy">当前登录用户。</param>
    public void Save(string path, string? savedBy)
    {
        ThrowIfDisposed();
        RegenerateAllStale();
        var validation = Validate();
        if (!validation.CanSave) throw new VisionPackageValidationException(validation);

        var now = DateTimeOffset.Now;
        var manifest = new PackageManifest
        {
            Layout = new ManifestLayoutRef { Id = Layout.Id, Version = Layout.Version },
            Revision = Revision + 1,
            SavedAt = now,
            SavedBy = savedBy,
        };
        foreach (var def in Layout.Entries)
        {
            if (_entries.GetValueOrDefault(def.Id) is not { HasContent: true } e) continue;
            manifest.Entries.Add(new ManifestEntry
            {
                Id = def.Id,
                Kind = def.Kind,
                Title = def.Title,
                Path = e.Path!,
                ContentHash = e.ContentHash!,
                DependsOn = def.DependsOn.Count > 0 ? new Dictionary<string, string>(def.DependsOn) : null,
                Inputs = e.Inputs.Count > 0 ? new Dictionary<string, string>(e.Inputs) : null,
                Attrs = e.Attrs.Count > 0 ? (JsonObject)e.Attrs.DeepClone() : null,
            });
        }

        string dir = Path.GetDirectoryName(Path.GetFullPath(path))!;
        Directory.CreateDirectory(dir);
        string tmp = Path.Combine(dir, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var fs = new FileStream(tmp, FileMode.CreateNew))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                var me = zip.CreateEntry(PackageManifest.EntryName, CompressionLevel.Optimal);
                using (var w = new StreamWriter(me.Open())) w.Write(manifest.ToJson());
                foreach (var m in manifest.Entries)
                {
                    string full = FullPath(m.Path);
                    zip.CreateEntryFromFile(full, m.Path, PackageZip.LevelFor(full, new FileInfo(full).Length));
                }
            }
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            TryDelete(tmp);
            if (ex is VisionPackageException) throw;
            throw new VisionPackageException($"保存视觉资产包失败：{ex.Message}", ex);
        }

        FilePath = path;
        Revision = manifest.Revision;
        SavedAt = now;
        SavedBy = savedBy;
        LoadedLayoutVersion = Layout.Version;
        IsDirty = false;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    // ── 内部 ─────────────────────────────────────────────────────────────────

    internal string FullPath(string rel) => Path.Combine(_work, rel.Replace('/', Path.DirectorySeparatorChar));

    internal SessionEntry? GetEntry(string id) => _entries.GetValueOrDefault(id);

    private LayoutEntry RequireDef(string id, string kind)
    {
        var def = Layout.Find(id) ?? throw new VisionPackageException($"布局 [{Layout.Id}] 里没有条目 \"{id}\"。");
        if (def.Kind != kind) throw new VisionPackageException($"条目 [{def.Title}] 的类型是 {def.Kind}，不是 {kind}。");
        if (_entries.GetValueOrDefault(id) is { KindMismatch: true })
            throw new VisionPackageException($"条目 [{def.Title}] 在包里的类型与布局不符，请先删除。");
        return def;
    }

    private SessionEntry GetOrCreate(string id)
    {
        if (!_entries.TryGetValue(id, out var e))
            _entries[id] = e = new SessionEntry(id, Layout.Find(id)!.Kind);
        return e;
    }

    private void Touch()
    {
        IsDirty = true;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static void TryDelete(string? path)
    {
        try { if (path != null && File.Exists(path)) File.Delete(path); } catch { /* 临时文件，删不掉随会话目录一起清 */ }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    /// <summary>释放缓存的图像并删除会话临时目录。未保存的修改随之丢弃。</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (var img in _imageCache.Values) img.Dispose();
        _imageCache.Clear();
        foreach (var p in _pendingImports) p.PendingImage?.Dispose();
        _pendingImports.Clear();
        try { Directory.Delete(_work, recursive: true); } catch { /* 被占用时留给系统临时目录清理 */ }
    }
}

/// <summary>会话内部的条目数据。</summary>
internal sealed class SessionEntry(string id, string kind)
{
    public string Id { get; } = id;
    public string Kind { get; } = kind;
    public string? Path { get; set; }
    public string? ContentHash { get; set; }
    public Dictionary<string, string> Inputs { get; set; } = [];
    public JsonObject Attrs { get; set; } = [];
    public bool HasContent => Path != null;

    /// <summary>从包里读出、布局里没有的条目用的显示名和依赖。</summary>
    public string? ManifestTitle { get; init; }
    public IReadOnlyDictionary<string, string> ManifestDependsOn { get; init; } = new Dictionary<string, string>();

    /// <summary>包里同 id 条目的类型与布局不一致。</summary>
    public bool KindMismatch { get; init; }
}

/// <summary>导入原图时与当前图的比对结论。</summary>
public enum ImageImportComparison
{
    /// <summary>条目原来没有图。</summary>
    New,

    /// <summary>与当前图是同一个文件，什么都没做。</summary>
    SameFile,

    /// <summary>尺寸相同、内容不同：依赖它的 ROI 保留坐标，需逐个确认位置。</summary>
    ContentChanged,

    /// <summary>尺寸（宽、高、通道、像素类型）不同：依赖它的 ROI 将失效，需重画。</summary>
    SizeChanged,
}

/// <summary><see cref="VisionPackageSession.ImportImage"/> 的结果。</summary>
public sealed class ImportImageResult
{
    internal ImportImageResult(string entryId, ImageImportComparison comparison, IReadOnlyList<string> affected, bool applied)
    {
        EntryId = entryId;
        Comparison = comparison;
        AffectedEntries = affected;
        Applied = applied;
    }

    /// <summary>原图条目 id。</summary>
    public string EntryId { get; }

    /// <summary>与当前图的比对结论。</summary>
    public ImageImportComparison Comparison { get; }

    /// <summary>受影响（依赖这张图、已建立）的条目显示名。</summary>
    public IReadOnlyList<string> AffectedEntries { get; }

    /// <summary>是否已经生效。</summary>
    public bool Applied { get; internal set; }

    /// <summary>是否需要界面提示后确认（<see cref="VisionPackageSession.ConfirmImageReplace"/>）。</summary>
    public bool RequiresConfirmation => !Applied && Comparison is ImageImportComparison.ContentChanged or ImageImportComparison.SizeChanged;

    /// <summary>给界面显示的确认提示。</summary>
    public string ConfirmMessage => Comparison switch
    {
        ImageImportComparison.SizeChanged =>
            $"新图尺寸与原图不同，依赖它的 {AffectedEntries.Count} 个条目将失效需重画：{string.Join("、", AffectedEntries)}。是否替换？",
        ImageImportComparison.ContentChanged =>
            $"新图与原图尺寸相同、内容不同，依赖它的 {AffectedEntries.Count} 个条目将保留坐标，需逐个确认位置：{string.Join("、", AffectedEntries)}。是否替换？",
        ImageImportComparison.SameFile => "与当前图是同一个文件。",
        _ => string.Empty,
    };

    internal string? PendingPath { get; init; }
    internal string? PendingRelativePath { get; init; }
    internal HObject? PendingImage { get; set; }
    internal JsonObject? PendingAttrs { get; init; }
}
