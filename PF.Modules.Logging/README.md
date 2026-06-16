# PF.Modules.Logging

PF.AutoFramework 日志查看 Prism 模块，提供实时日志滚动显示、分类过滤及历史文件浏览。以插件 DLL 方式加载。

## 功能

- **实时日志**：绑定 `LogService` 内存缓冲（最近 1000 条），日志级别颜色区分
- **分类过滤**：按 `Info / Warning / Error / Debug` 等级过滤
- **关键词搜索**：实时过滤包含关键词的日志条目
- **历史文件浏览**：按小时滚动的日志文件列表，可打开查看

## 日志文件位置

```
%APPDATA%\PFAutoFrameWork\Log\
    ├── 2025\
    │   ├── 06\
    │   │   ├── 16\
    │   │   │   ├── 08.log   ← 08:00–08:59 的日志
    │   │   │   ├── 09.log
    │   │   │   └── ...
```

日志文件保留 **30 天**，超期自动清理。

## 接入步骤

### 1. 注册日志服务（App.xaml.cs）

```csharp
// 推荐使用扩展方法
containerRegistry.AddLogging();
// 等价于：
containerRegistry.RegisterSingleton<ILogService, LogService>();
```

### 2. 在业务代码中注入使用

```csharp
public class FeedStation : StationBase<FeedMechanism>
{
    private readonly ILogService _log;

    public FeedStation(ILogService log, ...) { _log = log; }

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        _log.Info("送料工站开始初始化");
        await Mechanism.InitializeAsync(token);
        _log.Info("送料工站初始化完成");
    }
}
```

### 3. 结构化日志写入

```csharp
_log.Info("取料完成，位置：{Position}", position);
_log.Warning("传感器未检测到物料，重试第 {Retry} 次", retryCount);
_log.Error("轴运动超时", exception);
_log.Debug("IO 状态：{State}", ioState);
```

## 实时日志 UI 绑定

模块内部已自动订阅 `LogService.LogAdded` 事件，无需额外配置。新日志条目实时追加到 UI 列表并自动滚动到底部。
