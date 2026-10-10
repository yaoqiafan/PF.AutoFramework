namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// 视觉资产包（<c>.vpk</c>）入口：包目录配置、条目类型注册、新建/打开编辑会话。
/// 生产只读访问用 <see cref="VisionPackageReader"/>。格式与规则见框架 <c>DOC\视觉资产包设计稿.md</c>。
///
/// <para>典型用法：</para>
/// <code>
/// // 调试/建模
/// using var s = VisionPackage.Create(layout);            // 或 VisionPackage.Open(path, layout)
/// s.ImportImage("Image", @"D:\图\a.tiff");
/// s.SetRois("Region", rois);
/// s.Save(VisionPackage.PathOf("产品A"), userName);       // 不保存直接 Dispose = 放弃修改
///
/// // 生产
/// using var pkg = VisionPackageReader.Open(VisionPackage.PathOf("产品A"), new LayoutRequirement(layout.Id, layout.Version));
/// using var model = pkg.LoadShapeModel("Model");
/// </code>
/// </summary>
public static class VisionPackage
{
    /// <summary>包文件扩展名。</summary>
    public const string Extension = ".vpk";

    /// <summary>
    /// 包存放目录，由消费项目启动时通过 <c>AddVisionPackageServices(directory)</c> 设置一次；
    /// 之后用 <see cref="PathOf"/> 按名字拼路径。未配置时 <see cref="PathOf"/> 抛异常，不静默退化成相对路径。
    /// </summary>
    public static string? Directory { get; set; }

    /// <summary>按名字拼出包的完整路径：<see cref="Directory"/> + 名字 + <c>.vpk</c>。</summary>
    public static string PathOf(string name)
    {
        if (string.IsNullOrWhiteSpace(Directory))
            throw new InvalidOperationException(
                $"{nameof(VisionPackage)}.{nameof(Directory)} 未配置——消费项目需先在启动代码里调用 " +
                "AddVisionPackageServices(directory)，或手动设置该属性。");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("包名字不能为空。", nameof(name));
        return Path.Combine(Directory, name + Extension);
    }

    /// <summary>列出 <see cref="Directory"/> 下所有包的名字（不含扩展名）。目录未配置/不存在时返回空列表。</summary>
    public static IReadOnlyList<string> GetAvailableNames()
    {
        if (string.IsNullOrWhiteSpace(Directory) || !System.IO.Directory.Exists(Directory)) return [];
        return System.IO.Directory.GetFiles(Directory, "*" + Extension)
            .Select(Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    // ── 条目类型注册 ─────────────────────────────────────────────────────────

    private static readonly object KindsLock = new();
    private static readonly Dictionary<string, IPackageEntryKind> Kinds = new(StringComparer.Ordinal)
    {
        [BuiltInKinds.Image]      = new ImageKind(),
        [BuiltInKinds.Roi]        = new RoiKind(),
        [BuiltInKinds.ShapeModel] = new ShapeModelKind(),
        [BuiltInKinds.Data]       = new DataKind(),
        [BuiltInKinds.File]       = new FileKind(),
    };

    /// <summary>注册自定义条目类型（须在声明用到它的布局之前）。同名的内置类型不能覆盖。</summary>
    public static void RegisterKind(IPackageEntryKind kind)
    {
        ArgumentNullException.ThrowIfNull(kind);
        lock (KindsLock)
        {
            if (Kinds.TryGetValue(kind.Kind, out var existing) && existing.GetType().Assembly == typeof(VisionPackage).Assembly)
                throw new InvalidOperationException($"条目类型 \"{kind.Kind}\" 是内置类型，不能覆盖。");
            Kinds[kind.Kind] = kind;
        }
    }

    /// <summary>按名字取条目类型。</summary>
    public static bool TryGetKind(string kind, out IPackageEntryKind result)
    {
        lock (KindsLock) return Kinds.TryGetValue(kind, out result!);
    }

    internal static IPackageEntryKind GetKind(string kind)
        => TryGetKind(kind, out var k) ? k : throw new VisionPackageException($"条目类型 \"{kind}\" 未注册。");

    // ── 会话 ─────────────────────────────────────────────────────────────────

    /// <summary>按布局新建一个空的编辑会话（只在内存中，<see cref="VisionPackageSession.Save"/> 才写盘）。</summary>
    public static VisionPackageSession Create(VisionPackageLayout layout) => new(layout);

    /// <summary>
    /// 打开已有的包进行编辑。包的布局 id 必须与 <paramref name="layout"/> 相同；布局版本可以不同
    /// （布局升级后打开旧包：新增条目为 <see cref="EntryStatus.Missing"/>，删掉的条目为 <see cref="EntryStatus.Extra"/>，
    /// 处理完保存即按新版本写回）。
    /// </summary>
    public static VisionPackageSession Open(string path, VisionPackageLayout layout) => VisionPackageSession.OpenFile(path, layout);
}
