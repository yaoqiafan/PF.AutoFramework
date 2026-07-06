# PF.Application.Base

PF.AutoFramework 应用程序基础层，提供面向工业自动化控制软件的 WPF 入口样板。

## 核心组件

- **`PFApplicationBase`** — 继承 `PrismApplication`，封装单实例互斥锁、Splash 加载流程、DI 注册、Prism EA 桥接等全部样板，子类通过 6 个抽象钩子 + 5 个 virtual 钩子实现项目定制。
- **`MainWindowViewModelBase`** — 主窗口 ViewModel 基类，内置侧边栏导航、报警联动、权限拦截、空闲降权、MachineState 订阅；子类重写 `PollDeviceStatuses()` 填充设备状态栏。
- **`MainWindow`** — 数据驱动状态栏主窗口，通过 `DeviceStatusItems` 集合动态渲染硬件连接状态。

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
