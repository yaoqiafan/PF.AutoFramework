using PF.Core.Entities.Hardware;
using PF.Core.Models;

namespace PF.Core.Interfaces.Device.Hardware
{
    /// <summary>
    /// 硬件设备管理服务接口
    ///
    /// 设计原则（关注点分离）：
    ///   · 本接口只暴露机制（CRUD + 初始化 + 批量导入），不包含任何具体应用的默认数据。
    ///   · 通过 RegisterFactory 注册工厂函数（在组合根 App.xaml.cs 调用），
    ///     使服务本身不依赖任何具体设备类，符合依赖倒置原则。
    ///   · 配置通过 IParamService 持久化到数据库，支持异步 CRUD 热重载。
    ///   · ActiveDevices 供左侧设备树等 UI 模块订阅和展示。
    ///
    /// 典型启动流程（由上层 Workstation 驱动）：
    ///   1. RegisterFactory(...)                   注册设备工厂
    ///   2. 查询配置是否为空（首次运行检测）
    ///      → 构造应用层默认配置列表
    ///      → ImportConfigsAsync(defaultConfigs)   写入数据库
    ///   3. LoadAndInitializeAsync()               加载配置并连接所有设备
    /// </summary>
    public interface IHardwareManagerService
    {
        // ── 配置 CRUD ──────────────────────────────────────────────────────────

        /// <summary>获取内存缓存中的所有硬件配置记录（在 LoadAndInitializeAsync 后可用）</summary>
        IEnumerable<HardwareConfig> GetAllConfigs();

        /// <summary>按 DeviceId 查找内存缓存中的配置</summary>
        HardwareConfig? GetConfig(string deviceId);

        /// <summary>异步添加或更新配置（以 DeviceId 为唯一键），同时写入数据库</summary>
        Task SaveConfigAsync(HardwareConfig config);

        /// <summary>异步删除指定配置（已运行的设备需先停止），同时从数据库删除</summary>
        Task DeleteConfigAsync(string deviceId);

        /// <summary>
        /// 批量导入配置：将外部传入的配置集合逐一写入数据库并刷新内存缓存（upsert 语义）。
        ///
        /// 典型用途：上层 Workstation 在首次启动时检测到数据库为空，
        /// 构造应用层特定的默认设备列表，通过本方法一次性写入后再调用 LoadAndInitializeAsync。
        /// </summary>
        /// <param name="configs">要导入的配置集合，DeviceId 重复时覆盖已有记录</param>
        Task ImportConfigsAsync(IEnumerable<HardwareConfig> configs);

        // ── 工厂注册 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 在组合根注册设备工厂函数。
        /// key = HardwareConfig.ImplementationClassName；
        /// factory 接收配置并返回已构造（未连接）的设备实例。
        /// </summary>
        void RegisterFactory(string implementationClassName, Func<HardwareConfig, IHardwareDevice> factory);

        // ── 生命周期 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 从数据库加载配置，再根据已启用配置通过注册工厂实例化所有设备并调用 ConnectAsync。
        /// 通常在应用启动、且已通过 ImportConfigsAsync 确认配置存在后调用一次。
        /// </summary>
        /// <param name="progress">
        ///   可选进度回调，每初始化一个设备前后均会 Report 一次 <see cref="SplashProgressPayload"/>。
        ///   传 null 时退化为原有行为，保持向后兼容。
        /// </param>
        Task LoadAndInitializeAsync(IProgress<SplashProgressPayload>? progress = null);

        /// <summary>
        /// 先释放所有活跃设备，再重新加载配置并实例化。
        /// 配置变更（如新增/删除设备）后调用。
        /// </summary>
        Task ReloadAllAsync();

        /// <summary>
        /// 原子性地切换全局模拟模式：
        /// 将所有设备配置的 IsSimulated 写入数据库，然后触发热重载。
        /// 重载后所有设备将以新的模拟状态重新实例化并连接。
        /// </summary>
        /// <param name="enabled">true = 全部切换为模拟模式；false = 全部切换为真实硬件模式</param>
        Task SetGlobalSimulationModeAsync(bool enabled);

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
