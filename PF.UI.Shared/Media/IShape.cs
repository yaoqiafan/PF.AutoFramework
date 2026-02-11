using System;
using System.Windows;
using System.Windows.Media;

namespace PF.UI.Shared.Media;

public interface IShape
{
    event EventHandler RenderedGeometryChanged;

    void InvalidateGeometry(InvalidateGeometryReasons reasons);

    Brush Fill { get; set; }

    Thickness GeometryMargin { get; }

    Geometry RenderedGeometry { get; }

    Stretch Stretch { get; set; }

    Brush Stroke { get; set; }

    double StrokeThickness { get; set; }
}
