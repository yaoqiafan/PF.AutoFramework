# PF.Modules.Parameter

PF.AutoFramework 参数管理 Prism 模块，提供硬件参数与系统配置的可视化编辑、验证及审计界面。以插件 DLL 方式加载。

## 功能

- **参数分组展示**：按模块（机构 / 工站 / 系统）分组显示参数
- **在线编辑**：数值、字符串、枚举类型参数直接在 UI 中编辑，即时写库
- **修改审计**：每次修改记录操作人、时间、旧值、新值
- **配方切换**：支持加载 / 保存不同工艺配方（Recipe）
- **自定义视图**：通过 `[ParamView]` 特性注册各模块专属参数页面

## 接入步骤

### 1. 定义参数 POCO（必须是 class，不能是值类型）

```csharp
public class FeedParam
{
    public double PickSpeed    { get; set; } = 200.0;   // 取料速度 mm/s
    public double PlaceSpeed   { get; set; } = 150.0;   // 放料速度 mm/s
    public int    RetryCount   { get; set; } = 3;       // 重试次数
    public bool   EnableSensor { get; set; } = true;    // 传感器使能
}
```

### 2. 注册参数类型（App.xaml.cs）

```csharp
// 建立 POCO ↔ Entity 的类型映射
_paramService.RegisterParamType<FeedParam, FeedParamEntity>();
_paramService.RegisterParamType<DetectParam, DetectParamEntity>();
```

### 3. 为模块注册自定义参数视图

```csharp
// 在 UI 模块 RegisterTypes 中
[ParamView(ViewName = "FeedParamView", Title = "送料参数")]
public class FeedParamViewModel : ViewModelBase { ... }

// 注册路由（ViewName 必须一致）
_regionManager.RegisterForNavigation<FeedParamView, FeedParamViewModel>("FeedParamView");
```

### 4. 在业务代码中读写参数

```csharp
// 读取
var param = await _paramService.GetAsync<FeedParam>();
double speed = param.PickSpeed;

// 写入（自动持久化，值未变更时跳过）
await _paramService.SetAsync(new FeedParam { PickSpeed = 300.0 });

// 订阅变更（可用于触发重初始化等联动）
_paramService.ParamChanged += async (s, e) =>
{
    if (e.ParamType == typeof(FeedParam))
        await ReinitializeFeedStationAsync();
};
```

## 配方管理

```csharp
// 保存当前参数为配方
await _recipeService.SaveAsync("Recipe_A", currentParam);

// 加载配方
var recipe = await _recipeService.LoadAsync<OCRRecipeParam>("Recipe_A");
await _paramService.SetAsync(recipe);
```

## 注意事项

- 参数 POCO 泛型约束为 `where T : class`，直接使用 `int`、`double` 会编译报错，需包装为 POCO 类
- 参数实体（Entity）由框架自动通过 JSON 字段存储，无需手动定义复杂 EF Core 映射
