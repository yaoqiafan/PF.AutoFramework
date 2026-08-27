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
        /// ROI 形状模板编辑弹窗：选参考图（或用 DialogParameters 的 "ImagePath" 注入）→ 画 ROI →
        /// 建立模板 → 按名字存盘。范围到"存盘"为止，不含"在新图上查找/预览"——那是消费方自己的事。
        /// </summary>
        public const string ShapeTemplateEditor = "ShapeTemplateEditorDialog";

        /// <summary>
        /// ROI 形状模板验证弹窗：按名字加载一个已存在的模板 → 在一张图（DialogParameters 的
        /// "ImagePath" 注入，或弹窗里自己选）上查找匹配、调 <c>ShapeMatchOptions</c>、看叠加轮廓。
        /// 纯只读验证，不改模板本身，不写回任何状态。
        /// </summary>
        public const string ShapeTemplateVerify = "ShapeTemplateVerifyDialog";
    }
}
