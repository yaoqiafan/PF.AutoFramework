namespace PF.Core.Entities.Hardware
{
    /// <summary>
    /// 硬件设备配置模型（持久化到 JSON 配置文件）
    ///
    /// 设计原则：
    ///   · 机器上的每一个硬件设备对应一条 HardwareConfig 记录
    ///   · ImplementationClassName 用于工厂注册查找，与实际类名解耦
    ///   · ConnectionParameters 键值对由具体工厂函数解析，服务层不感知细节
    /// </summary>
    public class HardwareConfig
    {
        /// <summary>设备唯一标识，全局不重复（如 "AXIS_X_0"）</summary>
        public string DeviceId { get; set; } = string.Empty;

        /// <summary>设备显示名称，用于 UI 展示（如 "X轴伺服"）</summary>
        public string DeviceName { get; set; } = string.Empty;

        /// <summary>
        /// 设备分类，用于按类型查询（如 "Axis", "IOController", "Camera"）
        /// </summary>
        public string Category { get; set; } = string.Empty;

        /// <summary>
        /// 具体实现类名，与 IHardwareManagerService.RegisterFactory 的 key 对应。
        /// 例：在 App.xaml.cs 中 RegisterFactory("SimXAxis", ...)，此处填 "SimXAxis"。
        /// </summary>
        public string ImplementationClassName { get; set; } = string.Empty;

        /// <summary>是否为模拟（脱机）设备</summary>
        public bool IsSimulated { get; set; } = true;

        /// <summary>是否在程序启动时自动实例化此设备</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 连接参数字典，由对应工厂函数解析。
        /// 轴设备示例：{ "AxisIndex": "0" }
        /// 网络设备示例：{ "IpAddress": "192.168.1.100", "Port": "502" }
        /// </summary>
        public Dictionary<string, string> ConnectionParameters { get; set; } = new();

        /// <summary>
        /// 父级控制卡的 DeviceId。
        /// 空字符串表示顶级设备（如板卡自身）；
        /// 非空表示该设备挂载在指定板卡下（如轴、IO），
        /// HardwareManagerService 将据此在初始化时建立父子关联。
        /// </summary>
        public string ParentDeviceId { get; set; } = string.Empty;

        /// <summary>设备备注说明</summary>
        public string Remarks { get; set; } = string.Empty;
    }
}
