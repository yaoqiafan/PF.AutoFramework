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
* **🎨 现代扁平化 UI (Modern Flat UI)**：内置海量高颜值自定义控件库（如 `Growl` 消息提示、`Drawer` 抽屉视图、步骤条等），支持极简的现代化工业 UI 设计风格。
* **🔐 全局身份与权限管理**：内置完整的身份认证模块（`IdentityModule`），支持细粒度（页面级/按钮级）的权限管控及多级用户角色（操作员、工程师、管理员等）。
* **💾 动态参数与持久化系统**：基于 EF Core 9 + SQLite 提供强大的泛型参数服务（`IParamService`），JSON 序列化存储，支持自动建表与审计追踪。
* **🔌 高性能底层通信**：封装了稳定可靠的 TCP 服务端/客户端通信基类（信号量锁 + 自动重连），支持工业场景下的高频数据交互。
* **📊 工业级日志追踪**：内置高性能日志组件（`LogService`），生产者-消费者异步写入，支持分类日志、循环内存缓存（1000条）与自动日志滚动。
* **🏭 硬件设备抽象**：提供 `BaseDevice` / `BaseMechanism` / `StationBase` 三级设备抽象，内置模拟模式，便于离线开发与测试。

---

## 🏗️ 系统架构图

<img width="1024" height="559" alt="image" src="https://github.com/user-attachments/assets/b975209b-aa36-40f7-8353-2df6f837242d" />

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
│   └── PF.Infrastructure             # 底层实现（TCP通信、日志器、设备基类）
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
│   └── PF.Services                   # 业务服务（UserService、ParamService、LogService）
│
├── /05. 业务模块 (Modules)
│   ├── PF.Modules.Identity           # 身份认证与权限管理
│   ├── PF.Modules.Logging            # 日志查看与管理
│   ├── PF.Modules.Parameter          # 系统参数管理
│   └── PF.Modules.SecsGem            # 半导体设备 SECS/GEM 通信
│
├── /06. 应用入口 (Application)
│   └── PF.Application.Shell          # WPF App入口、Prism Bootstrapper、主题加载
│
└── /07. 外部服务 (External)
    └── ConsoleApp1                   # 示例/测试控制台项目
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

- `IParamService` — 泛型参数读写接口
- `ILogService` — 统一日志接口（Info/Warn/Error/Debug）
- `IUserService` — 用户认证与权限接口
- `IServer` / `IClient` — TCP 通信契约
- `IEntity` — 实体基类（Id, CreateTime, UpdateTime, Remarks）
- `ModuleNavigationAttribute` — 声明式视图导航注册特性
- `UserLevel` 枚举 — 角色层级：Null → Operator → Engineer → Administrator → SuperUser

### PF.Data — 数据访问层

- `DbContextFactory<TContext>` — 静态线程安全工厂，`ConcurrentDictionary` 缓存 DbContext 配置
- `ParamEntity` — 所有参数实体的基类（Name, JsonValue, Category, Version, TypeFullName）
- `GenericRepository<T>` — 通用 CRUD 仓储
- `ParamRepository<T>` — 参数专用仓储（按名称/分类查询、版本更新）
- 数据库路径：`%APPDATA%\PFAutoFrameWork\SystemParamsCollection.db`

### PF.Services — 业务逻辑层

**ParamService** — 泛型参数持久化服务：
- JSON 序列化存储，Schema 无关
- 值未变更时跳过写库（优化写入频率）
- `ParamChanged` 事件携带完整审计信息（旧值、新值、操作人、时间戳）
- 支持自定义类型映射注册：`RegisterParamType<TEntity, TModel>()`

**LogService** — 高性能日志服务：
- 内存循环缓冲区（最大 1000 条）
- `BlockingCollection<LogEntry>` 异步文件写入，不阻塞 UI 线程
- 支持按小时/天自动滚动日志文件
- 历史日志正则解析，自动清理超期日志（默认 30 天）

**UserService** — 用户认证服务：
- 数据库验证 + 回退认证
- `IsAuthorized(UserLevel)` 角色权限校验
- `CurrentUserChanged` 事件通知全局状态变更

### PF.Infrastructure — 底层基础设施

**TCPClient / TcpServer**：
- 全程 async/await，`SemaphoreSlim` 防并发死锁
- 客户端支持自动重连（可配置重连间隔）
- 完整的连接状态机（`ClientStatus` / `ServerStatus` 枚举）

**BaseDevice / BaseMechanism / StationBase**：
- 模板方法模式，子类仅需实现 `InternalConnectAsync()`、`InternalDisconnectAsync()`、`InternalResetAsync()`
- 自动重试（3 次，间隔 2 秒）
- **模拟模式**支持（`SimulationMode = true`），便于无硬件开发调试
- `AlarmTriggered` 事件驱动报警机制

---

## 🚀 开发者指南：如何创建一个新模块？

在 `PF.AutoFramework` 中开发新功能，强烈建议以**独立 Module** 的形式进行。以下是标准开发流程：

### Step 1: 创建 Prism 模块类

新建一个类库项目（如 `PF.Modules.Print`），并实现 `IModule` 接口：

```csharp
using Prism.Ioc;
using Prism.Modularity;

public class PrintModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 1. 注册该模块专属的服务
        // containerRegistry.RegisterSingleton<IPrintService, BarTenderPrintService>();

        // 2. 注册用于导航的视图
        containerRegistry.RegisterForNavigation<LabelPrintView>();
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // 模块初始化逻辑，如向主菜单注入菜单项
        var menuService = containerProvider.Resolve<INavigationMenuService>();
        menuService.RegisterAssembly(typeof(PrintModule).Assembly);
    }
}
```

### Step 2: 使用特性进行视图注册（推荐）

框架提供了基于特性的便捷路由注册机制，直接在 View 上打标签即可完成导航配置：

```csharp
using PF.Core.Attributes;
using System.Windows.Controls;

// 该视图将被自动发现，并分配到指定菜单分组中
[ModuleNavigation("LabelPrintView", "标签打印", GroupName = "生产操作", Icon = "Printer", Order = 1)]
public partial class LabelPrintView : UserControl
{
    public LabelPrintView()
    {
        InitializeComponent();
    }
}
```

### Step 3: 在 ViewModel 中调用全局服务

利用依赖注入（DI），轻松获取框架底层提供的服务：

```csharp
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Logging;
using Prism.Mvvm;

public class LabelPrintViewModel : BindableBase
{
    private readonly IParamService _paramService;
    private readonly ILogService _logService;

    public LabelPrintViewModel(IParamService paramService, ILogService logService)
    {
        _paramService = paramService;
        _logService = logService;
    }

    public async Task LoadConfigAsync()
    {
        var printerIp = await _paramService.GetParamAsync<SystemConfigParam>("PrinterIP");
        _logService.Info($"已加载打印机IP: {printerIp?.JsonValue}");
    }
}
```

### Step 4: 在 Shell 中注册模块

在 `PF.Application.Shell/App.xaml.cs` 的 `ConfigureModuleCatalog()` 中添加：

```csharp
protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
{
    moduleCatalog.AddModule<LoggingModule>();
    moduleCatalog.AddModule<ParameterModule>();
    moduleCatalog.AddModule<IdentityModule>();
    moduleCatalog.AddModule<PrintModule>();  // 新增
}
```

---

## 🛠️ 第三方集成建议

本框架的模块化特性非常适合集成工业自动化领域的第三方 SDK，将其封装为独立服务或模块：

| 场景 | 建议方案 |
|------|---------|
| **条码扫描与识别** | 集成 `ZXing.Net`，封装为 `IScannerService`，注入到扫描模块 |
| **工业标签打印** | 接入 BarTender SDK，隔离在独立打印模块，避免污染核心业务 |
| **视觉检测** | 封装 HalconDotNet / OpenCvSharp，作为独立 VisionModule |
| **PLC 通信** | 基于 `TCPClient` / Modbus 协议实现 `IPlcService`，复用通信基类 |
| **报警管理** | 新建 `AlarmModule`，订阅 `BaseDevice.AlarmTriggered` 事件统一管理 |

---

## ⚠️ 已知限制与注意事项

1. **数据库迁移**：当前使用 `EnsureCreatedAsync()` 初始化数据库，不支持 EF Core Migrations。生产环境升级 Schema 时需手动处理。
2. **TCP 重连上限**：启用 `AutoReconnect` 后无最大重试次数限制，网络永久中断时建议在业务层增加熔断逻辑。
3. **日志 UI 线程安全**：`LogService` 从后台线程写入内存缓冲，UI 绑定集合时需通过 `Dispatcher.Invoke` 确保线程安全。
4. **仅支持 Windows**：基于 WPF，目前不支持跨平台部署。

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
