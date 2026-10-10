using System.Text.RegularExpressions;
using PF.Vision.Halcon.Models;

namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// 布局（目录配置）：一类视觉资产包里应该有哪些条目、各是什么类型、谁依赖谁、树怎么分组、哪些必填。
/// 由消费项目在代码里用 <see cref="Create"/> 链式声明，<see cref="Builder.Build"/> 时校验
/// （id 唯一且合法、依赖存在、依赖类型匹配角色要求、无环）。
///
/// <para>布局版本的升级规则见设计稿 §6.3：新增必填条目、删除条目、改 id/kind/依赖、改图像存储方式、
/// <c>Data&lt;T&gt;</c> 字段改名或改含义，都必须升 <see cref="Version"/>。生产读取器要求版本完全相等。</para>
///
/// <para>自定义条目类型须先 <see cref="VisionPackage.RegisterKind"/>，再声明用到它的布局。</para>
/// </summary>
public sealed class VisionPackageLayout
{
    /// <summary>布局 id。</summary>
    public string Id { get; }

    /// <summary>布局版本。</summary>
    public int Version { get; }

    /// <summary>全部条目定义，按声明顺序（同时也是一个合法的拓扑顺序之外的显示顺序）。</summary>
    public IReadOnlyList<LayoutEntry> Entries { get; }

    /// <summary>按依赖排好的顺序（被依赖的在前），生成/状态计算都按这个顺序。</summary>
    internal IReadOnlyList<LayoutEntry> TopologicalOrder { get; }

    private readonly Dictionary<string, LayoutEntry> _byId;

    private VisionPackageLayout(string id, int version, List<LayoutEntry> entries, List<LayoutEntry> topo)
    {
        Id = id;
        Version = version;
        Entries = entries;
        TopologicalOrder = topo;
        _byId = entries.ToDictionary(e => e.Id, StringComparer.Ordinal);
    }

    /// <summary>按 id 取条目定义，没有返回 null。</summary>
    public LayoutEntry? Find(string id) => _byId.GetValueOrDefault(id);

    /// <summary>开始声明一个布局。</summary>
    /// <param name="id">布局 id，建议"项目.用途"形式，如 <c>PF.ShapeTemplate</c>。</param>
    /// <param name="version">布局版本，从 1 开始。</param>
    public static Builder Create(string id, int version)
    {
        if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("布局 id 不能为空。", nameof(id));
        if (version < 1) throw new ArgumentOutOfRangeException(nameof(version), "布局版本须 ≥ 1。");
        return new Builder(id, version);
    }

    internal static readonly Regex IdPattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

    /// <summary>布局的链式声明器。</summary>
    public sealed class Builder
    {
        private readonly string _id;
        private readonly int _version;
        private readonly List<LayoutEntry> _entries = [];
        private string? _group;

        internal Builder(string id, int version) { _id = id; _version = version; }

        /// <summary>之后声明的条目都归到这个分组（只影响编辑器树的显示）。传 null 结束分组。</summary>
        public Builder Group(string? name) { _group = name; return this; }

        /// <summary>原图。</summary>
        /// <param name="id">条目 id。</param>
        /// <param name="title">显示名。</param>
        /// <param name="required">是否必填。</param>
        /// <param name="storage">存储方式，默认无损 PNG。</param>
        /// <param name="jpegQuality">仅 <see cref="ImageStorage.Jpeg"/> 时有效，1~100。</param>
        public Builder Image(string id, string title, bool required = true,
                             ImageStorage storage = ImageStorage.Png, int jpegQuality = 90)
            => Entry(BuiltInKinds.Image, id, title, required, null,
                     new ImageEntryOptions(storage, Math.Clamp(jpegQuality, 1, 100)));

        /// <summary>画在某张原图上的一组 ROI（原图坐标）。</summary>
        public Builder Roi(string id, string title, string image, bool required = true)
            => Entry(BuiltInKinds.Roi, id, title, required,
                     new Dictionary<string, string> { [RoiKind.ImageRole] = image }, null);

        /// <summary>形状模型：由原图 + ROI 按建模参数生成（派生条目）。</summary>
        public Builder ShapeModel(string id, string title, string image, string roi,
                                  ShapeTemplateCreateOptions? options = null, bool required = true)
            => Entry(BuiltInKinds.ShapeModel, id, title, required,
                     new Dictionary<string, string> { [ShapeModelKind.ImageRole] = image, [ShapeModelKind.RoiRole] = roi },
                     new ShapeModelEntryOptions(options ?? new ShapeTemplateCreateOptions()));

        /// <summary>结构化参数，按 <typeparamref name="T"/> 序列化（宽松反序列化 + 可选自校验）。</summary>
        /// <param name="editorView">
        /// 可选：自定义编辑页的 Prism 视图名（已用 <c>RegisterForNavigation</c> 注册、实现 <c>IVisionPackageEntryEditor</c>），
        /// 替代编辑器默认的 PropertyGrid 页——比如需要"在参考图上点选"这类项目专属交互时。
        /// </param>
        public Builder Data<T>(string id, string title, bool required = true, string? editorView = null) where T : class, new()
            => Entry(BuiltInKinds.Data, id, title, required, null, new DataEntryOptions(typeof(T)), editorView);

        /// <summary>附件，原样保存。</summary>
        /// <param name="extensions">允许的扩展名（含点，如 <c>.txt</c>），null/空 = 不限。</param>
        public Builder File(string id, string title, string[]? extensions = null, bool required = false)
            => Entry(BuiltInKinds.File, id, title, required, null,
                     new FileEntryOptions(extensions?.Select(x => x.ToLowerInvariant()).ToArray() ?? []));

        /// <summary>自定义条目类型（须先 <see cref="VisionPackage.RegisterKind"/>）。</summary>
        /// <param name="kind">条目类型名。</param>
        /// <param name="id">条目 id。</param>
        /// <param name="title">显示名。</param>
        /// <param name="required">是否必填。</param>
        /// <param name="dependsOn">依赖：角色 → 条目 id。</param>
        /// <param name="options">交给条目类型自己解释的选项。</param>
        /// <param name="editorView">可选：覆盖条目类型默认编辑页的 Prism 视图名（见 <see cref="LayoutEntry.EditorView"/>）。</param>
        public Builder Entry(string kind, string id, string title, bool required = true,
                             IReadOnlyDictionary<string, string>? dependsOn = null, object? options = null,
                             string? editorView = null)
        {
            _entries.Add(new LayoutEntry(id, kind, title, _group, required,
                dependsOn ?? new Dictionary<string, string>(), options) { EditorView = editorView });
            return this;
        }

        /// <summary>校验并生成布局。</summary>
        public VisionPackageLayout Build()
        {
            var errors = new List<string>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var e in _entries)
            {
                if (!IdPattern.IsMatch(e.Id)) errors.Add($"条目 id \"{e.Id}\" 只能包含字母、数字、'.'、'_'、'-'");
                if (!ids.Add(e.Id)) errors.Add($"条目 id \"{e.Id}\" 重复");
            }

            var byId = _entries.GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
            foreach (var e in _entries)
            {
                if (!VisionPackage.TryGetKind(e.Kind, out var kind))
                {
                    errors.Add($"条目 \"{e.Id}\" 的类型 \"{e.Kind}\" 未注册");
                    continue;
                }
                foreach (var role in kind.Roles)
                    if (!e.DependsOn.ContainsKey(role.Name))
                        errors.Add($"条目 \"{e.Id}\" 缺少依赖角色 \"{role.Name}\"");
                foreach (var (role, depId) in e.DependsOn)
                {
                    var roleDef = kind.Roles.FirstOrDefault(r => r.Name == role);
                    if (roleDef == null) { errors.Add($"条目 \"{e.Id}\" 的类型 {e.Kind} 没有依赖角色 \"{role}\""); continue; }
                    if (!byId.TryGetValue(depId, out var dep)) { errors.Add($"条目 \"{e.Id}\" 依赖的 \"{depId}\" 不存在"); continue; }
                    if (!roleDef.AllowedKinds.Contains(dep.Kind))
                        errors.Add($"条目 \"{e.Id}\" 的依赖角色 \"{role}\" 要求类型 {string.Join("/", roleDef.AllowedKinds)}，" +
                                   $"实际 \"{depId}\" 是 {dep.Kind}");
                }
            }

            List<LayoutEntry> topo = [];
            if (errors.Count == 0 && !TrySort(byId, topo, out var cycle))
                errors.Add($"依赖存在环：{cycle}");

            if (errors.Count > 0)
                throw new VisionPackageException($"布局 [{_id}] 声明有误：{string.Join("；", errors)}");

            return new VisionPackageLayout(_id, _version, [.. _entries], topo);
        }

        private bool TrySort(Dictionary<string, LayoutEntry> byId, List<LayoutEntry> result, out string cycle)
        {
            var mark = new Dictionary<string, int>(StringComparer.Ordinal);   // 0 未访问 1 访问中 2 完成
            var stack = new List<string>();
            string? found = null;

            bool Visit(LayoutEntry e)
            {
                switch (mark.GetValueOrDefault(e.Id))
                {
                    case 2: return true;
                    case 1:
                        found = string.Join(" → ", stack.SkipWhile(s => s != e.Id).Append(e.Id));
                        return false;
                }
                mark[e.Id] = 1;
                stack.Add(e.Id);
                foreach (var depId in e.DependsOn.Values)
                    if (!Visit(byId[depId])) return false;
                stack.RemoveAt(stack.Count - 1);
                mark[e.Id] = 2;
                result.Add(e);
                return true;
            }

            foreach (var e in _entries)
                if (!Visit(e)) { cycle = found ?? "?"; return false; }
            cycle = string.Empty;
            return true;
        }
    }
}

/// <summary>布局里的一条条目定义。</summary>
/// <param name="Id">条目 id。</param>
/// <param name="Kind">条目类型。</param>
/// <param name="Title">显示名。</param>
/// <param name="Group">分组（编辑器树的父节点），没有为 null。</param>
/// <param name="Required">是否必填。</param>
/// <param name="DependsOn">依赖：角色 → 条目 id。</param>
/// <param name="Options">交给条目类型解释的选项（内置类型见 <see cref="ImageEntryOptions"/> 等）。</param>
public sealed record LayoutEntry(
    string Id,
    string Kind,
    string Title,
    string? Group,
    bool Required,
    IReadOnlyDictionary<string, string> DependsOn,
    object? Options)
{
    /// <summary>
    /// 覆盖条目类型默认编辑页的 Prism 视图名（null = 用条目类型的默认页）。视图本身或其 DataContext 实现
    /// <c>PF.Modules.Halcon</c> 的 <c>IVisionPackageEntryEditor</c>，编辑器把会话和条目 id 交给它。
    /// </summary>
    public string? EditorView { get; init; }
}

/// <summary>原图的存储方式。</summary>
public enum ImageStorage
{
    /// <summary>转成 PNG（无损，默认）。</summary>
    Png,

    /// <summary>导入的原文件字节原样保存，扩展名沿用原文件。</summary>
    Original,

    /// <summary>转成 LZW 压缩的 TIFF（无损）。</summary>
    Tiff,

    /// <summary>转成 BMP（无损，不压缩）。</summary>
    Bmp,

    /// <summary>转成 JPEG（有损，质量见 <see cref="ImageEntryOptions.JpegQuality"/>）。</summary>
    Jpeg,
}

/// <summary>原图条目的布局选项。</summary>
public sealed record ImageEntryOptions(ImageStorage Storage, int JpegQuality);

/// <summary>形状模型条目的布局选项：建模参数的默认值。</summary>
public sealed record ShapeModelEntryOptions(ShapeTemplateCreateOptions Defaults);

/// <summary><c>Data&lt;T&gt;</c> 条目的布局选项：数据类型。</summary>
public sealed record DataEntryOptions(Type DataType);

/// <summary>附件条目的布局选项：允许的扩展名（小写含点），空 = 不限。</summary>
public sealed record FileEntryOptions(string[] Extensions);
