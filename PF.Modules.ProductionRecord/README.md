# PF.Modules.ProductionRecord

PF.AutoFramework 生产数据 Prism 模块（当前 v1.0.2，命名空间为 `PF.Modules.Production`），与 SECS/GEM 完全解耦，适用于任何设备的生产数据记录场景。以插件 DLL 方式加载。

## 两个视图

| 视图 | ViewModel | 说明 |
|---|---|---|
| `ProductionMonitorView` | `ProductionMonitorViewModel` | 实时监控：订阅 `IProductionDataService.DataRecorded` 事件，每条新数据写入后自动追加到 `RecentRecords`，无需轮询；支持按记录类型过滤、清空列表、导出 |
| `ProductionHistoryView` | `ProductionHistoryViewModel` | 历史查询：按时间段 / 记录类型 / 关键词 / 最大条数过滤，选中一条可查看反序列化后的 JSON 详情，支持导出 Excel/CSV |

## 接入步骤

### 1. 注册生产数据服务（`PFApplicationBase` 默认已注册，通常无需手写）

```csharp
containerRegistry.RegisterSingleton<IProductionDataService, ProductionDataService>();
```

### 2. 定义生产数据 POCO（任意类型均可，Schema 无关）

```csharp
public class OcrProductionData
{
    public string  BatchNo     { get; set; } = "";  // 批次号
    public string  ProductId   { get; set; } = "";  // 产品 ID
    public string  Barcode     { get; set; } = "";  // 条码
    public string  OcrResult   { get; set; } = "";  // OCR 识别结果
    public bool    IsPass      { get; set; }         // 是否合格
}
```

### 3. 在工站中记录生产数据

```csharp
// 泛型 RecordAsync：POCO → JSON 非阻塞写入（内部队列），不阻塞工站循环
await _productionDataService.RecordAsync(new OcrProductionData
{
    BatchNo   = _currentBatch,
    ProductId = "WAFER-001",
    Barcode   = scanResult,
    OcrResult = ocrResult,
    IsPass    = ocrResult == expectedValue,
}, recordType: "OcrResult");   // recordType 可选，用于后续分类查询
```

### 4. 查询

```csharp
var filter = new ProductionQueryFilter
{
    StartTime  = DateTime.Today,
    RecordType = "OcrResult",
    MaxCount   = 5000,
};

// 返回原始 DTO（含 JsonValue 字符串），适合直接绑定表格
IReadOnlyList<ProductionRecord> raw = await _productionDataService.QueryAsync(filter);

// 或直接反序列化为已知强类型
IReadOnlyList<OcrProductionData> typed = await _productionDataService.QueryDataAsync<OcrProductionData>(filter);

// 单条 DTO 也可以自己按需反序列化
var one = raw[0].Deserialize<OcrProductionData>();
```

### 5. 订阅实时数据事件

```csharp
// 事件在非 UI 线程触发，UI 订阅需切线程
_productionDataService.DataRecorded += (sender, e) =>
{
    Application.Current.Dispatcher.Invoke(() =>
        RecentRecords.Add(e.Record));   // e.Record 是 ProductionRecord，取值用 e.Record.Deserialize<T>()
};
```

## 导出

`ExportToExcelAsync` / `ExportToCsvAsync` 接受**查询过滤器**而不是记录集合——内部会重新按 filter 查一遍再导出：

```csharp
await _productionDataService.ExportToExcelAsync(filter, @"D:\Export\ProductionReport.xlsx");
await _productionDataService.ExportToCsvAsync(filter, @"D:\Export\ProductionReport.csv");
```

## 数据维护

```csharp
// 清理超过保留天数的历史数据（默认 90 天）
await _productionDataService.PurgeOldDataAsync(retentionDays: 90);
```

## 数据库说明

生产数据存储于 `D:\PFConfig\PFAutoFrameWork\{项目名}\ProductionHistory.db`，按 `RecordType` 索引，不同类型的生产数据共用同一表结构（`JsonValue` 字段存储 POCO 序列化内容）。
