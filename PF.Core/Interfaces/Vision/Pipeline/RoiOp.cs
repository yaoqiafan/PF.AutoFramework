namespace PF.Core.Interfaces.Vision.Pipeline;

/// <summary>ROI 操作类型：指定该 ROI 区域是纳入检测还是排除。</summary>
public enum RoiOp
{
    /// <summary>将该 ROI 区域纳入检测范围。</summary>
    Include,
    /// <summary>将该 ROI 区域从检测范围中排除。</summary>
    Exclude,
}
