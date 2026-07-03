using PF.Core.Entities.Communication;
using PF.Core.Enums;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Logging;
using PF.Core.Models;
using PF.Data.Entity.Category;
using System.Collections.Concurrent;
using System.Text.Json;

namespace PF.Services.Communication
{
    /// <summary>
    /// 通讯实例管理服务实现（结构对齐 HardwareManagerService，但通讯实例之间没有硬件那种父子拓扑依赖，
    /// 加载流程是单层的：逐个实例化 + StartAsync，不需要按层级排序）。
    ///
    /// 生命周期：
    ///   ① App 启动时调用 RegisterParamType&lt;CommunicationParam, CommunicationConfig&gt;()
    ///   ② 调用 RegisterFactory(...) 注册所有实现类的工厂
    ///   ③ 首次运行时调用 ImportConfigsAsync(defaultConfigs) 写入初始配置
    ///   ④ 调用 LoadAndInitializeAsync() → 从数据库加载配置 → 实例化 → StartAsync
    ///      —— 该调用必须先于 IHardwareManagerService.LoadAndInitializeAsync()
    ///   ⑤ 其他模块通过 ActiveCommunications / GetCommunication&lt;T&gt; 取用实例引用
    ///   ⑥ ReloadAllAsync() 支持热重载
    ///
    /// 配置持久化：通过 IParamService 读写数据库 CommunicationParams 表
    ///   · Key   = CommunicationConfig.InstanceId
    ///   · Value = CommunicationConfig 对象的 JSON 序列化
    /// </summary>
    public sealed class CommunicationManagerService : ICommunicationManagerService, IDisposable
    {
        private readonly ILogService _logger;
        private readonly IParamService _paramService;

        // 工厂注册表：ImplementationClassName → Func<CommunicationConfig, ICommunication>
        private readonly Dictionary<string, Func<CommunicationConfig, ICommunication>> _factories = new();

        // 已激活的实例字典：InstanceId → ICommunication
        private readonly ConcurrentDictionary<string, ICommunication> _activeCommunications = new();

        // 内存配置缓存（在 LoadAndInitializeAsync 后有效）。
        // List 非线程安全，而调试面板的配置 CRUD 与热重载可能并发访问，读写都必须持 _configsLock
        private List<CommunicationConfig> _configs = new();
        private readonly object _configsLock = new();

        /// <inheritdoc/>
        public event EventHandler<ICommunication>? CommunicationAdded;
        /// <inheritdoc/>
        public event EventHandler<string>? CommunicationRemoved;

        /// <inheritdoc/>
        public IEnumerable<ICommunication> ActiveCommunications => _activeCommunications.Values;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="logger">日志服务</param>
        /// <param name="paramService">
        ///   参数服务（需事先调用 RegisterParamType&lt;CommunicationParam, CommunicationConfig&gt;() 注册映射）
        /// </param>
        public CommunicationManagerService(ILogService logger, IParamService paramService)
        {
            _logger = logger;
            _paramService = paramService;
        }

        // ── 工厂注册 ───────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public void RegisterFactory(string implementationClassName, Func<CommunicationConfig, ICommunication> factory)
        {
            _factories[implementationClassName] = factory;
            _logger.Info($"[CommunicationManager] 注册工厂: '{implementationClassName}'");
        }

        // ── 配置 CRUD ──────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public CommunicationConfig? GetConfig(string instanceId)
        {
            lock (_configsLock)
                return _configs.FirstOrDefault(c => c.InstanceId == instanceId);
        }

        /// <inheritdoc/>
        public async Task SaveConfigAsync(CommunicationConfig config)
        {
            lock (_configsLock)
            {
                var existing = _configs.FirstOrDefault(c => c.InstanceId == config.InstanceId);
                if (existing != null)
                    _configs.Remove(existing);
                _configs.Add(config);
            }

            await _paramService.SetParamAsync<CommunicationConfig>(
                config.InstanceId, config, description: config.Remarks);

            _logger.Info($"[CommunicationManager] 保存配置: '{config.InstanceId}' ({config.DisplayName})");
        }

        /// <inheritdoc/>
        public async Task DeleteConfigAsync(string instanceId)
        {
            lock (_configsLock)
            {
                var target = _configs.FirstOrDefault(c => c.InstanceId == instanceId);
                if (target == null) return;
                _configs.Remove(target);
            }

            await _paramService.DeleteParamAsync<CommunicationConfig>(instanceId);

            _logger.Info($"[CommunicationManager] 删除配置: '{instanceId}'");
        }

        /// <inheritdoc/>
        public async Task ImportConfigsAsync(IEnumerable<CommunicationConfig> configs)
        {
            var list = configs?.ToList() ?? [];
            if (list.Count == 0)
            {
                _logger.Warn("[CommunicationManager] ImportConfigsAsync 收到空集合，跳过导入。");
                return;
            }

            _logger.Info($"[CommunicationManager] 开始批量导入 {list.Count} 条通讯配置...");

            foreach (var config in list)
            {
                lock (_configsLock)
                {
                    var existing = _configs.FirstOrDefault(c => c.InstanceId == config.InstanceId);
                    if (existing != null)
                        _configs.Remove(existing);
                    _configs.Add(config);
                }

                await _paramService.SetParamAsync<CommunicationConfig>(
                    config.InstanceId, config, description: config.Remarks);
            }

            _logger.Success($"[CommunicationManager] 批量导入完成，共写入 {list.Count} 条配置。");
        }

        // ── 生命周期 ───────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public async Task LoadAndInitializeAsync(IProgress<SplashProgressPayload>? progress = null)
        {
            // 加载配置（加入异常保护）
            try
            {
                await LoadConfigsAsync();
            }
            catch (Exception ex)
            {
                _logger.Error($"[CommunicationManager] 加载配置失败: {ex.Message}");
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"加载通讯配置失败: {ex.Message}",
                    MsgType = MsgType.Error
                });
                return;
            }

            List<CommunicationConfig> enabledConfigs;
            lock (_configsLock) enabledConfigs = _configs.Where(c => c.IsEnabled).ToList();
            _logger.Info($"[CommunicationManager] 开始初始化，共 {enabledConfigs.Count} 个已启用通讯实例...");

            progress?.Report(new SplashProgressPayload
            {
                Status = $"通讯设备开始初始化，共 {enabledConfigs.Count} 个已启用通讯实例...",
                MsgType = MsgType.Info
            });

            // 逐个激活，并报告整体进度
            for (int i = 0; i < enabledConfigs.Count; i++)
            {
                var config = enabledConfigs[i];
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"正在处理通讯实例 ({i + 1}/{enabledConfigs.Count}): {config.DisplayName}",
                    MsgType = MsgType.Info
                });

                await ActivateCommunicationAsync(config, progress);
            }

            _logger.Success($"[CommunicationManager] 初始化完成，活跃实例数: {_activeCommunications.Count}");
            progress?.Report(new SplashProgressPayload
            {
                Status = $"通讯初始化完成，活跃实例数: {_activeCommunications.Count}",
                MsgType = MsgType.Success
            });
            await Task.Delay(300);
        }

        /// <inheritdoc/>
        public async Task ReloadAllAsync()
        {
            _logger.Info("[CommunicationManager] 热重载：正在停止所有活跃实例...");
            foreach (var (id, comm) in _activeCommunications)
            {
                await comm.StopAsync();
                if (comm is IDisposable disposable) disposable.Dispose();
                CommunicationRemoved?.Invoke(this, id);
            }
            _activeCommunications.Clear();

            await LoadAndInitializeAsync();
        }

        /// <inheritdoc/>
        public T GetCommunication<T>(string instanceId) where T : class
        {
            if (!_activeCommunications.TryGetValue(instanceId, out var comm))
                throw new InvalidOperationException($"未找到通讯实例 '{instanceId}'，请检查配置里的 InstanceId 是否正确、该实例是否已启用。");

            if (comm is not T typed)
                throw new InvalidOperationException($"通讯实例 '{instanceId}' 的实际类型 '{comm.GetType().Name}' 与请求的类型 '{typeof(T).Name}' 不匹配。");

            return typed;
        }

        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>从数据库加载所有通讯配置到内存缓存</summary>
        private async Task LoadConfigsAsync()
        {
            try
            {
                var paramInfos = await _paramService.GetParamsByCategoryAsync<CommunicationParam>();

                var configs = new List<CommunicationConfig>();
                foreach (var p in paramInfos)
                {
                    try
                    {
                        var config = JsonSerializer.Deserialize<CommunicationConfig>(p.Value.ToString());
                        if (config != null)
                            configs.Add(config);
                        else
                            _logger.Warn($"[CommunicationManager] 配置 '{p.Name}' 反序列化结果为空，已跳过。");
                    }
                    catch (Exception ex)
                    {
                        // 静默吞掉会让配置"凭空消失"且无从排查，必须留下带 Key 的告警
                        _logger.Warn($"[CommunicationManager] 配置 '{p.Name}' 反序列化失败，已跳过: {ex.Message}");
                    }
                }

                lock (_configsLock) _configs = configs;

                if (configs.Count == 0)
                    _logger.Info("[CommunicationManager] 数据库中无通讯配置，等待外部注入或配置。");
                else
                    _logger.Info($"[CommunicationManager] 从数据库加载了 {configs.Count} 条通讯配置。");
            }
            catch (Exception ex)
            {
                _logger.Error($"[CommunicationManager] 从数据库加载通讯配置失败: {ex.Message}");
                lock (_configsLock) _configs = [];
            }
        }

        /// <summary>实例化并启动单个通讯实例</summary>
        private async Task ActivateCommunicationAsync(CommunicationConfig config, IProgress<SplashProgressPayload>? progress = null)
        {
            if (!_factories.TryGetValue(config.ImplementationClassName, out var factory))
            {
                _logger.Warn($"[CommunicationManager] 未找到工厂 '{config.ImplementationClassName}'，跳过实例 '{config.InstanceId}'");
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"跳过 '{config.DisplayName}': 未找到工厂 {config.ImplementationClassName}",
                    MsgType = MsgType.Warning
                });
                return;
            }

            try
            {
                var comm = factory(config);
                if (!_activeCommunications.TryAdd(config.InstanceId, comm))
                {
                    // 直接索引覆盖会让先注册的实例既不 Stop 也不 Dispose 地泄漏，重复 InstanceId 属配置错误，保留前者、跳过后者
                    _logger.Warn($"[CommunicationManager] InstanceId '{config.InstanceId}' 重复，已保留先注册的实例、跳过 '{config.DisplayName}'，请检查配置。");
                    if (comm is IDisposable duplicated) duplicated.Dispose();
                    progress?.Report(new SplashProgressPayload
                    {
                        Status = $"跳过 '{config.DisplayName}': InstanceId '{config.InstanceId}' 重复",
                        MsgType = MsgType.Warning
                    });
               
                    return;
                }
                CommunicationAdded?.Invoke(this, comm);
                _logger.Info($"[CommunicationManager] 实例已注册: '{config.DisplayName}' ({config.InstanceId})");

                if (!config.AutoStart)
                {
                    _logger.Info($"[CommunicationManager] 实例 '{config.DisplayName}' AutoStart=false，跳过自动启动，连接生命周期交由外部（如硬件层）驱动。");
                    progress?.Report(new SplashProgressPayload
                    {
                        Status = $"'{config.DisplayName}' 注册完成（AutoStart=false，跳过自动启动）",
                        MsgType = MsgType.Success
                    });
                  
                    return;
                }

                // 报告：正在启动
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"正在启动通讯实例: {config.DisplayName}...",
                    MsgType = MsgType.Info
                });
               
                try
                {
                    var started = await comm.StartAsync();
                    if (started)
                    {
                        _logger.Info($"[CommunicationManager] 实例 '{config.DisplayName}' 启动成功。");
                        progress?.Report(new SplashProgressPayload
                        {
                            Status = $"'{config.DisplayName}' 启动成功",
                            MsgType = MsgType.Success
                        });
                     
                    }
                    else
                    {
                        _logger.Warn($"[CommunicationManager] 实例 '{config.DisplayName}' 启动返回 false，已保留在活跃列表，可在调试面板手动重试。");
                        progress?.Report(new SplashProgressPayload
                        {
                            Status = $"'{config.DisplayName}' 启动返回 false，已保留在活跃列表",
                            MsgType = MsgType.Warning
                        });
                       
                    }
                }
                catch (Exception startEx)
                {
                    _logger.Error($"[CommunicationManager] 实例 '{config.DisplayName}' 启动时发生异常: {startEx.Message}，已保留在活跃列表。");
                    progress?.Report(new SplashProgressPayload
                    {
                        Status = $"'{config.DisplayName}' 启动异常: {startEx.Message}",
                        MsgType = MsgType.Error
                    });
                  
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[CommunicationManager] 实例化 '{config.InstanceId}' 失败: {ex.Message}");
                progress?.Report(new SplashProgressPayload
                {
                    Status = $"实例化 '{config.DisplayName}' 失败: {ex.Message}",
                    MsgType = MsgType.Error
                });
                
            }
        }





        /// <inheritdoc/>
        public void Dispose()
        {
            foreach (var comm in _activeCommunications.Values)
            {
                try { if (comm is IDisposable disposable) disposable.Dispose(); }
                catch { /* 静默释放 */ }
            }
            _activeCommunications.Clear();
        }

    }
}
