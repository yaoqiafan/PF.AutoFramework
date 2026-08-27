using System.ComponentModel;

namespace PF.Vision.Halcon.Models;

/// <summary>
/// 建立形状模板（<c>CreateShapeModel</c>）的可调参数。全部用 C# 原生类型（不是 HTuple），
/// 方便直接绑 <c>pf:PropertyGrid</c> 反射编辑；<c>NumLevels</c>/<c>AngleStep</c>/<c>Contrast</c>
/// 传 0 表示交给 HALCON 自动计算（对应 HALCON 的 <c>"auto"</c> 惯用值）。
/// </summary>
public sealed class ShapeTemplateCreateOptions
{
    /// <summary>模板允许的旋转角度下限（弧度）。</summary>
    [Category("匹配范围")]
    [Description("模板允许的旋转角度下限（弧度）。")]
    public double AngleStart { get; set; } = -0.39;

    /// <summary>旋转角度覆盖范围（弧度），从 <see cref="AngleStart"/> 起。</summary>
    [Category("匹配范围")]
    [Description("旋转角度覆盖范围（弧度），从 AngleStart 起。默认 ±22.5° 左右，按实际偏移量调大调小。")]
    public double AngleExtent { get; set; } = 0.79;

    /// <summary>角度步长（弧度）。0 = 自动计算。</summary>
    [Category("匹配范围")]
    [Description("角度步长（弧度）。0 = 自动计算。")]
    public double AngleStep { get; set; } = 0;

    /// <summary>金字塔层数。0 = 自动计算。</summary>
    [Category("性能")]
    [Description("金字塔层数。0 = 自动计算。")]
    public int NumLevels { get; set; } = 0;

    /// <summary>边缘对比度阈值。0 = 自动计算。</summary>
    [Category("对比度")]
    [Description("边缘对比度阈值。0 = 自动计算。")]
    public int Contrast { get; set; } = 0;

    /// <summary>最小对比度，低于此值的边缘被当噪声忽略。</summary>
    [Category("对比度")]
    [Description("最小对比度，低于此值的边缘被当噪声忽略。")]
    public int MinContrast { get; set; } = 10;

    /// <summary>优化策略。HALCON 惯用值：none / auto / point_reduction_high 等，缺省 auto。</summary>
    [Category("匹配策略")]
    [Description("优化策略。HALCON 惯用值：none / auto / point_reduction_high 等，缺省 auto。")]
    public string Optimization { get; set; } = "auto";

    /// <summary>匹配度量方式：use_polarity（区分明暗极性）/ ignore_global_polarity / ignore_local_polarity。</summary>
    [Category("匹配策略")]
    [Description("匹配度量方式：use_polarity（区分明暗极性）/ ignore_global_polarity / ignore_local_polarity。")]
    public string Metric { get; set; } = "use_polarity";
}

/// <summary>
/// 在新图上查找已建模板（<c>FindShapeModel</c>）的可调参数。同样全用 C# 原生类型，
/// 方便直接绑 <c>pf:PropertyGrid</c>。
/// </summary>
public sealed class ShapeMatchOptions
{
    /// <summary>本次查找允许的旋转角度下限（弧度）。一般跟建模板时的范围一致。</summary>
    [Category("匹配范围")]
    [Description("本次查找允许的旋转角度下限（弧度）。一般跟建模板时的范围一致。")]
    public double AngleStart { get; set; } = -0.39;

    /// <summary>本次查找的旋转角度覆盖范围（弧度）。</summary>
    [Category("匹配范围")]
    [Description("本次查找的旋转角度覆盖范围（弧度）。")]
    public double AngleExtent { get; set; } = 0.79;

    /// <summary>最低匹配分数（0~1），低于此值不算命中。</summary>
    [Category("筛选")]
    [Description("最低匹配分数（0~1），低于此值不算命中。")]
    public double MinScore { get; set; } = 0.7;

    /// <summary>最多返回几个匹配实例。</summary>
    [Category("筛选")]
    [Description("最多返回几个匹配实例。")]
    public int NumMatches { get; set; } = 1;

    /// <summary>多个匹配之间允许的最大重叠比例（0~1）。</summary>
    [Category("筛选")]
    [Description("多个匹配之间允许的最大重叠比例（0~1）。")]
    public double MaxOverlap { get; set; } = 0.5;

    /// <summary>亚像素精度模式：none / least_squares / interpolation 等。</summary>
    [Category("精度")]
    [Description("亚像素精度模式：none / least_squares / interpolation 等。")]
    public string SubPixel { get; set; } = "least_squares";

    /// <summary>搜索金字塔层数。0 = 自动计算，一般应与建模板时的 NumLevels 保持一致或更低。</summary>
    [Category("性能")]
    [Description("搜索金字塔层数。0 = 自动计算，一般应与建模板时的 NumLevels 保持一致或更低。")]
    public int NumLevels { get; set; } = 0;

    /// <summary>贪婪度（0~1），越高越快但可能漏检，1 = 最快。</summary>
    [Category("性能")]
    [Description("贪婪度（0~1），越高越快但可能漏检，1 = 最快。")]
    public double Greediness { get; set; } = 0.9;
}

/// <summary>单次匹配的位姿与分数。<see cref="Angle"/> 单位弧度。</summary>
public sealed record ShapeMatchResult(double Row, double Column, double Angle, double Score);
