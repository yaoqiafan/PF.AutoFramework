namespace PF.Modules.Halcon;

/// <summary>Halcon 模块导航常量</summary>
public static class HalconNavigationConstants
{
    /// <summary>视图导航键</summary>
    public static class Views
    {
        /// <summary>视觉调试根页面（内嵌过程调试 + 管线运行）</summary>
        public const string Dashboard = "HalconDashboardView";
    }

    /// <summary>对话框键（IDialogService）</summary>
    public static class Dialogs
    {
        /// <summary>ROI 绘制弹窗</summary>
        public const string RoiEditor = "RoiEditorDialog";
    }
}
