using HalconDotNet;
using PF.Core.Interfaces.Vision.Pipeline;
using PF.Vision.Halcon.Models;
using System.IO.Compression;
using System.Text.Json;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// 基于形状的模板匹配（HALCON <c>CreateShapeModel</c>/<c>FindShapeModel</c>），直调 HALCON SDK，
/// **不经过 HDevEngine**——跟 <see cref="Internal.RoiRegionBuilder"/> 同一路子，纯托管方法调用，
/// 可在任意线程调用，不用像 <c>HalconVisionService</c> 那样排队到专用 Worker 线程。
///
/// <para>典型用法（三步）：<see cref="CreateTemplate"/> 在参考图的 ROI 区域内建模板 →
/// <see cref="SaveTemplate"/> 存盘（可选，<see cref="LoadTemplate"/> 读回）→
/// <see cref="FindMatches"/> 在新图上找，拿到匹配位姿（Row/Column/Angle/Score）。
/// ROI 区域建议用 <see cref="Internal.RoiRegionBuilder.Build"/> 从一组
/// <c>VisionRoiConfig</c> 拼出来——已经支持多区域 Include/Exclude 拼接成复杂形状。</para>
///
/// <para>模板不解释用途——位姿结果拿去做"补偿图像整体偏移/旋转"还是"限定后续算法处理范围"，
/// 由调用方决定，这里只管建模板和找模板两件事。</para>
///
/// <para><b>模板按名字存取，不是按裸路径</b>：<see cref="SaveTemplate"/>/<see cref="LoadTemplate"/>
/// 只收一个名字，实际文件路径由 <see cref="TemplateDirectory"/> 拼出来——跟 <c>.hdev</c> 过程目录
/// （<c>AddVisionServices(procedureDirectory, ...)</c>）同一个思路：消费项目在启动时用
/// <c>AddShapeTemplateServices(templateDirectory)</c> 配一次目录，之后无论是框架侧的
/// ROI 模板编辑弹窗（<c>ShapeTemplateEditorDialogView</c>），还是消费方自己代码里要找模板，
/// 都只需要认一个名字，不用互相传递/记住完整路径。</para>
///
/// <para><b>模板文件是打包格式（<c>.roipk</c>），不是裸 <c>.shm</c></b>：HALCON 形状模型本身
/// 只保存训练好的轮廓特征，不保存建模板时画的 ROI 区域——单存一个 <c>.shm</c> 没法在调试时
/// "重新打开、微调 ROI"。<see cref="SaveTemplate"/> 把 <c>model.shm</c>（生产匹配用）、
/// <c>rois.json</c>（ROI 绘制过程，<see cref="VisionRoiConfig"/> 列表）、<c>reference.png</c>
/// （建模板用的参考图，微调时用来重新画 ROI）打成一个 zip；<see cref="LoadTemplate"/>（生产路径）
/// 只解 <c>model.shm</c>，<see cref="LoadTemplateForEdit"/>（调试微调路径）解另外两块——两条路径
/// 互不影响，生产端不会因为这次改动多付任何解压/反序列化的开销。</para>
/// </summary>
public static class ShapeTemplateService
{
    /// <summary>
    /// 模板文件存放目录，由消费项目在启动时通过 <c>AddShapeTemplateServices(templateDirectory)</c>
    /// 设置一次。未配置时调用 <see cref="SaveTemplate"/>/<see cref="LoadTemplate"/> 会抛异常——
    /// 不要静默退化成相对路径，否则存的文件会落进进程当前工作目录这种谁也找不到的地方。
    /// </summary>
    public static string? TemplateDirectory { get; set; }

    private static string ResolvePath(string name)
    {
        if (string.IsNullOrWhiteSpace(TemplateDirectory))
            throw new InvalidOperationException(
                $"{nameof(ShapeTemplateService)}.{nameof(TemplateDirectory)} 未配置——" +
                "消费项目需先在启动代码里调用 AddShapeTemplateServices(templateDirectory) " +
                "（见 PF.Vision.Halcon.Extensions.VisionServiceExtensions），或手动设置该属性。");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("模板名字不能为空。", nameof(name));

        return System.IO.Path.Combine(TemplateDirectory, name + ".roipk");
    }

    /// <summary>
    /// 列出 <see cref="TemplateDirectory"/> 下所有可用模板的名字（不含扩展名），供下拉选择控件用
    /// （比如程式参数里"关联 ROI 模板"那个下拉框）。目录未配置/不存在时返回空列表，不抛异常——
    /// 这是给 UI 展示用的只读查询，不该让调用方（往往是 PropertyGrid 的编辑器）因为目录没配就
    /// 渲染失败。
    /// </summary>
    public static IReadOnlyList<string> GetAvailableTemplateNames()
    {
        if (string.IsNullOrWhiteSpace(TemplateDirectory) || !Directory.Exists(TemplateDirectory))
            return [];

        return Directory.GetFiles(TemplateDirectory, "*.roipk")
            .Select(System.IO.Path.GetFileNameWithoutExtension)
            .Where(n => !string.IsNullOrEmpty(n))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList()!;
    }

    /// <summary>
    /// 用一张参考图 + ROI 区域建立形状模板。内部先 <c>ReduceDomain</c> 把图像限制到
    /// <paramref name="roiRegion"/>，再 <c>CreateShapeModel</c>——模板只认区域内的边缘特征。
    /// 返回的 <see cref="ShapeTemplateHandle"/> 用完必须 <see cref="ShapeTemplateHandle.Dispose"/>，
    /// 否则 HALCON 模型资源（<c>ClearShapeModel</c>）不会释放。
    /// </summary>
    public static ShapeTemplateHandle CreateTemplate(
        HObject image, HObject roiRegion, ShapeTemplateCreateOptions? options = null)
    {
        options ??= new ShapeTemplateCreateOptions();

        HOperatorSet.ReduceDomain(image, roiRegion, out HObject reduced);
        try
        {
            HTuple numLevels = options.NumLevels == 0 ? new HTuple("auto") : new HTuple(options.NumLevels);
            HTuple angleStep = options.AngleStep == 0 ? new HTuple("auto") : new HTuple(options.AngleStep);
            HTuple contrast  = options.Contrast  == 0 ? new HTuple("auto") : new HTuple(options.Contrast);

            HOperatorSet.CreateShapeModel(
                reduced,
                numLevels,
                options.AngleStart,
                options.AngleExtent,
                angleStep,
                options.Optimization,
                options.Metric,
                contrast,
                options.MinContrast,
                out HTuple modelId);

            return new ShapeTemplateHandle(modelId);
        }
        finally
        {
            reduced.Dispose();
        }
    }

    /// <summary>
    /// 把模板按名字打包写盘：<c>model.shm</c>（<c>WriteShapeModel</c>）+ <c>rois.json</c>
    /// （<paramref name="rois"/> 序列化）+ <c>reference.png</c>（<paramref name="referenceImage"/>），
    /// 三者压成一个 <c>.roipk</c> zip，实际路径 = <see cref="TemplateDirectory"/> +
    /// <paramref name="name"/> + <c>.roipk</c>。同名已存在时整体覆盖。
    /// </summary>
    public static void SaveTemplate(
        ShapeTemplateHandle handle, HObject referenceImage,
        IReadOnlyList<VisionRoiConfig> rois, string name)
    {
        string path = ResolvePath(name);
        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PF.ShapeTemplate_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            HOperatorSet.WriteShapeModel(handle.ModelId, System.IO.Path.Combine(tempDir, "model.shm"));
            File.WriteAllText(System.IO.Path.Combine(tempDir, "rois.json"), JsonSerializer.Serialize(rois));
            HOperatorSet.WriteImage(referenceImage, "png", 0, System.IO.Path.Combine(tempDir, "reference.png"));

            if (File.Exists(path)) File.Delete(path);
            ZipFile.CreateFromDirectory(tempDir, path);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 按名字从盘上读回模板供生产匹配用——只解包 <c>model.shm</c>（<c>ReadShapeModel</c>），
    /// 不碰 <c>rois.json</c>/参考图，运行时路径不为这次改动多付任何解压/反序列化开销。
    /// 同样需要调用方负责 Dispose。找不到文件/包内缺模型条目时抛异常，不在这里吞掉——
    /// "模板名字打错了/还没建过/包已损坏"应该让消费方明确看到失败，不是静默拿到一个空模型。
    /// </summary>
    public static ShapeTemplateHandle LoadTemplate(string name)
    {
        string path = ResolvePath(name);
        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".shm");
        try
        {
            using var zip = ZipFile.OpenRead(path);
            var entry = zip.GetEntry("model.shm")
                ?? throw new InvalidOperationException($"模板包 [{name}] 缺少 model.shm，可能已损坏。");
            entry.ExtractToFile(tempFile, overwrite: true);

            HOperatorSet.ReadShapeModel(tempFile, out HTuple modelId);
            return new ShapeTemplateHandle(modelId);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    /// <summary>
    /// 按名字读回模板的 ROI 绘制过程 + 参考图，供调试时"重新打开模板微调 ROI"用——
    /// 跟 <see cref="LoadTemplate"/>（生产路径，只认 <c>model.shm</c>）完全独立，互不影响。
    /// 返回值里 <see cref="ShapeTemplateEditSession.ReferenceImage"/> 的所有权转给调用方，
    /// 用完需自行 Dispose（跟 <c>HOperatorSet.ReadImage</c> 的一贯所有权约定一致）。
    /// </summary>
    public static ShapeTemplateEditSession LoadTemplateForEdit(string name)
        => LoadEditSessionFromZip(ResolvePath(name), name);

    /// <summary>
    /// 跟 <see cref="LoadTemplateForEdit"/> 一样，但直接给一个 <c>.roipk</c> 文件的裸路径——
    /// 给"弹文件选择框选模板文件"这条交互用，不强求文件落在 <see cref="TemplateDirectory"/> 里，
    /// 也不需要提前知道名字。
    /// </summary>
    public static ShapeTemplateEditSession LoadTemplateForEditFromPath(string filePath)
        => LoadEditSessionFromZip(filePath, System.IO.Path.GetFileNameWithoutExtension(filePath));

    private static ShapeTemplateEditSession LoadEditSessionFromZip(string path, string displayName)
    {
        using var zip = ZipFile.OpenRead(path);

        var roisEntry = zip.GetEntry("rois.json")
            ?? throw new InvalidOperationException($"模板包 [{displayName}] 缺少 rois.json，可能是旧版本模板（改版前存的裸 .shm 不支持微调）。");
        List<VisionRoiConfig> rois;
        using (var roisStream = roisEntry.Open())
            rois = JsonSerializer.Deserialize<List<VisionRoiConfig>>(roisStream) ?? [];

        var imgEntry = zip.GetEntry("reference.png")
            ?? throw new InvalidOperationException($"模板包 [{displayName}] 缺少 reference.png，可能是旧版本模板。");
        string tempImg = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".png");
        try
        {
            imgEntry.ExtractToFile(tempImg, overwrite: true);
            HOperatorSet.ReadImage(out HObject image, tempImg);
            return new ShapeTemplateEditSession(image, rois);
        }
        finally
        {
            if (File.Exists(tempImg)) File.Delete(tempImg);
        }
    }

    /// <summary>
    /// 在新图上查找模板（<c>FindShapeModel</c>），按 <see cref="ShapeMatchOptions.NumMatches"/>
    /// 返回 0..N 个匹配，按 HALCON 返回顺序（一般是分数从高到低）。
    /// </summary>
    public static IReadOnlyList<ShapeMatchResult> FindMatches(
        HObject image, ShapeTemplateHandle handle, ShapeMatchOptions? options = null)
    {
        options ??= new ShapeMatchOptions();

        HOperatorSet.FindShapeModel(
            image,
            handle.ModelId,
            options.AngleStart,
            options.AngleExtent,
            options.MinScore,
            options.NumMatches,
            options.MaxOverlap,
            options.SubPixel,
            options.NumLevels,
            options.Greediness,
            out HTuple row, out HTuple column, out HTuple angle, out HTuple score);

        var results = new List<ShapeMatchResult>(row.Length);
        for (int i = 0; i < row.Length; i++)
            results.Add(new ShapeMatchResult(row[i].D, column[i].D, angle[i].D, score[i].D));
        return results;
    }

    /// <summary>
    /// 取某次匹配位姿下的模板轮廓——<c>GetShapeModelContours</c> 拿到模板参考系下的轮廓，
    /// 再用 <c>VectorAngleToRigid</c> + <c>AffineTransContourXld</c> 变换到匹配到的实际位置/角度。
    /// 直接喂给 <c>HalconImageViewer.DisplayOverlay</c> 就能在待匹配图上画出命中框。
    /// 返回值所有权归调用方，用完需 Dispose。
    /// </summary>
    public static HObject GetMatchedContour(ShapeTemplateHandle handle, ShapeMatchResult match)
    {
        HOperatorSet.GetShapeModelContours(out HObject modelContours, handle.ModelId, 1);
        try
        {
            HOperatorSet.VectorAngleToRigid(
                0, 0, 0, match.Row, match.Column, match.Angle, out HTuple homMat2D);
            HOperatorSet.AffineTransContourXld(modelContours, out HObject transformed, homMat2D);
            return transformed;
        }
        finally
        {
            modelContours.Dispose();
        }
    }
}

/// <summary>
/// <see cref="ShapeTemplateService.LoadTemplateForEdit"/> 的返回值——重新打开一个模板包用来微调
/// ROI 所需的全部东西：当初建模板的参考图 + 画的 ROI 列表。<see cref="ReferenceImage"/> 的所有权
/// 转给调用方，用完需自行 Dispose；本身不是 <see cref="IDisposable"/>，不引入新的释放规则。
/// </summary>
public sealed record ShapeTemplateEditSession(HObject ReferenceImage, IReadOnlyList<VisionRoiConfig> Rois);

/// <summary>
/// 一个已建立（或已读回）的形状模板的句柄，内部持有 HALCON <c>ModelID</c>。
/// 用完必须 <see cref="Dispose"/>（<c>ClearShapeModel</c>），否则 HALCON 侧模型资源不释放。
/// <c>ModelId</c> 特意只暴露给 <see cref="ShapeTemplateService"/> 内部方法用——外部调用方
/// 不需要、也不应该直接碰 HTuple，全部通过本类的静态方法操作。
/// </summary>
public sealed class ShapeTemplateHandle : IDisposable
{
    internal HTuple ModelId { get; }
    private bool _disposed;

    internal ShapeTemplateHandle(HTuple modelId) => ModelId = modelId;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { HOperatorSet.ClearShapeModel(ModelId); }
        catch { /* 模型已失效或引擎已释放，忽略——Dispose 不应该抛异常 */ }
    }
}
