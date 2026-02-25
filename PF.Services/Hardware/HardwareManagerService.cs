using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 硬件设备管理服务实现
    ///
    /// 生命周期：
    ///   ① App.xaml.cs 调用 RegisterFactory(...) 注册所有实现类的工厂
    ///   ② App.xaml.cs 调用 LoadAndInitializeAsync() → 从 JSON 加载配置 → 拓扑排序实例化 → ConnectAsync
    ///   ③ 其他模块通过 ActiveDevices / GetDevice(id) 取用设备引用
    ///   ④ ReloadAllAsync() 支持热重载（如运行时新增/删除设备配置后调用）
    ///
    /// 配置文件位置：{dataDirectory}/hardware_config.json
    /// 若文件不存在，首次启动会写入内置默认配置（SIM_CARD_0 + SimXAxis + SimVacuumIO）。
    ///
    /// 初始化顺序（拓扑分层）：
    ///   · 第1层：ParentDeviceId 为空的顶级设备（运动控制卡）
    ///   · 第2层：ParentDeviceId 非空的子设备（轴、IO）
    ///   若父设备连接失败，依赖它的子设备直接标记跳过（不实例化）并记录警告。
    ///   子设备实例化后，若实现 IAttachedDevice，自动注入父板卡实例引用。
    /// </summary>
    public sealed class HardwareManagerService : IHardwareManagerService, IDisposable
    {
        private readonly string _configFilePath;
        private readonly ILogService _logger;

        // 工厂注册表：ImplementationClassName → Func<HardwareConfig, IHardwareDevice>
        private readonly Dictionary<string, Func<HardwareConfig, IHardwareDevice>> _factories = new();

        // 已激活的设备字典：DeviceId → IHardwareDevice
        private readonly ConcurrentDictionary<string, IHardwareDevice> _activeDevices = new();

        // 配置缓存（内存中）
        private List<HardwareConfig> _configs = new();

        public event EventHandler<IHardwareDevice>? DeviceAdded;
        public event EventHandler<string>? DeviceRemoved;

        public IEnumerable<IHardwareDevice> ActiveDevices => _activeDevices.Values;

        public HardwareManagerService(ILogService logger, string dataDirectory)
        {
            _logger = logger;
            Directory.CreateDirectory(dataDirectory);
            _configFilePath = Path.Combine(dataDirectory, "hardware_config.json");
            LoadConfigs();
        }

        // ── 工厂注册 ───────────────────────────────────────────────────────────

        public void RegisterFactory(string implementationClassName, Func<HardwareConfig, IHardwareDevice> factory)
        {
            _factories[implementationClassName] = factory;
            _logger.Info($"[HardwareManager] 注册工厂: '{implementationClassName}'");
        }

        // ── 配置 CRUD ──────────────────────────────────────────────────────────

        public IEnumerable<HardwareConfig> GetAllConfigs() => _configs.AsReadOnly();

        public HardwareConfig? GetConfig(string deviceId)
            => _configs.FirstOrDefault(c => c.DeviceId == deviceId);

        public void SaveConfig(HardwareConfig config)
        {
            var existing = _configs.FirstOrDefault(c => c.DeviceId == config.DeviceId);
            if (existing != null)
                _configs.Remove(existing);
            _configs.Add(config);
            PersistConfigs();
            _logger.Info($"[HardwareManager] 保存配置: '{config.DeviceId}' ({config.DeviceName})");
        }

        public void DeleteConfig(string deviceId)
        {
            var target = _configs.FirstOrDefault(c => c.DeviceId == deviceId);
            if (target == null) return;
            _configs.Remove(target);
            PersistConfigs();
            _logger.Info($"[HardwareManager] 删除配置: '{deviceId}'");
        }

        // ── 生命周期 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 拓扑分层初始化所有已启用设备：
        ///   第1层 → ParentDeviceId 为空（板卡等顶级设备）
        ///   第2层 → ParentDeviceId 非空（轴、IO 等子设备）
        ///
        /// 若父设备未能成功连接，其所有子设备直接跳过并记录警告。
        /// 子设备实例化完成后，若实现 IAttachedDevice，自动绑定父板卡引用。
        /// </summary>
        public async Task LoadAndInitializeAsync()
        {
            var enabledConfigs = _configs.Where(c => c.IsEnabled).ToList();
            _logger.Info($"[HardwareManager] 开始拓扑初始化，共 {enabledConfigs.Count} 个已启用设备...");

            // ── 第1层：顶级设备（板卡，ParentDeviceId 为空）────────────────────
            var topLevel = enabledConfigs.Where(c => string.IsNullOrEmpty(c.ParentDeviceId)).ToList();
            _logger.Info($"[HardwareManager] 第1层：初始化 {topLevel.Count} 个顶级设备...");
            foreach (var config in topLevel)
                await ActivateDeviceAsync(config);

            // ── 第2层：子设备（轴/IO，ParentDeviceId 非空）──────────────────────
            var children = enabledConfigs.Where(c => !string.IsNullOrEmpty(c.ParentDeviceId)).ToList();
            _logger.Info($"[HardwareManager] 第2层：初始化 {children.Count} 个子设备...");
            foreach (var config in children)
            {
                // 检查父设备是否存在且已连接
                if (!_activeDevices.TryGetValue(config.ParentDeviceId, out var parentDevice))
                {
                    _logger.Warn($"[HardwareManager] 子设备 '{config.DeviceId}' 的父设备 " +
                                 $"'{config.ParentDeviceId}' 未被激活，跳过该子设备。");
                    continue;
                }

                if (!parentDevice.IsConnected)
                {
                    _logger.Warn($"[HardwareManager] 子设备 '{config.DeviceId}' 的父板卡 " +
                                 $"'{config.ParentDeviceId}' 连接失败，跳过该子设备。");
                    continue;
                }

                // 激活子设备，并注入父板卡引用
                var parentCard = parentDevice as IMotionCard;
                await ActivateDeviceAsync(config, parentCard);
            }

            _logger.Success($"[HardwareManager] 初始化完成，活跃设备数: {_activeDevices.Count}");
        }

        public async Task ReloadAllAsync()
        {
            _logger.Info("[HardwareManager] 热重载：正在释放所有活跃设备...");
            foreach (var (id, device) in _activeDevices)
            {
                await device.DisconnectAsync();
                device.Dispose();
                DeviceRemoved?.Invoke(this, id);
            }
            _activeDevices.Clear();

            LoadConfigs();
            await LoadAndInitializeAsync();
        }

        public IHardwareDevice? GetDevice(string deviceId)
            => _activeDevices.TryGetValue(deviceId, out var d) ? d : null;

        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 实例化并连接单个设备。
        /// 若 parentCard 不为 null 且设备实现 IAttachedDevice，则自动绑定父板卡。
        /// </summary>
        private async Task ActivateDeviceAsync(HardwareConfig config, IMotionCard? parentCard = null)
        {
            if (!_factories.TryGetValue(config.ImplementationClassName, out var factory))
            {
                _logger.Warn($"[HardwareManager] 未找到工厂 '{config.ImplementationClassName}'，跳过设备 '{config.DeviceId}'");
                return;
            }

            try
            {
                var device = factory(config);

                // 若设备声明了 IAttachedDevice 且父板卡已就绪，注入父板卡实例引用
                if (parentCard != null && device is IAttachedDevice attachable)
                    attachable.AttachToCard(parentCard);

                var connected = await device.ConnectAsync();
                if (!connected)
                    _logger.Warn($"[HardwareManager] 设备 '{config.DeviceName}' 连接失败，仍加入活跃列表以供 UI 显示");

                _activeDevices[config.DeviceId] = device;
                DeviceAdded?.Invoke(this, device);
                _logger.Info($"[HardwareManager] 设备激活: '{config.DeviceName}' ({config.DeviceId})");
            }
            catch (Exception ex)
            {
                _logger.Error($"[HardwareManager] 激活设备 '{config.DeviceId}' 失败: {ex.Message}");
            }
        }

        private void LoadConfigs()
        {
            if (!File.Exists(_configFilePath))
            {
                _configs = BuildDefaultConfigs();
                PersistConfigs();
                _logger.Info($"[HardwareManager] 配置文件不存在，已生成默认配置 → {_configFilePath}");
                return;
            }

            try
            {
                var json = File.ReadAllText(_configFilePath);
                _configs = JsonSerializer.Deserialize<List<HardwareConfig>>(json)
                           ?? new List<HardwareConfig>();
                _logger.Info($"[HardwareManager] 已加载 {_configs.Count} 条硬件配置");
            }
            catch (Exception ex)
            {
                _logger.Error($"[HardwareManager] 配置文件解析失败，使用默认配置: {ex.Message}");
                _configs = BuildDefaultConfigs();
            }
        }

        private void PersistConfigs()
        {
            try
            {
                var json = JsonSerializer.Serialize(_configs, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_configFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.Error($"[HardwareManager] 配置文件保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 内置默认配置（对应 PF.Workstation.Demo 中的模拟设备），首次运行时写入。
        ///
        /// 层级关系：
        ///   SIM_CARD_0（顶级板卡，ParentDeviceId 为空）
        ///   ├── SIM_X_AXIS_0（轴，ParentDeviceId = "SIM_CARD_0"）
        ///   └── SIM_VACUUM_IO（IO，ParentDeviceId = "SIM_CARD_0"）
        /// </summary>
        private static List<HardwareConfig> BuildDefaultConfigs() =>
        [
            new HardwareConfig
            {
                DeviceId                = "SIM_CARD_0",
                DeviceName              = "模拟运动控制卡[0]",
                Category                = "MotionCard",
                ImplementationClassName = "SimMotionCard",
                IsSimulated             = true,
                IsEnabled               = true,
                ParentDeviceId          = string.Empty,
                ConnectionParameters    = new Dictionary<string, string> { ["CardIndex"] = "0" },
                Remarks                 = "模拟运动控制卡，用于开发/调试"
            },
            new HardwareConfig
            {
                DeviceId                = "SIM_X_AXIS_0",
                DeviceName              = "模拟X轴[0]",
                Category                = "Axis",
                ImplementationClassName = "SimXAxis",
                IsSimulated             = true,
                IsEnabled               = true,
                ParentDeviceId          = "SIM_CARD_0",
                ConnectionParameters    = new Dictionary<string, string> { ["AxisIndex"] = "0" },
                Remarks                 = "模拟X轴，挂载于 SIM_CARD_0"
            },
            new HardwareConfig
            {
                DeviceId                = "SIM_VACUUM_IO",
                DeviceName              = "模拟真空IO卡",
                Category                = "IOController",
                ImplementationClassName = "SimVacuumIO",
                IsSimulated             = true,
                IsEnabled               = true,
                ParentDeviceId          = "SIM_CARD_0",
                Remarks                 = "模拟真空IO卡，挂载于 SIM_CARD_0"
            }
        ];

        public void Dispose()
        {
            foreach (var device in _activeDevices.Values)
            {
                try { device.Dispose(); }
                catch { /* 静默释放 */ }
            }
            _activeDevices.Clear();
        }
    }
}
