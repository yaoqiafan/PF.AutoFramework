# PF.Core

PF.AutoFramework 核心契约层（当前 v1.0.14），包含全部接口、枚举、特性、常量定义，**零外部依赖**。上层所有包均以此为基础，通常无需单独引用（通过 `PF.Infrastructure` 或 `PF.AutoFramework.Meta` 传递依赖）。

## 主要内容

### 硬件接口

| 接口 | 说明 |
|---|---|
| `IHardwareDevice` | 所有硬件的基接口：连接、断开、复位、仿真模式切换 |
| `IAttachedDevice` / `IAttachedDevice<TParent>` | 挂载设备契约。非泛型版供服务层做类型无关的 `TryAttachTo(IHardwareDevice)` 注入；泛型版供设备实现方与业务代码取强类型 `Parent`（如轴/IO 挂 `IMotionCard`，线阵相机挂 `IFrameGrabberCard`） |
| `IMotionCard` | 运动控制卡：轴使能 / 归零 / 移动 / IO 读写等抽象成员 |
| `IAxis` | 伺服轴：`MoveAbsAsync` / `MoveRelAsync` / `HomeAsync` / `WaitDoneAsync` |
| `IIOController` | 数字 IO 控制器：`ReadInput` / `WriteOutput` / `WaitInputAsync` |
| `ILightController` | 光源控制器：`SetLightValue(Channel, LightValue, token)` / `GetLightValue(Channel, token)`（读取失败必须抛异常，不得返回 0）/ `ChannelCount`（注册硬件时由 `ConnectionParameters["ChannelCount"]` 指定，不写则为 4） |
| `IBarcodeScan` | 条码扫描仪 |
| `IIntelligentCamera` | 智能相机：触发一次→相机内部跑完算法→返回结果字符串（如基恩士 OCR 相机） |
| `ILineScanCamera` | 线阵（线扫）相机：吐原始图像、算法在上位机侧，与 `IIntelligentCamera` 刻意不复用同一接口。`ApplyConfigAsync` / `ArmAsync`（必须早于轴运动）/ `WaitFrameAsync` / `FrameReceived` 事件 / `SaveLastImageAsync` / `DiscoverAsync`；不引用 `IAxis`，轴与相机的时序编排是机构层职责 |
| `IFrameGrabberCard` | 图像采集卡：与 `IMotionCard` 并列的宿主类设备，管"流怎么切帧"；`ApplyFrameControlAsync` / `SoftwareTriggerFrameAsync` / `DiscoverCamerasAsync` |
| `IGenICamNodeAccess` | 相机与采集卡共用的 GenICam 节点通用读写通道：`GetNodeAsync` / `SetNodeAsync` / `GetEnumEntriesAsync` / `ExecuteCommandAsync` / `EnumerateNodesAsync`（主动扫描全部节点，秒级耗时，只应用户触发，不放轮询里） |

### 机构 & 工站接口

```csharp
IMechanism         // 机构：初始化、停止、复位
IStation           // 工站：8 状态机驱动，Start / Pause / Resume / Stop
IMasterController  // 主控：并行编排所有工站
```

### 通讯接口

```csharp
ICommunication                 // 所有通讯实现（TCP Server/Client、串口、FileTransfer、Modbus）的公共契约
ICommunicationManagerService   // 通讯实例注册、加载、生命周期管理（结构对齐 IHardwareManagerService）
ISerialCommunication           // 串口通讯扩展契约
```

```csharp
IModbusMaster       // Modbus 主站共享操作契约：8 个标准功能码 + SendRawAsync（原始 PDU 透传）+ BuildFrame（报文预览）
IModbusRtuMaster    // 对齐 ISerialCommunication 的拆分方式；AutoReconnect / ReconnectIntervalMs
IModbusTcpMaster    // 对齐 IClient 的拆分方式；额外有 TransactionIdAutoIncrement / FixedTransactionId / ResetTransactionId
```

```csharp
CommunicationCategory  // 通讯实例的一级分组（调试面板分类树），含 Modbus
CommunicationRole      // 服务端/客户端角色，驱动二级分组（None 时不分组）
```

```csharp
[CommunicationUIAttribute(...)]
// 标注在 ICommunication 实现类上，决定调试面板导航到哪个调试视图
```

FileTransfer 传输通道相关接口/实体/事件（多 Lane 并行、CRC32+xxHash64 校验、断线重连）也定义在本层。

### HALCON 视觉接口

```csharp
IVisionService        // 视觉引擎服务契约（零 Halcon 依赖），见 PF.Vision.Halcon README
IVisionResult          // 视觉过程执行结果契约
IVisionContextManager  // 视觉引擎上下文管理；TryGet(EngineMode) 只取已拉起的引擎，不创建
IHalconDebugService     // 调试服务契约；ProcedureDirectory 暴露过程目录，签名解析/枚举不必拉起引擎
```

```csharp
EngineMode           // 视觉引擎运行模式（生产 / 调试 / 离线）
RoiOp / RoiType       // ROI 运算方式 / 类型
VisionRoiConfig       // ROI 配置实体
ProcedureSignature    // .hdev 过程的参数签名描述
```

### 配置路径隔离（ConstGlobalParam）

```csharp
ConstGlobalParam.ConfigRoot     // 所有项目共用的根目录：D:\PFConfig\PFAutoFrameWork
ConstGlobalParam.ProjectName    // 当前项目名，即根目录下的隔离子目录名
ConstGlobalParam.ConfigPath     // {ConfigRoot}\{ProjectName}\ —— 未 Initialize 时直接抛异常，不回退根目录
ConstGlobalParam.Initialize(projectName)   // 整个进程生命周期内只能调用一次
```

同一台机器换项目运行时，若共用一个配置目录，`AppParamDbContext` 的废弃参数清理会把上一个项目的参数/硬件/通讯配置整批清空。WPF 主程序由 `PFApplicationBase` 构造函数自动完成 `Initialize`；自建宿主（工具、`PF.SecsGem.Service`）必须显式调用。**禁止**在静态字段初始化器中捕获 `ConfigPath`（会先于 `Initialize` 求值抛 `TypeInitializationException`），必须写成 `=>` 表达式属性。

### 关键枚举

```csharp
MachineState     // Uninitialized / Initializing / Idle / Running / Paused /
                  // RunAlarm / InitAlarm / Resetting（共 8 个）
MachineTrigger   // Initialize / InitializeDone / Start / Pause / Resume / Stop /
                  // Error / Reset / ResetDone / ResetDoneUninitialized（共 10 个）
HardwareCategory // General / Axis / IOController / Camera / Robot / Scanner /
                  // Instrument / MotionCard / LightController / FrameGrabber
UserLevel        // Null=-1 / Operator=0 / Engineer=1 / Administrator=2 / SuperUser=3
OperationMode    // Normal / DryRun
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

[CommunicationUI(ViewName = "TcpClientDebugView")]
// 标注在 ICommunication 实现类上，通讯调试树点击后按此路由

[HardwareUI("Ni4TowerLightDebugView")]
// 标注在硬件设备实现类上，HardwareDebugView 反射发现自定义调试视图；
// 未标注的内置设备（轴/IO/卡/相机/条码/光源）仍走原硬编码分发，向后兼容
```

> **注意**：特性中的 `ViewName` 必须与 `RegisterForNavigation<View, ViewModel>(key)` 的 key 完全一致，否则导航失败。

### 服务接口速查

```csharp
IParamService            // 参数读写（泛型 POCO，where T : class）
ILogService              // 结构化日志
IAlarmService            // 报警触发 / 消除
IAlarmDictionaryService  // 报警码字典查询
IAlarmEventPublisher     // 报警事件桥接到 UI 层（解耦 PF.Services 与 Prism，Shell 层实现）
IUserService             // 登录 / 权限验证
IProductionDataService   // 生产数据记录
IStationSyncService      // 跨工站信号量同步
IAppTimerService         // 定时任务调度
IHardwareManagerService  // 硬件生命周期管理
ICommunicationManagerService  // 通讯实例生命周期管理
IRecipeService<T>        // 配方增删改查、导入导出、切换生效（T 继承 RecipeParamBase）
ITowerLightService       // 三色灯逻辑控制（通道状态、软件频闪、蜂鸣器屏蔽）
ITowerLightDoWriter      // 三色灯逻辑 tag → 物理 DO 点写入，与具体硬件型号解耦
```
