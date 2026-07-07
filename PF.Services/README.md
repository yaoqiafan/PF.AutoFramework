# PF.Services

PF.AutoFramework 核心业务服务实现层，包含 8 个服务的具体实现。所有服务均以单例注册，通过 DI 注入使用。

## 服务一览

| 服务实现类 | 接口 | 说明 |
|---|---|---|
| `ParamService` | `IParamService` | 参数持久化读写 |
| `HardwareManagerService` | `IHardwareManagerService` | 硬件生命周期管理 |
| `AlarmService` | `IAlarmService` | 报警触发与持久化 |
| `LogService` | `ILogService` | 结构化日志 |
| `ProductionDataService` | `IProductionDataService` | 生产数据记录 |
| `UserService` | `IUserService` | 用户认证与权限 |
| `StationSyncService` | `IStationSyncService` | 跨工站信号量 |
| `AppTimerService` | `IAppTimerService` | 定时任务调度 |
| `CommunicationManagerService` | `ICommunicationManagerService` | 通讯实例（TCP/串口/文件传输等）生命周期管理 |

## DI 注册（App.xaml.cs）

```csharp
containerRegistry.RegisterSingleton<IParamService, ParamService>();
containerRegistry.RegisterSingleton<IHardwareManagerService, HardwareManagerService>();
containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
containerRegistry.RegisterSingleton<ILogService, LogService>();
containerRegistry.RegisterSingleton<IProductionDataService, ProductionDataService>();
containerRegistry.RegisterSingleton<IUserService, UserService>();
containerRegistry.RegisterSingleton<IStationSyncService, StationSyncService>();
containerRegistry.RegisterSingleton<IAppTimerService, AppTimerService>();
```

## ParamService

- 泛型约束：`where T : class`（不可直接用 `int`、`bool` 等值类型，需包装为 POCO）
- 值未变更时跳过写库，避免不必要的磁盘 IO
- `ParamChanged` 事件携带完整审计信息（旧值 / 新值 / 用户 / 时间）

```csharp
// 注册参数类型（App.xaml.cs 启动时）
paramService.RegisterParamType<FeedParam, FeedParamEntity>();

// 读取
var param = await _paramService.GetAsync<FeedParam>();

// 写入（自动持久化）
await _paramService.SetAsync(new FeedParam { PickSpeed = 500 });

// 订阅变更
_paramService.ParamChanged += (sender, e) =>
{
    _log.Info($"参数变更：{e.ParamType.Name} 由 {e.OldValue} → {e.NewValue}，操作人：{e.UserName}");
};
```

## HardwareManagerService

```csharp
// 注册硬件工厂（App.xaml.cs）
hwManager.RegisterFactory("LTDMCMotionCard", config => new LTDMCMotionCard(config));
hwManager.RegisterFactory("EtherCatAxis",    config => new EtherCatAxis(config));

// 加载并初始化（拓扑排序，父卡优先）
await hwManager.LoadAndInitializeAsync();

// 切换全局仿真模式（需配合 ReloadAllAsync）
await hwManager.SetGlobalSimulationModeAsync(true);
await hwManager.ReloadAllAsync();
```

## AlarmService

- 复合键 `(Source, ErrorCode)` 幂等触发，同一报警不重复记录
- `Channel<PersistJob>`（容量 10000）异步持久化，不阻塞主线程
- 按年动态路由表 `AlarmRecord_{Year}`

```csharp
// 触发报警
await _alarmService.TriggerAsync("FeedStation", 1001);

// 消除报警
await _alarmService.ClearAsync("FeedStation", 1001);

// 查询活跃报警
var active = await _alarmService.GetActiveAlarmsAsync();
```

## LogService

- 内存循环缓冲 1000 条（UI 实时绑定）
- `Channel` 异步文件写入，按小时滚动，30 天自动清理
- 路径：`%APPDATA%\PFAutoFrameWork\Log\`

```csharp
_log.Info("初始化完成");
_log.Warning("传感器信号异常");
_log.Error("轴运动超时", ex);
```

## StationSyncService

跨工站命名信号量，用于工站间握手协调。

```csharp
// 初始化信号量（主控构造函数中注册）
_sync.Register("AllowFeed",    initialCount: 0);
_sync.Register("FeedComplete", initialCount: 0);

// 等待许可（工站循环内）
await _sync.WaitAsync("AllowFeed", token);

// 释放信号（上游工站）
_sync.Release("FeedComplete");

// 复位（主控 OnAfterResetSuccess 中）
_sync.ResetAll(initialCount: 0);
// 或单独复位
_sync.ResetSingleSignal("AllowFeed", initialCount: 0);
```

## CommunicationManagerService

结构对齐 `HardwareManagerService`（工厂注册 + 数据库加载配置 + 统一生命周期），但通讯实例之间没有硬件那种父子拓扑依赖，加载流程是单层的：逐个实例化 + `StartAsync`，不需要按层级排序。支持 `AutoStart` 开关（配置为 false 时仅注册不自动启动，供调试面板手动控制）。

```csharp
commManager.RegisterFactory("TCPClient", config => new TCPClient(config));
await commManager.LoadAndInitializeAsync();   // 必须先于 IHardwareManagerService.LoadAndInitializeAsync()

// 其他模块取用已实例化的通讯连接
var client = commManager.GetCommunication<TCPClient>("Camera1Client");
```

## AppTimerService

```csharp
// 注册定时任务（OnInitialized 中）
_timer.RegisterDaily("DiskCheck",    TimeOnly.Parse("08:00"), CheckDiskUsageAsync);
_timer.RegisterDaily("ImageCleanup", TimeOnly.Parse("08:00"), CleanOldImagesAsync);
```
