# PF.Modules.Alarm

PF.AutoFramework 报警管理 Prism 模块，提供报警触发面板、活跃报警看板、历史查询及 SOP 排故引导界面。以插件 DLL 方式加载，无需在 Shell 中显式引用类型。

## 功能视图

| 视图 | 说明 |
|---|---|
| 活跃报警看板 | 实时展示当前未消除报警，颜色区分等级 |
| 历史查询 | 按时间段 / 报警源 / 错误码过滤，支持导出 |
| SOP 排故面板 | 点击报警条目展示对应处置步骤 |
| 报警触发测试 | 工程师级别手动触发 / 消除报警（调试用） |

## 接入步骤

### 1. 定义报警码（使用 `[AlarmInfo]` 特性）

```csharp
// 在机构或工站类上标注，AlarmDictionaryService 启动时反射扫描
[AlarmInfo(Source = "FeedStation", ErrorCode = 1001,
           Level = AlarmLevel.Error, Description = "取料超时")]
[AlarmInfo(Source = "FeedStation", ErrorCode = 1002,
           Level = AlarmLevel.Warning, Description = "传感器信号异常")]
public class FeedStation : StationBase<FeedMechanism> { ... }
```

### 2. 注册报警服务（App.xaml.cs）

```csharp
containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
containerRegistry.RegisterSingleton<IAlarmDictionaryService, AlarmDictionaryService>();
containerRegistry.RegisterSingleton<IAlarmEventPublisher, AlarmEventPublisher>();
```

### 3. 加载模块（DirectoryModuleCatalog 自动扫描，无需显式注册）

确保构建后 `PF.Modules.Alarm.dll` 存在于 Shell 的 `Modules\` 子目录中。

### 4. 注册视图路由（模块 `Module.cs` 内已完成，参考即可）

```csharp
// ViewName 必须与 [ModuleNavigation] 特性中的 ViewName 一致
_regionManager.RegisterForNavigation<AlarmActiveView,  AlarmActiveViewModel>("AlarmActiveView");
_regionManager.RegisterForNavigation<AlarmHistoryView, AlarmHistoryViewModel>("AlarmHistoryView");
```

## 触发 / 消除报警

```csharp
// 工站或机构内注入 IAlarmService
await _alarmService.TriggerAsync("FeedStation", 1001);

// 复位后消除
await _alarmService.ClearAsync("FeedStation", 1001);
```

## SOP 配置

在 `%APPDATA%\PFAutoFrameWork\SOP\` 目录下放置 `{ErrorCode}.md` 文件，即可在 SOP 面板中自动显示对应处置步骤。
