using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware;
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
    ///   ② App.xaml.cs 调用 LoadAndInitializeAsync() → 从 JSON 加载配置 → 工厂实例化 → ConnectAsync
    ///   ③ 其他模块通过 ActiveDevices / GetDevice(id) 取用设备引用
    ///   ④ ReloadAllAsync() 支持热重载（如运行时新增/删除设备配置后调用）
    ///
    /// 配置文件位置：{dataDirectory}/hardware_config.json
    /// 若文件不存在，首次启动会写入内置默认配置（SimXAxis + SimVacuumIO）。
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

        public async Task LoadAndInitializeAsync()
        {
            _logger.Info($"[HardwareManager] 开始加载并初始化 {_configs.Count(c => c.IsEnabled)} 个已启用设备...");

            foreach (var config in _configs.Where(c => c.IsEnabled))
                await ActivateDeviceAsync(config);

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

        private async Task ActivateDeviceAsync(HardwareConfig config)
        {
            if (!_factories.TryGetValue(config.ImplementationClassName, out var factory))
            {
                _logger.Warn($"[HardwareManager] 未找到工厂 '{config.ImplementationClassName}'，跳过设备 '{config.DeviceId}'");
                return;
            }

            try
            {
                var device = factory(config);
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
        /// 内置默认配置（对应 PF.Workstation.Demo 中的两个模拟设备），
        /// 首次运行时写入，方便开发者快速上手。
        /// </summary>
        private static List<HardwareConfig> BuildDefaultConfigs() =>
        [
            new HardwareConfig
            {
                DeviceId               = "SIM_X_AXIS_0",
                DeviceName             = "模拟X轴[0]",
                Category               = "Axis",
                ImplementationClassName= "SimXAxis",
                IsSimulated            = true,
                IsEnabled              = true,
                ConnectionParameters   = new Dictionary<string, string> { ["AxisIndex"] = "0" },
                Remarks                = "模拟X轴，用于开发/调试"
            },
            new HardwareConfig
            {
                DeviceId               = "SIM_VACUUM_IO",
                DeviceName             = "模拟真空IO卡",
                Category               = "IOController",
                ImplementationClassName= "SimVacuumIO",
                IsSimulated            = true,
                IsEnabled              = true,
                Remarks                = "模拟真空IO卡，用于开发/调试"
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
