using System.Text.Json;
using System.Text.Json.Nodes;
using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Vision.Halcon.Internal;
using PF.Vision.Halcon.Models;
using PF.Vision.Halcon.Services;

namespace PF.Vision.Halcon.Packaging;

/// <summary>
/// 原图（源条目）。属性：<c>sourceName</c>、<c>sourceHash</c>（导入时原始文件哈希，只用于"是不是同一个文件"的提示）、
/// <c>width</c>/<c>height</c>/<c>channels</c>/<c>pixelType</c>、<c>storage</c>。
/// 存储版由会话在导入时按 <see cref="ImageEntryOptions"/> 生成，之后显示、画 ROI、生成模型都解码存储版。
/// </summary>
internal sealed class ImageKind : IPackageEntryKind
{
    public string Kind => BuiltInKinds.Image;
    public bool IsDerived => false;
    public IReadOnlyList<DependencyRole> Roles { get; } = [];
    public string? EditorViewName => "VisionPackageImagePage";

    public static ImageEntryOptions OptionsOf(LayoutEntry def)
        => def.Options as ImageEntryOptions ?? new ImageEntryOptions(ImageStorage.Png, 90);

    public string StoragePath(LayoutEntry definition, string? sourceFileName)
    {
        var o = OptionsOf(definition);
        string ext = o.Storage switch
        {
            ImageStorage.Png  => ".png",
            ImageStorage.Tiff => ".tif",
            ImageStorage.Bmp  => ".bmp",
            ImageStorage.Jpeg => ".jpg",
            _ => System.IO.Path.GetExtension(sourceFileName ?? string.Empty).ToLowerInvariant() is { Length: > 1 } e ? e : ".img",
        };
        return $"images/{definition.Id}{ext}";
    }

    /// <summary>按存储方式把图写成存储版文件。<paramref name="image"/> 是从 <paramref name="sourcePath"/> 读出的图。</summary>
    public static void WriteStored(HObject image, string sourcePath, ImageEntryOptions o, string targetPath)
    {
        if (o.Storage == ImageStorage.Original)
        {
            System.IO.File.Copy(sourcePath, targetPath, overwrite: true);
            return;
        }
        // PNG 用最快压缩：11000×16384 灰度图实测默认压缩写 22.6 s / 65 MB，fastest 写 3.0 s / 77 MB，读都约 0.9 s
        string format = o.Storage switch
        {
            ImageStorage.Png  => "png fastest",
            ImageStorage.Tiff => "tiff lzw",
            ImageStorage.Bmp  => "bmp",
            ImageStorage.Jpeg => $"jpeg {o.JpegQuality}",
            _ => throw new ArgumentOutOfRangeException(nameof(o)),
        };
        HOperatorSet.WriteImage(image, format, 0, targetPath);
    }

    public IEnumerable<string> Validate(EntryContext context)
    {
        if (context.Attrs.GetInt("width") <= 0 || context.Attrs.GetInt("height") <= 0)
            yield return "原图尺寸未知，请重新导入";
    }

    public string? OptionsFingerprint(EntryContext context) => null;
    public void Generate(EntryContext context) => throw new NotSupportedException();
}

/// <summary>
/// 一组 ROI（源条目，原图坐标），依赖一张原图。属性：<c>imageWidth</c>/<c>imageHeight</c>（画 ROI 时原图的尺寸）、
/// <c>bbox</c>（全部 Include 区域减 Exclude 后的外接矩形 [row1,col1,row2,col2]）。
/// </summary>
internal sealed class RoiKind : IPackageEntryKind
{
    public const string ImageRole = "image";

    public string Kind => BuiltInKinds.Roi;
    public bool IsDerived => false;
    public IReadOnlyList<DependencyRole> Roles { get; } = [new(ImageRole, [BuiltInKinds.Image])];
    public string? EditorViewName => "VisionPackageRoiPage";

    public string StoragePath(LayoutEntry definition, string? sourceFileName) => $"rois/{definition.Id}.json";

    public static IReadOnlyList<VisionRoiConfig> Read(string path)
        => JsonSerializer.Deserialize<List<VisionRoiConfig>>(System.IO.File.ReadAllText(path), KindJson.Options) ?? [];

    public static void Write(string path, IReadOnlyList<VisionRoiConfig> rois)
        => System.IO.File.WriteAllText(path, JsonSerializer.Serialize(rois, KindJson.Options));

    /// <summary>ROI 最终区域的外接矩形；区域为空返回 null。</summary>
    public static double[]? BoundingBox(IReadOnlyList<VisionRoiConfig> rois)
    {
        using HObject region = RoiRegionBuilder.Build(rois);
        HOperatorSet.AreaCenter(region, out HTuple area, out _, out _);
        if (area.Length == 0 || area.D <= 0) return null;
        HOperatorSet.SmallestRectangle1(region, out HTuple r1, out HTuple c1, out HTuple r2, out HTuple c2);
        return [r1.D, c1.D, r2.D, c2.D];
    }

    public IEnumerable<string> Validate(EntryContext context)
    {
        var rois = Read(context.ContentPath!);
        if (!rois.Any(r => r.Op == RoiOp.Include))
            yield return "没有任何纳入（Include）区域";
        else if (context.Attrs["bbox"] == null)
            yield return "ROI 区域为空（排除区域覆盖了全部纳入区域）";

        var img = context.GetDependencyAttrs(ImageRole);
        int w = context.Attrs.GetInt("imageWidth"), h = context.Attrs.GetInt("imageHeight");
        int iw = img.GetInt("width"), ih = img.GetInt("height");
        if (w != iw || h != ih)
            yield return $"原图尺寸已变（画 ROI 时 {w}×{h}，现在 {iw}×{ih}），需重画";
    }

    public string? OptionsFingerprint(EntryContext context) => null;
    public void Generate(EntryContext context) => throw new NotSupportedException();
}

/// <summary>
/// 形状模型（派生条目）：由依赖的原图 + ROI 按建模参数（属性 <c>options</c>，缺省取布局默认值）
/// 调 <see cref="ShapeTemplateService.CreateTemplate"/> 生成，写成 <c>.shm</c>。
/// 注意 <c>.shm</c> 文件头带生成时间：同一份输入重新生成，模型等价但文件字节（及指纹）不同。
/// </summary>
internal sealed class ShapeModelKind : IPackageEntryKind
{
    public const string ImageRole = "image";
    public const string RoiRole = "roi";

    public string Kind => BuiltInKinds.ShapeModel;
    public bool IsDerived => true;
    public IReadOnlyList<DependencyRole> Roles { get; } =
        [new(ImageRole, [BuiltInKinds.Image]), new(RoiRole, [BuiltInKinds.Roi])];
    public string? EditorViewName => "VisionPackageShapeModelPage";

    public string StoragePath(LayoutEntry definition, string? sourceFileName) => $"models/{definition.Id}.shm";

    /// <summary>当前建模参数：属性里有就用属性里的，否则取布局默认值。</summary>
    public static ShapeTemplateCreateOptions GetOptions(LayoutEntry def, JsonObject attrs)
    {
        if (attrs["options"] is JsonObject o)
            return o.Deserialize<ShapeTemplateCreateOptions>(KindJson.Options) ?? new ShapeTemplateCreateOptions();
        var d = (def.Options as ShapeModelEntryOptions)?.Defaults ?? new ShapeTemplateCreateOptions();
        return JsonSerializer.Deserialize<ShapeTemplateCreateOptions>(JsonSerializer.Serialize(d))!;   // 拷贝，别让调用方改到布局默认值
    }

    public static void SetOptions(JsonObject attrs, ShapeTemplateCreateOptions options)
        => attrs["options"] = JsonSerializer.SerializeToNode(options, KindJson.Options);

    public IEnumerable<string> Validate(EntryContext context) => [];

    public string? OptionsFingerprint(EntryContext context)
        => PackageHash.OfText(JsonSerializer.Serialize(GetOptions(context.Definition, context.Attrs)));

    public void Generate(EntryContext context)
    {
        var options = GetOptions(context.Definition, context.Attrs);
        var image = context.GetDependencyImage(ImageRole);
        var rois = context.GetDependencyRois(RoiRole);
        using HObject region = RoiRegionBuilder.Build(rois);
        using var handle = ShapeTemplateService.CreateTemplate(image, region, options);
        HOperatorSet.WriteShapeModel(handle.ModelId, context.OutputPath!);
        SetOptions(context.Attrs, options);
    }
}

/// <summary>
/// 结构化参数（源条目），按布局声明的类型序列化成 JSON。反序列化宽松：缺字段取默认值、多字段忽略；
/// 类型实现 <see cref="IVisionPackageData"/> 时做自校验。属性：<c>type</c>（类型全名，仅供查看）。
/// </summary>
internal sealed class DataKind : IPackageEntryKind
{
    public string Kind => BuiltInKinds.Data;
    public bool IsDerived => false;
    public IReadOnlyList<DependencyRole> Roles { get; } = [];
    public string? EditorViewName => "VisionPackageDataPage";

    public string StoragePath(LayoutEntry definition, string? sourceFileName) => $"data/{definition.Id}.json";

    public static Type TypeOf(LayoutEntry def)
        => (def.Options as DataEntryOptions)?.DataType
           ?? throw new VisionPackageException($"条目 [{def.Title}] 没有声明数据类型。");

    public static object Deserialize(string json, Type type)
    {
        try
        {
            return JsonSerializer.Deserialize(json, type, KindJson.Options) ?? Activator.CreateInstance(type)!;
        }
        catch (JsonException ex)
        {
            throw new VisionPackageException($"参数数据无法解析为 {type.Name}：{ex.Message}");
        }
    }

    public static IEnumerable<string> SelfValidate(object value)
        => value is IVisionPackageData v ? v.Validate() : [];

    public IEnumerable<string> Validate(EntryContext context)
    {
        object value;
        try { value = Deserialize(System.IO.File.ReadAllText(context.ContentPath!), TypeOf(context.Definition)); }
        catch (VisionPackageException ex) { return [ex.Message]; }
        return SelfValidate(value).ToList();
    }

    public string? OptionsFingerprint(EntryContext context) => null;
    public void Generate(EntryContext context) => throw new NotSupportedException();
}

/// <summary>附件（源条目），原文件原样保存。属性：<c>fileName</c>、<c>size</c>。</summary>
internal sealed class FileKind : IPackageEntryKind
{
    public string Kind => BuiltInKinds.File;
    public bool IsDerived => false;
    public IReadOnlyList<DependencyRole> Roles { get; } = [];
    public string? EditorViewName => "VisionPackageFilePage";

    public string StoragePath(LayoutEntry definition, string? sourceFileName)
        => $"files/{definition.Id}/{System.IO.Path.GetFileName(sourceFileName ?? "file.bin")}";

    public IEnumerable<string> Validate(EntryContext context)
    {
        var exts = (context.Definition.Options as FileEntryOptions)?.Extensions ?? [];
        string name = context.Attrs.GetString("fileName") ?? string.Empty;
        if (exts.Length > 0 && !exts.Contains(System.IO.Path.GetExtension(name).ToLowerInvariant()))
            yield return $"文件类型不符，只允许 {string.Join("、", exts)}";
    }

    public string? OptionsFingerprint(EntryContext context) => null;
    public void Generate(EntryContext context) => throw new NotSupportedException();
}
