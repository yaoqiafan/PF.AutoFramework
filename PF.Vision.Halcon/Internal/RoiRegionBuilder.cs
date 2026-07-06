using HalconDotNet;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Vision.Pipeline;

namespace PF.Vision.Halcon.Internal;

/// <summary>
/// 将 VisionRoiConfig 列表转换为 HALCON Region（HObject）。
/// Include ROI 取并集，Exclude ROI 取并集后再差集。
/// 此类方法直接调用 HALCON SDK（非 HDevEngine），可在任意线程调用。
/// </summary>
public static class RoiRegionBuilder
{
    /// <summary>
    /// 可选日志服务（DI 注册时由 <c>VisionServiceExtensions.AddVisionServices</c> 设置）。
    /// ROI 参数非法导致生成失败时记录警告，替代原先的完全静默降级。
    /// </summary>
    public static ILogService? Logger { get; set; }

    /// <summary>
    /// 计算最终检测区域：Union(Include) − Union(Exclude)。
    /// 无任何 Include ROI 时返回全黑空区域（安全兜底）。
    /// </summary>
    public static HObject Build(IEnumerable<VisionRoiConfig> rois)
    {
        var roiList  = rois.ToList();
        var includes = roiList.Where(r => r.Op == RoiOp.Include).ToList();
        var excludes = roiList.Where(r => r.Op == RoiOp.Exclude).ToList();

        HObject result;
        HOperatorSet.GenEmptyRegion(out result);

        if (includes.Count == 0)
            return result;

        // 合并 Include
        HObject includeUnion;
        HOperatorSet.GenEmptyRegion(out includeUnion);
        foreach (var roi in includes)
        {
            using var region = GetRegion(roi);
            if (!region.IsInitialized()) continue;
            HObject merged;
            HOperatorSet.Union2(includeUnion, region, out merged);
            includeUnion.Dispose();
            includeUnion = merged;
        }

        if (excludes.Count == 0)
        {
            result.Dispose();
            return includeUnion;
        }

        // 合并 Exclude
        HObject excludeUnion;
        HOperatorSet.GenEmptyRegion(out excludeUnion);
        foreach (var roi in excludes)
        {
            using var region = GetRegion(roi);
            if (!region.IsInitialized()) continue;
            HObject merged;
            HOperatorSet.Union2(excludeUnion, region, out merged);
            excludeUnion.Dispose();
            excludeUnion = merged;
        }

        HObject diff;
        HOperatorSet.Difference(includeUnion, excludeUnion, out diff);
        includeUnion.Dispose();
        excludeUnion.Dispose();
        result.Dispose();
        return diff;
    }

    /// <summary>将单个 VisionRoiConfig 转换为 HALCON Region。</summary>
    public static HObject GetRegion(VisionRoiConfig roi)
    {
        HObject region;
        var p = roi.RoiParams;

        try
        {
            switch (roi.Type)
            {
                case RoiType.Rect when p.Length >= 4:
                    HOperatorSet.GenRectangle1(out region, p[0], p[1], p[2], p[3]);
                    break;

                case RoiType.Rect2 when p.Length >= 5:
                    HOperatorSet.GenRectangle2(out region, p[0], p[1], p[2], p[3], p[4]);
                    break;

                case RoiType.Circle when p.Length >= 3:
                    HOperatorSet.GenCircle(out region, p[0], p[1], p[2]);
                    break;

                case RoiType.Ellipse when p.Length >= 5:
                    HOperatorSet.GenEllipse(out region, p[0], p[1], p[2], p[3], p[4]);
                    break;

                case RoiType.EllipseSector when p.Length >= 7:
                    HOperatorSet.GenEllipseSector(out region,
                        p[0], p[1], p[2], p[3], p[4], p[5], p[6]);
                    break;

                case RoiType.Polygon when p.Length >= 4:
                {
                    int count = p.Length / 2;
                    var rows  = new HTuple(Enumerable.Range(0, count).Select(i => p[i * 2]).ToArray());
                    var cols  = new HTuple(Enumerable.Range(0, count).Select(i => p[i * 2 + 1]).ToArray());
                    HOperatorSet.GenRegionPolygonFilled(out region, rows, cols);
                    break;
                }

                default:
                    HOperatorSet.GenEmptyRegion(out region);
                    break;
            }
        }
        catch (Exception ex)
        {
            // 参数非法（如 NaN、半径为负）时降级为空区域，但必须留下线索——
            // 静默降级会让"检测区域为空 → 全部漏检"极难排查
            Logger?.Warn(
                $"[Vision] ROI 生成失败，已降级为空区域: Type={roi.Type}, Params=[{string.Join(", ", p)}]",
                "Vision", ex);
            HOperatorSet.GenEmptyRegion(out region);
        }

        return region;
    }
}
