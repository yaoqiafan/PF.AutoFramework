# PF.Modules.ProductionRecord

PF.AutoFramework 生产数据 Prism 模块，提供历史生产数据浏览、多条件查询及 Excel / CSV 导出功能。以插件 DLL 方式加载。

## 功能

- **历史数据浏览**：分页展示生产记录，支持虚拟化大数据量场景
- **多条件查询**：按时间段、产品型号、工站、结果（OK / NG）过滤
- **数据导出**：一键导出为 Excel（`.xlsx`）或 CSV 格式
- **实时追加**：运行中新增记录时自动刷新列表

## 接入步骤

### 1. 注册生产数据服务（App.xaml.cs）

```csharp
containerRegistry.RegisterSingleton<IProductionDataService, ProductionDataService>();
```

### 2. 定义生产数据 POCO

```csharp
public class OcrProductionData
{
    public string  BatchNo     { get; set; } = "";  // 批次号
    public string  ProductId   { get; set; } = "";  // 产品 ID
    public string  Barcode     { get; set; } = "";  // 条码
    public string  OcrResult   { get; set; } = "";  // OCR 识别结果
    public bool    IsPass      { get; set; }         // 是否合格
    public DateTime RecordTime { get; set; }
}
```

### 3. 在工站中记录生产数据

```csharp
// 泛型 RecordAsync：POCO → JSON 非阻塞写入，不阻塞工站循环
await _productionDataService.RecordAsync(new OcrProductionData
{
    BatchNo    = _currentBatch,
    ProductId  = "WAFER-001",
    Barcode    = scanResult,
    OcrResult  = ocrResult,
    IsPass     = ocrResult == expectedValue,
    RecordTime = DateTime.Now
});
```

### 4. 订阅实时数据事件

```csharp
// ViewModel 中订阅，UI 实时追加新记录
_productionDataService.DataRecorded += (sender, e) =>
{
    Application.Current.Dispatcher.Invoke(() =>
        Records.Add(e.Data as OcrProductionData));
};
```

## 导出示例

```csharp
// Excel 导出（NPOI）
await _productionDataService.ExportToExcelAsync(
    records,
    outputPath: @"D:\Export\ProductionReport.xlsx");

// CSV 导出
await _productionDataService.ExportToCsvAsync(
    records,
    outputPath: @"D:\Export\ProductionReport.csv");
```

## 数据库说明

生产数据存储于 `%APPDATA%\PFAutoFrameWork\ProductionHistory.db`，按 `RecordType`（POCO 类型名）索引，不同类型的生产数据共用同一表结构（JSON 字段存储 POCO 内容）。
