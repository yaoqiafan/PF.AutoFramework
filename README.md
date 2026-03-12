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
* **🏭 完整的工控生命周期**：内置标准化 7 状态机，配合 `MasterController` 实现多工站联动初始化、启停、暂停、复位的全生命周期管理。
* **🔩 硬件三级抽象**：`BaseDevice`（设备）→ `BaseMechanism`（模组）→ `StationBase`（工站），模板方法模式，子类仅实现业务钩子。
* **🎨 现代扁平化 UI**：内置高颜值自定义控件库（`Growl`、`Drawer`、步骤条等），支持深色/浅色主题。
* **🔐 全局身份与权限管理**：内置完整身份认证模块，支持细粒度权限管控及多级用户角色。
* **💾 动态参数与持久化系统**：基于 EF Core 9 + SQLite 提供强大的泛型参数服务，JSON 序列化存储，支持审计追踪。
* **📊 工业级日志追踪**：高性能日志组件，生产者-消费者异步写入，支持分类与自动滚动。

---

## 🏗️ 系统架构图

<img width="2816" height="1536" alt="Architecture" src="https://github.com/user-attachments/assets/f2de5424-777f-482e-9555-fed2dfadc1f5" />

---

## 📂 工程结构说明

解决方案遵循严格的 **7 层分层架构**，依赖方向单向不循环：

```
PF.AutoFramework.slnx
│
├── /00. 全局配置
│   ├── Common.props                  # 全局编译属性
│   ├── Common.Desktop.props          # 桌面 WPF 专属属性
│   └── Directory.Packages.props      # 中央包版本管理
│
├── /01. 基础层 (Foundation)
│   ├── PF.Core                       # 核心契约层（接口、枚举、特性）
│   └── PF.Infrastructure             # 底层实现（硬件/模组/工站基类）
│
├── /02. UI 组件库
│   ├── PF.UI.Controls                # 自定义控件（Growl、Drawer、Steps）
│   ├── PF.UI.Infrastructure          # UI 基础设施（ViewModel 基类）
│   ├── PF.UI.Resources               # 主题资源字典
│   └── PF.UI.Shared                  # UI 共享工具
│
├── /03. 数据层 (Data)
│   └── PF.Data                       # EF Core 模型、DbContext、Repository
│
├── /04. 工具与服务层
│   ├── PF.CommonTools                # 通用工具
│   └── PF.Services                   # 业务服务（ParamService、HardwareManager）
│
├── /05. 业务模块 (Modules)
│   ├── PF.Modules.Identity           # 身份认证
│   ├── PF.Modules.Logging            # 日志查看
│   ├── PF.Modules.Parameter          # 参数管理
│   ├── PF.Modules.Debug              # 硬件调试面板
│   └── PF.Modules.SecsGem            # SECS/GEM 通信
│
├── /06. 应用入口 (Application)
│   └── PF.Application.Shell          # WPF App 入口
│
└── /07. Demo 工站
    ├── PF.Workstation.Demo           # 取放工站业务逻辑
    └── PF.Workstation.Demo.UI        # 取放工站 UI 模块
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
| **状态机** | Stateless | 5.20.1 |

---

# 开发者指南

本指南以**创建新项目**为主线，按开发顺序组织。当你开始一个新的自动化项目时，按以下步骤进行。

---

## 0. 快速开始（5 分钟跑起来）

```bash
# 1. 克隆仓库
git clone https://github.com/your-org/PF.AutoFramework.git

# 2. 打开解决方案
# 使用 Visual Studio 2022 或 JetBrains Rider 打开 PF.AutoFramework.slnx

# 3. 还原 NuGet 包
dotnet restore

# 4. 运行 Demo
# 设置 PF.Application.Shell 为启动项目，F5 运行
```

运行后你将看到：
- 深色主题主窗口
- 左侧导航菜单
- 系统调试 → 设备综合调试 / 业务模组调试 / 工站调试
- 取放工站 Demo 正常运行

---

## 1. 新项目初始化

### 1.1 必须创建的项目

创建新工站项目需要以下结构：

```
PF.Workstation.YourProject/           # 业务逻辑层
├── Hardware/                         # 硬件实现
│   ├── YourMotionCard.cs            # 运动控制卡
│   ├── YourAxis.cs                  # 轴设备
│   └── YourIO.cs                    # IO 设备
├── Mechanisms/                       # 机构层
│   └── YourMechanism.cs
├── Stations/                         # 工站层
│   └── YourStation.cs
├── Controllers/                      # 主控
│   └── YourMasterController.cs
└── YourProjectModule.cs             # Prism 模块注册

PF.Workstation.YourProject.UI/        # UI 层
├── Views/
│   ├── YourMechanismView.xaml       # 机构调试视图
│   └── YourStationDebugView.xaml    # 工站调试视图
├── ViewModels/
│   ├── YourMechanismViewModel.cs
│   └── YourStationDebugViewModel.cs
└── YourProjectUIModule.cs           # UI 模块注册
```

### 1.2 项目引用关系

```
PF.Workstation.YourProject
├── → PF.Core
├── → PF.Infrastructure
├── → PF.Services
└── → PF.Data

PF.Workstation.YourProject.UI
├── → PF.Core
├── → PF.UI.Infrastructure
├── → PF.Workstation.YourProject
└── → Prism.DryIoc
```

### 1.3 命名规范

| 类型 | 命名 | 示例 |
|------|------|------|
| 运动卡 | `{厂商}MotionCard` | `LeiSaiMotionCard` |
| 轴设备 | `{功能}Axis` | `GantryXAxis` |
| IO 设备 | `{功能}IO` | `VacuumIO` |
| 机构 | `{功能}Mechanism` | `GantryMechanism` |
| 工站 | `{功能}Station` | `PickPlaceStation` |
| 主控 | `{项目}MasterController` | `DemoMasterController` |

---

## 2. 硬件层开发（Layer 1）

硬件层是整个框架的基础，分为三个抽象层级：

```
BaseMotionCard（板卡）     ← 厂商 SDK 封装
    ↓ AttachToCard()
BaseAxisDevice（轴）       ← 代理到 ParentCard
BaseIODevice（IO）         ← 代理到 ParentCard
```

### 2.1 新增运动控制卡

继承 `BaseMotionCard`，实现 15 个抽象方法：

```csharp
// PF.Workstation.YourProject/Hardware/YourMotionCard.cs
public class YourMotionCard : BaseMotionCard
{
    public override int CardIndex { get; }
    public override int AxisCount => 4;
    public override int InputCount => 32;
    public override int OutputCount => 32;

    public YourMotionCard(HardwareConfig config, ILogService logger)
        : base(config.DeviceId, config.DeviceName, config.IsSimulated, logger)
    {
        Category = HardwareCategory.MotionCard;
        CardIndex = int.Parse(config.ConnectionParameters["CardIndex"]);
    }

    protected override async Task<bool> InternalConnectAsync(CancellationToken token)
    {
        if (IsSimulated) return true;
        
        // 调用厂商 SDK 初始化
        // var result = VendorSDK.Open(CardIndex);
        // return result == 0;
        return true;
    }

    protected override Task InternalDisconnectAsync()
    {
        if (!IsSimulated)
        {
            // VendorSDK.Close(CardIndex);
        }
        return Task.CompletedTask;
    }

    protected override Task InternalResetAsync(CancellationToken token)
    {
        return Task.CompletedTask;
    }

    // ===== 运动控制（7个）=====
    public override Task<bool> EnableAxisAsync(int axisIndex, CancellationToken token)
    {
        if (IsSimulated) return Task.FromResult(true);
        // return VendorSDK.EnableAxis(CardIndex, axisIndex);
        return Task.FromResult(true);
    }

    public override Task<bool> DisableAxisAsync(int axisIndex, CancellationToken token)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override Task<bool> StopAxisAsync(int axisIndex, CancellationToken token)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override Task<bool> HomeAxisAsync(int axisIndex, CancellationToken token)
    {
        if (IsSimulated)
        {
            await Task.Delay(500, token); // 模拟回零时间
            return Task.FromResult(true);
        }
        // return VendorSDK.HomeAxis(CardIndex, axisIndex);
        return Task.FromResult(true);
    }

    public override Task<bool> MoveAbsoluteAsync(int axisIndex, double position, 
        double speed, CancellationToken token)
    {
        if (IsSimulated) return Task.FromResult(true);
        // return VendorSDK.MoveAbs(CardIndex, axisIndex, position, speed);
        return Task.FromResult(true);
    }

    public override Task<bool> MoveRelativeAsync(int axisIndex, double distance, 
        double speed, CancellationToken token)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override Task<bool> JogAsync(int axisIndex, double speed, CancellationToken token)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    // ===== 轴状态读取（3个）=====
    public override double GetAxisCurrentPosition(int axisIndex)
    {
        if (IsSimulated) return 0;
        // return VendorSDK.GetPosition(CardIndex, axisIndex);
        return 0;
    }

    public override MotionIOStatus GetMotionIOStatus(int axisIndex)
    {
        if (IsSimulated) return MotionIOStatus.Ready;
        // return VendorSDK.GetStatus(CardIndex, axisIndex);
        return MotionIOStatus.Ready;
    }

    public override bool IsAxisMoving(int axisIndex)
    {
        if (IsSimulated) return false;
        return false;
    }

    // ===== IO 读写（3个）=====
    public override bool ReadInputPort(int portIndex)
    {
        if (IsSimulated) return false;
        // return VendorSDK.ReadInput(CardIndex, portIndex);
        return false;
    }

    public override void WriteOutputPort(int portIndex, bool value)
    {
        if (IsSimulated) return;
        // VendorSDK.WriteOutput(CardIndex, portIndex, value);
    }

    public override bool ReadOutputPort(int portIndex)
    {
        if (IsSimulated) return false;
        return false;
    }
}
```

### 2.2 新增轴设备

继承 `BaseAxisDevice`，**无需重写运动方法**（代理到 ParentCard）：

```csharp
// PF.Workstation.YourProject/Hardware/YourAxis.cs
public class YourAxis : BaseAxisDevice
{
    public override int AxisIndex { get; }

    public YourAxis(HardwareConfig config, ILogService logger)
        : base(config.DeviceId, config.DeviceName, config.IsSimulated, logger,
               dataDirectory: AppDomain.CurrentDomain.BaseDirectory)
    {
        Category = HardwareCategory.Axis;
        AxisIndex = int.Parse(config.ConnectionParameters["AxisIndex"]);
    }

    // 轴设备不需要独立连接，父板卡连接后即可使用
    protected override Task<bool> InternalConnectAsync(CancellationToken token)
        => Task.FromResult(ParentCard != null);

    protected override Task InternalDisconnectAsync() => Task.CompletedTask;
    
    protected override Task InternalResetAsync(CancellationToken token) => Task.CompletedTask;
}
```

> **关键点**：`BaseAxisDevice` 的所有运动方法（`EnableAsync`、`HomeAsync`、`MoveAbsoluteAsync` 等）已经实现为代理方法，自动调用 `ParentCard.XxxAxisAsync(AxisIndex, ...)`。子类只需提供 `AxisIndex`。

### 2.3 新增 IO 设备

```csharp
// PF.Workstation.YourProject/Hardware/YourIO.cs
public class YourIO : BaseIODevice
{
    public YourIO(HardwareConfig config, ILogService logger)
        : base(config.DeviceId, config.DeviceName, config.IsSimulated, logger)
    {
        Category = HardwareCategory.IOController;
    }

    protected override Task<bool> InternalConnectAsync(CancellationToken token)
        => Task.FromResult(ParentCard != null);

    protected override Task InternalDisconnectAsync() => Task.CompletedTask;
    
    protected override Task InternalResetAsync(CancellationToken token) => Task.CompletedTask;
}
```

### 2.4 注册硬件工厂

在 `App.xaml.cs` 中注册：

```csharp
private void RegisterHardwareFactories(IHardwareManagerService hwManager, ILogService logger)
{
    // 注册板卡
    hwManager.RegisterFactory("YourMotionCard", 
        cfg => new YourMotionCard(cfg, logger));
    
    // 注册轴设备
    hwManager.RegisterFactory("YourAxis", 
        cfg => new YourAxis(cfg, logger));
    
    // 注册 IO 设备
    hwManager.RegisterFactory("YourIO", 
        cfg => new YourIO(cfg, logger));
}
```

### 2.5 添加硬件配置

在 `DefaultParameters.cs` 中添加默认配置：

```csharp
// 板卡配置（顶层设备，ParentDeviceId 为空）
["YOUR_CARD_0"] = new HardwareParam
{
    Name = "YOUR_CARD_0",
    Category = "Hardware",
    JsonValue = JsonSerializer.Serialize(new HardwareConfig
    {
        DeviceId = "YOUR_CARD_0",
        DeviceName = "运动控制卡0",
        ImplementationClassName = "YourMotionCard",
        Category = "MotionCard",
        ParentDeviceId = "",  // 顶层
        IsSimulated = true,
        IsEnabled = true,
        ConnectionParameters = new Dictionary<string, string>
        {
            ["CardIndex"] = "0"
        }
    })
},

// 轴配置（子设备，ParentDeviceId 指向板卡）
["YOUR_X_AXIS"] = new HardwareParam
{
    Name = "YOUR_X_AXIS",
    Category = "Hardware",
    JsonValue = JsonSerializer.Serialize(new HardwareConfig
    {
        DeviceId = "YOUR_X_AXIS",
        DeviceName = "X轴",
        ImplementationClassName = "YourAxis",
        Category = "Axis",
        ParentDeviceId = "YOUR_CARD_0",  // 挂载到板卡
        IsSimulated = true,
        IsEnabled = true,
        ConnectionParameters = new Dictionary<string, string>
        {
            ["AxisIndex"] = "0"
        }
    })
},
```

> **拓扑关系**：`HardwareManagerService.LoadAndInitializeAsync()` 会自动拓扑排序，先初始化板卡，再初始化子设备，并调用 `AttachToCard()` 注入父板卡引用。

---

## 3. 机构层开发（Layer 2）

机构层聚合多个硬件，协调完成功能动作。

### 3.1 创建机构类

```csharp
// PF.Workstation.YourProject/Mechanisms/YourMechanism.cs
using PF.Core.Attributes;

[MechanismUI("你的机构调试", "YourMechanismView", order: 1)]
public class YourMechanism : BaseMechanism
{
    private readonly IHardwareManagerService _hwManager;
    private readonly ILogService _logger;
    
    private IAxis? _xAxis;
    private IIOController? _vacuumIO;

    public YourMechanism(
        IHardwareManagerService hwManager, 
        ILogService logger)
        : base("你的机构", logger)
    {
        _hwManager = hwManager ?? throw new ArgumentNullException(nameof(hwManager));
        _logger = logger;
    }

    // 初始化：延迟解析硬件 → 注册 → 连接 → 回零
    protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
    {
        _logger.Info($"[{MechanismName}] 开始初始化...");

        // 1. 延迟解析硬件（确保板卡已初始化）
        _xAxis = _hwManager.GetDevice("YOUR_X_AXIS") as IAxis
            ?? throw new InvalidOperationException("未找到 X 轴设备");
        
        _vacuumIO = _hwManager.GetDevice("YOUR_VACUUM_IO") as IIOController
            ?? throw new InvalidOperationException("未找到真空 IO 设备");

        // 2. 延迟注册到 BaseMechanism（启用报警聚合）
        RegisterHardwareDevice(_xAxis as IHardwareDevice);
        RegisterHardwareDevice(_vacuumIO as IHardwareDevice);

        // 3. 连接硬件
        if (!await _xAxis.ConnectAsync(token))
        {
            _logger.Error($"[{MechanismName}] X轴连接失败");
            return false;
        }

        if (!await _vacuumIO.ConnectAsync(token))
        {
            _logger.Error($"[{MechanismName}] 真空IO连接失败");
            return false;
        }

        // 4. 使能 + 回零
        if (!await _xAxis.EnableAsync(token))
        {
            _logger.Error($"[{MechanismName}] X轴使能失败");
            return false;
        }

        if (!await _xAxis.HomeAsync(token))
        {
            _logger.Error($"[{MechanismName}] X轴回零失败");
            return false;
        }

        // 5. 初始状态
        _vacuumIO.WriteOutput(0, false); // 关真空

        _logger.Success($"[{MechanismName}] 初始化完成");
        return true;
    }

    // 紧急停止
    protected override async Task InternalStopAsync()
    {
        _logger.Warn($"[{MechanismName}] 紧急停止");
        
        if (_xAxis != null) 
            await _xAxis.StopAsync();
        
        if (_vacuumIO != null) 
            _vacuumIO.WriteOutput(0, false);
    }

    // ===== 业务动作（供工站调用）=====
    
    public async Task PickAsync(CancellationToken token)
    {
        CheckReady(); // 防呆：未初始化或有报警时抛异常
        
        _logger.Info($"[{MechanismName}] 执行取料...");
        
        // 移动到取料位
        await _xAxis!.MoveAbsoluteAsync(100.0, 50.0, token);
        
        // 开真空
        _vacuumIO!.WriteOutput(0, true);
        
        // 等待真空建立
        if (!await _vacuumIO.WaitInputAsync(0, true, 2000, token))
        {
            throw new Exception("真空建立超时！");
        }
        
        _logger.Success($"[{MechanismName}] 取料完成");
    }

    public async Task PlaceAsync(CancellationToken token)
    {
        CheckReady();
        
        _logger.Info($"[{MechanismName}] 执行放料...");
        
        // 移动到放料位
        await _xAxis!.MoveAbsoluteAsync(200.0, 50.0, token);
        
        // 关真空
        _vacuumIO!.WriteOutput(0, false);
        
        await Task.Delay(100, token);
        
        _logger.Success($"[{MechanismName}] 放料完成");
    }
}
```

### 3.2 关键点说明

| 要点 | 说明 |
|------|------|
| `[MechanismUI]` | 标记后自动在调试面板显示，View 名称必须与注册时一致 |
| 延迟解析 | 构造函数不直接依赖设备，在 `InternalInitializeAsync` 中解析 |
| `RegisterHardwareDevice` | 注册后自动聚合报警，批量复位 |
| `CheckReady()` | 防呆保护，`HasAlarm` 或 `!IsInitialized` 时抛异常 |

### 3.3 注册到 DI 容器

在 `App.xaml.cs` 中：

```csharp
// 使用 RegisterMany 将同一实例注册为多个类型
var container = containerRegistry.GetContainer();
container.RegisterMany(
    new[] { typeof(YourMechanism), typeof(IMechanism) },
    typeof(YourMechanism),
    reuse: DryIoc.Reuse.Singleton);
```

> **为什么用 RegisterMany？**  
> `YourMechanism` 需要同时以两种类型被解析：
> 1. `YourMechanism` — 被工站直接依赖
> 2. `IMechanism` — 被调试模块通过 `IEnumerable<IMechanism>` 收集

---

## 4. 工站层开发（Layer 3）

工站层是业务逻辑的核心，管理步序状态机和后台线程。

### 4.1 工站 7 状态机

```
Uninitialized ──(Initialize)──► Initializing ──(InitDone)──► Idle
                                     │                            │
                                  (Error)                      (Start)
                                     ▼                            ▼
                                   Alarm ◄──(Error)─────────── Running
                                     │                        ↕ (Pause/Resume)
                                   (Reset)                     Paused
                                     ▼
                                 Resetting ──(ResetDone)──► Idle
```

| 状态 | 描述 |
|------|------|
| `Uninitialized` | 初始状态，硬件未就绪，**禁止启动** |
| `Initializing` | 正在执行硬件连接/回零 |
| `Idle` | 待机，就绪等待启动 |
| `Running` | 后台线程执行 `ProcessLoopAsync` |
| `Paused` | `_pauseEvent` 关闭，线程挂起 |
| `Alarm` | 故障，等待人工干预 |
| `Resetting` | 正在执行物理复位 |

### 4.2 创建工站类

```csharp
// PF.Workstation.YourProject/Stations/YourStation.cs
using PF.Core.Attributes;

[StationUI("你的工站调试", "YourStationDebugView", order: 1)]
public class YourStation : StationBase
{
    private readonly YourMechanism _mechanism;
    private readonly IStationSyncService _sync;

    // 步序枚举（间隔值，便于将来插入中间步）
    private enum Step
    {
        WaitMaterial = 10,
        Pick = 20,
        Process = 30,
        Place = 40,
        NotifyDownstream = 50,
    }
    private Step _currentStep = Step.WaitMaterial;

    public YourStation(
        YourMechanism mechanism,
        IStationSyncService sync,
        ILogService logger)
        : base("你的工站", logger)
    {
        _mechanism = mechanism;
        _sync = sync;
        
        // 模组故障 → 工站报警
        _mechanism.AlarmTriggered += (s, e) => TriggerAlarm();
    }

    // ===== 生命周期钩子 =====

    // 初始化（由 MasterController.InitializeAllAsync 调用）
    public override async Task ExecuteInitializeAsync(CancellationToken token)
    {
        Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
        
        try
        {
            if (!await _mechanism.InitializeAsync(token))
                throw new Exception($"[{StationName}] 机构初始化失败！");
            
            Fire(MachineTrigger.InitializeDone); // Initializing → Idle
        }
        catch
        {
            Fire(MachineTrigger.Error); // Initializing → Alarm
            throw;
        }
    }

    // 工艺大循环（Running 状态下的后台线程）
    protected override async Task ProcessLoopAsync(CancellationToken token)
    {
        // 路由到对应模式
        if (CurrentMode == OperationMode.Normal)
            await ProcessNormalAsync(token);
        else
            await ProcessDryRunAsync(token);
    }

    // 正常生产模式
    private async Task ProcessNormalAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            switch (_currentStep)
            {
                case Step.WaitMaterial:
                    _pauseEvent.Wait(token); // 暂停检查点
                    await WaitForMaterialAsync(token);
                    _currentStep = Step.Pick;
                    break;

                case Step.Pick:
                    _pauseEvent.Wait(token);
                    await _mechanism.PickAsync(token);
                    _currentStep = Step.Process;
                    break;

                case Step.Process:
                    _pauseEvent.Wait(token);
                    // 等待下游就绪
                    await _sync.WaitAsync("SlotEmpty", token);
                    // 执行工艺...
                    _currentStep = Step.Place;
                    break;

                case Step.Place:
                    await _mechanism.PlaceAsync(token);
                    _sync.Release("ProductReady"); // 通知下游
                    _currentStep = Step.NotifyDownstream;
                    break;

                case Step.NotifyDownstream:
                    await Task.Delay(50, token);
                    _currentStep = Step.WaitMaterial;
                    break;
            }
        }
    }

    // 空跑模式（跳过物料等待和信号量）
    private async Task ProcessDryRunAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _pauseEvent.Wait(token);
            
            // 简化流程：只执行轴运动
            await _mechanism.PickAsync(token);
            await Task.Delay(100, token);
            await _mechanism.PlaceAsync(token);
            await Task.Delay(100, token);
        }
    }

    // 物理复位（由 MasterController.ResetAllAsync 调用）
    public override async Task ExecuteResetAsync(CancellationToken token)
    {
        // 1. 清除硬件报警
        await _mechanism.ResetAsync(token);
        
        // 2. 重新初始化
        await _mechanism.InitializeAsync(token);

        // 3. 智能步序恢复（根据故障现场决定从哪里恢复）
        _currentStep = _currentStep >= Step.Place
            ? Step.WaitMaterial   // 放料后故障：等新料
            : Step.Pick;          // 取料中故障：重试

        // 4. Alarm → Idle
        ResetAlarm();
    }

    public override void Dispose()
    {
        _mechanism.AlarmTriggered -= (s, e) => TriggerAlarm();
        _mechanism.Dispose();
        base.Dispose();
    }
}
```

### 4.3 关键成员

| 成员 | 类型 | 说明 |
|------|------|------|
| `_pauseEvent` | `ManualResetEventSlim` | 暂停闸门，`Pause()` 关，`Resume()` 开 |
| `CurrentMode` | `OperationMode` | 当前模式（Normal/DryRun） |
| `Fire(trigger)` | `void` | 安全触发状态机跳转 |
| `TriggerAlarm()` | `void` | 触发 Error → Alarm |
| `ResetAlarm()` | `void` | 触发 Reset → Idle |

### 4.4 注册到 DI 容器

```csharp
var container = containerRegistry.GetContainer();
container.RegisterMany(
    new[] { typeof(YourStation), typeof(StationBase) },
    typeof(YourStation),
    reuse: DryIoc.Reuse.Singleton);
```

---

## 5. 主控开发（Layer 4）

主控管理所有工站的生命周期，协调流水线信号量。

### 5.1 创建主控类

```csharp
// PF.Workstation.YourProject/Controllers/YourMasterController.cs
public class YourMasterController : BaseMasterController
{
    private readonly IStationSyncService _sync;
    private readonly IEnumerable<StationBase> _stations;

    public YourMasterController(
        ILogService logger,
        IStationSyncService sync,
        IEnumerable<StationBase> stations)
        : base(logger, stations)
    {
        _sync = sync;
        _stations = stations;

        // 注册流水线信号量（初始计数决定"谁先行"）
        // SlotEmpty=1：槽位初始空闲 → 上游工站可立即开始
        // ProductReady=0：初始无产品 → 下游工站初始阻塞
        _sync.Register("SlotEmpty", initialCount: 1, maxCount: 1);
        _sync.Register("ProductReady", initialCount: 0, maxCount: 1);
    }
}
```

### 5.2 典型调用序列（UI 按钮绑定）

```csharp
// 程序启动 → 必须先初始化
await controller.InitializeAllAsync();

// 切换模式（仅 Idle 状态有效）
controller.SetMode(OperationMode.DryRun);

// 启动生产
controller.StartAll();

// 暂停/恢复
controller.PauseAll();
controller.ResumeAll();

// 正常停止
controller.StopAll();

// 故障复位
await controller.ResetAllAsync();
```

### 5.3 注册到 DI 容器

```csharp
containerRegistry.RegisterSingleton<IMasterController, YourMasterController>();
```

---

## 6. UI 模块开发

### 6.1 创建机构调试视图

**View** (`YourMechanismView.xaml`):
```xml
<UserControl x:Class="PF.Workstation.YourProject.UI.Views.YourMechanismView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <StackPanel Margin="10">
            <TextBlock Text="机构调试面板" FontSize="16" FontWeight="Bold"/>
            
            <Button Content="取料" Command="{Binding PickCommand}" Margin="0,10,0,0"/>
            <Button Content="放料" Command="{Binding PlaceCommand}" Margin="0,5,0,0"/>
            <Button Content="回零" Command="{Binding HomeCommand}" Margin="0,5,0,0"/>
            
            <TextBlock Text="{Binding Status}" Margin="0,10,0,0"/>
        </StackPanel>
    </Grid>
</UserControl>
```

**ViewModel** (`YourMechanismViewModel.cs`):
```csharp
public class YourMechanismViewModel : BindableBase, INavigationAware
{
    private readonly YourMechanism _mechanism;
    
    private string _status = "就绪";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }

    public DelegateCommand PickCommand { get; }
    public DelegateCommand PlaceCommand { get; }
    public DelegateCommand HomeCommand { get; }

    public YourMechanismViewModel(YourMechanism mechanism)
    {
        _mechanism = mechanism;
        
        PickCommand = new DelegateCommand(async () => await ExecutePickAsync());
        PlaceCommand = new DelegateCommand(async () => await ExecutePlaceAsync());
        HomeCommand = new DelegateCommand(async () => await ExecuteHomeAsync());
    }

    private async Task ExecutePickAsync()
    {
        Status = "取料中...";
        await _mechanism.PickAsync(CancellationToken.None);
        Status = "取料完成";
    }

    private async Task ExecutePlaceAsync()
    {
        Status = "放料中...";
        await _mechanism.PlaceAsync(CancellationToken.None);
        Status = "放料完成";
    }

    private async Task ExecuteHomeAsync()
    {
        Status = "回零中...";
        await _mechanism.ResetAsync(CancellationToken.None);
        await _mechanism.InitializeAsync(CancellationToken.None);
        Status = "回零完成";
    }

    public void OnNavigatedTo(NavigationContext context) { }
    public void OnNavigatedFrom(NavigationContext context) { }
    public bool IsNavigationTarget(NavigationContext context) => true;
}
```

### 6.2 创建工站调试视图

**View** (`YourStationDebugView.xaml`):
```xml
<UserControl x:Class="PF.Workstation.YourProject.UI.Views.YourStationDebugView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid>
        <StackPanel Margin="10">
            <TextBlock Text="工站调试面板" FontSize="16" FontWeight="Bold"/>
            
            <TextBlock Text="{Binding StationState}" Margin="0,10,0,0"/>
            <TextBlock Text="{Binding CurrentStep}" Margin="0,5,0,0"/>
            
            <Button Content="启动" Command="{Binding StartCommand}" Margin="0,10,0,0"/>
            <Button Content="暂停" Command="{Binding PauseCommand}" Margin="0,5,0,0"/>
            <Button Content="复位" Command="{Binding ResetCommand}" Margin="0,5,0,0"/>
        </StackPanel>
    </Grid>
</UserControl>
```

### 6.3 创建 UI 模块

```csharp
// PF.Workstation.YourProject.UI/YourProjectUIModule.cs
public class YourProjectUIModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 注册机构调试视图（key 必须与 [MechanismUI] 中的 ViewName 一致）
        containerRegistry.RegisterForNavigation<YourMechanismView, YourMechanismViewModel>(
            "YourMechanismView");

        // 注册工站调试视图（key 必须与 [StationUI] 中的 ViewName 一致）
        containerRegistry.RegisterForNavigation<YourStationDebugView, YourStationDebugViewModel>(
            "YourStationDebugView");
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // 注册程序集到菜单自动发现
        var menuService = containerProvider.Resolve<INavigationMenuService>();
        menuService.RegisterAssembly(typeof(YourProjectUIModule).Assembly);
    }
}
```

### 6.4 添加侧边栏菜单

在 View 上添加 `[ModuleNavigation]` 特性：

```csharp
[ModuleNavigation("YourProductionView", "生产操作",
    GroupName = "生产管理", Icon = "Production", Order = 1)]
public partial class YourProductionView : UserControl { }
```

### 6.5 注册模块到 Shell

在 `App.xaml.cs` 中：

```csharp
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    moduleCatalog.AddModule<IdentityModule>();
    moduleCatalog.AddModule<LoggingModule>();
    moduleCatalog.AddModule<ParameterModule>();
    moduleCatalog.AddModule<DebugModule>();
    moduleCatalog.AddModule<YourProjectUIModule>(); // 新增
}
```

---

## 7. 参数扩展

### 7.1 新增参数分类

**Step 1: 定义实体类**

```csharp
// PF.Data/Entity/Category/MotionParam.cs
[Table("MotionParams")]
public class MotionParam : ParamEntity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public override string ID { get; set; } = Guid.NewGuid().ToString();
}
```

**Step 2: 注册到 DbContext**

```csharp
// AppParamDbContext.cs
public DbSet<MotionParam> MotionParams { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.Entity<MotionParam>().HasIndex(p => p.Name).IsUnique();
}
```

**Step 3: 添加默认值**

```csharp
// DefaultParameters.cs
public Dictionary<string, MotionParam> GetMotionDefaults() => new()
{
    ["Axis1_Speed"] = new MotionParam
    {
        Name = "Axis1_Speed",
        Description = "1轴运动速度 (mm/s)",
        TypeFullName = typeof(double).FullName,
        JsonValue = JsonSerializer.Serialize(100.0),
        Category = "运动参数",
        Version = 1,
    }
};
```

**Step 4: 注册类型映射**

```csharp
// App.xaml.cs
var paramService = container.Resolve<IParamService>();
paramService.RegisterParamType<MotionParam, double>();
```

### 7.2 读写参数

```csharp
// 读取
var speed = await _paramService.GetParamAsync<double>("Axis1_Speed");

// 写入（自动审计追踪）
await _paramService.SetParamAsync("Axis1_Speed", 200.0, currentUser, "速度调整");
```

---

## 8. 核心概念

### 8.1 三层硬件抽象

```
BaseDevice（设备）
    ├── BaseMotionCard    ← 板卡，封装厂商 SDK
    ├── BaseAxisDevice    ← 轴，代理到 ParentCard
    └── BaseIODevice      ← IO，代理到 ParentCard

BaseMechanism（机构）      ← 聚合多硬件，业务动作
    └── RegisterHardwareDevice() → 报警聚合

StationBase（工站）        ← 步序状态机 + 后台线程
    ├── ProcessLoopAsync  ← 业务循环
    └── ExecuteResetAsync ← 智能恢复
```

### 8.2 代理模式

`BaseAxisDevice` 和 `BaseIODevice` 是代理包装器：
- 所有操作委托给 `ParentCard`（`IMotionCard`）
- 新增厂商只需实现 `XXXMotionCard`
- 轴/IO 代码零改动，完全解耦

### 8.3 自动发现机制

| 特性 | 作用目标 | 消费者 |
|------|----------|--------|
| `[ModuleNavigation]` | 侧边栏菜单 | `NavigationMenuService` |
| `[MechanismUI]` | 机构调试面板 | `MechanismDebugViewModel` |
| `[StationUI]` | 工站调试面板 | `StationDebugViewModel` |

**核心约束**：特性中的 `ViewName` 必须与 `RegisterForNavigation` 的 key 完全一致。

### 8.4 流水线同步

```csharp
// 上游工站
await _sync.WaitAsync("SlotEmpty", token);  // 等槽位
// ... 放料 ...
_sync.Release("ProductReady");              // 通知下游

// 下游工站
await _sync.WaitAsync("ProductReady", token); // 等产品
// ... 取料 ...
_sync.Release("SlotEmpty");                   // 通知上游
```

---

## 9. 最佳实践

### 9.1 命名规范

| 类型 | 前缀/后缀 | 示例 |
|------|-----------|------|
| 设备 ID | 大写下划线 | `SIM_X_AXIS_0` |
| 实现类名 | 帕斯卡 | `SimMotionCard` |
| 信号量 | 帕斯卡 | `SlotEmpty` |
| 步序枚举 | 帕斯卡 + 整数值 | `Pick = 20` |

### 9.2 错误处理

```csharp
// 设备层：抛异常 + 触发报警
if (!result)
{
    RaiseAlarm("ERR_001", "连接失败");
    return false;
}

// 机构层：CheckReady + 日志
public async Task PickAsync(CancellationToken token)
{
    CheckReady(); // 未初始化或有报警时抛异常
    _logger.Info("取料中...");
}

// 工站层：try-catch + 状态机
try
{
    await _mechanism.PickAsync(token);
}
catch (Exception ex)
{
    _logger.Error($"取料失败: {ex.Message}");
    TriggerAlarm();
}
```

### 9.3 线程安全

```csharp
// UI 更新必须使用 Dispatcher
Dispatcher.InvokeAsync(() => 
{
    Status = "更新状态";
});

// 后台线程不要直接访问 UI 控件
```

### 9.4 常见坑

| 问题 | 原因 | 解决 |
|------|------|------|
| View 导航失败 | key 不匹配 | 检查特性 ViewName 与注册 key |
| 设备未找到 | ID 拼写错误 | 检查 HardwareConfig.DeviceId |
| 信号量死锁 | ResetAll 时线程未停 | 确保 StopAll 后再 ResetAll |
| 重复实例 | RegisterSingleton 多次 | 使用 RegisterMany |

---

## 10. 完整示例：创建"点胶工站"项目

### 10.1 项目结构

```
PF.Workstation.Dispense/
├── Hardware/
│   └── SimMotionCard.cs          # 复用 Demo 的模拟卡
├── Mechanisms/
│   └── DispenseMechanism.cs      # 点胶机构
├── Stations/
│   └── DispenseStation.cs        # 点胶工站
└── Controllers/
    └── DispenseMasterController.cs

PF.Workstation.Dispense.UI/
├── Views/
│   ├── DispenseMechanismView.xaml
│   └── DispenseStationDebugView.xaml
├── ViewModels/
│   ├── DispenseMechanismViewModel.cs
│   └── DispenseStationDebugViewModel.cs
└── DispenseUIModule.cs
```

### 10.2 完整代码

详见 `PF.Workstation.Demo` 项目，复制并修改即可。

### 10.3 注册清单

```csharp
// App.xaml.cs

// 1. 硬件工厂
hwManager.RegisterFactory("SimMotionCard", cfg => new SimMotionCard(cfg, logger));
hwManager.RegisterFactory("SimAxis", cfg => new SimAxis(cfg, logger));
hwManager.RegisterFactory("SimIO", cfg => new SimIO(cfg, logger));

// 2. 机构（RegisterMany）
container.RegisterMany(
    new[] { typeof(DispenseMechanism), typeof(IMechanism) },
    typeof(DispenseMechanism),
    reuse: DryIoc.Reuse.Singleton);

// 3. 工站（RegisterMany）
container.RegisterMany(
    new[] { typeof(DispenseStation), typeof(StationBase) },
    typeof(DispenseStation),
    reuse: DryIoc.Reuse.Singleton);

// 4. 主控
containerRegistry.RegisterSingleton<IMasterController, DispenseMasterController>();

// 5. UI 模块
moduleCatalog.AddModule<DispenseUIModule>();
```

---

## ⚠️ 已知限制

1. **初始化必须先于启动**：必须调用 `InitializeAllAsync()` 才能 `StartAll()`
2. **模式切换仅限 Idle**：`SetMode()` 在非 Idle 状态返回 `false`
3. **数据库无迁移**：使用 `EnsureCreatedAsync()`，不支持 EF Core Migrations
4. **仅支持 Windows**：基于 WPF

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
