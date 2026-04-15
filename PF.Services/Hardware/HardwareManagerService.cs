using PF.Core.Entities.Hardware;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Data.Entity.Category;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PF.Services.Hardware
{
    /// <summary>
    /// 硬件设备管理服务实现
    ///
    /// 设计原则（关注点分离）：
    ///   · 本服务只提供机制（CRUD + 初始化），不包含任何具体应用的默认数据。
    ///   · 具体的设备配置（SIM_CARD_0 等）由上层 Workstation 通过 ImportConfigsAsync 注入。
    ///
    /// 生命周期：
    ///   ① App.xaml.cs 注册 RegisterParamType&lt;HardwareParam, HardwareConfig&gt;()
    ///   ② App.xaml.cs 调用 RegisterFactory(...) 注册所有实现类的工厂
    ///   ③ App.xaml.cs 检测首次运行时调用 ImportConfigsAsync(defaultConfigs) 写入初始配置
    ///   ④ App.xaml.cs 调用 LoadAndInitializeAsync() → 从数据库加载配置 → 拓扑排序实例化 → ConnectAsync
    ///   ⑤ 其他模块通过 ActiveDevices / GetDevice(id) 取用设备引用
    ///   ⑥ ReloadAllAsync() 支持热重载（如运行时新增/删除设备配置后调用）
    ///
    /// 配置持久化：通过 IParamService 读写数据库 HardwareParams 表
    ///   · Key   = HardwareConfig.DeviceId（如 "SIM_CARD_0"）
    ///   · Value = HardwareConfig 对象的 JSON 序列化
    ///
    /// 初始化顺序（拓扑分层）：
    ///   · 第1层：ParentDeviceId 为空的顶级设备（运动控制卡）
    ///   · 第2层：ParentDeviceId 非空的子设备（轴、IO）
    ///   若父设备连接失败，依赖它的子设备直接跳过并记录警告。
    ///   子设备实例化后，若实现 IAttachedDevice，自动注入父板卡实例引用。
    /// </summary>
    public sealed class HardwareManagerService : IHardwareManagerService, IDisposable
    {
        private readonly ILogService _logger;
        private readonly IParamService _paramService;

        // 工厂注册表：ImplementationClassName → Func<HardwareConfig, IHardwareDevice>
        private readonly Dictionary<string, Func<HardwareConfig, IHardwareDevice>> _factories = new();

        // 已激活的设备字典：DeviceId → IHardwareDevice
        private readonly ConcurrentDictionary<string, IHardwareDevice> _activeDevices = new();

        // 内存配置缓存（在 LoadAndInitializeAsync 后有效）
        private List<HardwareConfig> _configs = new();

        public event EventHandler<IHardwareDevice>? DeviceAdded;
        public event EventHandler<string>? DeviceRemoved;

        public IEnumerable<IHardwareDevice> ActiveDevices => _activeDevices.Values;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="paramService">
        ///   参数服务（需事先调用 RegisterParamType&lt;HardwareParam, HardwareConfig&gt;() 注册映射）
        /// </param>
        public HardwareManagerService(ILogService logger, IParamService paramService)
        {
            _logger = logger;
            _paramService = paramService;
        }

        // ── 工厂注册 ───────────────────────────────────────────────────────────

        public void RegisterFactory(string implementationClassName, Func<HardwareConfig, IHardwareDevice> factory)
        {
            _factories[implementationClassName] = factory;
            _logger.Info($"[HardwareManager] 注册工厂: '{implementationClassName}'");
        }

        // ── 配置 CRUD ──────────────────────────────────────────────────────────

        /// <summary>返回内存缓存中的所有配置（LoadAndInitializeAsync 后有效）</summary>
        public IEnumerable<HardwareConfig> GetAllConfigs() => _configs.AsReadOnly();

        public HardwareConfig? GetConfig(string deviceId)
            => _configs.FirstOrDefault(c => c.DeviceId == deviceId);

        /// <summary>
        /// 异步保存单条配置：更新内存缓存并写入数据库
        /// </summary>
        public async Task SaveConfigAsync(HardwareConfig config)
        {
            var existing = _configs.FirstOrDefault(c => c.DeviceId == config.DeviceId);
            if (existing != null)
                _configs.Remove(existing);
            _configs.Add(config);

            await _paramService.SetParamAsync<HardwareConfig>(
                config.DeviceId, config, description: config.Remarks);

            _logger.Info($"[HardwareManager] 保存配置: '{config.DeviceId}' ({config.DeviceName})");
        }

        /// <summary>
        /// 异步删除单条配置：从内存缓存和数据库中同时移除
        /// </summary>
        public async Task DeleteConfigAsync(string deviceId)
        {
            var target = _configs.FirstOrDefault(c => c.DeviceId == deviceId);
            if (target == null) return;

            _configs.Remove(target);
            await _paramService.DeleteParamAsync<HardwareConfig>(deviceId);

            _logger.Info($"[HardwareManager] 删除配置: '{deviceId}'");
        }

        /// <summary>
        /// 批量导入配置：将外部传入的配置集合逐一写入数据库并刷新内存缓存。
        ///
        /// 典型用途：上层 Workstation 在首次启动时检测到数据库为空，
        /// 构造应用层特定的默认设备列表，通过本方法一次性写入。
        /// 已存在的同 DeviceId 配置会被覆盖（upsert 语义）。
        /// </summary>
        /// <param name="configs">要导入的配置集合</param>
        public async Task ImportConfigsAsync(IEnumerable<HardwareConfig> configs)
        {
            var list = configs?.ToList() ?? [];
            if (list.Count == 0)
            {
                _logger.Warn("[HardwareManager] ImportConfigsAsync 收到空集合，跳过导入。");
                return;
            }

            _logger.Info($"[HardwareManager] 开始批量导入 {list.Count} 条硬件配置...");

            foreach (var config in list)
            {
                // 同步更新内存缓存
                var existing = _configs.FirstOrDefault(c => c.DeviceId == config.DeviceId);
                if (existing != null)
                    _configs.Remove(existing);
                _configs.Add(config);

                // 写入数据库
                await _paramService.SetParamAsync<HardwareConfig>(
                    config.DeviceId, config, description: config.Remarks);
            }

            _logger.Success($"[HardwareManager] 批量导入完成，共写入 {list.Count} 条配置。");
        }

        // ── 生命周期 ───────────────────────────────────────────────────────────

        /// <summary>
        /// 从数据库加载配置，然后拓扑分层初始化所有已启用设备：
        ///   第1层 → ParentDeviceId 为空（板卡等顶级设备）
        ///   第2层 → ParentDeviceId 非空（轴、IO 等子设备）
        ///
        /// 无论父设备是否连接成功，子设备均会被实例化并加入活跃列表，确保 UI 可见。
        /// 子设备实例化完成后，若实现 IAttachedDevice，自动绑定父板卡引用。
        /// </summary>
        public async Task LoadAndInitializeAsync(IProgress<SplashProgressPayload>? progress = null)
        {
            await LoadConfigsAsync();

            var enabledConfigs = _configs.Where(c => c.IsEnabled).ToList();
            _logger.Info($"[HardwareManager] 开始拓扑初始化，共 {enabledConfigs.Count} 个已启用设备...");
            progress?.Report(new SplashProgressPayload
            {
                Status = $"开始硬件拓扑初始化，共 {enabledConfigs.Count} 个已启用设备...",
                MsgType = MsgType.Info
            });

            // ── 第1层：顶级设备（板卡，ParentDeviceId 为空）────────────────────
            var topLevel = enabledConfigs.Where(c => string.IsNullOrEmpty(c.ParentDeviceId)).ToList();
            _logger.Info($"[HardwareManager] 第1层：初始化 {topLevel.Count} 个顶级设备...");
            progress?.Report(new SplashProgressPayload
            {
                Status = $"[第1层] 初始化 {topLevel.Count} 个顶级设备（板卡）...",
                MsgType = MsgType.Info
            });
            foreach (var config in topLevel)
                await ActivateDeviceAsync(config, parentCard: null, progress);

            // ── 第2层：子设备（轴/IO，ParentDeviceId 非空）──────────────────────
            var children = enabledConfigs.Where(c => !string.IsNullOrEmpty(c.ParentDeviceId)).ToList();
            _logger.Info($"[HardwareManager] 第2层：初始化 {children.Count} 个子设备...");
            progress?.Report(new SplashProgressPayload
            {
                Status = $"[第2层] 初始化 {children.Count} 个子设备（轴/IO）...",
                MsgType = MsgType.Info
            });
            foreach (var config in children)
            {
                IMotionCard? parentCard = null;

                if (!_activeDevices.TryGetValue(config.ParentDeviceId, out var parentDevice))
                {
                    _logger.Warn($"[HardwareManager] 子设备 '{config.DeviceId}' 的父设备 " +
                                 $"'{config.ParentDeviceId}' 未被激活，仍强制实例化子设备以供 UI 显示。");
                }
                else
                {
                    if (!parentDevice.IsConnected)
                        _logger.Warn($"[HardwareManager] 子设备 '{config.DeviceId}' 的父板卡 " +
                                     $"'{config.ParentDeviceId}' 未连接，子设备将以离线状态加入活跃列表。");

                    parentCard = parentDevice as IMotionCard;
                }

                await ActivateDeviceAsync(config, parentCard, progress);
            }

            _logger.Success($"[HardwareManager] 初始化完成，活跃设备数: {_activeDevices.Count}");
            progress?.Report(new SplashProgressPayload
            {
                Status = $"硬件初始化完成，活跃设备数: {_activeDevices.Count}",
                MsgType = MsgType.Success
            });
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

            // LoadAndInitializeAsync 内部会重新调用 LoadConfigsAsync 从数据库加载最新配置
            await LoadAndInitializeAsync();
        }

        /// <summary>
        /// 原子性地切换全局模拟模式：
        ///   1. 将内存中所有配置的 IsSimulated 改为 enabled
        ///   2. 通过 ImportConfigsAsync 批量写入数据库（持久化）
        ///   3. 调用 ReloadAllAsync 触发热重载（断开旧实例 → 重新实例化 → 以新模式连接）
        /// </summary>
        public async Task SetGlobalSimulationModeAsync(bool enabled)
        {
            var configs = _configs.ToList();
            if (configs.Count == 0)
            {
                _logger.Warn("[HardwareManager] SetGlobalSimulationModeAsync：当前无硬件配置，跳过。");
                return;
            }

            _logger.Info($"[HardwareManager] 切换全局模拟模式 → {(enabled ? "模拟" : "真实硬件")}，共 {configs.Count} 个设备...");

            foreach (var cfg in configs)
                cfg.IsSimulated = enabled;

            await ImportConfigsAsync(configs);

            _logger.Success($"[HardwareManager] 全局模拟模式配置已更新为: {(enabled ? "模拟" : "真实硬件")}，请手动重载硬件生效。");
        }

        public IHardwareDevice? GetDevice(string deviceId)
            => _activeDevices.TryGetValue(deviceId, out var d) ? d : null;

        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 从数据库加载所有硬件配置到内存缓存。
        /// 若数据库为空，仅记录日志并保持 _configs 为空——
        /// 上层 Workstation 应在调用 LoadAndInitializeAsync 之前通过 ImportConfigsAsync 注入配置。
        /// </summary>
        private async Task LoadConfigsAsync()
        {
            try
            {
                var paramInfos = await _paramService.GetParamsByCategoryAsync<HardwareParam>();

                _configs = paramInfos
                    .Select(p =>
                    {
                        try { return JsonSerializer.Deserialize<HardwareConfig>(p.Value.ToString()); }
                        catch { return null; }
                    })
                    .Where(c => c != null)
                    .ToList()!;

                if (!_configs.Any())
                    _logger.Info("[HardwareManager] 数据库中无硬件配置，等待外部注入或配置。");
                else
                    _logger.Info($"[HardwareManager] 从数据库加载了 {_configs.Count} 条硬件配置。");
            }
            catch (Exception ex)
            {
                _logger.Error($"[HardwareManager] 从数据库加载硬件配置失败: {ex.Message}");
                _configs = [];
            }
        }

        /// <summary>
        /// 实例化并连接单个设备。
        /// 若 parentCard 不为 null 且设备实现 IAttachedDevice，则自动绑定父板卡。
        /// </summary>
        private async Task ActivateDeviceAsync(HardwareConfig config, IMotionCard? parentCard = null,
            IProgress<SplashProgressPayload>? progress = null)
        {
            if (!_factories.TryGetValue(config.ImplementationClassName, out var factory))
            {
                _logger.Warn($"[HardwareManager] 未找到工厂 '{config.ImplementationClassName}'，跳过设备 '{config.DeviceId}'");
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"跳过 [{config.DeviceName}]：未找到对应工厂实现",
                    MsgType = MsgType.Warning
                });
                return;
            }

            // ── 加载前汇报 ─────────────────────────────────────────────────────
            progress?.Report(new SplashProgressPayload
            {
                Status = $"正在初始化: [{config.DeviceName}]...",
                MsgType = MsgType.Info
            });

            try
            {
                var device = factory(config);

                if (parentCard != null && device is IAttachedDevice attachable)
                    attachable.AttachToCard(parentCard);

                // 先注册到活跃列表并通知 UI，确保设备无论连接结果如何均可在界面显示
                _activeDevices[config.DeviceId] = device;
                DeviceAdded?.Invoke(this, device);
                _logger.Info($"[HardwareManager] 设备已注册: '{config.DeviceName}' ({config.DeviceId})");

                // 独立尝试连接，失败只记录警告，不阻断后续设备的初始化流程
                try
                {
                    var connected = await device.ConnectAsync();
                    if (connected)
                    {
                        _logger.Info($"[HardwareManager] 设备 '{config.DeviceName}' 连接成功。");
                        // ── 连接成功汇报 ──────────────────────────────────────
                        progress?.Report(new SplashProgressPayload
                        {
                            Status = $"[{config.DeviceName}] 连接成功",
                            MsgType = MsgType.Success
                        });
                    }
                    else
                    {
                        _logger.Warn($"[HardwareManager] 设备 '{config.DeviceName}' 连接返回 false，" +
                                     "设备已保留在活跃列表，可在 UI 中手动切换模拟模式后重连。");
                        progress?.Report(new SplashProgressPayload
                        {
                            Status = $"[{config.DeviceName}] 连接返回 false，已保留以供手动重连",
                            MsgType = MsgType.Warning
                        });
                    }
                }
                catch (Exception connEx)
                {
                    _logger.Error($"[HardwareManager] 设备 '{config.DeviceName}' 连接时发生异常: {connEx.Message}，" +
                                  "设备已保留在活跃列表。");
                    // ── 连接异常汇报 ──────────────────────────────────────────
                    progress?.Report(new SplashProgressPayload
                    {
                        Status = $"[{config.DeviceName}] 连接异常: {connEx.Message}",
                        MsgType = MsgType.Error
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[HardwareManager] 实例化设备 '{config.DeviceId}' 失败: {ex.Message}");
                // ── 实例化异常汇报 ────────────────────────────────────────────
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"[{config.DeviceName}] 实例化失败: {ex.Message}",
                    MsgType = MsgType.Fatal
                });
            }
        }

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
