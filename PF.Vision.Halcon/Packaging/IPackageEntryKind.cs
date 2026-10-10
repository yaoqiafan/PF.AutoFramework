using System.Text.Json;
using System.Text.Json.Nodes;
using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;

namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// 条目类型：决定一种条目怎么存、怎么校验、（派生类型）怎么生成、用哪个编辑页。
/// 框架内置 Image / Roi / ShapeModel / Data / File 五种（<see cref="BuiltInKinds"/>），
/// 项目可实现本接口并用 <see cref="VisionPackage.RegisterKind"/> 注册自定义类型。
///
/// <para>指纹统一由框架按数据文件的 SHA-256 计算；依赖指纹的比较、状态传播也由框架做，
/// 条目类型只负责自己内容层面的校验（<see cref="Validate"/>）。</para>
/// </summary>
public interface IPackageEntryKind
{
    /// <summary>类型名，写进 manifest 的 <c>kind</c>。</summary>
    string Kind { get; }

    /// <summary>是否派生条目（由程序根据依赖生成，不能手工编辑）。</summary>
    bool IsDerived { get; }

    /// <summary>需要的依赖角色。</summary>
    IReadOnlyList<DependencyRole> Roles { get; }

    /// <summary>右侧编辑页的 Prism 视图名；null 表示只显示只读信息页。</summary>
    string? EditorViewName { get; }

    /// <summary>新内容写入时，数据文件在包内的相对路径（正斜杠）。</summary>
    /// <param name="definition">布局里的条目定义。</param>
    /// <param name="sourceFileName">导入的源文件名（附件、原样存储的原图会用到），没有为 null。</param>
    string StoragePath(LayoutEntry definition, string? sourceFileName);

    /// <summary>内容层面的校验，返回全部错误（中文）。只在条目有数据、且依赖都存在时调用。</summary>
    IEnumerable<string> Validate(EntryContext context);

    /// <summary>派生条目的生成参数指纹，参数变了条目就过期；没有参数或非派生类型返回 null。</summary>
    string? OptionsFingerprint(EntryContext context);

    /// <summary>生成派生条目：把结果写到 <see cref="EntryContext.OutputPath"/>，可往 <see cref="EntryContext.Attrs"/> 写属性。非派生类型不会被调用。</summary>
    void Generate(EntryContext context);
}

/// <summary>依赖角色：角色名 + 允许指向的条目类型。</summary>
public sealed record DependencyRole(string Name, IReadOnlyList<string> AllowedKinds);

/// <summary>
/// 条目类型回调时拿到的上下文：本条目的定义、数据、属性，以及按角色访问依赖条目的方法。
/// 依赖的图像对象是会话缓存的，<b>只借用、不要 Dispose</b>。
/// </summary>
public sealed class EntryContext
{
    private readonly VisionPackageSession _session;
    private readonly SessionEntry _entry;

    internal EntryContext(VisionPackageSession session, LayoutEntry definition, SessionEntry entry, string? outputPath = null)
    {
        _session = session;
        Definition = definition;
        _entry = entry;
        OutputPath = outputPath;
    }

    /// <summary>条目 id。</summary>
    public string EntryId => Definition.Id;

    /// <summary>布局里的条目定义。</summary>
    public LayoutEntry Definition { get; }

    /// <summary>条目是否已有数据。</summary>
    public bool HasContent => _entry.HasContent;

    /// <summary>条目数据文件的完整路径（会话临时目录里），没有数据为 null。</summary>
    public string? ContentPath => _entry.HasContent ? _session.FullPath(_entry.Path!) : null;

    /// <summary>生成派生条目时要写入的完整路径（仅 <see cref="IPackageEntryKind.Generate"/> 期间有值）。</summary>
    public string? OutputPath { get; }

    /// <summary>条目属性（可读写）。</summary>
    public JsonObject Attrs => _entry.Attrs;

    /// <summary>依赖条目数据文件的完整路径。</summary>
    public string GetDependencyPath(string role) => _session.FullPath(Dependency(role).Path!);

    /// <summary>依赖条目的属性（只读用）。</summary>
    public JsonObject GetDependencyAttrs(string role) => Dependency(role).Attrs;

    /// <summary>依赖的原图（解码后的存储版，会话缓存）。只借用，不要 Dispose。</summary>
    public HObject GetDependencyImage(string role) => _session.GetCachedImage(Definition.DependsOn[role]);

    /// <summary>依赖的 ROI 列表。</summary>
    public IReadOnlyList<VisionRoiConfig> GetDependencyRois(string role)
        => RoiKind.Read(GetDependencyPath(role));

    private SessionEntry Dependency(string role)
    {
        if (!Definition.DependsOn.TryGetValue(role, out var id))
            throw new VisionPackageException($"条目 [{Definition.Title}] 没有依赖角色 \"{role}\"。");
        var dep = _session.GetEntry(id);
        if (dep is not { HasContent: true })
            throw new VisionPackageException($"条目 [{Definition.Title}] 依赖的 [{id}] 还没有建立。");
        return dep;
    }
}

/// <summary>内置条目类型名。</summary>
public static class BuiltInKinds
{
    /// <summary>原图。</summary>
    public const string Image = "Image";
    /// <summary>ROI。</summary>
    public const string Roi = "Roi";
    /// <summary>形状模型。</summary>
    public const string ShapeModel = "ShapeModel";
    /// <summary>结构化参数。</summary>
    public const string Data = "Data";
    /// <summary>附件。</summary>
    public const string File = "File";
}

internal static class KindJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static int GetInt(this JsonObject o, string name) => o[name]?.GetValue<int>() ?? 0;
    public static string? GetString(this JsonObject o, string name) => o[name]?.GetValue<string>();
}
