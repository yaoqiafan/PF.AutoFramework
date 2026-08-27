# PF.Modules.Alarm

PF.AutoFramework 报警管理 Prism 模块（当前 v1.0.4），以插件 DLL 方式加载，无需在 Shell 中显式引用类型。提供统一的报警中心视图：活跃报警看板、历史查询、SOP 排故面板三者合一。

## 功能视图

只有一个导航入口 `AlarmCenterView`（`AlarmCenterViewModel`），内部整合：

- **活跃报警列表**：绑定 `IAlarmService.ActiveAlarms`，实时订阅 `AlarmTriggeredEvent` / `AlarmClearedEvent` 增量更新
- **历史查询**：按时间段 / 等级 / 分类 / 来源 / 错误码 / 描述关键词过滤，分页展示
- **SOP 排故面板**：选中一条报警后，右侧联动显示该报警字典里预置的排故指导文本（`SelectedSolution`），未选中时显示提示占位文案
- **清除操作**：清除单条选中报警、清除全部活跃报警
- **系统复位**：`SystemResetCommand` 发布 `SystemResetRequestedEvent`，由 Shell 桥接到 `IMasterController.RequestSystemResetAsync()`——是全线硬件复位+状态机跳转，不只是清掉报警记录

另有一个报警弹窗对话框 `PFAlarmBaseWindow`（承载 `AlarmDetailCardView`）：报警触发时弹出，展示详情大图（`DecodePixelHeight=900` 限制解码尺寸，避免大图撑爆内存）。

## 接入步骤

### 1. 定义报警码（`AlarmInfoAttribute` 标注在常量字段上，不是类上）

所有报警码必须集中定义在 `PF.Core.Constants.AlarmCodes`（或项目自己的对应静态类）里，作为 `const string` 字段，字段上标 `[AlarmInfo]`：

```csharp
public static class AlarmCodes
{
    public static class Hardware
    {
        /// <summary>送料超时</summary>
        [AlarmInfo(
            category:    "工艺异常",
            message:     "送料超时",
            messageEn:   "Feed timeout",
            severity:    AlarmSeverity.Error,
            solution:    "1. 检查料仓是否有料;\n2. 检查取料轴是否卡滞;\n3. 复位后重试;",
            messageID:   50100,
            messageIDHex:"Feed timeout")]
        public const string FeedTimeout = "HW_FEED_001";
    }
}
```

业务代码严禁硬编码错误码字符串，一律引用 `AlarmCodes.*` 常量。`AlarmDictionaryService` 启动时反射扫描全部程序集，把这些常量字段收集进报警字典。

### 2. 注册报警服务（App.xaml.cs，`PFApplicationBase` 默认已注册，通常无需手写）

```csharp
containerRegistry.RegisterSingleton<IAlarmService, AlarmService>();
containerRegistry.RegisterSingleton<IAlarmDictionaryService, AlarmDictionaryService>();
containerRegistry.RegisterSingleton<IAlarmEventPublisher, AlarmEventPublisher>();
```

### 3. 触发 / 清除报警

```csharp
// 工站或机构内注入 IAlarmService
// 以 (source, errorCode) 复合键幂等：同一复合键重复触发会被跳过，不重复落盘
bool firstTime = _alarmService.TriggerAlarm("FeedStation", AlarmCodes.Hardware.FeedTimeout);

// 携带运行时消息（覆盖字典里的静态描述，不持久化到历史）
_alarmService.TriggerAlarm("FeedStation", AlarmCodes.Hardware.FeedTimeout, $"X轴位置偏差 {delta}mm");

// 清除同一来源下的单条报警
_alarmService.ClearAlarm("FeedStation", AlarmCodes.Hardware.FeedTimeout);

// 清除该来源下的所有活跃报警（如复位成功后）
_alarmService.ClearAlarm("FeedStation");

// 一键清除全部活跃报警（关联【复位】按钮）
_alarmService.ClearAllActiveAlarms();
```

`errorCode` 不在字典中时会自动生成一条通用兜底记录，故障不会被静默吞掉。

### 4. 历史查询（自动按年度分表路由）

```csharp
var records = await _alarmService.QueryHistoricalAlarmsAsync(
    year: 0,                       // 0 = 当前年
    startTime: DateTime.Today,
    severity: AlarmSeverity.Error,
    source: "FeedStation",
    pageSize: 500,
    page: 0);
```

## SOP 内容存放位置

排故指导文本**不是**外部 Markdown 文件，而是直接写在 `AlarmInfoAttribute` 的 `solution` 参数里，随字典一起反射加载，`AlarmCenterView` 选中报警后直接绑定展示。
