using PF.Core.Interfaces.Vision;

namespace PF.Vision.Halcon.Models;

/// <summary>
/// IVisionResult 的 Halcon 实现（仅在 PF.Vision.Halcon 内部使用）。
/// </summary>
internal sealed class HalconVisionResult : IVisionResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public TimeSpan ElapsedTime { get; init; }
    public string ProcedureName { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> ControlOutputs { get; init; }
        = new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> IconicOutputs { get; init; }
        = new Dictionary<string, object?>();

    internal static HalconVisionResult Failure(string procedureName, string error, TimeSpan elapsed)
        => new()
        {
            Success = false,
            ProcedureName = procedureName,
            ErrorMessage = error,
            ElapsedTime = elapsed,
        };

    internal static HalconVisionResult Succeeded(
        string procedureName,
        TimeSpan elapsed,
        Dictionary<string, object?> controlOutputs,
        Dictionary<string, object?> iconicOutputs)
        => new()
        {
            Success = true,
            ProcedureName = procedureName,
            ElapsedTime = elapsed,
            ControlOutputs = controlOutputs,
            IconicOutputs = iconicOutputs,
        };
}
