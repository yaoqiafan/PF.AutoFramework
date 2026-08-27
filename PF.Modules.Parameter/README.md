# PF.Modules.Parameter

PF.AutoFramework 参数管理 Prism 模块（当前 v1.0.6），提供系统参数与硬件参数的可视化编辑、验证及审计界面。以插件 DLL 方式加载。

只有一个导航入口 `ParameterView`（`ParameterViewModel`）：左侧按类型/分类树浏览，右侧参数列表在线编辑、批量保存、查看修改历史，`SuperUser` 可"刷新并重置默认值"。

## 两套"自定义参数视图"机制，容易混淆

| 机制 | 用途 | 载体 |
|---|---|---|
| `[ParamView(typeof(TView), typeof(TMapper))]` | 标注在**业务参数 POCO/Entity/枚举类型**上，给某个参数类型接一个自定义编辑器（而不是走通用表格） | `ViewFactory` 反射读取，`TMapper` 须实现 `IViewDataMapper` |
| `ViewFactory.RegisterHardwareConfigType<TView, TMapper>("ImplementationClassName")` | 在模块 `RegisterTypes` 里显式调用，给某个**硬件实现类**接硬件配置弹窗 | 按 `HardwareConfig.ImplementationClassName` 字符串路由 |

`IViewDataMapper` 只有两个方法：`MapToView(view, data)` / `MapFromView(view)`，负责视图 ↔ 数据对象的双向搬运。

### 硬件配置视图现状（内置，随框架提供）

```
LTDMCMotionCard / EtherCatAxis / EtherCatIO / HKBarcodeScan / MvCodeReaderBarcodeScan /
KeyenceBarcodeScan / KeyenceIntelligentCamera / CTS_LightController /
HikFrameGrabberCard / HikLineScanCamera / HikComLightController
```

均在 `ParameterModule.RegisterTypes` 里通过 `ViewFactory.RegisterHardwareConfigType<TView, TMapper>(key)` 注册，新增硬件类型参考已有实现即可。要点：

- `HikFrameGrabberCard`（采集卡）无父设备；`HikLineScanCamera`（线阵相机）有"父设备ID(采集卡)"字段——填写即走采集卡链路（CameraLink/CXP/XoF），留空即 GigE/USB 直连，**没有**单独的"链路类型"字段，拓扑本身就表达链路类型。曝光/增益/行触发/编码器等随配方变化的参数不在这里配，走机构层按扫描任务下发，调试用 `PF.Modules.Debug` 的线阵相机调试页逐项试。
- `HikComLightController` 只需填串口通讯实例 ID，串口本身（波特率/校验位等）在"通讯配置"里改，不在硬件参数里配。
- 康视达光源注册键历史上拼错为 `"CTS_LightControoller"`，v1.0.5 起订正为 `"CTS_LightController"`，不留兼容键；现场若还有旧配置行，需同步改 `ImplementationClassName`，否则该设备实例化不出来。
- `CTSLightControllerParamView` / `HikComLightControllerParamView` 均带"通道数"字段（v1.0.6 起，对应 `ConnectionParameters["ChannelCount"]`，缺省 4），配合 `PF.Core` 的 `ILightController.ChannelCount`。

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

### 2. 注册参数类型映射（App.xaml.cs）

```csharp
// 建立 Entity(IEntity) ↔ Model(class) 的类型映射；泛型顺序是【实体在前、模型在后】，
// 顺序反了不会编译报错，但会在运行时映射不到，务必对照 IParamService 签名核对：
// void RegisterParamType<TEntity, TModel>() where TEntity : IEntity where TModel : class
_paramService.RegisterParamType<FeedParamEntity, FeedParam>();
```

### 3. 为参数类型接自定义编辑视图（可选，不接则走通用表格）

```csharp
[ParamView(typeof(FeedParamView), typeof(FeedParamViewMapper))]
public class FeedParam { ... }
```

### 4. 在业务代码中读写参数（按名称存取，不是按类型单例）

```csharp
// 读取（带默认值重载可避免手动判空）
var pickSpeed = await _paramService.GetParamAsync<FeedParam>("Feed1");

// 写入（自动持久化，值未变更时跳过写库）
await _paramService.SetParamAsync("Feed1", new FeedParam { PickSpeed = 300.0 }, currentUser, "调高取料速度");

// 订阅变更（可用于触发重初始化等联动）
_paramService.ParamChanged += async (s, e) =>
{
    if (e.ParamType == typeof(FeedParam))
        await ReinitializeFeedStationAsync();
};
```

## 配方管理（`IRecipeService<T>`，`T : RecipeParamBase`）

配方与普通参数是两套体系：配方参数类需继承 `RecipeParamBase`（自带 `RecipeName`/`CreateTime`/`UpdateTime`/`Validate()`/`ToJson()`），服务按名称或 PPID 存取：

```csharp
// 按名称取一个配方
var recipe = await _recipeService.RecipeParam("Recipe_A");

// 写入/覆盖保存
await _recipeService.RecipeParamWriteAsync(recipe, IsCover: true);

// 切换当前生效配方（用于工站实际生产）
await _recipeService.RecipeChangedAsync(recipe);

// 列出全部配方名
var names = _recipeService.RecipeNames;
```

配方文件落在 `D:\PFConfig\PFAutoFrameWork\{项目名}\Recipe\` 下（项目隔离，见根 README 配置路径章节）。

## 注意事项

- `RegisterParamType<TEntity, TModel>` 的泛型约束是 `where TEntity : IEntity where TModel : class`——**实体在前、模型在后**，这条在 `IParamService` 接口注释里明确要求，也是本仓库最容易踩的坑之一。
- 参数 Model 必须是 `class`，直接用 `int`、`double` 等值类型会编译报错，需包装为 POCO 类。
- 参数以 JSON 字段存储在 `SystemParamsCollection.db`，无需手动定义复杂 EF Core 映射。
