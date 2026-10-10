namespace PF.Vision.Halcon.Packaging;

/// <summary>条目在编辑会话中的状态（只存在于会话里，不写进包）。</summary>
public enum EntryStatus
{
    /// <summary>布局里有，但还没建立。必填项未建立时不能保存。</summary>
    Missing,

    /// <summary>有数据，且与各依赖当前的指纹一致。</summary>
    Ok,

    /// <summary>有数据，但某个依赖改过：派生条目需重新生成，源条目（如 ROI）需人工确认。</summary>
    Stale,

    /// <summary>有数据，但已不能用（依赖缺失、原图尺寸变了、生成失败、自校验不通过）。</summary>
    Invalid,

    /// <summary>包里有、布局里没有（布局升级后遗留），需删除后才能保存。</summary>
    Extra,
}

/// <summary>会话里一个条目的当前状态快照，供编辑器显示树节点。</summary>
/// <param name="Id">条目 id。</param>
/// <param name="Kind">条目类型。</param>
/// <param name="Title">显示名。</param>
/// <param name="Group">布局里的分组名（树的父节点），没有分组为 null。</param>
/// <param name="Required">布局里是否必填。</param>
/// <param name="IsDerived">是否派生条目（由程序生成）。</param>
/// <param name="HasContent">是否已有数据。</param>
/// <param name="Status">状态。</param>
/// <param name="Reasons">非 <see cref="EntryStatus.Ok"/> 时的原因（中文）。</param>
/// <param name="DependsOn">依赖：角色 → 条目 id。</param>
/// <param name="ContentHash">当前数据的指纹，没有数据为 null（界面据此判断内容是否变化、要不要重画）。</param>
public sealed record PackageEntryState(
    string Id,
    string Kind,
    string Title,
    string? Group,
    bool Required,
    bool IsDerived,
    bool HasContent,
    EntryStatus Status,
    IReadOnlyList<string> Reasons,
    IReadOnlyDictionary<string, string> DependsOn,
    string? ContentHash);

/// <summary>校验问题的严重程度。</summary>
public enum IssueSeverity
{
    /// <summary>阻止保存。</summary>
    Error,

    /// <summary>不阻止保存（比如派生条目过期，保存时会自动重新生成）。</summary>
    Warning,
}

/// <summary>一条校验问题。</summary>
/// <param name="EntryId">相关条目 id；整包层面的问题为 null。</param>
/// <param name="Title">相关条目显示名。</param>
/// <param name="Severity">严重程度。</param>
/// <param name="Message">中文说明。</param>
public sealed record PackageIssue(string? EntryId, string? Title, IssueSeverity Severity, string Message)
{
    /// <inheritdoc/>
    public override string ToString() => Title == null ? Message : $"{Title}：{Message}";
}

/// <summary>整包校验结果。</summary>
public sealed class PackageValidation
{
    /// <summary>全部问题。</summary>
    public IReadOnlyList<PackageIssue> Issues { get; }

    /// <summary>阻止保存的问题。</summary>
    public IEnumerable<PackageIssue> Errors => Issues.Where(i => i.Severity == IssueSeverity.Error);

    /// <summary>没有阻止保存的问题。</summary>
    public bool CanSave => !Errors.Any();

    internal PackageValidation(IReadOnlyList<PackageIssue> issues) => Issues = issues;
}
