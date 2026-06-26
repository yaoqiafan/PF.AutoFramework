namespace PF.Core.Interfaces.Vision.Pipeline;

/// <summary>
/// 单个 ROI 的数据描述。RoiParams 为平铺参数数组，含义由 Type 决定：
/// <list type="bullet">
/// <item>Rect          → [row1, col1, row2, col2]</item>
/// <item>Rect2         → [row, col, phi, len1, len2]</item>
/// <item>Circle        → [row, col, radius]</item>
/// <item>Ellipse       → [row, col, phi, radius1, radius2]</item>
/// <item>EllipseSector → [row, col, phi, radius1, radius2, startPhi, endPhi]</item>
/// <item>Polygon       → [row0, col0, row1, col1, ...]</item>
/// </list>
/// </summary>
public sealed class VisionRoiConfig
{
    /// <summary>ROI 名称标识。</summary>
    public string   Name      { get; set; } = string.Empty;
    /// <summary>ROI 几何形状类型。</summary>
    public RoiType  Type      { get; set; } = RoiType.Rect;
    /// <summary>ROI 操作（纳入或排除检测范围）。</summary>
    public RoiOp    Op        { get; set; } = RoiOp.Include;
    /// <summary>ROI 几何参数数组（含义由 Type 决定，见类注释）。</summary>
    public double[] RoiParams { get; set; } = [];
}
