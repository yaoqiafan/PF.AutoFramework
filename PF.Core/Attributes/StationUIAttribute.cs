namespace PF.Core.Attributes
{
    /// <summary>
    /// 工站调试界面路由特性，供 StationDebugView 通过反射自动发现并注册工站 UI。
    ///
    /// 使用示例：
    ///   [StationUI("取放工站调试", "PickPlaceStationDebugView", 1)]
    ///   public class PickPlaceStation : StationBase { ... }
    ///
    /// 发现机制：
    ///   StationDebugViewModel 遍历注入的 IEnumerable&lt;StationBase&gt; 实例，
    ///   对每个实例的运行时类型读取此特性，提取 Title / ViewName / Order，
    ///   并通过 IRegionManager.RequestNavigate 驱动右侧内容区域的动态切换。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class StationUIAttribute : Attribute
    {
        /// <summary>侧边栏显示名称</summary>
        public string Title { get; }

        /// <summary>Prism 导航视图名称（与 RegisterForNavigation 注册时使用的 key 一致）</summary>
        public string ViewName { get; }

        /// <summary>侧边栏排序序号（数字越小越靠前，默认 99）</summary>
        public int Order { get; set; } = 99;

        public StationUIAttribute(string title, string viewName, int order = 99)
        {
            Title   = title;
            ViewName = viewName;
            Order   = order;
        }
    }
}
