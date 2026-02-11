using System.Windows;

namespace PF.UI.Shared.Drawing;

public sealed class PathSegmentData
{
    public PathSegmentData(Point startPoint, System.Windows.Media.PathSegment pathSegment)
    {
        PathSegment = pathSegment;
        StartPoint = startPoint;
    }

    public System.Windows.Media.PathSegment PathSegment { get; }

    public Point StartPoint { get; }
}
