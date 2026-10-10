using HalconDotNet;
using PF.Vision.Halcon.Models;

namespace PF.Vision.Halcon.Services;

/// <summary>
/// 基于形状的模板匹配（HALCON <c>CreateShapeModel</c>/<c>FindShapeModel</c>），直调 HALCON SDK，
/// **不经过 HDevEngine**——跟 <see cref="Internal.RoiRegionBuilder"/> 同一路子，纯托管方法调用，
/// 可在任意线程调用，不用像 <c>HalconVisionService</c> 那样排队到专用 Worker 线程。
///
/// <para>本类只管算法：<see cref="CreateTemplate"/> 在图的 ROI 区域内建模板、<see cref="FindMatches"/>
/// 在新图上找、<see cref="GetMatchedContour"/> 取命中位置的轮廓。ROI 区域建议用
/// <see cref="Internal.RoiRegionBuilder.Build"/> 从一组 <c>VisionRoiConfig</c> 拼出来。</para>
///
/// <para><b>模板的存取不在这里</b>：形状模型作为视觉资产包（<c>.vpk</c>）的 <c>ShapeModel</c> 条目保存，
/// 编辑用 <see cref="Packaging.VisionPackageSession"/>，生产读取用
/// <see cref="Packaging.VisionPackageReader.LoadShapeModel"/>（v1.1.0 起取代原 <c>.roipk</c> 的
/// SaveTemplate/LoadTemplate 系列方法）。</para>
///
/// <para>模板不解释用途——位姿结果拿去做"补偿图像整体偏移/旋转"还是"限定后续算法处理范围"，由调用方决定。</para>
/// </summary>
public static class ShapeTemplateService
{
    /// <summary>
    /// 用一张图 + ROI 区域建立形状模板。内部先 <c>ReduceDomain</c> 把图像限制到
    /// <paramref name="roiRegion"/>，再 <c>CreateShapeModel</c>——模板只认区域内的边缘特征。
    /// 返回的 <see cref="ShapeTemplateHandle"/> 用完必须 <see cref="ShapeTemplateHandle.Dispose"/>，
    /// 否则 HALCON 模型资源（<c>ClearShapeModel</c>）不会释放。
    /// </summary>
    public static ShapeTemplateHandle CreateTemplate(
        HObject image, HObject roiRegion, ShapeTemplateCreateOptions? options = null)
    {
        options ??= new ShapeTemplateCreateOptions();
        HalconThreadContext.EnsureRegionClipOff();

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
    /// 在新图上查找模板（<c>FindShapeModel</c>），按 <see cref="ShapeMatchOptions.NumMatches"/>
    /// 返回 0..N 个匹配，按 HALCON 返回顺序（一般是分数从高到低）。
    /// </summary>
    public static IReadOnlyList<ShapeMatchResult> FindMatches(
        HObject image, ShapeTemplateHandle handle, ShapeMatchOptions? options = null)
    {
        options ??= new ShapeMatchOptions();
        HalconThreadContext.EnsureRegionClipOff();

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
/// 一个已建立（或已读回）的形状模板的句柄，内部持有 HALCON <c>ModelID</c>。
/// 用完必须 <see cref="Dispose"/>（<c>ClearShapeModel</c>），否则 HALCON 侧模型资源不释放。
/// <c>ModelId</c> 只在本程序集内部使用——外部调用方通过 <see cref="ShapeTemplateService"/> 的静态方法操作。
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
