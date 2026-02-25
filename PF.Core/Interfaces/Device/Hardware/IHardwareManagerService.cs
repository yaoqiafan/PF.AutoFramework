using PF.Core.Entities.Hardware;

namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 硬件设备管理服务接口
    ///
    /// 设计原则：
    ///   · 通过 RegisterFactory 注册工厂函数（在组合根 App.xaml.cs 调用），
    ///     使服务本身不依赖任何具体设备类，符合依赖倒置原则。
    ///   · 配置以 JSON 文件持久化，支持 CRUD 热重载。
    ///   · ActiveDevices 供左侧设备树等 UI 模块订阅和展示。
    ///
    /// 典型使用流程：
    ///   1. App.xaml.cs RegisterFactory("SimXAxis", config => new SimXAxis(...))
    ///   2. App.xaml.cs LoadAndInitializeAsync() → 实例化并连接所有已启用设备
    ///   3. 其他模块通过 ActiveDevices / GetDevice(id) 获取设备引用
    /// </summary>
    public interface IHardwareManagerService
    {
        // ── 配置 CRUD ──────────────────────────────────────────────────────────

        /// <summary>获取所有硬件配置记录</summary>
        IEnumerable<HardwareConfig> GetAllConfigs();

        /// <summary>按 DeviceId 查找配置</summary>
        HardwareConfig? GetConfig(string deviceId);

        /// <summary>添加或更新配置（以 DeviceId 为唯一键）</summary>
        void SaveConfig(HardwareConfig config);

        /// <summary>删除指定配置（已运行的设备需先停止）</summary>
        void DeleteConfig(string deviceId);

        // ── 工厂注册 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 在组合根注册设备工厂函数。
        /// key = HardwareConfig.ImplementationClassName；
        /// factory 接收配置并返回已构造（未连接）的设备实例。
        /// </summary>
        void RegisterFactory(string implementationClassName, Func<HardwareConfig, IHardwareDevice> factory);

        // ── 生命周期 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 根据所有已启用的配置，通过注册的工厂实例化所有设备并调用 ConnectAsync。
        /// 通常在应用启动完成后调用一次。
        /// </summary>
        Task LoadAndInitializeAsync();

        /// <summary>
        /// 先释放所有活跃设备，再重新加载配置并实例化。
        /// 配置变更（如新增/删除设备）后调用。
        /// </summary>
        Task ReloadAllAsync();

        // ── 设备查询 ───────────────────────────────────────────────────────────

        /// <summary>当前所有已实例化并处于活跃状态的设备</summary>
        IEnumerable<IHardwareDevice> ActiveDevices { get; }

        /// <summary>按 DeviceId 获取活跃设备，不存在则返回 null</summary>
        IHardwareDevice? GetDevice(string deviceId);

        // ── 事件 ───────────────────────────────────────────────────────────────

        /// <summary>新设备被激活（实例化 + 连接成功）后触发</summary>
        event EventHandler<IHardwareDevice> DeviceAdded;

        /// <summary>设备被移除（释放）后触发，参数为 DeviceId</summary>
        event EventHandler<string> DeviceRemoved;
    }
}
