using System.IO.Compression;
using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Vision.Halcon.Services;

namespace PF.Vision.Halcon.Packaging;

/// <summary>程序期望的布局：id 与版本都必须与包完全相等。</summary>
/// <param name="Id">布局 id。</param>
/// <param name="Version">布局版本。</param>
public sealed record LayoutRequirement(string Id, int Version)
{
    /// <summary>按布局对象生成要求。</summary>
    public static LayoutRequirement Of(VisionPackageLayout layout) => new(layout.Id, layout.Version);
}

/// <summary>
/// 视觉资产包的生产只读访问。<see cref="Open"/> 只读 manifest 并校验格式版本、布局 id 与版本（完全相等），
/// 之后按需只解压用到的条目，每次读取都核对内容哈希（防损坏），<c>Data&lt;T&gt;</c> 宽松反序列化后做自校验。
/// 不提供解码原图的方法——需要看图的场景走 <see cref="VisionPackageSession"/>。
/// 只认本地文件，包从哪里来（服务器下载等）不在本类范围内。
/// </summary>
public sealed class VisionPackageReader : IDisposable
{
    private readonly FileStream _stream;
    private readonly ZipArchive _zip;
    private readonly PackageManifest _manifest;
    private readonly Dictionary<string, ManifestEntry> _byId;
    private readonly string _name;

    /// <summary>包文件路径。</summary>
    public string FilePath { get; }

    /// <summary>布局 id。</summary>
    public string LayoutId => _manifest.Layout.Id;

    /// <summary>布局版本。</summary>
    public int LayoutVersion => _manifest.Layout.Version;

    /// <summary>内容修订号（建议记进生产日志）。</summary>
    public int Revision => _manifest.Revision;

    /// <summary>最后保存时间。</summary>
    public DateTimeOffset SavedAt => _manifest.SavedAt;

    /// <summary>最后保存人。</summary>
    public string? SavedBy => _manifest.SavedBy;

    /// <summary>全部条目记录（只读用）。</summary>
    public IReadOnlyList<ManifestEntry> Entries => _manifest.Entries;

    private VisionPackageReader(string path, FileStream stream, ZipArchive zip, PackageManifest manifest)
    {
        FilePath = path;
        _name = Path.GetFileNameWithoutExtension(path);
        _stream = stream;
        _zip = zip;
        _manifest = manifest;
        _byId = manifest.Entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    /// <summary>打开包并校验格式与布局；任一不符抛 <see cref="VisionPackageException"/>（中文原因）。</summary>
    public static VisionPackageReader Open(string path, LayoutRequirement expect)
    {
        ArgumentNullException.ThrowIfNull(expect);
        string name = Path.GetFileNameWithoutExtension(path);
        if (!File.Exists(path)) throw new VisionPackageException($"视觉资产包文件不存在：{path}");

        var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        ZipArchive? zip = null;
        try
        {
            zip = new ZipArchive(fs, ZipArchiveMode.Read);
            var me = zip.GetEntry(PackageManifest.EntryName)
                     ?? throw new VisionPackageException($"[{name}] 不是视觉资产包（缺少 manifest.json）。");
            string json;
            using (var r = new StreamReader(me.Open())) json = r.ReadToEnd();
            var m = PackageManifest.FromJson(json, name);

            if (m.Layout.Id != expect.Id)
                throw new VisionPackageException($"视觉资产包 [{name}] 的布局是 {m.Layout.Id}，程序需要 {expect.Id}。");
            if (m.Layout.Version != expect.Version)
                throw new VisionPackageException(
                    $"视觉资产包 [{name}] 的布局版本是 {m.Layout.Version}，程序需要 {expect.Version}，请在编辑器中打开并重新保存。");
            foreach (var e in m.Entries) PackageZip.CheckRelative(e.Path, name);

            return new VisionPackageReader(path, fs, zip, m);
        }
        catch
        {
            zip?.Dispose();
            fs.Dispose();
            throw;
        }
    }

    /// <summary>包里是否有这个条目。</summary>
    public bool Has(string id) => _byId.ContainsKey(id);

    /// <summary>读形状模型，返回句柄（用完 Dispose）。</summary>
    public ShapeTemplateHandle LoadShapeModel(string id)
    {
        var e = Require(id, BuiltInKinds.ShapeModel);
        string tmp = Path.Combine(Path.GetTempPath(), "PF.VisionPackage_" + Guid.NewGuid().ToString("N") + ".shm");
        try
        {
            ExtractVerified(e, tmp);
            HOperatorSet.ReadShapeModel(tmp, out HTuple modelId);
            return new ShapeTemplateHandle(modelId);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* 临时文件 */ }
        }
    }

    /// <summary>读 ROI 列表（原图坐标）。</summary>
    public IReadOnlyList<VisionRoiConfig> GetRois(string id)
    {
        var e = Require(id, BuiltInKinds.Roi);
        string tmp = Path.GetTempFileName();
        try
        {
            ExtractVerified(e, tmp);
            return RoiKind.Read(tmp);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* 临时文件 */ }
        }
    }

    /// <summary>读结构化参数：宽松反序列化（缺字段取默认值、多字段忽略），<typeparamref name="T"/> 实现了 <see cref="IVisionPackageData"/> 时做自校验，不通过抛异常。</summary>
    public T GetData<T>(string id) where T : class, new()
    {
        var e = Require(id, BuiltInKinds.Data);
        string json;
        using (var ms = new MemoryStream())
        {
            CopyVerified(e, ms);
            json = System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        var value = (T)DataKind.Deserialize(json, typeof(T));
        var errors = DataKind.SelfValidate(value).ToList();
        if (errors.Count > 0)
            throw new VisionPackageException($"视觉资产包 [{_name}] 的 [{e.Title}] 参数不合法：{string.Join("；", errors)}");
        return value;
    }

    /// <summary>把附件解到 <paramref name="destDirectory"/>，返回文件完整路径（保持原文件名）。</summary>
    public string ExtractFile(string id, string destDirectory)
    {
        var e = Require(id, BuiltInKinds.File);
        Directory.CreateDirectory(destDirectory);
        string dest = Path.Combine(destDirectory, Path.GetFileName(e.Path));
        ExtractVerified(e, dest);
        return dest;
    }

    /// <summary>把任意条目的数据文件原样解到 <paramref name="destDirectory"/>，返回完整路径（比如要把 .shm 路径交给 HDevelop 过程时）。</summary>
    public string ExtractRaw(string id, string destDirectory)
    {
        var e = Require(id, null);
        Directory.CreateDirectory(destDirectory);
        string dest = Path.Combine(destDirectory, Path.GetFileName(e.Path));
        ExtractVerified(e, dest);
        return dest;
    }

    private ManifestEntry Require(string id, string? kind)
    {
        if (!_byId.TryGetValue(id, out var e))
            throw new VisionPackageException($"视觉资产包 [{_name}] 里没有条目 \"{id}\"。");
        if (kind != null && e.Kind != kind)
            throw new VisionPackageException($"视觉资产包 [{_name}] 的条目 [{e.Title}] 类型是 {e.Kind}，不是 {kind}。");
        return e;
    }

    private void ExtractVerified(ManifestEntry e, string dest)
    {
        using (var fs = new FileStream(dest, FileMode.Create, FileAccess.ReadWrite))
            CopyVerified(e, fs);
    }

    private void CopyVerified(ManifestEntry e, Stream target)
    {
        var ze = _zip.GetEntry(e.Path)
                 ?? throw new VisionPackageException($"视觉资产包 [{_name}] 缺少条目 [{e.Title}] 的数据文件，包可能已损坏。");
        long start = target.Position;
        using (var src = ze.Open()) src.CopyTo(target);
        target.Position = start;
        string hash = PackageHash.OfStream(target);
        target.Position = start;
        if (hash != e.ContentHash)
            throw new VisionPackageException($"视觉资产包 [{_name}] 的条目 [{e.Title}] 数据校验失败，包可能已损坏。");
    }

    /// <summary>关闭包文件。</summary>
    public void Dispose()
    {
        _zip.Dispose();
        _stream.Dispose();
    }
}
