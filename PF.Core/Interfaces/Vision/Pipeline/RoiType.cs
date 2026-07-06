namespace PF.Core.Interfaces.Vision.Pipeline;

/// <summary>ROI 几何形状类型。</summary>
public enum RoiType
{
    /// <summary>轴对齐矩形（由左上角和右下角坐标定义）。</summary>
    Rect,
    /// <summary>旋转矩形（由中心点、旋转角及半长轴定义）。</summary>
    Rect2,
    /// <summary>圆形（由圆心和半径定义）。</summary>
    Circle,
    /// <summary>椭圆（由中心点、旋转角及两半轴长度定义）。</summary>
    Ellipse,
    /// <summary>椭圆扇形（在椭圆基础上增加起止角度参数）。</summary>
    EllipseSector,
    /// <summary>多边形（由任意数量的顶点依次连接围成）。</summary>
    Polygon,
}
