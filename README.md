# PF.AutoFramework

[![.NET](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/)
[![WPF](https://img.shields.io/badge/UI-WPF-lightgrey.svg)]()
[![Prism](https://img.shields.io/badge/Framework-Prism-yellow.svg)](https://prismlibrary.com/)
[![EF Core](https://img.shields.io/badge/ORM-EF%20Core-green.svg)]()
[![License](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**PF.AutoFramework** 是一个基于 **.NET 8** 和 **WPF** 构建的企业级、高可扩展桌面端应用底层框架。它深度集成了 Prism 框架，推行严格的模块化（Modular）和 MVVM 架构。

本框架专为聚力智能工业自动化控制、半导体制造设备（SECS/GEM）量身定制，旨在提供一个开箱即用、稳定可靠且具有现代化视觉体验的上位机软件基础设施。

---

## ✨ 核心特性

* **🧩 极致的模块化架构**：基于 Prism 实现 UI、核心逻辑与数据层的彻底解耦。支持插件式开发，子模块动态加载。
* **🎨 现代扁平化 UI (Modern Flat UI)**：内置海量高颜值自定义控件库（如 `Growl` 消息提示、`Drawer` 抽屉视图、步骤条等），支持极简的现代化工业 UI 设计风格。
* **🔐 全局身份与权限管理**：内置完整的身份认证模块（`IdentityModule`），支持细粒度（页面级/按钮级）的权限管控及多级用户角色（操作员、工程师、管理员等）。
* **💾 动态参数与持久化系统**：基于 EF Core 提供强大的泛型参数服务（`IParamService`），轻松实现系统参数、硬件配置的自动建表与高速读写。
* **🔌 高性能底层通信**：封装了稳定可靠的 TCP 服务端/客户端通信基类，支持工业场景下的高频数据交互。
* **📊 工业级日志追踪**：内置自定义分类日志组件（`CategoryLogger`），支持精准的异常捕获与追溯。

---

## 🏗️ 系统架构图


<img width="1024" height="559" alt="image" src="https://github.com/user-attachments/assets/b975209b-aa36-40f7-8353-2df6f837242d" />

---

## 📂 工程结构说明

解决方案遵循严格的分层架构，核心项目职责如下：

### 1. 核心与基础设施层 (Core & Infrastructure)
* `PF.Core`: 核心契约层。定义了全局接口（`IUserService`, `ILogService`）、实体基类（`IEntity`）、枚举及核心特性（如 `ModuleNavigationAttribute`）。**所有项目均可依赖此层**。
* `PF.Infrastructure`: 底层基础设施层。包含系统级服务封装（如 Windows 服务状态检查、`CategoryLogger` 日志器、TCP 通信基类）。
* `PF.CommonTools`: 通用工具箱。包含 JSON 序列化助手、反射工具、系统 API 互操作等纯函数工具类。

### 2. 数据与服务层 (Data & Services)
* `PF.Data`: 数据访问层。基于 Entity Framework Core，包含 `DbContextFactory` 动态数据库工厂，以及核心参数模型（`SystemConfigParam`, `UserLoginParam`）。
* `PF.Services`: 业务逻辑实现层。提供统一的后台服务，如 `UserService`（用户登录与状态管理）和泛型 `ParamService`（参数持久化）。

### 3. 用户界面与表示层 (UI & Presentation)
* `PF.UI`: 核心样式与控件资源库。封装了附加属性（Attached Properties）、交互行为（Behaviors，如 `EventToCommand`）、样式字典及复杂自定义控件。
* `PF.Application.Shell`: 应用程序入口（Shell）。负责应用启动、依赖注入容器（IoC）初始化、主题加载及模块发现。

### 4. 业务模块 (Modules)
* `PF.Modules.Identity`: 身份权限模块。处理登录弹窗、用户管理视图及权限分配。
* `PF.Modules.SecsGem`: 工业设备通信模块。处理半导体行业的 SECS/GEM 协议交互。
* *(未来可在此扩展如：条码扫描/打印模块、视觉检测模块、报警管理模块等)*

---

## 🚀 开发者指南：如何创建一个新模块？

在 `PF.AutoFramework` 中开发新功能，强烈建议以**独立 Module** 的形式进行。以下是标准开发流程：

### Step 1: 创建 Prism 模块类
新建一个类库项目（如 `PF.Modules.Print`），并实现 `IModule` 接口：

```csharp
using Prism.Ioc;
using Prism.Modularity;
using PF.Core.Constants;

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
    }
}
```

### Step 2: 使用特性进行视图注册 (推荐)
框架提供了一套基于特性的便捷路由注册机制，直接在 View 上打标签即可完成导航配置：

```csharp
using PF.Core.Attributes;
using System.Windows.Controls;

// 该视图将被自动注册，并可分配到指定的菜单分组中
[ModuleNavigation("LabelPrintView", "标签打印", GroupName = "生产操作", Icon = "Printer")]
public partial class LabelPrintView : UserControl
{
    public LabelPrintView()
    {
        InitializeComponent();
    }
}
```

### Step 3: 在 ViewModel 中调用全局服务
利用依赖注入（DI），你可以轻松获取并使用框架底层提供的服务，例如参数读写和日志记录：

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
        // 高效读取数据库中存储的系统配置
        var printerIp = await _paramService.GetParamAsync<SystemConfigParam>("PrinterIP");
        _logService.Info($"已加载打印机IP: {printerIp?.JsonValue}");
    }
}
```

---

## 🛠️ 第三方集成建议

本框架的模块化特性非常适合集成工业自动化领域的第三方 SDK。你可以将它们封装为独立的服务或模块：
* **条码扫描与识别**：可集成 `ZXing.Net`，将其封装为 `IScannerService`。
* **工业标签打印**：可接入 BarTender SDK 等，隔离在独立的打印模块中，避免污染核心业务逻辑。

---

## 📄 开源协议

本项目采用 [MIT License](LICENSE) 开源协议。
