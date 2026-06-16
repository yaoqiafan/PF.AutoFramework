# PF.Core

PF.AutoFramework 核心契约层，包含全部接口、枚举、特性定义，**零外部依赖**。上层所有包均以此为基础，通常无需单独引用（通过 `PF.Infrastructure` 或 `PF.AutoFramework.Meta` 传递依赖）。

## 主要内容

### 硬件接口

| 接口 | 说明 |
|---|---|
| `IHardwareDevice` | 所有硬件的基接口：连接、断开、复位、仿真模式切换 |
| `IMotionCard` | 运动控制卡：21 个抽象成员（轴使能 / 归零 / 移动 / IO 读写） |
| `IAxis` | 伺服轴：`MoveAbsAsync` / `MoveRelAsync` / `HomeAsync` / `WaitDoneAsync` |
| `IIOController` | 数字 IO 控制器：`ReadInput` / `WriteOutput` / `WaitInputAsync` |
| `ILightController` | 三色灯控制器 |
| `IBarcodeScan` | 条码扫描仪 |
| `IIntelligentCamera` | 智能相机（OCR / 检测） |

### 机构 & 工站接口

```csharp
IMechanism   // 机构：初始化、停止、复位
IStation     // 工站：8 状态机驱动，Start / Pause / Resume / Stop
IMasterController  // 主控：并行编排所有工站
```

### 关键枚举

```csharp
MachineState    // Uninitialized / Initializing / Idle / Running / Paused /
                // RunAlarm / InitAlarm / Resetting（共 8 个）
MachineTrigger  // Initialize / Start / Pause / Resume / Stop / Error /
                // Reset / ResetDone / ResetDoneUninitialized / InitializeDone
HardwareCategory // General / Axis / IOController / Camera / Robot /
                 // Scanner / Instrument / MotionCard / LightController
UserLevel       // Null=-1 / Operator=0 / Engineer=1 / Administrator=2 / SuperUser=3
OperationMode   // Normal / DryRun
```

### 自动发现特性

```csharp
[ModuleNavigation(ViewName = "AlarmView", Title = "报警", GroupName = "监控", Icon = "Bell", Order = 1)]
// 在侧边栏自动生成导航菜单项

[AlarmInfo(Source = "FeedStation", ErrorCode = 1001, Description = "送料超时")]
// 报警码元数据，AlarmDictionaryService 反射扫描

[ParamView(ViewName = "FeedParamView")]
// 参数视图路由

[MechanismUI(ViewName = "FeedMechDebugView")]
[StationUI(ViewName  = "FeedStationDebugView")]
// 调试面板自动注册
```

> **注意**：特性中的 `ViewName` 必须与 `RegisterForNavigation<View, ViewModel>(key)` 的 key 完全一致，否则导航失败。

### 服务接口速查

```csharp
IParamService           // 参数读写（泛型 POCO，where T : class）
ILogService             // 结构化日志
IAlarmService           // 报警触发 / 消除
IAlarmDictionaryService // 报警码字典查询
IUserService            // 登录 / 权限验证
IProductionDataService  // 生产数据记录
IStationSyncService     // 跨工站信号量同步
IAppTimerService        // 定时任务调度
IHardwareManagerService // 硬件生命周期管理
```
