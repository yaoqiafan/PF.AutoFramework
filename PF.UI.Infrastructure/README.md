# PF.UI.Infrastructure

PF.AutoFramework UI 基础设施层，当前版本 1.0.3。提供 WPF + Prism 开发所需的 ViewModel 基类、导航菜单服务、弹窗/消息服务、Prism 事件桥接类型及硬件状态图标资源。

## ViewModelBase

所有 ViewModel 的根基类，继承 Prism `BindableBase` 并实现 `IDestructible`。**构造函数无参**——不是通过参数注入依赖，而是在基类构造函数体内直接用 Prism 的 `ContainerLocator.Container.Resolve<T>()` 拉取（Service Locator 模式），子类无需、也不应该在自己的构造函数里声明这些依赖：

```csharp
public abstract class ViewModelBase : BindableBase, IDestructible
{
    protected ViewModelBase() { /* 内部 Resolve，见下 */ }

    public  IServiceProvider ServiceProvider { get; }
    protected IEventAggregator EventAggregator { get; }
    protected IDialogService   DialogService   { get; }   // Prism 原生弹窗服务
    protected IMessageService  MessageService  { get; }   // 本包自定义消息/输入/等待弹窗服务，见下节
    protected ILogService      LogService      { get; }
    protected IUserService     UserService     { get; }
}
```

```csharp
public class FeedStationViewModel : ViewModelBase
{
    private string _status = "";
    public string Status { get => _status; set => SetProperty(ref _status, value); }

    public DelegateCommand StartCommand => new(OnStart);
    private void OnStart() => LogService.Info("工站启动");   // 直接用基类暴露的 protected 属性，无需自己注入
}
```

## 导航服务（INavigationMenuService）

结合 `[ModuleNavigation]` 特性自动构建侧边栏菜单树：

```csharp
[ModuleNavigation(viewName: "AlarmView", title: "报警管理", groupName: "监控")]
[ModuleNavigation("AlarmView", "报警管理", "监控", Icon = "Bell", Order = 10, GroupOrder = 1)]
public class AlarmViewModel : ViewModelBase { ... }

public interface INavigationMenuService
{
    ObservableCollection<NavigationItem> MenuItems { get; }   // 属性，非方法——直接绑定
    void RegisterAssembly(Assembly assembly);                  // 扫描程序集里所有 [ModuleNavigation] 标注类型
}

// Shell 中直接绑定
public ObservableCollection<NavigationItem> MenuItems => _navigationMenuService.MenuItems;
```

`ModuleNavigationAttribute` 允许在同一视图模型上重复标注（`AllowMultiple = true`）以出现在多个分组里；`ViewName`/`Title` 是位置参数，`GroupName` 有默认值 `"默认分组"`，`Icon`/`Order`/`GroupOrder`/`GroupIcon`/`NavigationParameter` 均可选。

## 消息/弹窗服务（IMessageService）

不是叫 `IDialogService`——那是 Prism 自带的区域导航式弹窗，本包额外提供的是更贴近业务场景的 `IMessageService`，覆盖五种交互形态：

```csharp
public interface IMessageService
{
    MessageBoxResult ShowSystemMessage(string message, string title = "提示",
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information);   // 系统原生弹窗，直接阻塞 UI 线程

    Task<ButtonResult> ShowMessageAsync(string message, string title = "提示",
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information);    // 自定义弹窗，异步等待，推荐

    void ShowMessage(string message, string title = "提示",
        MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information,
        Action<ButtonResult>? callback = null);                                                                   // 非阻塞，回调拿结果

    Task<string?> ShowInputAsync(string message, string title = "输入", string defaultText = "");                // 取消返回 null

    Task ExecuteWithWaitAsync(Func<Task> action, string message = "请稍候，正在处理中...", string title = "请稍候");  // 包一层转圈等待框
}
```

```csharp
var result = await MessageService.ShowMessageAsync("确认复位所有工站？", buttons: MessageBoxButton.OKCancel);
if (result == ButtonResult.OK)
    await _masterController.RequestSystemResetAsync();

string? batchNo = await MessageService.ShowInputAsync("请输入批次号：");

await MessageService.ExecuteWithWaitAsync(() => hwManager.LoadAndInitializeAsync(), "正在加载硬件配置...");
```

v1.0.3 起 `ShowMessageAsync`/`ShowInputAsync`/`ExecuteWithWaitAsync`/`ShowMessage` 内部按调用线程分流：UI 线程调用保持原有内联模态行为（对 `GetAwaiter().GetResult()` 不死锁）；后台线程（如工站循环线程）调用改走 `Dispatcher.InvokeAsync` 异步排队，不再把后台线程同步卡死到操作员关闭弹窗为止。

## 硬件状态图标资源

包内嵌入 7 种硬件类型图标（PNG），可在 XAML 中直接引用：

| 资源 Key | 说明 |
|---|---|
| `HardwareImage/运动控制卡.png` | 运动控制卡 |
| `HardwareImage/驱动器.png` | 伺服驱动器 |
| `HardwareImage/IO模块.png` | IO 模块 |
| `HardwareImage/相机.png` | 相机 |
| `HardwareImage/海康读码器.png` | 海康条码扫描仪 |
| `HardwareImage/控制器.png` | 控制器 |
| `HardwareImage/安全门.png` | 安全门传感器 |

```xml
<Image Source="pack://application:,,,/PF.UI.Infrastructure;component/HardwareImage/IO模块.png" />
```

## PrismBase：Region 生命周期与事件桥接类型

```csharp
public class ParamViewModel : RegionViewModelBase
{
    public override void OnNavigatedTo(NavigationContext ctx) => LoadParams();
}
```

### RegionViewModelBase — Region 生命周期契约（v1.0.2 起，行为变更）

`RegionViewModelBase : ViewModelBase, INavigationAware, IConfirmNavigationRequest, IRegionMemberLifetime`，**默认 `KeepAlive => false`**：导航离开后 Prism 自动把该视图实例从所属 Region 中移除，使其可被 GC 回收。

- 默认行为（大多数页面）：无需任何额外代码，导航离开即释放，不再永久滞留在 `Region.Views` 中导致内存持续累积。
- 若某个 ViewModel 重写了 `IsNavigationTarget(NavigationContext) => true`（希望"同一实例反复进出时状态不丢"），**必须同时重写 `KeepAlive => true`**：

  ```csharp
  public class FeedConnectionDebugViewModel : RegionViewModelBase
  {
      public override bool IsNavigationTarget(NavigationContext ctx) => true;   // 同一实例复用，保留 LogEntries 等状态
      public override bool KeepAlive => true;   // 必须同时重写，否则 Region 会在导航离开时把实例移除，复用逻辑形同虚设
  }
  ```

### Prism 事件桥接类型（PrismBase 目录）

供 `PF.Infrastructure` 与 Prism 解耦——`PF.Infrastructure` 零 Prism 依赖，实际的事件桥接在 `App.xaml.cs` 中用这些 `PubSubEvent` 类型完成：

`MachineStateChangedEvent` / `AlarmTriggeredEvent` / `AlarmClearedEvent` / `AlarmAcknowledgeEvent` / `HardwareResetRequestedEvent` / `SystemResetRequestedEvent` / `ReinitializeRequiredEvent` / `UserChangedEvent`。

`PFDialogViewModelBase` 是弹窗类 ViewModel（配合 `PFDialogBaseWindow`）的基类；`ViewDataMapperBase`（`Mappers` 目录）是硬件/通讯参数界面常用的"实体 ↔ 界面模型"映射基类；`PermissionHelper`（`Navigation` 目录）供菜单/页面按钮做 `UserLevel` 权限过滤。
