# PF.Application.Base

PF.AutoFramework 应用程序基础层，提供面向工业自动化控制软件的 WPF 入口样板。

## 核心组件

- **`PFApplicationBase`** — 继承 `PrismApplication`，封装单实例互斥锁、Splash 加载流程、DI 注册、Prism EA 桥接等全部样板，子类通过 6 个抽象钩子 + 5 个 virtual 钩子实现项目定制。
- **`MainWindowViewModelBase`** — 主窗口 ViewModel 基类，内置侧边栏导航、报警联动、权限拦截、空闲降权、MachineState 订阅；子类重写 `PollDeviceStatuses()` 填充设备状态栏。
- **`MainWindow`** — 数据驱动状态栏主窗口，通过 `DeviceStatusItems` 集合动态渲染硬件连接状态。

## 通讯管理集成（v1.0.1 起）

- 通讯实例配置持久化到数据库（新增 `CommunicationParams` 表），支持默认配置导入与工厂注册，用法与硬件配置一致（见 `PF.Services` README 的 `CommunicationManagerService`）。
- 启动顺序：通讯管理器（`ICommunicationManagerService.LoadAndInitializeAsync()`）**先于**硬件管理器初始化，硬件工厂可按需引用已实例化的通讯连接（如相机通过已启动的 TCP 客户端收发数据）。
- 关闭流程（`Window_Closing`）健壮性修复：`e.Cancel` 前置 + 重入门防止重复触发，释放顺序固定为 Alarm → Production → Log（`LogService` 最后释放，确保关闭前的超时告警仍能落盘）。

## 使用方式

```csharp
// App.xaml.cs
public partial class App : PFApplicationBase
{
    protected override string AppMutexId => "YourApp-UniqueID";
    protected override IDefaultParam CreateDefaultParameters() => new DefaultParameters();
    protected override void RegisterHardwareFactories(IHardwareManagerService hwManager) { ... }
    protected override void RegisterMechanismsAndStations(IContainerRegistry containerRegistry) { ... }
    protected override void RegisterRecipes(IContainerRegistry containerRegistry) { ... }
    protected override Task<bool> InitializeMechanismsAsync() { ... }

    protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
    {
        moduleCatalog.AddModule<IdentityModule>();
        // ... 其他模块
        moduleCatalog.AddModule<YourUIModule>();
    }
}
```
