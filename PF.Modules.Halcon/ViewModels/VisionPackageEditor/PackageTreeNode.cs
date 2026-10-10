using System.Collections.ObjectModel;
using PF.Vision.Halcon.Packaging;
using Prism.Mvvm;

namespace PF.Modules.Halcon.ViewModels.VisionPackageEditor;

/// <summary>编辑器左侧树的节点：分组节点（<see cref="EntryId"/> 为 null）或条目节点。</summary>
public sealed class PackageTreeNode : BindableBase
{
    /// <summary>分组节点。</summary>
    public PackageTreeNode(string title)
    {
        Title = title;
        IsGroup = true;
        IsExpanded = true;
    }

    /// <summary>条目节点。</summary>
    public PackageTreeNode(PackageEntryState state)
    {
        EntryId = state.Id;
        Title = state.Title;
        Update(state);
    }

    /// <summary>显示名。</summary>
    public string Title { get; }

    /// <summary>条目 id；分组节点为 null。</summary>
    public string? EntryId { get; }

    /// <summary>是否分组节点。</summary>
    public bool IsGroup { get; }

    /// <summary>子节点。</summary>
    public ObservableCollection<PackageTreeNode> Children { get; } = [];

    private bool _isExpanded;
    /// <summary>是否展开。</summary>
    public bool IsExpanded { get => _isExpanded; set => SetProperty(ref _isExpanded, value); }

    private bool _isSelected;
    /// <summary>是否选中（双向绑定 TreeViewItem.IsSelected，校验栏跳转靠它）。</summary>
    public bool IsSelected { get => _isSelected; set => SetProperty(ref _isSelected, value); }

    private string _glyph = string.Empty;
    /// <summary>状态符号：● 正常 ◐ 需处理 ○ 未建立 ✕ 无效 … 多余。</summary>
    public string Glyph { get => _glyph; private set => SetProperty(ref _glyph, value); }

    private string _level = "none";
    /// <summary>状态级别（ok / warn / error / none），XAML 据此着色。</summary>
    public string Level { get => _level; private set => SetProperty(ref _level, value); }

    private string _tip = string.Empty;
    /// <summary>悬停提示：状态与原因。</summary>
    public string Tip { get => _tip; private set => SetProperty(ref _tip, value); }

    /// <summary>按最新状态刷新显示。</summary>
    public void Update(PackageEntryState s)
    {
        (Glyph, Level) = s.Status switch
        {
            EntryStatus.Ok      => ("●", "ok"),
            EntryStatus.Stale   => ("◐", "warn"),
            EntryStatus.Invalid => ("✕", "error"),
            EntryStatus.Extra   => ("…", "error"),
            _                   => ("○", s.Required ? "error" : "none"),
        };
        Tip = s.Status == EntryStatus.Ok ? "正常" : string.Join("\n", s.Reasons);
    }
}
