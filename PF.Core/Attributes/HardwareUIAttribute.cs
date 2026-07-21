namespace PF.Core.Attributes
{
    /// <summary>
    /// 硬件调试界面路由特性，供 HardwareDebugView 通过反射自动发现并注册跳转目标。
    ///
    /// 使用示例：
    ///   [HardwareUI("Ni4TowerLightDebugView")]
    ///   public sealed class TowerLightNi4Device : BaseDevice { ... }
    ///
    /// 发现机制：
    ///   HardwareDebugViewModel 在设备节点被点击时，对设备运行时类型读取此特性取得 ViewName，
    ///   优先于内置类型的硬编码分发，通过 IRegionManager.RequestNavigate 驱动右侧调试内容区切换。
    ///   未标注本特性的内置设备（轴/IO/卡/相机/条码/光源）仍走原有硬编码分发，保持向后兼容。
    ///
    /// 约定：与 <see cref="CommunicationUIAttribute"/> / <see cref="StationUIAttribute"/> 一致，
    ///   导航参数统一以 "Device" 为键传入被点击的设备实例。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class HardwareUIAttribute : Attribute
    {
        /// <summary>Prism 导航视图名称（与 RegisterForNavigation 注册时使用的 key 一致）</summary>
        public string ViewName { get; }

        /// <summary>初始化硬件UI特性</summary>
        public HardwareUIAttribute(string viewName)
        {
            ViewName = viewName;
        }
    }
}
