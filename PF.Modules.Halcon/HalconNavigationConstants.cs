namespace PF.Modules.Halcon;

/// <summary>Halcon 模块导航常量</summary>
public static class HalconNavigationConstants
{
    /// <summary>模块内部区域名</summary>
    public static class Regions
    {
        public const string HalconContentRegion = nameof(HalconContentRegion);
    }

    /// <summary>视图导航键</summary>
    public static class Views
    {
        public const string Dashboard     = "HalconDashboardView";
        public const string HalconDebug   = "HalconDebugView";
        public const string PipelineRunner = "PipelineRunnerView";
    }

    /// <summary>对话框键（IDialogService）</summary>
    public static class Dialogs
    {
        public const string RoiEditor = "RoiEditorDialog";
    }
}
