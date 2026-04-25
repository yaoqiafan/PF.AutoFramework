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
* **🏭 完整的工控生命周期**：内置标准化 8 状态机（含 `InitAlarm` / `RunAlarm` 分离），配合 `MasterController` 实现多工站联动初始化、启停、暂停、复位的全生命周期管理。
* **🔩 硬件三级抽象**：`BaseDevice`（设备）→ `BaseMechanism`（模组）→ `StationBase<T>`（工站），模板方法模式，子类仅实现业务钩子。支持运动控制卡中间件层（`IMotionCard` / `IAttachedDevice`）。
* **🎨 现代扁平化 UI**：内置高颜值自定义控件库（`Growl`、`Drawer`、步骤条等），支持深色/浅色主题。
* **🔐 全局身份与权限管理**：内置完整身份认证模块，支持细粒度权限管控及多级用户角色。
* **💾 动态参数与持久化系统**：基于 EF Core 9 + SQLite 提供强大的泛型参数服务，JSON 序列化存储，支持审计追踪。
* **🔌 高性能底层通信**：封装稳定可靠的 TCP 服务端/客户端通信基类（信号量锁 + 自动重连）。
* **📊 工业级日志追踪**：高性能日志组件，生产者-消费者异步写入，支持分类与自动滚动。
* **📈 生产数据记录**：泛型生产数据记录服务，支持任意 POCO 类型，JSON 存储，条件查询，Excel/CSV 导出。

---

## 🏗️ 系统架构图

<img width="2816" height="1536" alt="Architecture" src="https://github.com/user-attachments/assets/f2de5424-777f-482e-9555-fed2dfadc1f5" />

---

## 📂 工程结构说明

解决方案遵循严格的 **7 层分层架构**，依赖方向单向不循环。状态机包含 **8 个状态**（含 `InitAlarm` / `RunAlarm` 分离）、**10 个触发器**：

```
PF.AutoFramework.slnx
│
├── /00. 全局配置
│   ├── Directory.Build.props         # MSBuild 全局构建配置
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
│   ├── PF.Data                       # EF Core 模型、DbContext、Repository
│   └── PF.SecsGem.DataBase           # SECS/GEM 协议专用数据库（实体、DbContext、Repository）
│
├── /04. 工具与服务层
│   ├── PF.CommonTools                # 通用工具
│   └── PF.Services                   # 业务服务（ParamService、HardwareManager、ProductionDataService）
│
├── /05. 业务模块 (Modules)
│   ├── PF.Modules.Alarm              # 报警中心
│   ├── PF.Modules.Identity           # 身份认证
│   ├── PF.Modules.Logging            # 日志查看
│   ├── PF.Modules.Parameter          # 参数管理
│   ├── PF.Modules.Debug              # 硬件调试面板
│   ├── PF.Modules.SecsGem            # SECS/GEM 通信
│   └── PF.Modules.ProductionRecord   # 生产数据记录与历史查询
│
├── /06. 应用入口 (Application)
│   └── PF.Application.Shell          # WPF App 入口
│
├── /07. Demo 工站（AutoOCR）
│   ├── PF.WorkStation.AutoOcr        # OCR 工站业务逻辑（双工位五站 + 主控 + 配方 + 成本参数）
│   └── PF.WorkStation.AutoOcr.UI    # OCR 工站 UI 模块（Views / ViewModels）
│
├── /08. 独立服务
│   └── PF.SecsGem.Service            # SECS/GEM Windows 后台服务（独立进程，TCP 双服务器转发）
│
└── /09. 元包 (Meta Package)
    └── PF.AutoFramework.Meta         # NuGet 元包，聚合所有框架项目依赖
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
| **日志** | log4net | 3.3.0 |
| **主机** | Microsoft.Extensions.Hosting | 10.0.2 |
| **状态机** | Stateless | 5.20.1 |
| **Excel** | NPOI | 2.7.5 |
| **JSON** | System.Text.Json | 内置 |
| **Windows 服务** | Microsoft.Extensions.Hosting.WindowsServices | 10.0.2 |
| **服务控制** | System.ServiceProcess.ServiceController | 8.0.0 |
| **XAML 编译** | XAMLTools.MSBuild | — |

---

## 📦 核心项目详解

### PF.Core — 核心契约层

> 无任何外部依赖，所有项目均可安全引用。

- `IHardwareDevice` — 硬件设备统一接口（7 个属性 + 4 个方法 + 3 个事件），继承 `IDisposable`
  - 属性：`DeviceId`、`DeviceName`、`IsConnected`、`HasAlarm`、`Category`、`IsSimulated`、`SuppressHealthMonitoring`
  - 方法：`ConnectAsync` / `DisconnectAsync` / `ResetAsync` / `ResetHardwareAlarmAsync`
  - 事件：`ConnectionChanged` / `AlarmTriggered`（携带 `DeviceAlarmEventArgs`）/ `HardwareAlarmAutoCleared`
- `IMotionCard` : `IHardwareDevice` — 运动控制卡接口（17 个方法 + 4 个属性）
  - 运动控制（7）：`EnableAxisAsync` / `DisableAxisAsync` / `StopAxisAsync` / `HomeAxisAsync` / `MoveAbsoluteAsync` / `MoveRelativeAsync` / `JogAsync`
  - 轴状态（3）：`GetAxisCurrentPosition` / `GetMotionIOStatus`（返回 `MotionIOStatus` 结构体）/ `ClearAxisError`
  - IO 读写（3）：`ReadInputPort` / `WriteOutputPort` / `ReadOutputPort`
  - 位置锁存（3）：`SetLatchMode` / `GetLatchNumber` / `GetLatchPos`
  - 板卡配置（1）：`LoadConfigAsync`
  - 属性：`CardIndex`、`AxisCount`、`InputCount`、`OutputCount`
- `IAttachedDevice` — 子设备与父板卡绑定接口（`ParentCard` 属性 + `AttachToCard()`）
- `IAxis` : `IHardwareDevice` — 单轴控制器接口（点表管理 + 运动控制 + 位置锁存）
- `IIOController` : `IHardwareDevice` — 数字 IO 接口（含泛型枚举重载）
- `IMechanism` : `IDisposable` — 机构抽象接口（`InitializeAsync` / `ResetAsync` / `StopAsync`）
  - 属性：`MechanismName`、`IsInitialized`、`HasAlarm`
  - 事件：`AlarmTriggered` / `AlarmAutoCleared`
- `IStation` — 工站统一接口（状态机、步序、启停控制）
- `IMasterController` — 全局主控接口（`InitializeAllAsync` / `StartAllAsync` / `StopAllAsync` / `PauseAll` / `ResumeAllAsync` / `ResetAllAsync` / `SetMode` / `RequestSystemResetAsync`）
- `ISecsGemManger` — SECS/GEM 协议管理接口（消息发送/接收/状态管理）
- `IParamService` — 泛型参数读写接口（JSON 序列化、变更事件、批量操作）
- `ILogService` — 统一日志接口（Info/Warn/Error/Debug/Success，内存缓冲区，历史查询，分类管理）
- `IUserService` — 用户认证与权限接口
- `IHardwareManagerService` — 硬件生命周期管理接口（工厂注册、配置 CRUD、拓扑初始化、仿真模式）
- `IStationSyncService` — 工站流水线信号量协同接口（作用域隔离、信号量重置、快照查询）
- `IProductionDataService` — 生产数据记录服务接口（泛型写入、条件查询、CSV/Excel 导出）
- `IAlarmService` — 报警服务接口（报警触发/清除/确认，事件发布）
- `IAlarmEventPublisher` — 报警事件发布接口（跨模块解耦通信）
- `IAlarmDictionaryService` — 报警码字典服务（元数据加载、反射扫描）
- `IRecipeService<T>` — 配方管理接口
- `IGenericRepository<T>` — 通用 CRUD 仓储接口
- `IParamRepository<T>` — 参数专用仓储接口（带变更追踪）
- `MachineState` — 8 状态：`Uninitialized` → `Initializing` → `Idle` → `Running` / `Paused`，异常分支 `InitAlarm` / `RunAlarm`，复位 `Resetting`
- `MachineTrigger` — 10 触发：`Initialize` / `InitializeDone` / `Start` / `Pause` / `Resume` / `Stop` / `Error` / `Reset` / `ResetDone` / `ResetDoneUninitialized`
- `OperationMode` — 运行模式：`Normal` / `DryRun`
- `HardwareCategory` — 设备分类枚举（9 种）：`General` / `Axis` / `IOController` / `Camera` / `Robot` / `Scanner` / `Instrument` / `MotionCard` / `LightController`
- `ModuleNavigationAttribute` — 声明式侧边栏菜单注册特性（ViewName / Title / GroupName / Icon / Order）
- `MechanismUIAttribute` — 模组调试 UI 自动发现特性
- `StationUIAttribute` — 工站调试 UI 自动发现特性
- `AlarmInfoAttribute` — 报警元数据注解特性
- `ParamViewAttribute` — 参数视图路由特性
- `UserLevel` — 角色层级：`Null`(-1) → `Operator`(0) → `Engineer`(1) → `Administrator`(2) → `SuperUser`(3)

### PF.Data — 数据访问层

- `DbContextFactory<TContext>` — 静态线程安全工厂，`ConcurrentDictionary` 缓存 DbContext 配置
- `ParamEntity` — 所有参数实体的基类（Name, JsonValue, Category, Version, TypeFullName）
- `HardwareParam` — 硬件配置参数实体（存储 `HardwareConfig` 的 JSON 序列化结果）
- `ProductionDataEntity` — 生产数据实体（ID / JsonValue / TypeFullName / RecordType / RecordTime / CreateTime）
- `UserLoginParam` — 用户登录参数实体（用户名/密码/角色持久化）
- `SystemConfigParam` — 系统通用配置参数实体
- `GenericRepository<T>` — 通用 CRUD 仓储
- `ParamRepository<T>` — 参数专用仓储（带变更追踪）
- 数据库路径：`%APPDATA%\PFAutoFrameWork\SystemParamsCollection.db`
- 生产数据库：`%APPDATA%\PFAutoFrameWork\ProductionHistory.db`

### PF.Services — 业务逻辑层

**ParamService** — 泛型参数持久化服务：
- JSON 序列化存储，Schema 无关
- 值未变更时跳过写库（优化写入频率）
- `ParamChanged` 事件携带完整审计信息（旧值、新值、操作人、时间戳）

**HardwareManagerService** — 硬件生命周期管理：
- `RegisterFactory()` 注册设备工厂函数
- `ImportConfigsAsync(configs)` 批量写入默认硬件配置（首次启动时调用，upsert 语义）
- `SaveConfigAsync(config)` / `DeleteConfigAsync(deviceId)` 单条配置的增删
- `LoadAndInitializeAsync()` 拓扑排序（父卡优先）初始化；子设备无论父板卡是否连接成功均会实例化并保留在活跃列表，UI 可见
- `GetDevice(deviceId)` 运行时按 ID 获取设备实例
- `SetGlobalSimulationModeAsync(bool)` 原子切换全部设备的仿真模式并持久化，需配合 `ReloadAllAsync()` 生效
- `ReloadAllAsync()` 热重载：断开全部设备 → 释放 → 重新从数据库加载配置并初始化

**LogService** — 高性能日志服务：
- 内存循环缓冲区（最大 1000 条）
- `Channel` 异步文件写入
- 按小时/天自动滚动，历史日志自动清理（默认 30 天）

**ProductionDataService** — 生产数据记录服务：
- 泛型写入 `RecordAsync<TData>()`：任意 POCO → JSON 序列化，非阻塞立即返回
- 条件查询 `QueryAsync(filter)` / `QueryDataAsync<TData>(filter)`
- 导出 `ExportToCsvAsync()` / `ExportToExcelAsync()`（via NPOI）
- `DataRecorded` 事件：每条数据写入后触发，供 UI 实时订阅
- 多数据库支持：仅需修改 `DbContextOptionsBuilder`（SQLite / SQL Server / MySQL）

### PF.Infrastructure — 底层基础设施

**BaseDevice**：模板方法模式，3 次重试连接（间隔 2s），模拟模式直通，`RaiseAlarm()` 触发事件链。内置健康监控循环（默认 1000ms 间隔，仿真模式 ×5），实现 `INotifyPropertyChanged`。

**硬件代理层架构**：`IMotionCard` 是所有硬件操作的统一入口。`BaseAxisDevice` 和 `BaseIODevice` 均实现 `IAttachedDevice`，所有运动/IO 方法均为代理方法，委托至 `ParentCard.XxxAsync(...)`，实现与具体 SDK 的彻底解耦。

**BaseAxisDevice**：轴设备，所有运动方法通过 `ParentCard` 代理，内置点表 JSON 持久化。仿真模式下延迟 1000ms 返回。子类仅需提供 `AxisIndex` 和 `Param` 两个抽象成员。

**BaseIODevice**：IO 设备，`ReadInput`/`WriteOutput`/`ReadOutput` 均代理到 `ParentCard`。`WaitInputAsync` 以 20ms 轮询检测输入变化。提供泛型枚举重载 `ReadInput<T>` / `WriteOutput<T>` / `WaitInputAsync<T>`。子类仅需提供 `InputCount` 和 `OutputCount` 两个抽象属性。

**BaseMotionCard**：板卡基类，21 个抽象成员（4 个属性 + 17 个方法），由厂商子类调用具体 SDK。具体板卡实现还需满足 `BaseDevice` 的 3 个抽象方法，共 **24 个**需实现的抽象成员。

**BaseMechanism**：聚合多硬件，自动订阅硬件 `AlarmTriggered` 事件，支持延迟注册设备。构造函数签名为 `(string name, IHardwareManagerService, IParamService, ILogService)`。内置丰富的工具方法：
- `WaitAxisMoveDoneAsync(IAxis, int, CancellationToken)` — 以 50ms 轮询等待轴运动完成（`MoveDone && !Moving`），默认 30s 超时，支持取消
- `WaitHomeDoneAsync(IAxis, int, CancellationToken)` — 以 10ms 轮询等待回零完成，默认 30s 超时
- `MoveAbsAndWaitAsync(...)` — 绝对运动 + 等待组合
- `MoveRelAndWaitAsync(...)` — 相对运动 + 等待组合
- `MoveToPointAndWaitAsync(IAxis, string, int, CancellationToken)` — 点表运动 + 等待
- `MoveMultiAxesToPointsAsync(...)` — `Task.WhenAll` 并发多轴点表运动
- `EnsurePointsExist<TEnum>(IAxis)` — 泛型点位自动补全：将枚举所有成员与轴点表对比，缺失的自动插入并调用 `SavePointTable()` 持久化
- `CheckReady()` — 防呆保护，`HasAlarm` 或 `!IsInitialized` 时抛异常

**StationBase\<T\>** / **StationBase\<TMemory, TStep\>**：`Stateless` 8 状态机 + 后台线程管理。实现 `INotifyPropertyChanged`，提供 `CurrentStepDescription`（步序描述字符串，子类赋值自动通知 UI）。采用 `SemaphoreSlim(1,1)` 状态锁 + `CancelAndAwaitOldTaskAsync` 取消旧任务后再启动新任务，确保并发安全。暂停机制使用 `CancellationTokenSource`（`_runCts`）实现取消式暂停。子类须实现 `ProcessNormalLoopAsync` 和 `ProcessDryRunLoopAsync` 两个抽象方法。

**BaseMasterController**：全局主控基类，编排所有工站。独立 `StateMachine` + `SemaphoreSlim(1,1)` 保护。并行操作最大并发度 4（`MaxDegreeOfParallelism`）。初始化超时 120s，复位超时 30s。内置防撕裂守卫：当子工站意外跌落到 `Uninitialized` 时自动触发全局报警。智能启动路由：根据当前状态自动决策初始化/启动/恢复。构造函数签名为 `(ILogService, HardwareInputEventBus?, IEnumerable<IStation>, IAlarmService?)`。

### PF.SecsGem.Service — SECS/GEM 独立后台服务

> 独立 Windows 后台服务（`BackgroundService`），与主 WPF 进程解耦，作为 SECS/GEM 协议的专用转发代理。

- **双 TCP 服务器架构**（`Worker` 私有字段）：
  - `SecsGemServer`：对外监听，负责与设备主机（Host）的 SECS/GEM 报文收发
  - `LocationServer`：对内监听（`127.0.0.1:6800`），负责与 WPF 主程序交互
- **消息缓冲区**（`MessageBuffer`，`Worker` 嵌套类）：内置粘包/半包处理，按 SECS 协议的 4 字节长度头解帧
- **消息队列**：`ConcurrentQueue<byte[]>` + 独立消费任务，异步处理 S0F0（LinkTest）及业务报文转发
- **状态同步**：连接/断连事件通过 `0x02` 状态帧实时通知本地客户端
- **日志记录**：独立 `Channel` 异步写入十六进制报文日志，按年月日自动分目录，路径 `D:\SWLog\SecsGemService\`
- **配置来源**：启动时从 `PF.SecsGem.DataBase` 读取 `SecsGemSystemParam`（IP/Port）

### PF.WorkStation.AutoOcr — AutoOCR Demo 工站

> 框架内置的完整 Demo 工站，展示双工位五站流水线联动。项目目录结构为 `Mechanisms/` / `Stations/`（含 `AutoOCRMachineController.cs`）/ `Recipe/` / `CostParam/`。

**机构（Mechanisms）**：
- `WS1FeedingModel` — 工位一送料模组（Z/X 轴取放料、晶圆盒尺寸识别、层位扫描）
- `WS2FeedingModule` — 工位二送料模组（与工位一对称，右侧传感器/IO）
- `WSDetectionModule` — 视觉检测模组（X/Y/Z 龙门定位、OCR 相机触发、图像存档）
- `WS1MaterialPullingModule` — 工位一拉料模组（Y 轴搬运、夹爪控制、条码扫描、叠片检测）
- `WS2MaterialPullingModule` — 工位二拉料模组（与工位一对称，右侧传感器/IO）
- `WSDataModule` — 数据记录模组（MES 批次查询、OCR 比对、检测数据归档、JSON 快照持久化）
- `WSSecsGemModule` — SECS/GEM 通信模组（S1/S2/S6/S7/S10 流处理、动态事件触发）

**工站（Stations）**（5 个）：
- `WS1FeedingStation` — 工位一来料/上料工站（44 步序：批次验证 → 配方加载 → 晶圆盒检测 → 层位扫描 → 逐层取料）
- `WS2FeedingStation` — 工位二来料/上料工站（与工位一对称，44 步序）
- `WSDetectionStation` — OCR 检测工站（28 步序：`Task.WhenAny` 双工位竞争 → 龙门运动 → OCR 拍照 → 数据比对）
- `WS1MaterialPullingStation` — 工位一出料/拉料工站（40 步序：取料 → 条码扫描 → 送检 → 回料）
- `WS2MaterialPullingStation` — 工位二出料/拉料工站（与工位一对称，40 步序）

**主控**：`AutoOCRMachineController` — 继承 `BaseMasterController`，协调三站联动

**配方**：`OCRRecipe<OCRRecipeParam>` — 单例配方管理，`OCRRecipeParam` 存储 OCR 检测参数

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
- AutoOCR 双工位五站流水线 Demo 正常运行（工位一送料 → 工位一拉料 → 检测 → 工位二送料 → 工位二拉料）

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

继承 `BaseMotionCard`，实现 21 个抽象成员（4 属性 + 17 方法）+ 3 个 `BaseDevice` 抽象方法：

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
        return true;
    }

    protected override Task InternalDisconnectAsync()
    {
        if (!IsSimulated) { /* VendorSDK.Close(CardIndex); */ }
        return Task.CompletedTask;
    }

    protected override Task InternalResetAsync(CancellationToken token) => Task.CompletedTask;

    // ===== 运动控制（7个）=====
    public override Task<bool> EnableAxisAsync(int axisIndex)
    {
        if (IsSimulated) return Task.FromResult(true);
        // VendorSDK.ServoOn(CardIndex, axisIndex);
        return Task.FromResult(true);
    }

    public override Task<bool> DisableAxisAsync(int axisIndex)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override Task<bool> StopAxisAsync(int axisIndex, bool isEmgStop = false)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override async Task<bool> HomeAxisAsync(int axisIndex,
        int homeModel, int homeVel, int homeAcc, int homeDec, int homeOffset,
        CancellationToken token = default)
    {
        if (IsSimulated) { await Task.Delay(500, token); return true; }
        // VendorSDK.HomeAxis(CardIndex, axisIndex, homeModel, ...);
        return true;
    }

    public override async Task<bool> MoveAbsoluteAsync(int axisIndex, double position,
        double velocity, double acc, double dec, double sTime,
        CancellationToken token = default)
    {
        if (IsSimulated) { await Task.Delay(200, token); return true; }
        // VendorSDK.MoveAbs(CardIndex, axisIndex, position, velocity, acc, dec);
        return true;
    }

    public override Task<bool> MoveRelativeAsync(int axisIndex, double distance,
        double velocity, double acc, double dec, double sTime,
        CancellationToken token = default)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override Task<bool> JogAsync(int axisIndex,
        double velocity, double acc, double dec, bool isPositive)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    // ===== 轴状态读取（3个）=====
    public override double? GetAxisCurrentPosition(int axisIndex)
    {
        if (IsSimulated) return 0.0;
        // return VendorSDK.GetCurrentPos(CardIndex, axisIndex);
        return 0.0;
    }

    public override MotionIOStatus GetMotionIOStatus(int axisIndex)
    {
        if (IsSimulated) return new MotionIOStatus { MoveDone = true, SVO = true };
        // 从厂商 SDK 读取轴 IO 状态并填充 MotionIOStatus
        return new MotionIOStatus();
    }

    public override Task<bool> ClearAxisError(int axisIndex)
    {
        if (IsSimulated) return Task.FromResult(true);
        // VendorSDK.ClearAlarm(CardIndex, axisIndex);
        return Task.FromResult(true);
    }

    // ===== IO 读写（3个）=====
    public override bool? ReadInputPort(int portIndex) => false;
    public override bool WriteOutputPort(int portIndex, bool value) => true;
    public override bool? ReadOutputPort(int portIndex) => false;

    // ===== 位置锁存（3个）=====
    public override Task<bool> SetLatchMode(int LatchNo, int AxisNo, int InPutPort,
        int LtcMode = 0, int LtcLogic = 0, double Filter = 0, double LatchSource = 0,
        CancellationToken token = default)
    {
        if (IsSimulated) return Task.FromResult(true);
        return Task.FromResult(true);
    }

    public override Task<int> GetLatchNumber(int LatchNo, int AxisNo,
        CancellationToken token = default)
    {
        if (IsSimulated) return Task.FromResult(0);
        return Task.FromResult(0);
    }

    public override Task<double?> GetLatchPos(int LatchNo, int AxisNo,
        CancellationToken token = default)
    {
        if (IsSimulated) return Task.FromResult((double?)0.0);
        return Task.FromResult((double?)null);
    }

    // ===== 板卡配置（1个）=====
    protected override Task<bool> InternalLoadConfigAsync(string configFilePath)
    {
        // 加载板卡配置文件（可选实现）
        return Task.FromResult(true);
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

    protected override Task<bool> InternalConnectAsync(CancellationToken token)
        => Task.FromResult(ParentCard != null);

    protected override Task InternalDisconnectAsync() => Task.CompletedTask;
    protected override Task InternalResetAsync(CancellationToken token) => Task.CompletedTask;
}
```

> **关键点**：`BaseAxisDevice` 的所有运动方法已实现为代理方法，自动调用 `ParentCard.XxxAxisAsync(AxisIndex, ...)`。

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
    hwManager.RegisterFactory("YourMotionCard", cfg => new YourMotionCard(cfg, logger));
    hwManager.RegisterFactory("YourAxis", cfg => new YourAxis(cfg, logger));
    hwManager.RegisterFactory("YourIO", cfg => new YourIO(cfg, logger));
}
```

### 2.5 添加硬件配置

在 `DefaultParameters.cs` 中添加默认配置：

```csharp
// 板卡配置（顶层设备）
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
        ConnectionParameters = new Dictionary<string, string> { ["CardIndex"] = "0" }
    })
},

// 轴配置（子设备）
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
        ConnectionParameters = new Dictionary<string, string> { ["AxisIndex"] = "0" }
    })
},
```

> **拓扑关系**：`HardwareManagerService.LoadAndInitializeAsync()` 会自动拓扑排序，先初始化板卡，再初始化子设备。

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
    private IAxis? _xAxis;
    private IIOController? _vacuumIO;

    // BaseMechanism 构造函数必须传入 IHardwareManagerService、IParamService、ILogService
    public YourMechanism(IHardwareManagerService hwManager, IParamService paramService, ILogService logger)
        : base("你的机构", hwManager, paramService, logger)
    {
    }

    // 定义轴点位枚举（由 EnsurePointsExist<T> 自动校验/补全）
    private enum XAxisPoint { 待机位, 取料位, 放料位 }

    protected override async Task<bool> InternalInitializeAsync(CancellationToken token)
    {
        _logger.Info($"[{MechanismName}] 开始初始化...");

        // 1. 延迟解析硬件（确保板卡已初始化后再取设备引用）
        _xAxis = HardwareManagerService.GetDevice("YOUR_X_AXIS") as IAxis
            ?? throw new InvalidOperationException("未找到 X 轴设备");
        _vacuumIO = HardwareManagerService.GetDevice("YOUR_VACUUM_IO") as IIOController
            ?? throw new InvalidOperationException("未找到真空 IO 设备");

        // 2. 延迟注册（报警聚合 + 批量复位）
        RegisterHardwareDevice(_xAxis as IHardwareDevice);
        RegisterHardwareDevice(_vacuumIO as IHardwareDevice);

        // 3. 连接 + 使能 + 回零
        if (!await _xAxis.ConnectAsync(token)) return false;
        if (!await _vacuumIO.ConnectAsync(token)) return false;
        if (!await _xAxis.EnableAsync()) return false;
        if (!await _xAxis.HomeAsync(token)) return false;
        _vacuumIO.WriteOutput(0, false);

        // 4. 校验并补全点表（缺失点位自动插入并持久化，枚举即文档）
        EnsurePointsExist<XAxisPoint>(_xAxis);

        _logger.Success($"[{MechanismName}] 初始化完成");
        return true;
    }

    protected override async Task InternalStopAsync()
    {
        if (_xAxis != null) await _xAxis.StopAsync();
        if (_vacuumIO != null) _vacuumIO.WriteOutput(0, false);
    }

    // 业务动作（供工站调用）
    public async Task PickAsync(CancellationToken token)
    {
        CheckReady(); // 防呆保护
        // MoveAbsoluteAsync 签名：(position, velocity, acc, dec, sTime, token)
        // 实际项目中 acc/dec/sTime 从 _xAxis.Param 或 IParamService 读取
        await _xAxis!.MoveAbsoluteAsync(100.0, 50.0, 500.0, 500.0, 0, token);
        _vacuumIO!.WriteOutput(0, true);
        if (!await _vacuumIO.WaitInputAsync(0, true, 2000, token))
            throw new Exception("真空建立超时！");
    }

    public async Task PlaceAsync(CancellationToken token)
    {
        CheckReady();
        await _xAxis!.MoveAbsoluteAsync(200.0, 50.0, 500.0, 500.0, 0, token);
        _vacuumIO!.WriteOutput(0, false);
        await Task.Delay(100, token);
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

```csharp
var container = containerRegistry.GetContainer();
container.RegisterMany(
    new[] { typeof(YourMechanism), typeof(IMechanism) },
    typeof(YourMechanism),
    reuse: DryIoc.Reuse.Singleton);
```

---

## 4. 工站层开发（Layer 3）

工站层是业务逻辑的核心，管理步序状态机和后台线程。

### 4.1 工站 8 状态机

```
Uninitialized ──(Initialize)──► Initializing ──(InitializeDone)──► Idle
                                     │                                  │
                                  (Error)                            (Start)
                                     ▼                                  ▼
                                 InitAlarm                          Running
                                                                      ↕ (Pause/Resume)
                              RunAlarm ◄──(Error)─────────────────  Paused
                                 │
                               (Reset)
                                 ▼
                             Resetting ──(ResetDone)──► Idle
                            Resetting ──(ResetDoneUninitialized)──► Uninitialized
```

| 状态 | 描述 |
|------|------|
| `Uninitialized` | 初始状态，硬件未就绪，**禁止启动** |
| `Initializing` | 正在执行硬件连接/回零 |
| `Idle` | 待机，就绪等待启动 |
| `Running` | 后台线程执行 `ProcessLoopAsync` |
| `Paused` | `_runCts` 取消（`CancellationTokenSource` 取消式暂停），线程挂起 |
| `InitAlarm` | 初始化阶段故障（来自 `Initializing` 的 Error） |
| `RunAlarm` | 运行阶段故障（来自 `Running` / `Paused` 的 Error） |
| `Resetting` | 正在执行物理复位（复位完成后根据来源回到 `Idle` 或 `Uninitialized`） |

| 触发器 | 描述 |
|--------|------|
| `Initialize` | Uninitialized → Initializing |
| `InitializeDone` | Initializing → Idle |
| `Start` | Idle → Running |
| `Pause` | Running → Paused |
| `Resume` | Paused → Running |
| `Stop` | Idle/Running/Paused → Uninitialized |
| `Error` | 根据当前状态路由到 `InitAlarm` 或 `RunAlarm` |
| `Reset` | InitAlarm/RunAlarm → Resetting |
| `ResetDone` | Resetting → Idle（来自 RunAlarm） |
| `ResetDoneUninitialized` | Resetting → Uninitialized（来自 InitAlarm） |

### 4.2 创建工站类

```csharp
// PF.Workstation.YourProject/Stations/YourStation.cs
using PF.Core.Attributes;

[StationUI("你的工站调试", "YourStationDebugView", order: 1)]
public class YourStation : StationBase<StationMemoryBaseParam>
{
    private readonly YourMechanism _mechanism;
    private readonly IStationSyncService _sync;

    private enum Step
    {
        WaitMaterial = 10,
        Pick = 20,
        Process = 30,
        Place = 40,
        NotifyDownstream = 50,
    }
    private Step _currentStep = Step.WaitMaterial;

    // 步序切换时赋值 CurrentStepDescription，UI 通过绑定自动刷新
    // 例：CurrentStepDescription = "等待来料...";

    public YourStation(YourMechanism mechanism, IStationSyncService sync, ILogService logger)
        : base("你的工站", logger)
    {
        _mechanism = mechanism;
        _sync = sync;
        _mechanism.AlarmTriggered += (s, e) => TriggerAlarm();
    }

    public override async Task ExecuteInitializeAsync(CancellationToken token)
    {
        Fire(MachineTrigger.Initialize);
        try
        {
            if (!await _mechanism.InitializeAsync(token))
                throw new Exception($"[{StationName}] 机构初始化失败！");
            Fire(MachineTrigger.InitializeDone);
        }
        catch
        {
            Fire(MachineTrigger.Error);
            throw;
        }
    }

    protected override async Task ProcessNormalLoopAsync(CancellationToken token)
    {
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
                    _pauseEvent.Wait(token);
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
                    await _sync.WaitAsync("SlotEmpty", token);
                    _currentStep = Step.Place;
                    break;

                case Step.Place:
                    await _mechanism.PlaceAsync(token);
                    _sync.Release("ProductReady");
                    _currentStep = Step.NotifyDownstream;
                    break;

                case Step.NotifyDownstream:
                    await Task.Delay(50, token);
                    _currentStep = Step.WaitMaterial;
                    break;
            }
        }
    }

    private async Task ProcessDryRunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            _pauseEvent.Wait(token);
            await _mechanism.PickAsync(token);
            await Task.Delay(100, token);
            await _mechanism.PlaceAsync(token);
            await Task.Delay(100, token);
        }
    }

    public override async Task ExecuteResetAsync(CancellationToken token)
    {
        await _mechanism.ResetAsync(token);
        await _mechanism.InitializeAsync(token);

        // 智能步序恢复
        _currentStep = _currentStep >= Step.Place ? Step.WaitMaterial : Step.Pick;
        ResetAlarm();
    }
}
```

### 4.3 注册到 DI 容器

```csharp
var container = containerRegistry.GetContainer();
container.RegisterMany(
    new[] { typeof(YourStation), typeof(StationBase<StationMemoryBaseParam>) },
    typeof(YourStation),
    reuse: DryIoc.Reuse.Singleton);
```

> **并发安全说明**：`StationBase<T>` 内置 `SemaphoreSlim(1,1)` 状态锁，所有 `Fire()` 调用均线程安全。
> 触发 `Running` 状态入口（`Start` / `Resume`）必须通过 `await FireAsync(...)` 异步触发，以正确等待 `CancelAndAwaitOldTaskAsync` 中旧任务的彻底终止，避免旧任务残留并发访问硬件。

---

## 5. 主控开发（Layer 4）

### 5.1 创建主控类

```csharp
// PF.Workstation.YourProject/Controllers/YourMasterController.cs
public class YourMasterController : BaseMasterController
{
    private readonly IStationSyncService _sync;

    public YourMasterController(
        ILogService logger,
        HardwareInputEventBus? hardwareEventBus,  // 硬件输入事件总线（可传 null 忽略）
        IStationSyncService sync,
        IEnumerable<IStation> stations,
        IAlarmService? alarmService = null)
        : base(logger, hardwareEventBus, stations, alarmService)
    {
        _sync = sync;

        // 注册流水线信号量（初始计数决定"谁先行"）
        _sync.Register("SlotEmpty", initialCount: 1, maxCount: 1);
        _sync.Register("ProductReady", initialCount: 0, maxCount: 1);
    }

    // 复位成功进入 Idle 之前执行（可选）：清理信号量、重置业务状态等
    protected override void OnAfterResetSuccess()
    {
        _sync.ResetSingleSignal("SlotEmpty", initialCount: 1);
        _sync.ResetSingleSignal("ProductReady", initialCount: 0);
    }
}
```

> **HardwareInputEventBus 说明**：基类自动订阅硬件输入事件（急停/启动/暂停/复位），并实现智能启动逻辑——`Uninitialized` 状态时先初始化再启动，`Paused` 时直接恢复，其他状态忽略。无物理面板时传入 `null` 即可。取代原 `PhysicalButtonEventBus`。

### 5.2 典型调用序列

```csharp
await controller.InitializeAllAsync();        // 必须，否则无法 Start
controller.SetMode(OperationMode.DryRun);     // 仅 Idle 状态有效
await controller.StartAllAsync();             // 异步：等待 Running.OnEntryAsync 完成
controller.PauseAll();                        // 同步
await controller.ResumeAllAsync();            // 异步
await controller.StopAllAsync();              // 异步
await controller.ResetAllAsync();             // 异步
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

### 6.2 创建 UI 模块

```csharp
// PF.Workstation.YourProject.UI/YourProjectUIModule.cs
public class YourProjectUIModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<YourMechanismView, YourMechanismViewModel>(
            "YourMechanismView");
        containerRegistry.RegisterForNavigation<YourStationDebugView, YourStationDebugViewModel>(
            "YourStationDebugView");
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var menuService = containerProvider.Resolve<INavigationMenuService>();
        menuService.RegisterAssembly(typeof(YourProjectUIModule).Assembly);
    }
}
```

### 6.3 添加侧边栏菜单

```csharp
[ModuleNavigation("YourProductionView", "生产操作",
    GroupName = "生产管理", Icon = "Production", Order = 1)]
public partial class YourProductionView : UserControl { }
```

### 6.4 注册模块到 Shell

```csharp
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    moduleCatalog.AddModule<IdentityModule>();
    moduleCatalog.AddModule<LoggingModule>();
    moduleCatalog.AddModule<ParameterModule>();
    moduleCatalog.AddModule<DebugModule>();
    moduleCatalog.AddModule<YourProjectUIModule>();
}
```

---

## 7. 参数扩展

### 7.1 新增参数分类

```csharp
// Step 1: 定义实体类
[Table("MotionParams")]
public class MotionParam : ParamEntity
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public override string ID { get; set; } = Guid.NewGuid().ToString();
}

// Step 2: 注册到 DbContext
public DbSet<MotionParam> MotionParams { get; set; }

// Step 3: 添加默认值
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

// Step 4: 注册类型映射
paramService.RegisterParamType<MotionParam, double>();
```

### 7.2 枚举驱动的参数定义（推荐模式）

`PF.CommonTools` 提供了 `EnumParameterExtensions`，可通过枚举配合标准 .NET 特性声明参数名称、描述和默认值，实现**枚举即文档**，并避免魔术字符串：

```csharp
// Step 1: 在工站项目中定义参数枚举
using System.ComponentModel;

public enum E_Params
{
    [Category("运动参数")]
    [Description("X 轴运动速度 (mm/s)")]
    [DefaultValue(100.0)]
    XAxisSpeed,

    [Category("运动参数")]
    [Description("X 轴加速度 (mm/s²)")]
    [DefaultValue(500.0)]
    XAxisAcc,

    [Category("超时参数")]
    [Description("轴运动超时 (ms)")]
    [DefaultValue(5000)]
    AxisMoveTimeout,
}
```

```csharp
// Step 2: 利用扩展方法读取元信息（零装箱，线程安全缓存）
using PF.CommonTools.EnumRelated;

string desc    = E_Params.XAxisSpeed.GetDescription();   // "X 轴运动速度 (mm/s)"
string cat     = E_Params.XAxisSpeed.GetCategory();      // "运动参数"
double def     = E_Params.XAxisSpeed.GetDefaultValueAs<E_Params, double>(); // 100.0
int timeout    = E_Params.AxisMoveTimeout.GetDefaultValueAs<E_Params, int>(); // 5000
```

```csharp
// Step 3: 结合 IParamService 读写（枚举名称作为 key，避免硬编码字符串）
double speed = (await _paramService.GetParamAsync<MotionParamValue>(
    E_Params.XAxisSpeed.ToString()))?.Speed
    ?? E_Params.XAxisSpeed.GetDefaultValueAs<E_Params, double>();
```

> **优势**：重命名参数时 IDE 自动检测所有引用；枚举成员即为参数目录，无需维护独立文档。

### 7.4 读写参数

> **注意**：`IParamService` 的泛型约束为 `where T : class`，**不支持直接使用值类型**（`int`/`double`/`bool` 等）。
> 推荐将参数数据封装为 POCO 类（如 Step 1 中定义的 `MotionParamValue`），再进行读写。

```csharp
// ① 定义包装类（仅需一层浅封装）
public class MotionParamValue
{
    public double Speed { get; set; }
}

// ② 读取参数（T 必须是引用类型）
var param = await _paramService.GetParamAsync<MotionParamValue>("Axis1_Speed");
double speed = param?.Speed ?? 100.0;

// ③ 写入参数
await _paramService.SetParamAsync(
    "Axis1_Speed",
    new MotionParamValue { Speed = 200.0 },
    currentUser,
    "速度调整");
```

---

## 8. 生产数据记录

### 8.1 定义数据模型

```csharp
public class WeldingProcessData
{
    public string LotId { get; set; }
    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public int DurationMs { get; set; }
    public bool Passed { get; set; }
}
```

### 8.2 写入数据

```csharp
// RecordAsync 签名：RecordAsync<TData>(TData data, string? recordType = null)
await _productionService.RecordAsync(data, recordType: "WeldingProcess");
```

### 8.3 历史查询

```csharp
// ProductionQueryFilter 可用字段：StartTime / EndTime / RecordType / Keyword / MaxCount
var filter = new ProductionQueryFilter
{
    StartTime = DateTime.Today,
    EndTime = DateTime.Now,
    RecordType = "WeldingProcess",  // 精确匹配记录类型
    MaxCount = 500
};
var records = await _productionService.QueryAsync(filter);
var typedData = await _productionService.QueryDataAsync<WeldingProcessData>(filter);
```

### 8.4 导出数据

```csharp
await _productionService.ExportToExcelAsync(filter, @"C:\Reports\Welding.xlsx");
await _productionService.ExportToCsvAsync(filter, @"C:\Reports\Welding.csv");
```

### 8.5 实时监控

```csharp
_productionService.DataRecorded += OnDataRecorded;

private void OnDataRecorded(object? sender, ProductionDataRecordedEventArgs e)
{
    Application.Current?.Dispatcher.InvokeAsync(() =>
    {
        var data = e.Record.Deserialize<WeldingProcessData>();
        RecentRecords.Insert(0, e.Record);
    });
}
```

---

## 9. 核心概念

### 9.1 三层硬件抽象

```
BaseDevice（设备）
    ├── BaseMotionCard    ← 板卡，封装厂商 SDK
    ├── BaseAxisDevice    ← 轴，代理到 ParentCard
    └── BaseIODevice      ← IO，代理到 ParentCard

BaseMechanism（机构）      ← 聚合多硬件，业务动作
    └── RegisterHardwareDevice() → 报警聚合

StationBase<T>（工站）     ← 步序状态机 + 后台线程 + 泛型内存持久化
    ├── ProcessNormalLoopAsync  ← 正常生产循环
    ├── ProcessDryRunLoopAsync  ← 空跑循环
    └── ExecuteResetAsync ← 智能恢复（断点续跑）
```

### 9.2 代理模式

`BaseAxisDevice` 和 `BaseIODevice` 是代理包装器：
- 所有操作委托给 `ParentCard`（`IMotionCard`）
- 新增厂商只需实现 `XXXMotionCard`
- 轴/IO 代码零改动，完全解耦

### 9.3 自动发现机制

| 特性 | 作用目标 | 消费者 |
|------|----------|--------|
| `[ModuleNavigation]` | 侧边栏菜单 | `NavigationMenuService` |
| `[MechanismUI]` | 机构调试面板 | `MechanismDebugViewModel` |
| `[StationUI]` | 工站调试面板 | `StationDebugViewModel` |
| `[AlarmInfo]` | 报警元数据 | `AlarmDictionaryService` |
| `[ParamView]` | 参数视图路由 | `ParameterModule` |

**核心约束**：特性中的 `ViewName` 必须与 `RegisterForNavigation` 的 key 完全一致。

### 9.4 流水线同步

```csharp
// 上游工站
await _sync.WaitAsync("SlotEmpty", token);
_sync.Release("ProductReady");

// 下游工站
await _sync.WaitAsync("ProductReady", token);
_sync.Release("SlotEmpty");
```

---

## 10. 最佳实践

### 10.1 命名规范

| 类型 | 前缀/后缀 | 示例 |
|------|-----------|------|
| 设备 ID | 大写下划线 | `SIM_X_AXIS_0` |
| 实现类名 | 帕斯卡 | `SimMotionCard` |
| 信号量 | 帕斯卡 | `SlotEmpty` |
| 步序枚举 | 帕斯卡 + 整数值 | `Pick = 20` |

### 10.2 错误处理

```csharp
// 设备层：抛异常 + 触发报警
if (!result) { RaiseAlarm("ERR_001", "连接失败"); return false; }

// 机构层：CheckReady + 日志
public async Task PickAsync(CancellationToken token)
{
    CheckReady();
    _logger.Info("取料中...");
}

// 工站层：try-catch + 状态机
try { await _mechanism.PickAsync(token); }
catch (Exception ex) { _logger.Error($"取料失败: {ex.Message}"); TriggerAlarm(); }
```

### 10.3 线程安全

```csharp
// UI 更新必须使用 Dispatcher
Dispatcher.InvokeAsync(() => { Status = "更新状态"; });
```

### 10.4 步序描述 UI 绑定

在工站 ViewModel 中绑定 `CurrentStepDescription`，实时显示当前步序：

```xml
<TextBlock Text="{Binding Station.CurrentStepDescription}" />
```

在工站类的 `ProcessLoopAsync` 中每次步序切换时赋值：

```csharp
case Step.WaitMaterial:
    CurrentStepDescription = "等待来料...";
    await WaitForMaterialAsync(token);
    _currentStep = Step.Pick;
    break;

case Step.Pick:
    CurrentStepDescription = "正在取料...";
    await _mechanism.PickAsync(token);
    _currentStep = Step.Place;
    break;
```

### 10.5 硬件仿真模式切换

开发阶段无需真实硬件，通过 `SetGlobalSimulationModeAsync` 一键切换：

```csharp
// 切换为全仿真模式（重载后生效）
await _hwManager.SetGlobalSimulationModeAsync(true);
await _hwManager.ReloadAllAsync();

// 切换为真实硬件模式
await _hwManager.SetGlobalSimulationModeAsync(false);
await _hwManager.ReloadAllAsync();
```

### 10.6 常见坑

| 问题 | 原因 | 解决 |
|------|------|------|
| View 导航失败 | key 不匹配 | 检查特性 ViewName 与注册 key |
| 设备未找到 | ID 拼写错误 | 检查 HardwareConfig.DeviceId |
| 信号量死锁 | ResetAll 时线程未停 | 确保 StopAllAsync 后再 ResetAllAsync |
| 重复实例 | RegisterSingleton 多次 | 使用 RegisterMany |
| Start 后旧任务仍在运行 | 使用了同步 Fire 触发 Running | 触发 Start/Resume 必须用 `await FireAsync` |
| 机构初始化取不到设备 | 构造函数中调用了 GetDevice | 必须在 `InternalInitializeAsync` 中延迟解析 |
| 点表丢失 | 未调用 EnsurePointsExist | 在 `InternalInitializeAsync` 末尾调用 `EnsurePointsExist<TEnum>(axis)` |
| 仿真模式切换未生效 | 仅改配置未重载 | 切换后需调用 `ReloadAllAsync()` |
| 工站抽象方法找不到 | 接口名已更新 | `ProcessLoopAsync` → `ProcessNormalLoopAsync` / `ProcessDryRunLoopAsync` |
| StationSyncService.Reset 不存在 | API 已更新 | 使用 `ResetSingleSignal` / `ResetScope` / `ResetAll` |

---

## 🛠️ 第三方集成建议

| 场景 | 建议方案 |
|------|---------|
| **条码扫描** | 集成 `ZXing.Net`，封装为 `IScannerService` |
| **工业标签打印** | 接入 BarTender SDK，隔离在独立打印模块 |
| **视觉检测** | 封装 HalconDotNet / OpenCvSharp，作为独立 VisionModule |
| **PLC 通信** | 基于 `TCPClient` / Modbus 协议实现 `IPlcService` |
| **运动控制卡** | 继承 `BaseMotionCard`，封装厂商 SDK |
| **报警管理** | 新建 `AlarmModule`，订阅 `BaseDevice.AlarmTriggered` 事件 |

---

## 11. 完整设计模式索引

本框架运用了多种经典设计模式：

| 模式 | 应用位置 | 说明 |
|------|---------|------|
| **模板方法** | `BaseDevice.ConnectAsync` → `InternalConnectAsync` | 定义算法骨架（模拟检查→重试循环→OnConnected），子类仅覆写钩子 |
| **代理/委托** | `BaseAxisDevice` / `BaseIODevice` | 所有操作委托给 `ParentCard`，新增厂商仅需实现一个 `XXXMotionCard` |
| **状态机** | `StationBase<T>` / `BaseMasterController` | Stateless 库，8 状态 10 触发，`SemaphoreSlim` 保护并发 |
| **MVVM** | 所有 UI 模块 | Prism ViewModelBase，View-ViewModel 绑定，Region 导航 |
| **模块插件** | Prism `IModule` | 每个业务域独立模块，动态加载注册 |
| **工厂** | `HardwareManagerService.RegisterFactory` | `Func<HardwareConfig, IHardwareDevice>` 字典，按类名字符串匹配 |
| **仓储** | `GenericRepository<T>` / `ParamRepository<T>` | 数据访问抽象，EF Core 实现 |
| **观察者/事件聚合** | 三层事件体系 | (a) C# 原生事件（设备→模组报警传播）；(b) Prism `EventAggregator`（跨模块解耦）；(c) 服务级事件（`ParamChanged`、`DataRecorded`） |
| **属性自动发现** | `[ModuleNavigation]` / `[MechanismUI]` / `[StationUI]` | 反射扫描特性，零配置注册 UI 视图 |
| **生产者-消费者** | `ProductionDataService` | `Channel<T>` 有界队列（10000）+ 单消费线程，非阻塞写入 |
| **双重信号量互锁** | `IStationSyncService` | 流水线协同，命名信号量 + 作用域生命周期 |
| **任务竞争** | `WSDetectionStation` | `Task.WhenAny` 实现双工位检测资源的竞争获取 |
| **单例** | DryIoc `Reuse.Singleton` | 全局唯一的服务、模组、工站、主控实例 |

---

## 12. UI 组件库详解

### PF.UI.Controls — 自定义 WPF 控件库（~230 个 .cs 文件）

> 独立的自定义控件库，可脱离框架单独使用。

| 分类 | 控件 | 说明 |
|------|------|------|
| **附加属性** | `BackgroundSwitchElement` / `BorderElement` / `ComboBoxAttach` / `DataGridAttach` / `IconElement` / `InfoElement` / `TipElement` / `TitleElement` | XAML 附加属性，声明式配置控件外观 |
| **通知** | `Growl` | Toast 通知（成功/警告/错误/信息），替代 MessageBox |
| **导航** | `Drawer` / `SideMenu` | 抽屉式导航 + 侧边菜单 |
| **步骤** | `StepBar` | 步骤条控件，适用于向导式流程 |
| **标签** | `Tag` | 标签/徽章控件 |
| **功能区** | `Ribbon` | Office 风格功能区 |
| **属性编辑** | `PropertyGrid` | 属性网格，类似 WinForms PropertyGrid |
| **截图** | `Screenshot` | 屏幕截图控件 |
| **特效** | `ConfettiCannon` / `CoverFlow` / `RunningBlock` | 彩纸庆祝 / 封面流 / 滚动文字 |
| **输入** | `ComboBox` / `TextBox` / `PasswordBox` / `NumericUpDown` / `SearchBar` / `AutoCompleteTextBox` / `CheckComboBox` / `SearchComboBox` / `PinBox` | 增强型输入控件 |
| **布局** | `FlexPanel` / `CirclePanel` / `HoneycombPanel` / `WaterfallPanel` / `SimplePanel` / `RelativePanel` | 自定义布局面板 |
| **时间** | `Clock` / `FlipClock` / `CalendarWithClock` / `TimeBar` | 时钟/日历控件 |
| **窗口** | `GlowWindow` / `BlurWindow` / `PopupWindow` | 自定义窗口基类 |
| **交互** | `EventTrigger` / `InvokeCommandAction` / `EventToCommand` | Behavior 系统 |

### PF.UI.Infrastructure — UI 基础设施

| 类 | 用途 |
|----|------|
| `ViewModelBase` | 抽象基类，继承 `BindableBase` + `IDestructible`，自动解析 `IEventAggregator` / `IDialogService` / `IServiceProvider` / `IMessageService` |
| `RegionViewModelBase` | Region 级 ViewModel 基类 |
| `PFDialogViewModelBase` | 对话框 ViewModel 基类 |
| `NavigationMenuService` | 自动扫描程序集 `[ModuleNavigation]` 特性，构建侧边栏菜单树（`ObservableCollection<NavigationItem>`），支持分组排序 |
| `PermissionHelper` | 视图路由名→中文名映射，按 `UserLevel` 提供默认可访问视图列表 |
| `MessageService` | 对话框服务抽象（`IMessageService`），封装确认/输入/等待对话框 |
| `ViewDataMapperBase` | 参数对话框数据映射基类，硬件参数编辑器的数据桥梁 |
| `EnterKeyTraversalBehavior` | WPF 附加行为，Enter 键自动跳转下一控件 |

**Prism 事件定义**：

| 事件 | 用途 |
|------|------|
| `AlarmTriggeredEvent` | 报警触发（跨模块广播） |
| `AlarmClearedEvent` | 报警清除 |
| `AlarmAcknowledgeEvent` | 报警确认 |
| `HardwareResetRequestedEvent` | 硬件复位请求（Shell 桥接至 Infrastructure） |
| `SystemResetRequestedEvent` | 系统复位请求 |
| `UserChangedEvent` | 用户切换通知 |

### PF.UI.Shared — 共享 UI 工具库（~180 个 .cs 文件）

底层 WPF 工具库，不含业务逻辑：

- **值转换器**：`BooleanToVisibilityConverter`（WPF 内置，别名 `Boolean2VisibilityConverter`）/ `Boolean2VisibilityReConverter`（自定义）/ `Color2HexStringConverter` 等
- **扩展方法**：`DependencyObject` / `FrameworkElement` / `Color` / `String` / `Geometry`
- **绘图/几何**：贝塞尔曲线扁平化、路径几何计算
- **媒体效果**：`GeometryEffect` / `SketchGeometryEffect` / `ArcGeometrySource`
- **Win32 互操作**：窗口句柄封装、剪贴板钩子、键盘/鼠标钩子
- **辅助类**：动画、绑定、颜色、DPI、图标、资源、安全、可视化、窗口

### PF.UI.Resources — 主题与资源

- 深色/浅色主题字典（`Theme=Light` / `Theme=Dark`）
- 所有自定义控件的 XAML 样式
- 图片、图标、字体资源
- 启动画面（Splash Screen）

---

## 13. 业务模块详解

### PF.Modules.Alarm — 报警中心

| 类 | 用途 |
|----|------|
| `AlarmModule` | Prism 模块注册（`AlarmCenterView` / `AlarmDetailCardView` / `PFAlarmBaseWindow`） |
| `AlarmCenterViewModel` | 报警中心主 ViewModel，报警列表展示 |
| `AlarmDetailCardViewModel` | 报警详情卡片 ViewModel，确认/清除操作 |
| `AlarmCenterView` | 报警中心视图（`[ModuleNavigation]` 自动注册） |
| `NullToVisibilityConverter` | 空值→可见性转换器 |

### PF.Modules.Debug — 硬件调试面板

> 通过 `[MechanismUI]` / `[StationUI]` 特性自动发现并加载调试面板。

| 调试视图 | ViewModel | 调试对象 |
|---------|-----------|---------|
| `HardwareDebugView` | `HardwareDebugViewModel` | 设备总览 |
| `AxisDebugView` | `AxisDebugViewModel` | 轴运动调试（JOG/绝对/相对/回零/使能） |
| `IODebugView` | `IODebugViewModel` | IO 输入输出实时监控 |
| `CardDebugView` | `CardDebugViewModel` | 板卡状态 |
| `CameraDebugView` | `CameraDebugViewModel` | 相机调试 |
| `BarcodeScanDebugView` | `BarcodeScanDebugViewModel` | 条码扫描调试 |
| `LightControllerDebugView` | `LightControllerDebugViewModel` | 光源控制器调试 |
| `MechanismDebugView` | `MechanismDebugViewModel` | 机构调试（自动发现 `[MechanismUI]`） |
| `StationDebugView` | `StationDebugViewModel` | 工站调试（自动发现 `[StationUI]`） |
| `AxisParamDialog` | `AxisParamDialogViewModel` | 轴参数编辑对话框 |

### PF.Modules.Identity — 身份认证

| 类 | 用途 |
|----|------|
| `LoginViewModel` | 登录逻辑（Singleton），密码验证、角色切换 |
| `UserManagementViewModel` | 用户管理（增删改查） |
| `PagePermissionViewModel` | 页面权限分配 |
| `LoginView` | 登录对话框 |
| `UserManagementView` | 用户管理视图 |
| `PagePermissionView` | 权限管理视图 |

### PF.Modules.Logging — 日志查看

| 类 | 用途 |
|----|------|
| `LogListViewModel` | 日志列表（实时显示，按级别/分类过滤） |
| `LogManagementViewModel` | 日志管理（清理、导出配置） |

### PF.Modules.Parameter — 参数管理

| 类 | 用途 |
|----|------|
| `ParameterViewModel` | 参数管理主 ViewModel |
| `ParamItemViewModel` | 参数项 ViewModel |
| `CommonChangeParamDialogViewModel` | 参数修改对话框 |
| `ViewFactory` | 硬件参数视图工厂（根据硬件类型选择编辑器） |
| **硬件参数编辑器** | `LTDMCMotionCardParamView` / `EtherCatAxisParamView` / `EtherCatIOParamView` / `HKBarcodeScanParamView` / `KeyenceIntelligentCameraParamView` / `CTSLightControllerParamView` |
| **基础类型编辑器** | `BooleanParamView` / `EnumParamView` / `NumericParamView` / `StringParamView` / `UserParamView` |
| **数据映射器** | `GenericViewDataMapper` / 各硬件类型 `ParamViewMapper` |

### PF.Modules.ProductionRecord — 生产记录

| 类 | 用途 |
|----|------|
| `ProductionMonitorViewModel` | 实时生产监控 |
| `ProductionHistoryViewModel` | 历史记录查询 |

### PF.Modules.SecsGem — SECS/GEM 通信调试

| 类 | 用途 |
|----|------|
| `SecsGemDebugViewModel` | SECS/GEM 调试主面板 |
| `SecsCommandBuilderViewModel` | 命令构建器（编辑 SxFy 报文） |
| `SecsConnectionViewModel` | 连接状态查看 |
| `SecsLogViewModel` | 通信日志查看 |
| `SecsParameterViewModel` | 参数编辑（VID/CEID/ReportID） |
| `SecsServiceManagerViewModel` | 服务管理 |
| `CommandEditDialog` | 命令编辑对话框 |
| `SecsNodeConfigDialog` | 节点配置对话框 |
| `VidSelectDialog` | VID 选择对话框 |

---

## 14. 工具与辅助项目详解

### PF.CommonTools — 通用工具库

| 类 | 用途 |
|----|------|
| `EnumParameterExtensions` | 枚举参数扩展：`GetDescription()` / `GetCategory()` / `GetDefaultValueAs<T,R>()`，零装箱，`ConcurrentDictionary` 缓存 |
| `EnumParamInfo` | 枚举参数元数据（Description / Category / DefaultValue / TypeFullName） |
| `JsonSingleValueHelper` | JSON 单值序列化辅助 |
| `TypeExtensions` / `TypeScanner` | 反射工具（程序集扫描、类型发现） |
| `ServerMangerTool` | 服务管理工具 |

### PF.SecsGem.DataBase — SECS/GEM 数据库

| 类 | 用途 |
|----|------|
| `SecsGemDbContext` | EF Core 上下文，7 个 `DbSet` |
| `SecsGemSystemEntity` | 系统配置（IP / Port） |
| `VIDEntity` / `CEIDEntity` / `ReportIDEntity` / `CommandIDEntity` | SECS/GEM 协议实体 |
| `IncentiveEntity` / `ResponseEntity` | 激励/响应命令实体 |
| `SecsGemDataBaseManger` | 数据库管理门面类 |

### PF.AutoFramework.Meta — NuGet 元包

聚合所有框架项目的 NuGet 元包，引用后自动传递所有子项目依赖。

---

## 15. 完整依赖关系图

```
PF.Core（零依赖）
  ← PF.CommonTools
  ← PF.Data（EF Core, Prism.Core）
  ← PF.Infrastructure（Stateless, log4net, NPOI, PF.Core, PF.SecsGem.DataBase）
  ← PF.SecsGem.DataBase（EF Core, PF.Core）
  ← PF.UI.Shared
  ← PF.UI.Resources（PF.UI.Shared）
  ← PF.UI.Controls（PF.UI.Resources, PF.UI.Shared）
  ← PF.UI.Infrastructure（Prism.Wpf, PF.UI.Controls）
  ← PF.Services（PF.Data, PF.Infrastructure, Prism.DryIoc）
  ← PF.Modules.*（PF.UI.Infrastructure, PF.Services, PF.Core）
  ← PF.WorkStation.AutoOcr（PF.Infrastructure, PF.Services, PF.Core）
  ← PF.WorkStation.AutoOcr.UI（PF.WorkStation.AutoOcr, PF.UI.Infrastructure, Prism.DryIoc）
  ← PF.Application.Shell（依赖所有，组合根）
  ← PF.SecsGem.Service（PF.Infrastructure, PF.SecsGem.DataBase）
```

**关键架构约束**：
- `PF.Infrastructure` **零 Prism 依赖**：Shell 通过事件桥接（`HardwareResetRequestedEvent` → `BaseMasterController`）维持 Infrastructure 的纯净性
- `PF.Core` **零外部依赖**：所有项目均可安全引用
- 依赖方向严格单向，无循环引用

---

## 16. 应用启动流程

`App.xaml.cs` 作为组合根，执行以下启动序列：

1. **单实例检查**：命名 `Mutex`（`Global\PFAutoFrameworkOCRAppID-...`），防止多开
2. **Prism 配置**：DryIoc 容器 + 模块目录
3. **DI 注册**（按顺序）：
   - 日志（`log4net`）
   - 参数数据库（`AppParamDbContext` → SQLite → `EnsureCreatedAsync`）
   - 生产数据库（`ProductionDbContext`）
   - SECS/GEM 服务（`SecsGemDbContext` + 双 TCP 服务器）
   - 硬件工厂（6 种设备：LTDMC / EtherCat / HKBarcode / Keyence / CTS_LightController）
   - 硬件输入事件总线（`HardwareInputEventBus`，取代原 `PhysicalButtonEventBus`）
   - 机制（7 个模组 Singleton：`WS1FeedingModel` / `WS2FeedingModule` / `WSDetectionModule` / `WS1MaterialPullingModule` / `WS2MaterialPullingModule` / `WSDataModule` / `WSSecsGemModule`）
   - 工站（5 个站 Singleton：`WS1FeedingStation` / `WS2FeedingStation` / `WSDetectionStation` / `WS1MaterialPullingStation` / `WS2MaterialPullingStation`）
   - 主控（`AutoOCRMachineController` Singleton）
   - 配方（`OCRRecipe<OCRRecipeParam>`）
   - 报警服务（独立 `AlarmHistory.db`）
   - 身份服务（`UserService`）
   - UI 基础设施（`NavigationMenuService` / `MessageService` / `Splash`）
4. **模块加载**（按顺序）：`Alarm` → `Logging` → `Parameter` → `Identity` → `Debug` → `AutoOcr.UI` → `SecsGem` → `ProductionRecord`
5. **Shell 初始化**：
   - 注册 Shell 程序集菜单 → `PermissionHelper` 初始化
   - 静默登录（SuperUser，密码 = `DateTime.Now.ToString("yyyyMMddHH00")`）
   - 事件桥接：Prism `EventAggregator` → Infrastructure 层事件
6. **Splash 启动画面**：进度报告 → 配置加载 → 硬件拓扑初始化 → 机制初始化（按顺序：`WS1FeedingModel` → `WS1MaterialPullingModule` → `WS2FeedingModule` → `WS2MaterialPullingModule` → `WSDetectionModule` → `WSDataModule` → `WSSecsGemModule`）
7. **硬件监控启动**：`IHardwareInputMonitor.StartStandardMonitoring()` 启动双线程扫描

---

## 17. SECS/GEM 独立服务架构

```
┌─────────────────┐     TCP 127.0.0.1:6800     ┌──────────────────────┐
│  WPF 主程序      │ ◄──────────────────────────► │ LocationServer      │
│  (InternalClient)│                              │   (本地服务器)        │
└─────────────────┘                              └──────────┬───────────┘
                                                            │ 消息转发
                                                            ▼
                                                 ┌──────────────────────┐
                                                 │ SecsGemServer        │
                                                 │   (外部服务器)        │
                                                 │   (可配置 IP:Port)    │
                                                 └──────────┬───────────┘
                                                            │
                                                            ▼
                                                 ┌──────────────────────┐
                                                 │ 设备主机 (Host)       │
                                                 │ (工厂 MES / SEMI 设备) │
                                                 └──────────────────────┘
```

**消息流**：
- **出站**（WPF → Host）：`LocationServer` 接收 → 去掉 0x00 命令字节 → `SecsGemServer.SendAsync()`
- **入站**（Host → WPF）：`SecsGemServer` 接收 → `MessageBuffer` 粘包/半包处理 → S0F0 LinkTest 直接回复 → 其他报文 → `LocationServer` 转发
- **状态同步**：Host 连接/断连 → `LocationServer` 发送 `[0x02, status]` 状态帧
- **日志**：独立 `Channel` 异步写入十六进制报文日志 → `D:\SWLog\SecsGemService\{year}\{month}\{yyyy-MM-dd}.log`

---

## 18. 已注册硬件厂商实现

| 类 | 厂商/类型 | 说明 |
|----|----------|------|
| `LTMDCMotionCard` | 雷泰 (LTDMC) | 运动控制卡，继承 `BaseMotionCard` |
| `EtherCatAxis` | EtherCAT | 轴设备，继承 `BaseAxisDevice` |
| `EtherCatIO` | EtherCAT | IO 设备，继承 `BaseIODevice` |
| `HKBarcodeScan` | 海康机器人 | 条码扫描器（TCP 通信），继承 `BaseBarcodeScan` |
| `KeyenceIntelligentCamera` | 基恩士 | 视觉相机，继承 `BaseIntelligentCamera` |
| `CTSLightController` | CTS | 光源控制器，继承 `BaseLightController` |
| `OPTLightController` | OPT | 光源控制器，继承 `BaseLightController`（`internal`，桩实现） |

---

## ⚠️ 已知限制

1. **初始化必须先于启动**：必须调用 `InitializeAllAsync()` 才能 `StartAll()`
2. **模式切换仅限 Idle**：`SetMode()` 在非 Idle 状态返回 `false`
3. **数据库无迁移**：使用 `EnsureCreatedAsync()`，不支持 EF Core Migrations
4. **信号量重置时序**：`ResetAll()` 必须在所有工站线程停止后调用
5. **TCP 重连无上限**：建议在业务层增加熔断逻辑
6. **日志 UI 线程安全**：需通过 `Dispatcher.Invoke` 确保线程安全
7. **仅支持 Windows**：基于 WPF

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
