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

        /// <summary>
        /// 视觉资产包（<c>.vpk</c>）编辑器：左侧按布局显示条目树，右侧按条目类型切换编辑页，校验通过后保存。
        /// DialogParameters：<c>"Layout"</c>（<c>VisionPackageLayout</c>，必需）；<c>"PackagePath"</c>（string）或
        /// <c>"PackageName"</c>（string，按 <c>VisionPackage.PathOf</c> 拼路径）——文件存在则打开，否则新建；
        /// <c>"ImagePath"</c>（string，可选，新建时预填第一张原图）。关闭时带回 <c>"PackagePath"</c>、
        /// <c>"Revision"</c>（int）、<c>"Saved"</c>（bool，本次是否保存过；保存过时结果为 OK）。
        /// </summary>
        public const string VisionPackageEditor = "VisionPackageEditorDialog";
    }
}
