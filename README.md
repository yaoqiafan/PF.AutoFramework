# PF.AutoFramework

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-lightgrey.svg)]()
[![Prism](https://img.shields.io/badge/Framework-Prism%209-yellow.svg)](https://prismlibrary.com/)
[![EF Core](https://img.shields.io/badge/ORM-EF%20Core%209-green.svg)]()
[![DryIoc](https://img.shields.io/badge/DI-DryIoc%206-orange.svg)]()
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**PF.AutoFramework** 是一个基于 **.NET 8** 和 **WPF** 构建的企业级、高可扩展桌面端应用底层框架。它深度集成了 Prism 9 框架，推行严格的模块化（Modular）和 MVVM 架构。

本框架专为**聚力智能工业自动化控制**、**半导体制造设备（SECS/GEM）** 量身定制，旨在提供一个开箱即用、稳定可靠且具有现代化视觉体验的上位机软件基础设施。

---

## ✨ 核心特性

* **🧩 极致的模块化架构**：基于 Prism 9 实现 UI、核心逻辑与数据层的彻底解耦。支持插件式开发，子模块动态加载。
* **🏭 完整的工控生命周期**：内置标准化 `Uninitialized → Initializing → Idle → Running` 状态机，配合 `MasterController` 实现多工站联动初始化、启停、暂停、复位的全生命周期管理。
* **🔩 硬件三级抽象**：`BaseDevice`（设备）→ `BaseMechanism`（模组）→ `StationBase`（工站），模板方法模式，子类仅实现业务钩子。支持运动控制卡中间件层（`IMotionCard` / `IAttachedDevice`）。
* **🎨 现代扁平化 UI**：内置海量高颜值自定义控件库（`Growl` 消息提示、`Drawer` 抽屉视图、步骤条等），支持极简的现代化工业 UI 设计风格。
* **🔐 全局身份与权限管理**：内置完整身份认证模块（`IdentityModule`），支持细粒度（页面级/按钮级）权限管控及多级用户角色。
* **💾 动态参数与持久化系统**：基于 EF Core 9 + SQLite 提供强大的泛型参数服务（`IParamService`），JSON 序列化存储，支持自动建表与审计追踪。
* **🔌 高性能底层通信**：封装稳定可靠的 TCP 服务端/客户端通信基类（信号量锁 + 自动重连）。
* **📊 工业级日志追踪**：内置高性能日志组件（`LogService`），生产者-消费者异步写入，支持分类日志与自动滚动。

---


## 🏗️ 系统架构图

<img width="2816" height="1536" alt="Gemini_Generated_Image_m74mdfm74mdfm74m" src="https://github.com/user-attachments/assets/f2de5424-777f-482e-9555-fed2dfadc1f5" />

---

## 📂 工程结构说明

解决方案遵循严格的 **7 层分层架构**，依赖方向单向不循环：

```
PF.AutoFramework.slnx
│
├── /00. 全局配置
│   ├── Common.props                  # 全局编译属性（TargetFramework、Nullable等）
│   ├── Common.Desktop.props          # 桌面 WPF 专属属性
│   └── Directory.Packages.props      # 中央包版本管理（Central Package Management）
│
├── /01. 基础层 (Foundation)
│   ├── PF.Core                       # 核心契约层（接口、枚举、实体基类、特性）
│   └── PF.Infrastructure             # 底层实现（TCP通信、日志器、硬件/模组/工站基类）
│
├── /02. UI 组件库
│   ├── PF.UI.Controls                # 自定义 WPF 控件（Growl、Drawer、Steps等）
│   ├── PF.UI.Infrastructure          # UI 基础设施（ViewModel基类、对话框、导航服务）
│   ├── PF.UI.Resources               # 主题资源字典（Dark/Light/Default皮肤）
│   └── PF.UI.Shared                  # UI 共享工具
│
├── /03. 数据层 (Data)
│   └── PF.Data                       # EF Core 模型、DbContextFactory、GenericRepository
│
├── /04. 工具与服务层 (Tools & Services)
│   ├── PF.CommonTools                # 通用工具（JSON、反射、系统API）
│   └── PF.Services                   # 业务服务（UserService、ParamService、HardwareManagerService）
│
├── /05. 业务模块 (Modules)
│   ├── PF.Modules.Identity           # 身份认证与权限管理
│   ├── PF.Modules.Logging            # 日志查看与管理
│   ├── PF.Modules.Parameter          # 系统参数管理
│   ├── PF.Modules.Debug              # 模组调试面板（MechanismUI自动发现）
│   └── PF.Modules.SecsGem            # 半导体设备 SECS/GEM 通信
│
├── /06. 应用入口 (Application)
│   └── PF.Application.Shell          # WPF App入口、Prism Bootstrapper、主题加载
│
└── /07. Demo 工站
    ├── PF.Workstation.Demo           # 取放工站业务逻辑（MasterController、PickPlaceStation）
    └── PF.Workstation.Demo.UI        # 取放工站 UI 模块（ViewModel、View、UIModule）
```

---

## 🔧 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| **平台** | .NET | 8.0 LTS |
| **UI 框架** | WPF | Windows-only |
| **应用框架** | Prism | 9.0.537 |
| **DI 容器** | DryIoc | 6.2.0 |
| **ORM** | Entity Framework Core | 9.0.12 |
| **数据库** | SQLite | EF Core Provider |
| **日志** | log4net | 3.2.0 |
| **主机** | Microsoft.Extensions.Hosting | 10.0.2 |
| **状态机** | Stateless | 5.20.1 |
| **Excel** | NPOI | 2.7.5 |
| **JSON** | System.Text.Json | 内置 |

---

## 📦 核心项目详解

### PF.Core — 核心契约层

> 无任何外部依赖，所有项目均可安全引用。

- `IHardwareDevice` — 硬件设备统一接口（连接/断开/复位/报警事件）
- `IMotionCard` — 运动控制卡接口（继承 `IHardwareDevice`，增加 AxisCount/InputCount/OutputCount）
- `IAttachedDevice` — 子设备与父板卡绑定接口（`ParentCard` 属性 + `AttachToCard()`）
- `IParamService` — 泛型参数读写接口
- `ILogService` — 统一日志接口（Info/Warn/Error/Debug）
- `IUserService` — 用户认证与权限接口
- `IHardwareManagerService` — 硬件生命周期管理接口
- `IStationSyncService` — 工站流水线信号量协同接口
- `MachineState` / `MachineTrigger` / `OperationMode` — 状态机枚举
- `HardwareCategory` — 设备分类枚举（Axis / IOController / MotionCard / Camera 等）
- `ModuleNavigationAttribute` — 声明式视图导航注册特性
- `MechanismUIAttribute` — 模组调试 UI 自动发现特性
- `UserLevel` — 角色层级：Null → Operator → Engineer → Administrator → SuperUser

### PF.Data — 数据访问层

- `DbContextFactory<TContext>` — 静态线程安全工厂，`ConcurrentDictionary` 缓存 DbContext 配置
- `ParamEntity` — 所有参数实体的基类（Name, JsonValue, Category, Version, TypeFullName）
- `HardwareParam` — 硬件配置参数实体（存储 `HardwareConfig` 的 JSON 序列化结果）
- `GenericRepository<T>` — 通用 CRUD 仓储
- 数据库路径：`%APPDATA%\PFAutoFrameWork\SystemParamsCollection.db`

### PF.Services — 业务逻辑层

**ParamService** — 泛型参数持久化服务：
- JSON 序列化存储，Schema 无关
- 值未变更时跳过写库（优化写入频率）
- `ParamChanged` 事件携带完整审计信息（旧值、新值、操作人、时间戳）
- `RegisterParamType<TEntity, TModel>()` 注册自定义类型映射

**HardwareManagerService** — 硬件生命周期管理：
- `RegisterFactory()` 注册设备工厂函数
- `LoadAndInitializeAsync()` 拓扑排序（父卡优先）初始化
- `ImportConfigsAsync()` 批量写入配置到数据库
- `GetDevice(deviceId)` 运行时按 ID 获取设备实例

**LogService** — 高性能日志服务：
- 内存循环缓冲区（最大 1000 条）
- `BlockingCollection<LogEntry>` 异步文件写入
- 按小时/天自动滚动，历史日志自动清理（默认 30 天）

### PF.Infrastructure — 底层基础设施

**BaseDevice**：模板方法模式，3 次重试连接（间隔 2s），模拟模式直通，`RaiseAlarm()` 触发事件链

**BaseAxisDevice**：继承 `BaseDevice`，实现 `IAxis + IAttachedDevice`，内置点表（`AxisPoint`）JSON 持久化

**BaseIODevice**：继承 `BaseDevice`，实现 `IIOController + IAttachedDevice`

**BaseMotionCard**：继承 `BaseDevice`，实现 `IMotionCard`，`LoadConfigAsync` 带日志包装

**BaseMechanism**：聚合多硬件，自动订阅所有硬件 `AlarmTriggered` 事件，`CheckReady()` 防呆

**StationBase**：`Stateless` 七状态机 + 后台线程管理，标准生命周期 `Uninitialized → Idle`

---

## 🚀 开发者指南一：创建 Prism 业务模块

在 `PF.AutoFramework` 中开发新功能，强烈建议以**独立 Module** 的形式进行。

### Step 1: 实现 IModule 接口

新建类库项目（如 `PF.Modules.Print`），实现 `IModule`：

```csharp
public class PrintModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 注册该模块专属服务
        containerRegistry.RegisterSingleton<IPrintService, BarTenderPrintService>();
        // 注册用于导航的视图
        containerRegistry.RegisterForNavigation<LabelPrintView>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuService = containerProvider.Resolve<INavigationMenuService>();
        menuService.RegisterAssembly(typeof(PrintModule).Assembly);
    }
}
```

### Step 2: 用特性声明导航路由

```csharp
using PF.Core.Attributes;

[ModuleNavigation("LabelPrintView", "标签打印",
    GroupName = "生产操作", Icon = "Printer", Order = 1)]
public partial class LabelPrintView : UserControl { }
```

### Step 3: ViewModel 注入全局服务

```csharp
public class LabelPrintViewModel : BindableBase
{
    private readonly IParamService _paramService;
    private readonly ILogService _logService;

    public LabelPrintViewModel(IParamService paramService, ILogService logService)
    {
        _paramService = paramService;
        _logService   = logService;
    }

    public async Task LoadConfigAsync()
    {
        var printerIp = await _paramService.GetParamAsync<SystemConfigParam>("PrinterIP");
        _logService.Info($"已加载打印机IP: {printerIp?.JsonValue}");
    }
}
```

### Step 4: 在 Shell 中注册模块

```csharp
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    moduleCatalog.AddModule<LoggingModule>();
    moduleCatalog.AddModule<ParameterModule>();
    moduleCatalog.AddModule<IdentityModule>();
    moduleCatalog.AddModule<PrintModule>(); // 新增
}
```

---

## 📋 开发者指南二：扩展参数分类

框架内置 `CommonParam`、`UserLoginParam`、`SystemConfigParam`、`HardwareParam` 四张参数表。新增独立参数分类（如运动参数）按以下步骤操作：

### Step 1: 定义实体类（PF.Data）

```csharp
// PF.Data/Entity/Category/MotionParam.cs
[Table("MotionParams")]
public class MotionParam : ParamEntity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public override string ID { get; set; } = Guid.NewGuid().ToString();
}
```

### Step 2: 注册到 DbContext

```csharp
// AppParamDbContext.cs
public DbSet<MotionParam> MotionParams { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.Entity<MotionParam>().HasIndex(p => p.Name).IsUnique();
}
```

### Step 3: 在 DefaultParameters 中添加初始值

```csharp
// DefaultParameters.cs — 新增方法（并在 IDefaultParam 接口中声明）
public Dictionary<string, MotionParam> GetMotionDefaults() => new()
{
    ["Axis1_Speed"] = new MotionParam
    {
        Name        = "Axis1_Speed",
        Description = "1轴运动速度 (mm/s)",
        TypeFullName = typeof(double).FullName,
        JsonValue   = JsonSerializer.Serialize(100.0),
        Category    = "运动参数",
        Version     = 1,
    }
};
```

在 `EnsureDefaultParametersCreatedAsync()` 中追加：

```csharp
await EnsureParametersExistAsync(MotionParams, defaultParam.GetMotionDefaults(), cancellationToken);
```

### Step 4: ViewModel 读写新分类

```csharp
// 读取
var param = await _paramService.GetParamAsync<MotionParam>("Axis1_Speed");
double speed = JsonSerializer.Deserialize<double>(param.JsonValue);

// 写入（自动存入 MotionParams 表）
await _paramService.SetParamAsync<MotionParam>("Axis1_Speed", newSpeed, currentUser, "1轴速度");
```

---

## ⚙️ 开发者指南三：硬件 → 模组 → 工站开发流程

框架将工业控制分为三个层次。每层有对应的抽象基类，子类只需实现业务钩子。

```
MasterController          ← 主控：生命周期调度、状态机联动
    └── StationBase       ← 工站：步序状态机 + 后台线程
            └── BaseMechanism   ← 模组：协调多硬件完成功能动作
                    └── BaseDevice  ← 设备：物理连接与通信
                          ▲
                    IMotionCard     ← 板卡（可选中间层，管理子轴/IO）
                    BaseAxisDevice  ← 轴设备（实现 IAttachedDevice）
                    BaseIODevice    ← IO设备（实现 IAttachedDevice）
```

---

### Layer 1: 硬件设备（继承 BaseDevice）

`BaseDevice` 封装了连接重试（3次，间隔2秒）、模拟模式拦截、报警事件。子类只需实现 3 个抽象钩子：

```csharp
// PF.Infrastructure/Hardware/Motor/MyServo.cs
public class MyServo : BaseAxisDevice  // 轴设备继承 BaseAxisDevice
{
    public MyServo(HardwareConfig config, ILogService logger)
        : base(config.DeviceId, config.DeviceName,
               config.IsSimulated, logger,
               dataDirectory: AppDomain.CurrentDomain.BaseDirectory)
    {
        Category = HardwareCategory.Axis;
    }

    // ① 建立物理连接（TCP / 板卡SDK等）
    protected override async Task<bool> InternalConnectAsync(CancellationToken token)
    {
        // 通过 ParentCard 访问父板卡句柄
        if (ParentCard == null) return false;
        // 调用板卡SDK初始化本轴...
        return true;
    }

    // ② 断开连接
    protected override async Task InternalDisconnectAsync()
    {
        await Task.CompletedTask;
    }

    // ③ 报警复位
    protected override async Task InternalResetAsync(CancellationToken token)
    {
        // 向硬件发送复位指令
        await Task.CompletedTask;
    }

    // 业务方法：供模组层调用
    public async Task<bool> MoveAbsoluteAsync(double pos, double speed, CancellationToken token)
    {
        if (!IsConnected) throw new InvalidOperationException($"[{DeviceName}] 未连接");
        // 调用运动指令...
        return true;
    }
}
```

**关键要点**：
- `IsSimulated = true` 时，`ConnectAsync()` 自动跳过物理连接，延迟 500ms 后返回 `true`
- 调用 `RaiseAlarm(errorCode, message)` 触发 `AlarmTriggered` 事件链向上传递
- `BaseAxisDevice` 还实现了 `IAttachedDevice`：初始化时 `HardwareManagerService` 会自动调用 `AttachToCard(parentCard)`，子类通过 `ParentCard` 属性访问父板卡实例

---

### Layer 1b: 运动控制卡中间件（继承 BaseMotionCard）

当多个轴/IO设备共享同一物理板卡时，使用板卡中间件层统一管理：

```csharp
// PF.Workstation.Demo/Hardware/SimMotionCard.cs
public class SimMotionCard : BaseMotionCard
{
    public override int CardIndex   { get; }        // 板卡槽位索引
    public override int AxisCount   { get; } = 4;   // 管理4轴
    public override int InputCount  { get; } = 32;  // 32路输入
    public override int OutputCount { get; } = 32;  // 32路输出

    public SimMotionCard(HardwareConfig config, ILogService logger)
        : base(config.DeviceId, config.DeviceName, config.IsSimulated, logger)
    {
        Category  = HardwareCategory.MotionCard;
        CardIndex = int.Parse(config.ConnectionParameters["CardIndex"]);
    }

    protected override async Task<bool> InternalConnectAsync(CancellationToken token)
    {
        // 调用板卡初始化DLL（如 DMCNET_InitDll, GT_Open 等）
        return true;
    }

    // ... InternalDisconnectAsync, InternalResetAsync
}
```

**拓扑关系（HardwareConfig.ParentDeviceId）**：

```
HardwareConfig { DeviceId="SIM_CARD_0",    ParentDeviceId="" }        ← 顶层板卡
HardwareConfig { DeviceId="SIM_X_AXIS_0",  ParentDeviceId="SIM_CARD_0" } ← 子轴
HardwareConfig { DeviceId="SIM_VACUUM_IO", ParentDeviceId="SIM_CARD_0" } ← 子IO
```

`HardwareManagerService.LoadAndInitializeAsync()` 会自动拓扑排序：先初始化板卡（Phase 1），再初始化子设备（Phase 2），并调用 `IAttachedDevice.AttachToCard()` 注入父板卡引用。

---

### Layer 2: 功能模组（继承 BaseMechanism）

`BaseMechanism` 聚合多个 `BaseDevice`，自动订阅硬件报警并向上传递：

```csharp
[MechanismUI("龙门取放调试", "GantryMechanismView", order: 1)]  // 在调试面板自动显示
public class GantryMechanism : BaseMechanism
{
    private readonly IAxis         _xAxis;
    private readonly IIOController _vacuumIO;

    public GantryMechanism(IAxis xAxis, IIOController vacuumIO, ILogService logger)
        // 所有硬件传入基类，基类自动订阅 AlarmTriggered
        : base("龙门取放模组", logger, xAxis, vacuumIO)
    {
        _xAxis    = xAxis;
        _vacuumIO = vacuumIO;
    }

    // ① 初始化：依次连接 → 使能 → 回原点
    protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
    {
        if (!await _xAxis.ConnectAsync(token))    return false;
        if (!await _vacuumIO.ConnectAsync(token)) return false;
        if (!await _xAxis.HomeAsync(token))       return false;
        _vacuumIO.WriteOutput(0, false); // 关真空
        return true;
    }

    // ② 紧急停止
    protected override async Task InternalStopAsync()
    {
        await _xAxis.StopAsync();
        _vacuumIO.WriteOutput(0, false);
    }

    // 业务动作：供工站层调用
    public async Task PickAsync(CancellationToken token)
    {
        CheckReady(); // 报警或未初始化时直接抛异常
        await _xAxis.MoveAbsoluteAsync(PickX, FastSpeed, token);
        _vacuumIO.WriteOutput(VacuumValve, true);
        if (!await _vacuumIO.WaitInputAsync(VacuumSensor, true, 2000, token))
            throw new Exception("真空建立超时！");
    }
}
```

**关键要点**：
- `[MechanismUI]` 特性标记后，调试模块（`PF.Modules.Debug`）会自动发现并在调试面板注册对应 View
- `CheckReady()` 是防呆保护：`HasAlarm == true` 或 `IsInitialized == false` 时直接抛异常
- `ResetAsync()` 自动遍历所有注入硬件批量复位，无需手动实现

---

### Layer 3: 生产工站（继承 StationBase）

#### 完整生命周期状态机

`StationBase` 使用 `Stateless` 管理 **7 个状态**：

```
Uninitialized ──(Initialize)──► Initializing ──(InitializeDone)──► Idle
                                     │                                │
                                  (Error)                          (Start)
                                     ▼                                ▼
                                   Alarm ◄──(Error)────────────── Running
                                     │                           ↕ (Pause/Resume)
                                   (Reset)                        Paused
                                     ▼
                                 Resetting ──(ResetDone)──► Idle
```

| 状态 | 描述 | 进入条件 |
|------|------|---------|
| `Uninitialized` | 初始状态，硬件未就绪，**禁止启动** | 程序启动 |
| `Initializing` | 正在执行硬件连接/回原点 | `Initialize` 触发器 |
| `Idle` | 待机，就绪等待启动 | `InitializeDone` / `ResetDone` |
| `Running` | 后台线程执行 `ProcessLoopAsync` | `Start` 触发器 |
| `Paused` | `_pauseEvent` 关闭，线程挂起 | `Pause` 触发器 |
| `Alarm` | 故障停线，等待人工干预 | `Error` 触发器 |
| `Resetting` | 正在执行物理复位（主控级别） | `Reset` 触发器（仅主控机使用） |

#### 实现工站类

```csharp
public class AssemblyStation : StationBase
{
    private readonly GantryMechanism _gantry;

    // 步序枚举（间隔整数值，便于将来插入中间步）
    private enum Step
    {
        WaitMaterial     = 10,
        Pick             = 20,
        WaitSlotEmpty    = 30,
        Place            = 40,
        NotifyDownstream = 50,
    }
    private Step _currentStep = Step.WaitMaterial;

    public AssemblyStation(GantryMechanism gantry, ILogService logger)
        : base("装配工站", logger)
    {
        _gantry = gantry;
        _gantry.AlarmTriggered += (s, e) => TriggerAlarm(); // 模组故障 → 工站报警
    }

    // ★ 初始化钩子（生命周期第一阶段，由 MasterController.InitializeAllAsync() 调用）
    public override async Task ExecuteInitializeAsync(CancellationToken token)
    {
        Fire(MachineTrigger.Initialize);   // Uninitialized → Initializing
        try
        {
            if (!await _gantry.InitializeAsync(token))
                throw new Exception($"[{StationName}] 模组初始化失败！");
            Fire(MachineTrigger.InitializeDone); // Initializing → Idle
        }
        catch
        {
            Fire(MachineTrigger.Error);    // Initializing → Alarm
            throw;
        }
    }

    // ★ 工艺大循环（Running 状态下的后台线程，由 StationBase 托管）
    protected override async Task ProcessLoopAsync(CancellationToken token)
    {
        // 路由到对应模式（Normal / DryRun）
        if (CurrentMode == OperationMode.Normal)
            await ProcessNormalAsync(token);
        else
            await ProcessDryRunAsync(token);
    }

    private async Task ProcessNormalAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            switch (_currentStep)
            {
                case Step.WaitMaterial:
                    _pauseEvent.Wait(token);         // ← 暂停检查点
                    await WaitForMaterialAsync(token);
                    _currentStep = Step.Pick;
                    break;

                case Step.Pick:
                    _pauseEvent.Wait(token);
                    await _gantry.PickAsync(token);
                    _currentStep = Step.WaitSlotEmpty;
                    break;

                case Step.WaitSlotEmpty:
                    _pauseEvent.Wait(token);
                    await _sync.WaitAsync("SlotEmpty", token); // 流水线协同
                    _currentStep = Step.Place;
                    break;

                case Step.Place:
                    await _gantry.PlaceAsync(token);
                    _sync.Release("ProductReady");   // 通知下游
                    _currentStep = Step.NotifyDownstream;
                    break;

                case Step.NotifyDownstream:
                    await Task.Delay(50, token);
                    _currentStep = Step.WaitMaterial;
                    break;
            }
        }
    }

    // ★ 物理复位钩子（由 MasterController.ResetAllAsync() 调用）
    public override async Task ExecuteResetAsync(CancellationToken token)
    {
        await _gantry.ResetAsync(token);       // 清除硬件报警
        await _gantry.InitializeAsync(token);  // 重新回原点

        // 智能步序回跳（根据故障现场决定从哪里恢复）
        _currentStep = _currentStep >= Step.Place
            ? Step.WaitMaterial   // 放料后发生故障：跳过取料，等下一批
            : Step.Pick;          // 取料中发生故障：重试取料

        ResetAlarm(); // Alarm → Idle
    }

    public override void Dispose()
    {
        _gantry.AlarmTriggered -= ...;
        _gantry.Dispose();
        base.Dispose();
    }
}
```

**`StationBase` 提供的 protected 成员**：

| 成员 | 类型 | 说明 |
|------|------|------|
| `_pauseEvent` | `ManualResetEventSlim` | 暂停闸门，`Pause()` 关闭，`Resume()` 打开 |
| `_logger` | `ILogService` | 注入的日志服务 |
| `_machine` | `StateMachine<...>` | 工站状态机实例 |
| `CurrentMode` | `OperationMode` | 当前运行模式（Normal/DryRun） |
| `Fire(trigger)` | `protected void` | 安全触发状态机跳转（CanFire 检查） |
| `TriggerAlarm()` | `public void` | 触发 Error → Alarm |
| `ResetAlarm()` | `public void` | 触发 Reset → Idle |

---

### Layer 4: 主控（MasterController）

`MasterController` 管理所有工站的生命周期，驱动**全局状态机**，并协调流水线信号量：

```csharp
public class MasterController
{
    // 构造时注册信号量（仅在 MasterController 内部执行）
    public MasterController(ILogService logger, IStationSyncService sync,
                            IEnumerable<StationBase> subStations)
    {
        // 注册流水线信号量（初始计数决定谁先行）
        sync.Register(WorkstationSignals.SlotEmpty,    initialCount: 1, maxCount: 1);
        sync.Register(WorkstationSignals.ProductReady, initialCount: 0, maxCount: 1);
        // ...
    }

    // ① 一次性全线初始化（程序启动后调用一次）
    public async Task InitializeAllAsync()
    {
        // Uninitialized → Initializing → 顺序初始化各工站 → Idle
    }

    // ② 切换运行模式（仅 Idle 状态可切换，同时下发给所有工站）
    public bool SetMode(OperationMode mode) { ... }

    // ③ 启动/停止/暂停/恢复
    public void StartAll()  => Fire(MachineTrigger.Start);
    public void StopAll()   => Fire(MachineTrigger.Stop);
    public void PauseAll()  => Fire(MachineTrigger.Pause);
    public void ResumeAll() => Fire(MachineTrigger.Resume);

    // ④ 物理复位（故障后调用）
    public async Task ResetAllAsync()
    {
        // Alarm → Resetting → 顺序复位各工站 → 重置信号量 → Idle
    }
}
```

**典型调用序列（UI 按钮绑定）**：

```
程序启动     → await controller.InitializeAllAsync()  // 必须，否则无法 Start
模式切换     → controller.SetMode(OperationMode.DryRun)  // 仅 Idle 状态有效
生产启动     → controller.StartAll()
生产暂停     → controller.PauseAll() / ResumeAll()
正常停止     → controller.StopAll()
故障复位     → await controller.ResetAllAsync()       // 执行硬件复位后回 Idle
```

---

### Step 4: 注册到 DI 容器（App.xaml.cs）

```csharp
private void RegisterHardwareTypes(IContainerRegistry containerRegistry)
{
    var container = containerRegistry.GetContainer();

    // 1. 获取 ParamService 并注册自定义类型映射
    var paramService = container.Resolve<IParamService>();
    paramService.RegisterParamType<HardwareParam, HardwareConfig>();

    // 2. 构造 HardwareManagerService
    var logService = container.Resolve<ILogService>();
    var hardwareManager = new HardwareManagerService(logService, paramService);

    // 3. 注册设备工厂（ImplementationClassName → 创建函数）
    hardwareManager.RegisterFactory("SimMotionCard",
        cfg => new SimMotionCard(cfg, logService));
    hardwareManager.RegisterFactory("SimXAxis",
        cfg => new SimXAxis(cfg, logService));
    hardwareManager.RegisterFactory("SimVacuumIO",
        cfg => new SimVacuumIO(cfg, logService));

    // 4. 注册到 DI 容器
    containerRegistry.RegisterInstance<IHardwareManagerService>(hardwareManager);

    // 5. 注册工站与主控（依赖 DI 自动解析）
    containerRegistry.RegisterSingleton<GantryMechanism>();
    containerRegistry.RegisterSingleton<PickPlaceStation>();
    containerRegistry.RegisterSingleton<MasterController>();
}
```

---

## 🔀 开发者指南四：流水线同步（IStationSyncService）

多工站流水线协同通过**双信号量互锁**实现，确保取放工站与点胶工站严格交替、无竞态：

### 注册信号量（MasterController 构造函数中）

```csharp
// 初始计数决定"谁先行"
// SlotEmpty=1：槽位初始空闲 → 取放工站可立即开始第一轮
// ProductReady=0：初始无产品 → 点胶工站初始阻塞，等取放先放料
_sync.Register(WorkstationSignals.SlotEmpty,    initialCount: 1, maxCount: 1);
_sync.Register(WorkstationSignals.ProductReady, initialCount: 0, maxCount: 1);
```

### 工站内使用

```csharp
// 取放工站（生产者）
await _sync.WaitAsync(WorkstationSignals.SlotEmpty, token);   // ← 等槽位
await _gantry.PlaceAsync(token);
_sync.Release(WorkstationSignals.ProductReady);                // → 通知下游

// 点胶工站（消费者）
await _sync.WaitAsync(WorkstationSignals.ProductReady, token); // ← 等产品
await _dispenser.DispenseAsync(token);
_sync.Release(WorkstationSignals.SlotEmpty);                   // → 通知上游
```

### 添加新信号

1. 在 `WorkstationSignals` 中添加常量：`public const string NewSignal = "NewSignal";`
2. 在 `MasterController` 构造函数中注册：`_sync.Register(WorkstationSignals.NewSignal, 0, 1);`
3. 在对应工站循环中使用 `WaitAsync` / `Release`

> ⚠️ **注意**：`ResetAll()` 会销毁并重建信号量对象。调用前必须确保所有工站线程已停止，否则仍持有旧信号量引用的线程将永远阻塞。

---

## 🎛️ 开发者指南五：运行模式与空跑调试

框架内置 `OperationMode` 枚举，通过 `MasterController.SetMode()` 统一下发至所有工站：

| 模式 | 说明 | 适用场景 |
|------|------|---------|
| `Normal` | 等待真实 IO / 物料信号，完整流水线协同 | 生产环境 |
| `DryRun` | 跳过物料等待和信号量协同，执行真实轴运动 | 调试轨迹、验证步序 |

### 在工站中路由模式

框架推荐将 Normal 和 DryRun 拆分为**独立方法**，彻底避免 `if/else` 耦合：

```csharp
protected override async Task ProcessLoopAsync(CancellationToken token)
{
    // 入口路由：CurrentMode 在 Idle 下固定，循环期间不会改变
    if (CurrentMode == OperationMode.Normal)
        await ProcessNormalLoopAsync(token);
    else if (CurrentMode == OperationMode.DryRun)
        await ProcessDryRunLoopAsync(token);
}

private async Task ProcessNormalLoopAsync(CancellationToken token)
{
    // 完整工艺：真实IO等待 + 信号量协同，无任何 DryRun 代码
}

private async Task ProcessDryRunLoopAsync(CancellationToken token)
{
    // 空跑专用：短延迟模拟物料 + 真实轴运动 + 跳过信号量
}
```

### 切换模式（UI 按钮绑定示例）

```csharp
// 仅 Idle 状态可切换，返回 false 时说明当前不可切换
bool ok = _masterController.SetMode(OperationMode.DryRun);
if (!ok) ShowWarning("请先停止设备后再切换模式");
```

---

## 🔩 开发者指南六：硬件配置管理（HardwareManagerService）

所有硬件配置（设备 ID、类名、连接参数、父子关系）均持久化在 SQLite 数据库中（通过 `IParamService`），支持运行时动态增删：

### HardwareConfig 核心字段

| 字段 | 说明 | 示例 |
|------|------|------|
| `DeviceId` | 全局唯一 ID | `"SIM_X_AXIS_0"` |
| `ImplementationClassName` | 工厂注册名 | `"SimXAxis"` |
| `Category` | 设备分类 | `"Axis"` |
| `ParentDeviceId` | 父设备 ID（空串 = 顶层）| `"SIM_CARD_0"` |
| `IsSimulated` | 是否模拟模式 | `true` |
| `ConnectionParameters` | 设备专属连接参数字典 | `{ "AxisIndex": "0" }` |

### 运行时访问设备

```csharp
// 按 ID 获取设备实例
var xAxis = hardwareManager.GetDevice("SIM_X_AXIS_0") as IAxis;

// 遍历所有活跃设备
foreach (var device in hardwareManager.ActiveDevices)
    Console.WriteLine($"{device.DeviceName}: {(device.IsConnected ? "✓" : "✗")}");
```

### 添加新设备配置

```csharp
// 1. 在 DefaultParameters.GetHardwareDefaults() 中追加
["MY_NEW_AXIS"] = new HardwareParam
{
    Name     = "MY_NEW_AXIS",
    Category = "Hardware",
    JsonValue = JsonSerializer.Serialize(new HardwareConfig
    {
        DeviceId               = "MY_NEW_AXIS",
        ImplementationClassName = "MyAxisImpl",
        ParentDeviceId         = "SIM_CARD_0",  // 挂载到已有板卡
        IsSimulated            = true,
        IsEnabled              = true,
        ConnectionParameters   = new() { ["AxisIndex"] = "1" }
    })
},

// 2. 在 App.xaml.cs 注册工厂
hardwareManager.RegisterFactory("MyAxisImpl",
    cfg => new MyAxisImpl(cfg, logService));
```

---

## 🛠️ 第三方集成建议

本框架的模块化特性非常适合集成工业自动化领域的第三方 SDK：

| 场景 | 建议方案 |
|------|---------|
| **条码扫描** | 集成 `ZXing.Net`，封装为 `IScannerService`，注入扫描模块 |
| **工业标签打印** | 接入 BarTender SDK，隔离在独立打印模块 |
| **视觉检测** | 封装 HalconDotNet / OpenCvSharp，作为独立 VisionModule |
| **PLC 通信** | 基于 `TCPClient` / Modbus 协议实现 `IPlcService` |
| **运动控制卡** | 继承 `BaseMotionCard`，封装厂商 SDK（如 GT 系列、DMCNET 等） |
| **报警管理** | 新建 `AlarmModule`，订阅 `BaseDevice.AlarmTriggered` 事件统一管理 |

---

## ⚠️ 已知限制与注意事项

1. **初始化必须先于启动**：状态机强制执行 `Uninitialized → Idle` 才能 `Start`，程序启动后必须调用 `MasterController.InitializeAllAsync()` 才能进入生产。
2. **模式切换仅限 Idle**：`SetMode()` 会检查 `CurrentState == Idle`，非 Idle 状态下调用返回 `false` 并记录警告。
3. **数据库迁移**：当前使用 `EnsureCreatedAsync()` + 手动 `CREATE TABLE IF NOT EXISTS` 处理 Schema 升级，不支持 EF Core Migrations。
4. **信号量重置时序**：`IStationSyncService.ResetAll()` 必须在所有工站线程完全停止后调用，否则持有旧引用的线程将永久阻塞。
5. **TCP 重连无上限**：启用 `AutoReconnect` 后无最大重试次数，网络永久中断时建议在业务层增加熔断逻辑。
6. **日志 UI 线程安全**：`LogService` 从后台线程写入内存缓冲，UI 绑定集合需通过 `Dispatcher.Invoke` 确保线程安全。
7. **仅支持 Windows**：基于 WPF，不支持跨平台部署。

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
