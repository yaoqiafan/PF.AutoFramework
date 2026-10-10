using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// 视觉资产包（<c>.vpk</c>）根目录下 <c>manifest.json</c> 的内容：包的格式版本、布局及版本、修订号、
/// 最后保存信息，以及全部条目的记录。读取一律以 manifest 为准，不按约定路径猜文件。
/// </summary>
public sealed class PackageManifest
{
    /// <summary>格式标识，固定为 <see cref="FormatName"/>。</summary>
    public const string FormatName = "PF.VisionPackage";

    /// <summary>本框架读写的包格式版本。</summary>
    public const int CurrentFormatVersion = 1;

    /// <summary>manifest 在包内的固定路径。</summary>
    public const string EntryName = "manifest.json";

    /// <summary>格式标识。</summary>
    public string Format { get; set; } = FormatName;

    /// <summary>包格式版本（框架层面），读取器只认 <see cref="CurrentFormatVersion"/>。</summary>
    public int FormatVersion { get; set; } = CurrentFormatVersion;

    /// <summary>这个包按哪个布局、哪个布局版本建的。</summary>
    public ManifestLayoutRef Layout { get; set; } = new();

    /// <summary>内容修订号：首次保存为 1，之后每次保存 +1。</summary>
    public int Revision { get; set; }

    /// <summary>最后一次保存的时间。</summary>
    public DateTimeOffset SavedAt { get; set; }

    /// <summary>最后一次保存的用户（调用方传入当前登录用户），可空。</summary>
    public string? SavedBy { get; set; }

    /// <summary>全部条目。</summary>
    public List<ManifestEntry> Entries { get; set; } = [];

    /// <summary>manifest 序列化选项：驼峰命名、缩进、中文不转义；读时大小写不敏感。</summary>
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented               = true,
        DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
        Encoder                     = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    internal string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    internal static PackageManifest FromJson(string json, string displayName)
    {
        PackageManifest? m;
        try { m = JsonSerializer.Deserialize<PackageManifest>(json, JsonOptions); }
        catch (JsonException ex)
        {
            throw new VisionPackageException($"视觉资产包 [{displayName}] 的 manifest.json 无法解析：{ex.Message}");
        }
        if (m == null)
            throw new VisionPackageException($"视觉资产包 [{displayName}] 的 manifest.json 为空。");
        if (m.Format != FormatName)
            throw new VisionPackageException($"[{displayName}] 不是视觉资产包（格式标识为 \"{m.Format}\"）。");
        if (m.FormatVersion != CurrentFormatVersion)
            throw new VisionPackageException(
                $"视觉资产包 [{displayName}] 的格式版本是 {m.FormatVersion}，当前框架只支持 {CurrentFormatVersion}" +
                (m.FormatVersion > CurrentFormatVersion ? "，请升级框架。" : "。"));
        return m;
    }
}

/// <summary>manifest 里的布局引用。</summary>
public sealed class ManifestLayoutRef
{
    /// <summary>布局 id。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>布局版本。</summary>
    public int Version { get; set; }
}

/// <summary>manifest 里的一条条目记录。</summary>
public sealed class ManifestEntry
{
    /// <summary>包内唯一 id，用作文件名和取值的键。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>条目类型（<see cref="IPackageEntryKind.Kind"/>）。</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>显示名。</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>数据文件在包内的相对路径（正斜杠）。</summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>数据文件的 SHA-256（<c>sha256:</c> 前缀 + 小写十六进制）。</summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>依赖：角色 → 条目 id。</summary>
    public Dictionary<string, string>? DependsOn { get; set; }

    /// <summary>生成/绘制时各依赖（及参数）的指纹快照：角色 → 指纹。</summary>
    public Dictionary<string, string>? Inputs { get; set; }

    /// <summary>条目类型自己的属性。</summary>
    public JsonObject? Attrs { get; set; }
}
