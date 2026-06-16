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
    }
}
