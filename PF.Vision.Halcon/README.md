# PF.Vision.Halcon

PF.AutoFramework HALCON HDevEngine 视觉服务层（Layer 04），提供过程执行引擎、ROI 管理、视觉工作流管线。**零 Prism.Wpf / WPF 依赖**，可独立于 UI（`PF.Modules.Halcon`）引用，供工站业务代码直接调用。

## 核心接口（PF.Core.Interfaces.Vision）

```csharp
public interface IVisionService
{
    IReadOnlyList<string> GetAvailableProcedures();
    IReadOnlyList<string> GetLoadedProcedures();

    Task<bool> LoadProcedureAsync(string procedureName, CancellationToken cancellationToken = default);
    Task UnloadProcedureAsync(string procedureName, CancellationToken cancellationToken = default);

    // 单步执行：内部通过 Channel 委托给单一 Worker 线程串行执行
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

## ⚠️ HObject 所有权契约（务必遵守）

HALCON 的 `HObject`（图像/区域/轮廓）是非托管内存，生命周期规则**不对称**：

| 获取方式 | 所有权 | 要求 |
|---|---|---|
| `ExecuteAsync` / `ExecutePipelineAsync` 的**返回值** | 归调用方 | 使用完毕**必须**由调用方释放 |
| `ProcedureExecuted` **事件参数** | 仅回调期间有效 | 需要长期保留必须自行 `HOperatorSet.CopyObj` 克隆；严禁保存原引用或释放它 |
| `ExecutePipelineAsync` 的 `externalInputs` 参数 | 仍归调用方 | 实现层内部存副本，不会接管释放责任 |

违反这条契约（在事件回调外持有原始引用，或忘记释放返回值）是本层已知的历史 Bug 来源（管线黑板此前未持有独立句柄副本导致的 HObject 泄漏，已在 v1.0.1 修复）。

## DI 注册

```csharp
// App.xaml.cs 的 RegisterTypes 中
containerRegistry.AddVisionServices(
    procedureDirectory: @"D:\VisionProcedures",
    pipelineDirectory:  null); // 默认取 procedureDirectory 同级的 "Workflows" 目录
```

## 健壮性说明（v1.0.1）

- 过程文件缺失或加载失败不会再杀死 Worker 线程——此前 `LoadProcedureFromFile` 抛出的 `FileNotFoundException` 会导致 Worker 循环退出，此后所有视觉调用永久挂起；现在 `DoLoad`/`DoExecute` 捕获全部异常并转为失败结果返回，Worker 循环外再加最后一道防线。
- `ExecuteAsync`/`ExecutePipelineAsync` 的超时现在覆盖排队 + 执行全程（此前只覆盖入队阶段，真正执行时 `await` 可无限等待）；超时返回失败结果，主动 `cancellationToken` 取消则抛 `OperationCanceledException`。
- 管线步骤条件解析失败从"视为 true 继续执行"改为终止管线，避免在条件判断出错时仍执行后续危险步骤。
- `FileSystemWatcher`（驱动 `ProcedureDirectoryChanged`）加了防抖，避免文件保存过程中的多次写入触发重复重载。

## 依赖关系

```
业务代码 / PF.Modules.Halcon（UI 层）
    ↓
IVisionService（PF.Core 契约）
    ↓
HalconVisionService（本包实现，HDevEngine 运行时加载 .hdev 算子文件）
```
