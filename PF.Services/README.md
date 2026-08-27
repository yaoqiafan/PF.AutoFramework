# PF.Services

PF.AutoFramework 核心业务服务实现层，当前版本 1.0.5。目录下共 14 个服务类（早期文档写"8 个"已经过时——`HardwareInputMonitor`/`TowerLightService`/`TowerLightDoWriter`/`IOMappingService`/`AlarmDictionaryService`/`CommunicationManagerService` 都是后来补的），均以单例通过 DI 注入使用。

## 服务一览

| 服务实现类 | 接口 | 说明 |
|---|---|---|
| `ParamService` | `IParamService` | 参数持久化读写 |
| `HardwareManagerService` | `IHardwareManagerService` | 硬件生命周期管理（工厂注册、拓扑加载、仿真模式切换） |
| `CommunicationManagerService` | `ICommunicationManagerService` | 通讯实例（TCP/串口/文件传输等）生命周期管理，结构对齐 `HardwareManagerService` |
| `AlarmService` | `IAlarmService` | 报警触发 / 消除 / 历史查询 |
| `AlarmDictionaryService` | `IAlarmDictionaryService` | 报警码字典（`[AlarmInfo]` 反射扫描 + 数据库覆盖） |
| `LogService` | `ILogService` | 结构化分类日志 |
| `ProductionDataService` | `IProductionDataService` | 泛型生产数据记录 + 导出 |
| `UserService` | `IUserService` | 登录 / 权限 / 用户管理 |
| `StationSyncService` | `IStationSyncService` | 跨工站命名信号量（支持分组 scope） |
| `AppTimerService` | `IAppTimerService` | 定时任务调度（间隔 / 每日 / 每周 / 每月） |
| `HardwareInputMonitor` | — | 通用输入轮询 + 安全门专项监控 |
| `TowerLightService` / `TowerLightDoWriter` | `ITowerLightService` / `ITowerLightDoWriter` | 三色灯状态机 + DO 写入适配 |
| `IOMappingService` | — | 枚举 ↔ IO 端口名映射，供调试面板显示中文释义 |

## DI 注册（App.xaml.cs）

```csharp
containerRegistry.RegisterSingleton<IParamService, ParamService>();
containerRegistry.RegisterSingleton<IHardwareManagerService, HardwareManagerService>();
containerRegistry.RegisterSingleton<ICommunicationManagerService, CommunicationManagerService>();
containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
containerRegistry.RegisterSingleton<ILogService, LogService>();
containerRegistry.RegisterSingleton<IProductionDataService, ProductionDataService>();
containerRegistry.RegisterSingleton<IUserService, UserService>();
containerRegistry.RegisterSingleton<IStationSyncService, StationSyncService>();
containerRegistry.RegisterSingleton<IAppTimerService, AppTimerService>();
```

## ParamService

- 泛型约束：`where T : class`（不可直接用 `int`/`bool` 等值类型，需包装为 POCO）
- 值未变更时跳过写库
- `ParamChanged` 事件携带完整审计信息

```csharp
paramService.RegisterParamType<FeedParam, FeedParamEntity>();          // App.xaml.cs 启动时，实体在前、模型在后

await _paramService.SetParamAsync("Feed1", new FeedParam { PickSpeed = 500 }, userInfo, "调整取料速度");
var param  = await _paramService.GetParamAsync<FeedParam>("Feed1");                 // 找不到返回 null
var param2 = await _paramService.GetParamAsync("Feed1", new FeedParam());            // 找不到返回给定默认值
await _paramService.DeleteParamAsync<FeedParam>("Feed1", userInfo);
await _paramService.BatchSetParamsAsync(new Dictionary<string, FeedParam> { ["Feed1"] = p1, ["Feed2"] = p2 }, userInfo);
var all      = await _paramService.GetAllParamsAsync();
var byCat    = await _paramService.GetParamsByCategoryAsync<FeedParam>();

_paramService.ParamChanged += (sender, e) =>
    _log.Info($"参数变更：{e.ParamName} 由 {e.OldValue} → {e.NewValue}，操作人：{e.UserName}");
```

## HardwareManagerService

```csharp
hwManager.RegisterFactory("LTDMCMotionCard", config => new LTDMCMotionCard(config));
await hwManager.LoadAndInitializeAsync(progress);   // 拓扑排序：父卡优先，progress 可选（Splash 进度回调）

await hwManager.SetGlobalSimulationModeAsync(true);
await hwManager.ReloadAllAsync();                    // 仿真模式切换后必须调用才生效

var cfg    = hwManager.GetConfig("SIM_X_AXIS_0");
var device = hwManager.GetDevice("SIM_X_AXIS_0");
await hwManager.SaveConfigAsync(newCfg);
await hwManager.DeleteConfigAsync("SIM_X_AXIS_0");
await hwManager.ImportConfigsAsync(configs);
```

v1.0.5 起父设备引用由 `IMotionCard` 放宽为 `IHardwareDevice`，挂载改走 `IAttachedDevice.TryAttachTo`——线阵相机可以挂在图像采集卡下而不再局限于运动控制卡；分层规则不变（只看 `ParentDeviceId` 是否为空），挂载类型不匹配只记 Warn，设备仍以未挂载状态进入活跃列表，不会拖垮整机初始化。

## CommunicationManagerService

结构对齐 `HardwareManagerService`，但通讯实例之间没有硬件那种父子拓扑，加载是单层的（逐个实例化 + `StartAsync`）：

```csharp
commManager.RegisterFactory("TCPClient", config => new TCPClient(config));
await commManager.LoadAndInitializeAsync(progress);   // 必须先于 IHardwareManagerService.LoadAndInitializeAsync()
await commManager.ReloadAllAsync();

var client = commManager.GetCommunication<TCPClient>("Camera1Client");
await commManager.SaveConfigAsync(cfg);
await commManager.DeleteConfigAsync("Camera1Client");
```

## AlarmService

- 触发/消除是**同步方法**（内部异步持久化，不阻塞调用方）
- `Channel<PersistJob>`（容量 10000）异步落库，按年动态路由 `AlarmRecord_{Year}`
- `TriggerAlarm` 内部按 `(Source, ErrorCode)` 幂等，重复触发不重复记录

```csharp
bool triggered = _alarmService.TriggerAlarm("FeedStation", "HW_SRV_001");
bool triggered2 = _alarmService.TriggerAlarm("FeedStation", "HW_SRV_001", "轴 X 超时未到位");   // 带运行期附加信息

_alarmService.ClearAlarm("FeedStation");                 // 清除该来源全部活跃报警
_alarmService.ClearAlarm("FeedStation", "HW_SRV_001");    // 清除单条
_alarmService.ClearAllActiveAlarms();                     // 全局清除（复位流程用）

IReadOnlyList<AlarmRecord> active = _alarmService.ActiveAlarms;   // 属性，非异步方法
var history = await _alarmService.QueryHistoricalAlarmsAsync(...);

_alarmService.AlarmTriggered += (s, record) => { /* BaseMechanism.RegisterHardwareDevice 用它做报警聚合 */ };
_alarmService.AlarmCleared   += (s, record) => { };
```

## AlarmDictionaryService

```csharp
await _alarmDict.InitializeAsync();          // 启动时反射扫描 [AlarmInfo] + 加载数据库覆盖项
AlarmInfo info = _alarmDict.GetAlarmInfo("HW_SRV_001");   // 未知报警码兜底返回 MessageID=-1 / MessageIDHex="NoneError"
IReadOnlyDictionary<string, AlarmInfo> all = _alarmDict.GetAll();
```

## LogService

- 内存循环缓冲 1000 条（`OnLogAdded` 事件供 UI 实时绑定，非 Prism 事件）
- `Channel` 异步文件写入，按小时滚动，30 天自动清理
- 支持"分类"（Category）体系：每个分类可独立设最低级别 + 独立文件前缀

```csharp
_log.Info("初始化完成");
_log.Warn("传感器信号异常");
_log.Error("轴运动超时", ex);

_log.AddCategory("Communication", LogLevel.Debug, "Comm");   // 分类、最低级别、文件名前缀
_log.Info("收到心跳", "Communication");

var today  = _log.QueryLogsToday(LogLevel.Warn);
var range  = _log.QueryLogs(start, end, LogLevel.Error, "Communication");
var errors = _log.QueryErrorHistoricalLogs(start, end);

_log.OnLogAdded += entry => { /* 更新 UI 绑定的 ObservableCollection */ };
```

路径：`D:\PF_Logs\`（与配置目录不同，不按项目隔离，见仓库根 `CLAUDE.md`）。

## UserService

```csharp
bool ok = await _userService.LoginAsync(userName, password);
_userService.Logout();
_userService.ResetToOperator();                          // 无人操作超时降级

bool allowed  = _userService.IsAuthorized(UserLevel.Engineer);
bool pageOk   = _userService.HasPagePermission("ParamEditView");

var users = await _userService.GetUserListAsync();
await _userService.SaveUserAsync(user);
await _userService.DeleteUserAsync(user);
```

## ProductionDataService

```csharp
await _prodService.InitializeAsync();
await _prodService.RecordAsync(new OcrResult { ... }, recordType: "OCR");   // 任意 POCO，JSON 存储

var records = await _prodService.QueryAsync(filter);
var typed   = await _prodService.QueryDataAsync<OcrResult>(filter);        // 反序列化回强类型

await _prodService.ExportToCsvAsync(filter, path);
await _prodService.ExportToExcelAsync(filter, path);       // NPOI
await _prodService.PurgeOldDataAsync(retentionDays: 90);
```

## StationSyncService

跨工站命名信号量，v1.0.5 起支持按 `scope` 分组（不同工站互不影响 Drain/复位），API 全部带 `scope = "global"` 默认参数：

```csharp
// 注册（主控构造函数中，单线程顺序调用）
_sync.Register("AllowFeed",    initialCount: 0, maxCount: 1, scope: "WS1");
_sync.Register("FeedComplete", initialCount: 0);   // 不传 scope 落在默认 "global" 分组

// 运行期（多线程安全）
await _sync.WaitAsync("AllowFeed", token, scope: "WS1");
_sync.Release("FeedComplete");
_sync.DrainSignal("AllowFeed", scope: "WS1");       // 排空残留计数，不等待/不阻塞

// 复位（对应工站线程已停止后调用）
_sync.ResetScope("WS1");        // 只复位这一个 scope 下的全部信号量
_sync.ResetSingleSignal("FeedComplete");
_sync.ResetAll();                // 复位所有 scope

var snapshot = _sync.GetSnapshot();   // Key 格式 "scope/name"，调试面板轮询用
```

`ResetScope`/`ResetSingleSignal` 内部是"广播取消 → Drain 屏障等飞行中的 `WaitAsync` 全部退出（3000ms 超时保护）→ 安全 Dispose 重建"三步走，消除了飞行中调用恰好用旧令牌构建 linked token 时的 `ObjectDisposedException` 竞态。

## AppTimerService

```csharp
IDisposable h1 = _timer.Register(1000, () => Poll());                                          // 固定间隔（毫秒）
IDisposable h2 = _timer.RegisterDailyAt("DiskCheck",    new TimeSpan(8, 0, 0), CheckDiskUsage); // 每日
IDisposable h3 = _timer.RegisterWeeklyAt("WeeklyReport", DayOfWeek.Monday, new TimeSpan(9, 0, 0), SendReport);
IDisposable h4 = _timer.RegisterMonthlyAt("MonthlyPurge", 1, new TimeSpan(2, 0, 0), Purge, catchUpOnStart: true);

_timer.Start();   // OnInitialized 中启动
// Dispose() 返回的 IDisposable 用于取消单个注册
```

回调是同步 `Action`，不是 `Func<Task>`——耗时操作需自行 `Task.Run` 或转入现有的后台任务体系。

## HardwareInputMonitor

安全门监控（`AutoOCRMachineController` 用）与通用输入轮询分成两套独立可开关的循环：

```csharp
_monitor.StartStandardMonitoring(token);
_monitor.StartSafetyMonitoring(token);
_monitor.SetSafetyDoorEnabled("Door1", enabled: true);
var doors = _monitor.GetSafetyDoorSnapshot();
_monitor.StopAll();
```

## TowerLightService / TowerLightDoWriter

```csharp
_towerLight.SetLight(LightColor.Red, LightState.Blink, blinkIntervalMs: 500);
_towerLight.SetLights(new Dictionary<LightColor, LightState> { [LightColor.Green] = LightState.On });
_towerLight.TurnOffAll();
```

`TowerLightDoWriter.Write(tag, value)` 是把逻辑灯色/蜂鸣器状态落到具体 DO 点位的适配层，`TowerLightService` 内部依赖它而不直接碰 IO。

## IOMappingService

```csharp
_ioMap.RegisterInputEnum<SafetyDoorInput>("SIM_IO_0");
string name = _ioMap.GetInputName("SIM_IO_0", 3);          // 中文释义，供调试面板展示
IOMapInfo info = _ioMap.GetInputInfo("SIM_IO_0", 3);
```
