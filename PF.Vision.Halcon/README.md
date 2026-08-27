# PF.Vision.Halcon

PF.AutoFramework HALCON 视觉服务层（Layer 04，当前版本 1.0.3）。**零 Prism.Wpf / WPF 依赖**，可独立于 UI（`PF.Modules.Halcon`）引用，供工站业务代码直接调用。包含两条相对独立的能力线：基于 HDevEngine 的**过程/管线执行引擎**，和直调 HALCON SDK、不经 HDevEngine 的 **ROI 形状模板匹配**。

## 核心接口（PF.Core.Interfaces.Vision）

```csharp
public interface IVisionService
{
    IReadOnlyList<string> GetAvailableProcedures();   // 过程目录中扫描到的 .hdev 文件名
    IReadOnlyList<string> GetLoadedProcedures();       // 当前已加载到内存缓存的过程名快照

    Task<bool> LoadProcedureAsync(string procedureName, CancellationToken cancellationToken = default);
    Task UnloadProcedureAsync(string procedureName, CancellationToken cancellationToken = default);

    // 单步执行：内部通过 Channel 委托给单一 Worker 线程串行执行，超时覆盖排队+执行全程
    Task<IVisionResult> ExecuteAsync(VisionRequest request, CancellationToken cancellationToken = default);

    // 管线执行：步骤间通过上下文黑板自动传递 HObject，全程在同一 Worker 线程完成
    Task<IVisionResult> ExecutePipelineAsync(
        VisionPipelineDefinition pipeline,
        Dictionary<string, object?>? externalInputs = null,
        CancellationToken cancellationToken = default);

    event EventHandler<IVisionResult> ProcedureExecuted;
    event EventHandler<string> ProcedureDirectoryChanged;
}
```

`IVisionResult` 以 `object` 装箱传递控制量（`ControlOutputs`，对应 `.hdev` 变量的 `HTuple`）与图标量（`IconicOutputs`，对应 `HObject`），由调用方按需强转。

`IVisionContextManager` 管理**三模式引擎**（`EngineMode.Production`/`Debug`/`Offline`），每种模式一个独立 HDevEngine + Worker 线程，按需拉起、用完释放，三者可同时存在、共享同一算法目录：

```csharp
IVisionService  GetOrCreate(EngineMode mode);   // 按需拉起，已存在则直接返回
Task            ReleaseAsync(EngineMode mode);  // 释放该模式引擎
bool            IsActive(EngineMode mode);
IVisionService? TryGet(EngineMode mode);        // 只读查询，绝不创建；未拉起时返回 null（v1.0.2 新增）
IReadOnlyList<EngineMode> ActiveEngines { get; }
```

## ⚠️ HObject 所有权契约（务必遵守）

HALCON 的 `HObject`（图像/区域/轮廓）是非托管内存，生命周期规则**不对称**：

| 获取方式 | 所有权 | 要求 |
|---|---|---|
| `ExecuteAsync` / `ExecutePipelineAsync` 的**返回值** | 归调用方 | 使用完毕**必须**由调用方释放 |
| `ProcedureExecuted` **事件参数** | 仅回调期间有效 | 需要长期保留必须自行 `HOperatorSet.CopyObj` 克隆；严禁保存原引用或释放它 |
| `ExecutePipelineAsync` 的 `externalInputs` 参数 | 仍归调用方 | 实现层内部存副本，不会接管释放责任 |
| `ShapeTemplateService` 各方法返回的 `HObject`（`GetMatchedContour` 等）/`ShapeTemplateHandle` | 归调用方 | 用完必须 Dispose，规则与上面一致 |

违反这条契约（在事件回调外持有原始引用，或忘记释放返回值）是本层已知的历史 Bug 来源（管线黑板此前未持有独立句柄副本导致的 HObject 泄漏，已在 v1.0.1 修复）。

## DI 注册

```csharp
// App.xaml.cs 的 RegisterTypes 中
containerRegistry.AddVisionServices(
    procedureDirectory: @"D:\VisionProcedures",
    pipelineDirectory:  null); // 默认取 procedureDirectory 同级的 "Workflows" 目录

// 用到 ROI 形状模板匹配时，额外配一次模板目录（不用则不必调用）
containerRegistry.AddShapeTemplateServices(templateDirectory: @"D:\VisionProcedures\ShapeTemplates");
```

`AddVisionServices` 注册 `IVisionContextManager`、`IHalconDebugService`、`VisionPipelineLoader` 三个单例；`AddShapeTemplateServices` 只是把目录写进 `ShapeTemplateService.TemplateDirectory`（静态属性），不存在会自动创建，`ShapeTemplateService` 本身是静态类，不进容器。

## ROI 形状模板匹配（`ShapeTemplateService`，v1.0.x 新增）

基于 `CreateShapeModel`/`FindShapeModel`，**直调 HALCON SDK、不经过 HDevEngine**——跟 `Internal.RoiRegionBuilder` 同一路子，纯托管方法调用，可在任意线程调用，不用像过程执行那样排队到专用 Worker 线程。

**典型用法（三步）**：`CreateTemplate(image, roiRegion, options?)` 在参考图的 ROI 区域内建模板 → `SaveTemplate(handle, referenceImage, rois, name)` 按名字存盘（`LoadTemplate(name)` 读回）→ `FindMatches(image, handle, options?)` 在新图上找，返回 `IReadOnlyList<ShapeMatchResult>`（`Row`/`Column`/`Angle`/`Score`）。ROI 区域建议用 `Internal.RoiRegionBuilder.Build` 从一组 `VisionRoiConfig` 拼出来（已支持多区域 Include/Exclude 拼接复杂形状）。

**按名字存取，不是按裸路径**：`SaveTemplate`/`LoadTemplate` 只收一个名字，实际文件路径由 `ShapeTemplateService.TemplateDirectory` 拼出来——未配置时调用会直接抛异常，不会静默退化成相对路径。`GetAvailableTemplateNames()` 列出目录下所有可用模板名（目录未配置/不存在时返回空列表，供下拉框等 UI 场景用）。

**模板文件是打包格式 `.roipk`（zip），不是裸 `.shm`**：HALCON 形状模型本身只保存训练好的轮廓特征，不保存建模板时画的 ROI 区域，单存 `.shm` 没法在调试时"重新打开、微调 ROI"。`SaveTemplate` 把 `model.shm`（生产匹配用）+ `rois.json`（`VisionRoiConfig` 列表）+ `reference.png`（建模用的参考图）打成一个 zip；生产路径 `LoadTemplate` 只解 `model.shm`；调试微调路径用 `LoadTemplateForEdit(name)` / `LoadTemplateForEditFromPath(filePath)` 解另外两块（返回 `ShapeTemplateEditSession(ReferenceImage, Rois)`）——两条路径互不影响，生产端不为这个能力多付任何解压/反序列化开销。同名 `SaveTemplate` 会整体覆盖。

`ShapeTemplateHandle`（`CreateTemplate`/`LoadTemplate` 的返回值）内部持有 HALCON `ModelId`，用完必须 `Dispose()`（`ClearShapeModel`），`ModelId` 本身不对外暴露。

可调参数全用 C# 原生类型（非 `HTuple`），方便直接绑 `pf:PropertyGrid`：

- `ShapeTemplateCreateOptions`（建模）：`AngleStart`/`AngleExtent`/`AngleStep`、`NumLevels`（0=自动）、`Contrast`（0=自动）/`MinContrast`、`Optimization`（默认 `auto`）、`Metric`（默认 `use_polarity`）。
- `ShapeMatchOptions`（查找）：`AngleStart`/`AngleExtent`、`MinScore`（默认 0.7）、`NumMatches`（默认 1）、`MaxOverlap`（默认 0.5）、`SubPixel`（默认 `least_squares`）、`NumLevels`（0=自动）、`Greediness`（默认 0.9）。

`GetMatchedContour(handle, match)` 取某次匹配位姿下的模板轮廓（模板参考系轮廓 + `VectorAngleToRigid`/`AffineTransContourXld` 变换到实际位置/角度），可直接喂给 `HalconImageViewer.DisplayOverlay` 画出命中框；返回值所有权归调用方，用完需 Dispose。

> **v1.0.3 修复的严重 Bug**：`FindMatches` 里 `NumLevels==0` 时此前错误传成字符串 `"auto"`（抄自 `CreateShapeModel` 的惯例），但 `find_shape_model` 算子的 `NumLevels` 默认值本来就是整数 0，不接受该字符串，必现 `HALCON error #1208`，升级前"查找模板"功能实际上完全不可用。现直接传整数。

## 过程调试服务解耦（`IHalconDebugService`，v1.0.2 起，含破坏性变更）

`GetProcedureSignatureAsync`/`GetAvailableProcedures`/`LaunchHDevelop` 这三个只读 API 此前都要 `GetOrCreate(EngineMode.Debug)` 拉起一个 Debug 引擎，只为取过程目录这一个字符串——代价是生产机每次开机凭空多出一个 HDevEngine + 一条 LongRunning 系统线程 + 一个递归 `FileSystemWatcher`，永不使用。v1.0.2 起改为 `HalconDebugService` 构造函数直接注入 `procedureDirectory`（走 `AddVisionServices` 注册的项目不受影响，扩展方法已同步传入；手动 `new HalconDebugService(...)` 的地方需补第三参），`IHalconDebugService` 新增只读属性 `ProcedureDirectory`（已规范化的完整路径）。`GetOrCreate(EngineMode.Debug)` 现在只剩 `EnableDebugServerAsync`/`ForceReloadAsync`/`RunTestAsync` 三个真正需要引擎的调用点。

同一版本一并修复的关联问题：

- **子目录过程解析不出签名**：过程枚举是 `AllDirectories` 递归的，但查找原先只看顶层。新增 `Internal.HdevProcedureCatalog` 统一两侧规则，查找优先级为顶层 `.hdvp` → 子目录 `.hdvp` → 顶层 `.hdev` → 子目录 `.hdev`；过程名里的 `*`/`?` 不再被当通配符；多处同名按路径排序取首个，结果可复现。
- **未启动调试服务器时投作业永久挂起**：Debug 引擎配置为 `WaitForDebugConnection=true` + 无限超时，此前只要投作业就会等一个不会到来的 HDevelop 连接。现加 `DebugServerStarted` 门控，服务器未启动时正常执行。
- **`IsDebugServerActive` 在引擎释放后失步**：改为以引擎上的 `DebugServerStarted` 为唯一真相，`ActivePort`/`Password` 一并跟随回落；`DisableDebugServerAsync` 引擎已释放时直接返回，不再为停一个不存在的服务器把引擎重新拉起来。

依赖：需 `PF.Core` 1.0.13+。

## 健壮性说明（v1.0.1）

- 过程文件缺失或加载失败不会再杀死 Worker 线程——此前 `LoadProcedureFromFile` 抛出的 `FileNotFoundException` 会导致 Worker 循环退出，此后所有视觉调用永久挂起；现在 `DoLoad`/`DoExecute` 捕获全部异常并转为失败结果返回，Worker 循环外再加最后一道防线。
- `ExecuteAsync`/`ExecutePipelineAsync` 的超时覆盖排队 + 执行全程（此前只覆盖入队阶段，真正执行时 `await` 可无限等待）；超时返回失败结果，主动 `cancellationToken` 取消则抛 `OperationCanceledException`。
- 管线步骤条件解析失败从"视为 true 继续执行"改为终止管线，避免条件判断出错时仍执行后续危险步骤。
- `FileSystemWatcher`（驱动 `ProcedureDirectoryChanged`）加了防抖，避免文件保存过程中的多次写入触发重复重载。

## 依赖关系

```
业务代码 / PF.Modules.Halcon（UI 层）
    ↓
IVisionService / IHalconDebugService / IVisionContextManager（PF.Core 契约）
    ↓
HalconVisionService / HalconDebugService（本包实现，HDevEngine 运行时加载 .hdev 算子文件）
ShapeTemplateService（本包实现，直调 HALCON SDK，不经 HDevEngine）
```
