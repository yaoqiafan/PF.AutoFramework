using System.Windows.Media;

namespace PF.UI.Shared.Media;

public interface IGeometrySourceParameters
{
    Stretch Stretch { get; }

    Brush Stroke { get; }

    double StrokeThickness { get; }
}
