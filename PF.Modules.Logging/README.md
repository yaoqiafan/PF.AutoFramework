# PF.Modules.Logging

PF.AutoFramework 日志查看 Prism 模块（当前 v1.0.2），以插件 DLL 方式加载。提供两个视图：一个嵌在 Shell 常驻区域的实时滚动条，一个可导航的历史查询页。

## 两个视图

| 视图 | 挂载方式 | 说明 |
|---|---|---|
| `LogListView`（`LogListViewModel`） | `RegionManager.RegisterViewWithRegion(LoggingListRegion, ...)`，模块 `OnInitialized` 时直接挂进区域，非导航页 | 实时滚动列表，订阅 `ILogService.OnLogAdded` 事件，新日志追加即刷新 |
| `LogManagementView`（`LogManagementViewModel`） | 侧边栏导航页（`NavigationConstants.Views.LogManagementView`） | 按日期范围 / 级别 / 分类 / 关键词过滤查询历史日志，分页展示，支持导出 |

历史查询走 `ILogService.QueryHistoricalLogs(LogQueryParams)`，底层直接扫描按小时滚动的日志文件，不经数据库。

## 日志分类（`LogCategories`）

内置 `System` / `Database` / `UI` / `Communication` / `Hardware` / `Recipe` / `SecsGem` / `Vision` / `Custom` 九个分类，也可运行时用 `ILogService.AddCategory(category, minLevel, fileNamePrefix)` 追加自定义分类。

## 日志文件位置

```
D:\PF_Logs\
    └── 2026\
        └── 08\
            └── 27\
                ├── 08.log   ← 08:00–08:59 的日志
                ├── 09.log
                └── ...
```

按小时滚动，保留 **30 天**，超期自动清理。日志路径与 `D:\PFConfig\PFAutoFrameWork\` 下的配置路径隔离机制无关，所有项目共用同一根目录。

## 接入步骤

### 1. 注册日志服务（App.xaml.cs）

```csharp
// 推荐使用扩展方法（PF.Services 提供，PFApplicationBase 默认已调用）
containerRegistry.AddLogging();
```

### 2. 在业务代码中注入使用

```csharp
public class FeedStation : StationBase<FeedMechanism>
{
    private readonly ILogService _log;

    public FeedStation(ILogService log, ...) { _log = log; }

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        _log.Info("送料工站开始初始化", category: LogCategories.System);
        await Mechanism.InitializeAsync(token);
        _log.Info("送料工站初始化完成", category: LogCategories.System);
    }
}
```

注意：各级别方法只接受**普通字符串**，不支持 `{Placeholder}` 结构化模板，拼接消息需自行 `$"..."` 或 `string.Format`。

```csharp
_log.Info($"取料完成，位置：{position}");
_log.Warn($"传感器未检测到物料，重试第 {retryCount} 次");
_log.Error("轴运动超时", exception: ex);
_log.Debug($"IO 状态：{ioState}");
```

### 3. 分类日志器（推荐，避免每次手写 category 字符串）

`PF.Infrastructure.Logging.CategoryLoggerFactory` 按内置分类预置了一批工厂方法（`System` / `Database` / `UI` / ...），返回的 `CategoryLogger` 自动带上对应分类：

```csharp
private readonly CategoryLogger _log = CategoryLoggerFactory.System(logService);
_log.Info("送料工站开始初始化");
```

## 实时日志 UI 绑定

`LogListView` 已在模块初始化时自动订阅 `ILogService.OnLogAdded` 事件并挂入区域，消费项目无需额外配置；新日志条目实时追加到列表并自动滚动到底部。
