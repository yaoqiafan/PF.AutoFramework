using PF.Vision.Halcon.Packaging;

namespace PF.Modules.Halcon.ViewModels.VisionPackageEditor;

/// <summary>
/// 自定义条目类型的编辑页约定：<see cref="IPackageEntryKind.EditorViewName"/> 指向一个已用
/// <c>RegisterForNavigation</c> 注册的视图，编辑器解析出视图后，若视图本身或它的 DataContext 实现了本接口，
/// 就调用 <see cref="Attach"/> 把会话和条目 id 交给它；切走或关闭编辑器时调用 <see cref="Detach"/>。
/// 编辑页对会话做的修改，编辑器会通过 <see cref="VisionPackageSession.Changed"/> 自动刷新树和校验栏。
/// </summary>
public interface IVisionPackageEntryEditor
{
    /// <summary>开始编辑：拿到会话与条目 id。</summary>
    void Attach(VisionPackageSession session, string entryId);

    /// <summary>结束编辑（切到别的条目或编辑器关闭）。</summary>
    void Detach();
}
