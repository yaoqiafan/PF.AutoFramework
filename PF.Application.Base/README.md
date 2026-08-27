# PF.Application.Base

PF.AutoFramework 应用程序基础层，提供面向工业自动化控制软件的 WPF 入口样板。消费项目的 `App.xaml.cs` 只需继承 `PFApplicationBase` 并实现/覆写若干钩子，即可获得单实例保护、Splash 加载流程、全部框架服务的 DI 注册、Prism EventAggregator 桥接、主窗口与主显界面、配置目录隔离、SECS/GEM 服务项目绑定校验等全部样板。

## 核心组件

- **`PFApplicationBase`**（继承 `PrismApplication`）— 应用入口抽象基类，见下方钩子说明。
- **`MainWindow`** — 数据驱动状态栏的主 Shell 窗口（`CreateShell()` 返回值），侧边栏导航 + 底部/侧边设备状态栏，状态栏通过 `DeviceStatusItems` 集合动态渲染。
- **`MainView` / `MainViewModel`** — 主显界面（首页），随 `PFApplicationBase.RegisterTypes` 统一注册，作为 `MainWindow` 内的 Region 内容而非独立窗口。左侧产品图片 + 简介卡片，右侧品牌信息 + 常用操作区（含"打开说明书"）。消费项目通常不必再自建 MainView，只需在 `MainWindowViewModelBase` 子类中重写 `ProductImagePath` / `ProductDescription` / `UserManualPath` 三个虚属性即可接入。
- **`MainWindowViewModelBase`** — 主窗口 ViewModel 基类，内置侧边栏导航、报警数量/最高级别联动（`ActiveAlarmCount` / `HighestAlarmSeverity`）、权限拦截、空闲降权、`MachineState` 订阅与状态文案/颜色、标题栏版本徽章（`SoftWareVersion` / `RuntimeVersion`）；子类重写 `PollDeviceStatuses()` 填充 `DeviceStatusItems`。
- **`DeviceStatusItem`** — 状态栏单个设备条目，默认 `IsConnected` 二值灯；设置 `StatusText`（配合 `StatusBrush`）后自动切换为「自定义文本 + 颜色」的多态显示，用于承载三态及以上状态（如 SECS 控制状态：离线/本地/远程）。枚举 → 文本/颜色的映射由消费方自备转换器负责，框架只承载结果、不感知具体业务枚举。
- **公共服务** — `IdleMonitorService`（空闲降权）、`PrismAlarmEventPublisher`（`IAlarmEventPublisher` 的 Prism EA 实现）、`TowerLightManager`（报警/状态联动三色灯）。

## 项目配置隔离与 SECS/GEM 开关

- **`ProjectName`**（`protected virtual string`，默认取入口程序集名）— 决定配置目录 `D:\PFConfig\PFAutoFrameWork\{ProjectName}\`。基类**实例构造函数**中调用 `ConstGlobalParam.Initialize(ProjectName)`，早于任何触碰 `ConfigPath` 的代码。重写此属性的项目必须同步设置 `Installer\installer.conf` 的 `PROJECT_NAME`，否则 PF.SecsGem.Service 会指向另一个项目目录的 `SecsGemConfig.db`。
- **`UsesSecsGemService`**（`protected virtual bool`，默认 `true`）— 不需要 SECS/GEM 的项目重写为 `false` 即可整体关闭：跳过 `RegisterSecsGemServices` 整段 DI 注册（不再创建空的 `SecsGemConfig.db`）、跳过 `VerifySecsServiceProjectBinding` 校验、并在 `ModuleCatalog` 层拦下所有命名空间以 `PF.Modules.SecsGem.` 开头的模块（即便子类 `ConfigureModuleCatalog` 里仍写了 `AddModule<SecsGemModule>()`）。**该开关只管框架自己的注册**，项目在 `RegisterMechanismsAndStations` / `RegisterTypes` 里注册的、依赖 `ISecsGemManager` 的机构或 ViewModel 需要项目自己同步加门（Demo Shell 的 `App.xaml.cs` 有完整示例）。
- **`VerifySecsServiceProjectBinding`**（`protected virtual`）— Splash 阶段校验 SCM 中实际注册的 `SecsGemService` 是否属于本项目，不一致时写日志并弹窗（只告警不阻断）；`UsesSecsGemService=false` 或服务未部署时自动跳过。

## 通讯管理集成

- 通讯实例配置持久化到数据库（`CommunicationParams` 表），支持默认配置导入与工厂注册；启动顺序为通讯管理器（`ICommunicationManagerService.LoadAndInitializeAsync()`）**先于**硬件管理器初始化，硬件工厂闭包可通过注入的 `commManager` 引用已实例化、已连接的通讯实例（如相机走已启动的 TCP 客户端收发数据）。
- 关闭流程（`Window_Closing`）：`e.Cancel` 前置 + 重入门防止重复触发，释放顺序固定为 Alarm → Production → Log（`LogService` 最后释放，确保关闭前的超时告警仍能落盘）。

## 使用方式

```csharp
// App.xaml.cs
public partial class App : PFApplicationBase
{
    protected override string AppMutexId => "YourApp-UniqueID";

    // 不需要 SECS/GEM 的项目按需覆写，默认 true：
    // protected override bool UsesSecsGemService => false;

    protected override IDefaultParam CreateDefaultParameters() => new DefaultParameters();

    protected override void RegisterCommunicationFactories(ICommunicationManagerService commManager) { ... }
    protected override void RegisterHardwareFactories(IHardwareManagerService hwManager, ICommunicationManagerService commManager) { ... }
    protected override void RegisterMechanismsAndStations(IContainerRegistry containerRegistry) { ... }
    protected override void RegisterRecipes(IContainerRegistry containerRegistry) { ... }
    protected override Task<bool> InitializeMechanismsAsync(IProgress<SplashProgressPayload>? progress = null) { ... }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<IdentityModule>();
        // ... 其他模块
        moduleCatalog.AddModule<YourUIModule>();
    }
}
```

## 钩子速查

**必须实现（抽象）**

| 钩子 | 说明 |
|---|---|
| `AppMutexId` | 项目唯一互斥体 ID |
| `CreateDefaultParameters()` | 返回项目默认参数集（硬件配置、系统参数、用户默认） |
| `RegisterCommunicationFactories(commManager)` | 注册项目特有的通讯实例工厂 |
| `RegisterHardwareFactories(hwManager, commManager)` | 注册项目特有的硬件工厂；`commManager` 供硬件工厂闭包按 `InstanceId` 查找已实例化的通讯连接 |
| `RegisterMechanismsAndStations(containerRegistry)` | 注册项目特有的机构、工站、主控（含 `IPanelIoConfig`、`ITowerLightDoWriterConfig`） |
| `RegisterRecipes(containerRegistry)` | 注册项目特有的配方服务 |
| `InitializeMechanismsAsync(progress)` | 按顺序初始化项目机构，返回 `true` 表示全部成功 |

**可选重写（virtual）**

| 钩子 | 默认行为 |
|---|---|
| `ProjectName` | 入口程序集名，决定配置隔离子目录 |
| `UsesSecsGemService` | `true`；见上文说明 |
| `LoadConfigurationAsync()` | `Task.Delay(1000)` 后返回 `true` |
| `RegisterIOMappings(ioMappingService)` | 空实现 |
| `RegisterVisionServices(containerRegistry)` | 空实现，用到 Halcon 视觉的项目在此注册 `IVisionService` 等 |
| `RegisterSecsGemServices(containerRegistry)` | 完整的 SECS/GEM DI 注册；`UsesSecsGemService=false` 时不会被调用 |
| `RegisterMainWindowViewModel(containerRegistry)` | 注册 `MainWindowViewModelBase` 本身；有自定义子类的项目覆写 |
| `RegisterProjectDailyTasks()` | 空实现，项目特有每日定时任务 |
| `VerifySecsServiceProjectBinding(updateStage)` | 见上文说明 |

当前版本、逐条变更历史见 `PF.Application.Base.csproj` 的 `PackageReleaseNotes`。
