namespace PF.UI.Infrastructure.Data;

public struct DoubleRange : IValueRange<double>
{
    public double Start { get; set; }

    public double End { get; set; }
}
