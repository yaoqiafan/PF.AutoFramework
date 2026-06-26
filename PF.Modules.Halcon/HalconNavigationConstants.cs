namespace PF.Modules.Halcon;

/// <summary>Halcon 模块导航常量</summary>
public static class HalconNavigationConstants
{
    /// <summary>模块内部区域名</summary>
    public static class Regions
    {
        /// <summary>右侧内容切换区域</summary>
        public const string HalconContentRegion = nameof(HalconContentRegion);
    }

    /// <summary>视图导航键</summary>
    public static class Views
    {
        /// <summary>仪表盘入口（左侧过程列表 + 右侧 Region）</summary>
        public const string Dashboard = "HalconDashboardView";

        /// <summary>算子调试面板（参数输入 + 图像显示 + 结果输出）</summary>
        public const string ProcedureDebug = "ProcedureDebugView";

        /// <summary>管线运行面板（选择并执行 JSON 管线，查看各步输出）</summary>
        public const string PipelineRunner = "PipelineRunnerView";

        /// <summary>HALCON 调试集成面板（Level 2 调试服务器 + HDevelop 联调）</summary>
        public const string HalconDebug = "HalconDebugView";
    }

    /// <summary>对话框键（IDialogService）</summary>
    public static class Dialogs
    {
        /// <summary>ROI 绘制弹窗</summary>
        public const string RoiEditor = "RoiEditorDialog";
    }
}
