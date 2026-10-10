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

dict 类型的控制输出转成 JSON 字符串；dict 里用 `set_dict_object` 夹带了图标量时（v1.0.5 起），图标量拆到 `IconicOutputs`，键为 `"{参数名}.{dict 键}"`（如 `"OutDict.DieRegions"`），JSON 里只剩元组键。

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
| `VisionPackageSession.GetImage` 返回的图 / `LoadShapeModel` 返回的句柄 | 归调用方 | 同上；会话内部缓存的图由会话自己释放 |

违反这条契约（在事件回调外持有原始引用，或忘记释放返回值）是本层已知的历史 Bug 来源（管线黑板此前未持有独立句柄副本导致的 HObject 泄漏，已在 v1.0.1 修复）。

## DI 注册

```csharp
// App.xaml.cs 的 RegisterTypes 中
containerRegistry.AddVisionServices(
    procedureDirectory: @"D:\VisionProcedures",
    pipelineDirectory:  null); // 默认取 procedureDirectory 同级的 "Workflows" 目录

// 用到视觉资产包（.vpk）时，额外配一次包目录（不用则不必调用）
containerRegistry.AddVisionPackageServices(packageDirectory: @"D:\VisionProcedures\Packages");
```

`AddVisionServices` 注册 `IVisionContextManager`、`IHalconDebugService`、`VisionPipelineLoader` 三个单例；`AddVisionPackageServices` 只是把目录写进 `VisionPackage.Directory`（静态属性），不存在会自动创建，之后用 `VisionPackage.PathOf(name)` 按名字拼路径。

## 形状模板匹配（`ShapeTemplateService`）

基于 `CreateShapeModel`/`FindShapeModel`，**直调 HALCON SDK、不经过 HDevEngine**——跟 `Internal.RoiRegionBuilder` 同一路子，纯托管方法调用，可在任意线程调用，不用像过程执行那样排队到专用 Worker 线程。

只管算法：`CreateTemplate(image, roiRegion, options?)` 在图的 ROI 区域内建模板 → `FindMatches(image, handle, options?)` 在新图上找，返回 `IReadOnlyList<ShapeMatchResult>`（`Row`/`Column`/`Angle`/`Score`）→ `GetMatchedContour(handle, match)` 取命中位置的模板轮廓（可直接喂给 `HalconImageViewer.DisplayOverlay`）。ROI 区域建议用 `Internal.RoiRegionBuilder.Build` 从一组 `VisionRoiConfig` 拼出来（支持多区域 Include/Exclude）。

**模板的存取不在这里**（v1.1.0 起）：原 `.roipk` 的 `SaveTemplate`/`LoadTemplate*`/`TemplateDirectory` 已删除，形状模型作为视觉资产包的 `ShapeModel` 条目保存，见下一节。

`ShapeTemplateHandle` 内部持有 HALCON `ModelId`，用完必须 `Dispose()`（`ClearShapeModel`）。可调参数全用 C# 原生类型，方便直接绑 `pf:PropertyGrid`：

- `ShapeTemplateCreateOptions`（建模）：`AngleStart`/`AngleExtent`/`AngleStep`、`NumLevels`（0=自动）、`Contrast`（0=自动）/`MinContrast`、`Optimization`（默认 `auto`）、`Metric`（默认 `use_polarity`）。
- `ShapeMatchOptions`（查找）：`AngleStart`/`AngleExtent`、`MinScore`（默认 0.7）、`NumMatches`（默认 1）、`MaxOverlap`（默认 0.5）、`SubPixel`（默认 `least_squares`）、`NumLevels`（0=自动）、`Greediness`（默认 0.9）。

> **v1.0.3 修复的严重 Bug**：`FindMatches` 里 `NumLevels==0` 时此前错误传成字符串 `"auto"`，必现 `HALCON error #1208`。现直接传整数。

## 视觉资产包（`.vpk`，`PF.Vision.Halcon.Packaging`，v1.1.0 新增）

一个 zip 包，根目录 `manifest.json` 记录格式版本、布局 id/版本、修订号、保存时间/保存人与全部条目。项目用**布局**声明包里有哪些条目，框架按布局管理依赖、状态、生成与校验。完整设计见框架仓库 `DOC/视觉资产包设计稿.md`。

```csharp
// 布局（项目在代码里声明一次）
var layout = VisionPackageLayout.Create("PF.ShapeTemplate", version: 1)
    .Group("输入").Image("Image", "原图").Roi("Region", "模板区域", image: "Image")
    .Group("生成").ShapeModel("Model", "形状模型", image: "Image", roi: "Region")
    .Build();

// 编辑（调试界面用 PF.Modules.Halcon 的 VisionPackageEditorDialog 即可，代码里也能直接用会话）
using (var s = VisionPackage.Create(layout))                 // 或 VisionPackage.Open(path, layout)
{
    s.ImportImage("Image", @"D:\图\a.tiff");                // 原来没图或无下游时直接生效，否则需 ConfirmImageReplace
    s.SetRois("Region", rois);
    s.Save(VisionPackage.PathOf("产品A"), userName);          // 自动生成派生条目、强制校验；不保存直接 Dispose = 放弃
}

// 生产
using var pkg = VisionPackageReader.Open(VisionPackage.PathOf("产品A"), LayoutRequirement.Of(layout));
using var model = pkg.LoadShapeModel("Model");               // 只解压这一个条目并核对哈希
```

- **内置条目**：`Image`（默认无损 PNG，可选原文件原样/TIFF/BMP/JPEG；显示、画 ROI、生成模型一律用存储版）、`Roi`（原图坐标）、`ShapeModel`（派生，由原图 + ROI 按建模参数生成）、`Data<T>`（宽松反序列化，`T` 实现 `IVisionPackageData` 时做自校验）、`File`（附件原样保存）。自定义类型实现 `IPackageEntryKind` 并 `VisionPackage.RegisterKind`。
- **依赖与状态**：`Missing`/`Ok`/`Stale`/`Invalid`/`Extra`，按依赖指纹判断并向下游传播。换原图：同一文件不动；尺寸相同内容不同 → ROI 需 `ConfirmRoi`；尺寸不同 → ROI 失效需重画。派生条目 `Regenerate`/`RegenerateAllStale`。
- **保存即生效**：没有草稿；校验不通过抛 `VisionPackageValidationException`，原文件不动；成功后修订号 +1。
- **版本**：生产读取器要求布局 id 与版本**完全相等**；`Data<T>` 字段改名/改含义、增删必填条目等须升布局版本。
- **注意**：`.shm` 文件头带生成时间，同一份输入重新生成模型等价但指纹不同。

## HALCON 区域裁剪按线程缓存（`HalconThreadContext`，v1.1.0）

`clip_region=true`（默认）时新生成的区域被裁剪到"当前图像尺寸"，这个尺寸**按线程缓存**：线程第一次用 HALCON 时记下当时的全局 `width/height`（未读图时 128×128），别的线程之后读了大图也不会跟着变。曾导致：UI 线程上 ROI 区域被裁空（ROI 编辑器看不到拖拽预览）、视觉引擎进程启动后第一次执行过程匹配必失败。

- `RoiRegionBuilder` / `ShapeTemplateService` 内部调用 `HalconThreadContext.EnsureRegionClipOff()`，在当前线程关闭区域裁剪；
- `HalconVisionService` 执行过程前调用 `HalconThreadContext.SyncClipSize(输入图像)`，把工作线程裁剪尺寸设成输入图像尺寸（保留裁剪语义，与 HDevelop 一致）；
- 消费项目自己在多线程里生成区域时，同样可以调这两个方法。

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
ShapeTemplateService / Packaging（本包实现，直调 HALCON SDK，不经 HDevEngine）
```
