# PF.UI.Infrastructure

PF.AutoFramework UI 基础设施层，提供 WPF + Prism 开发所需的 ViewModel 基类、导航服务、对话框服务及硬件状态图标资源。

## ViewModelBase

所有 ViewModel 的根基类，继承 `BindableBase`（Prism），内置日志、参数、报警服务注入。

```csharp
public class FeedStationViewModel : ViewModelBase
{
    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);   // INotifyPropertyChanged 自动通知
    }

    public FeedStationViewModel(ILogService log, IParamService param)
        : base(log, param) { }

    // 支持 DelegateCommand
    public DelegateCommand StartCommand => new(OnStart);
    private void OnStart() { Log.Info("工站启动"); }
}
```

## 导航服务（INavigationMenuService）

结合 `[ModuleNavigation]` 特性自动构建侧边栏菜单树。

```csharp
// 标注导航项（在对应 ViewModel 上）
[ModuleNavigation(
    ViewName  = "AlarmView",
    Title     = "报警管理",
    GroupName = "监控",
    Icon      = "Bell",
    Order     = 10)]
public class AlarmViewModel : ViewModelBase { ... }

// Shell 中绑定菜单列表
public IEnumerable<NavigationMenuItem> MenuItems
    => _navigationMenuService.GetMenuItems();
```

## 对话框服务（IDialogService）

```csharp
// 弹出确认对话框
var result = await _dialogService.ShowConfirmAsync("确认复位所有工站？");
if (result == DialogResult.OK)
    await _masterController.RequestSystemResetAsync();

// 弹出输入对话框
var input = await _dialogService.ShowInputAsync("请输入批次号：");

// 弹出消息提示
await _dialogService.ShowMessageAsync("初始化完成", MessageType.Success);
```

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
<!-- XAML 引用包内图片 -->
<Image Source="pack://application:,,,/PF.UI.Infrastructure;component/HardwareImage/IO模块.png" />
```

## PrismBase 辅助类

```csharp
// 导航感知 ViewModel（实现 INavigationAware）
public class ParamViewModel : NavigationViewModelBase
{
    public override void OnNavigatedTo(NavigationContext ctx)
    {
        // 每次导航到此视图时调用
        LoadParams();
    }
}
```
