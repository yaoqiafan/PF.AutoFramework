using System.Windows;
using System.Windows.Controls;

namespace PF.UI.Controls;

public class AxleCanvas : Canvas
{
    public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(
        nameof(Orientation), typeof(Orientation), typeof(AxleCanvas), new PropertyMetadata(default(Orientation)));

    public Orientation Orientation
    {
        get => (Orientation) GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    protected override Size ArrangeOverride(Size arrangeSize)
    {
        foreach (UIElement publicChild in InternalChildren)
        {
            if (publicChild == null) continue;

            var x = 0.0;
            var y = 0.0;

            if (Orientation == Orientation.Horizontal)
            {
                x = (arrangeSize.Width - publicChild.DesiredSize.Width) / 2;

                var top = GetTop(publicChild);
                if (!double.IsNaN(top))
                {
                    y = top;
                }
                else
                {
                    var bottom = GetBottom(publicChild);
                    if (!double.IsNaN(bottom))
                        y = arrangeSize.Height - publicChild.DesiredSize.Height - bottom;
                }
            }
            else
            {
                y = (arrangeSize.Height - publicChild.DesiredSize.Height) / 2;

                var left = GetLeft(publicChild);
                if (!double.IsNaN(left))
                {
                    x = left;
                }
                else
                {
                    var right = GetRight(publicChild);
                    if (!double.IsNaN(right))
                        x = arrangeSize.Width - publicChild.DesiredSize.Width - right;
                }
            }

            publicChild.Arrange(new Rect(new Point(x, y), publicChild.DesiredSize));
        }
        return arrangeSize;
    }
}
