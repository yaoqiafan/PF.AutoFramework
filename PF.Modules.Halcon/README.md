# PF.Modules.Halcon

PF.AutoFramework HALCON 视觉调试 Prism 模块（Layer 05，当前版本 1.1.0），提供过程调试、管线运行、ROI 编辑、视觉资产包（`.vpk`）编辑的可视化界面。以插件 DLL 方式加载，依赖 `PF.Vision.Halcon` 服务层（零 UI 依赖）完成实际的视觉引擎调用。

## 界面构成

```
HalconDashboardView（侧边栏根入口）
    └─ HalconContentRegion（内部区域，KeepAlive 保留导航状态）
         ├─ HalconDebugView      — 过程调试：过程列表 + HWindowControlWPF 图像查看器 + 单步执行 + 引擎耦合自检
         └─ PipelineRunnerView   — 管线运行：按顺序执行多步视觉管线，步骤间通过上下文黑板自动传图
```

两个独立弹窗（`IDialogService`，`HalconModule.RegisterTypes` 注册）：

| 弹窗 | 导航 key | 作用 |
|---|---|---|
| `RoiEditorDialogView` | `RoiEditorDialog` | 拖拽/绘制方式定义 `VisionRoiConfig`（矩形/旋转矩/圆/椭圆/扇形/多边形），画布控件用 `HalconRoiEditor` |
| `VisionPackageEditorDialogView` | `VisionPackageEditorDialog` | 视觉资产包（`.vpk`）编辑器：按布局编辑原图 / ROI / 形状模型 / 参数 / 附件，校验通过后保存 |

> v1.1.0 起删除了 `ShapeTemplateEditorDialog` / `ShapeTemplateVerifyDialog`（及对应导航常量、操作日志键目录），
> 由 `VisionPackageEditorDialog` 取代——原来的"建立模板"是布局里的 原图 + ROI + 形状模型 三个条目，"验证模板"是形状模型页的"试找"。

## 视觉资产包编辑器 `VisionPackageEditorDialog`（v1.1.0 新增）

包格式、布局声明、依赖与状态规则见 `PF.Vision.Halcon` README 与框架仓库 `DOC/视觉资产包设计稿.md`；本弹窗是它的通用编辑界面。

**打开**：`DialogParameters` 传 `"Layout"`（`VisionPackageLayout`，必需）+ `"PackagePath"`（string）或 `"PackageName"`（string，按 `VisionPackage.PathOf` 拼路径）——文件存在则打开，否则新建；`"ImagePath"`（string，可选）新建时预填第一张原图。关闭带回 `"PackagePath"`、`"Revision"`（int）、`"Saved"`（bool，本次是否保存过；保存过结果为 OK）。

```csharp
var p = new DialogParameters { { "Layout", layout }, { "PackageName", "产品A" } };
DialogService.ShowDialog(HalconNavigationConstants.Dialogs.VisionPackageEditor, p, r =>
{
    if (r.Parameters.GetValue<bool>("Saved")) { /* 包已更新，修订号 r.Parameters.GetValue<int>("Revision") */ }
});
```

**界面**：

- 左侧条目树按布局分组生成，状态符号：● 正常、◐ 需重新生成 / 需确认、○ 未建立、✕ 无效（或多余）；悬停看原因。
- 右侧按条目类型切换编辑页：
  - 原图：预览、尺寸/来源/存储方式、导入/替换（替换时按与当前图的比对结论提示）；
  - ROI：复用 `HalconRoiEditor` 在依赖的原图上画，「应用 ROI」写入会话，换图后「确认位置」；
  - 形状模型：建模参数（PropertyGrid）、生成、试找（可另选一张图）并叠加命中轮廓；
  - 参数（`Data<T>`）：按类型生成 PropertyGrid，显示自校验错误；
  - 附件：导入/导出；
  - 多余条目 / 没有编辑页的自定义类型：只读信息页 + 删除。
- 底部校验栏常驻，点一条跳到对应条目；顶部工具栏：导入原图 / 全部重新生成 / 保存（当前登录用户记入包）。
- 页面上未应用的修改，切走 / 保存 / 关闭前都会提示；有未保存的修改时关闭提示放弃。标题栏 × 与底部「关闭」走同一流程。
- 打开大包（含原图预解码）、导入原图、生成模型、保存都在后台线程执行，期间显示忙碌遮罩。

**自定义条目类型的编辑页**：`IPackageEntryKind.EditorViewName` 指向一个用 `RegisterForNavigation` 注册的视图；视图本身或其 DataContext 实现 `IVisionPackageEntryEditor`（`Attach(session, entryId)` / `Detach()`）即可挂进右侧。

## `HalconDebugView`：过程调试 + 引擎耦合自检（v1.0.3 起）

底部新增「引擎耦合自检」按钮与报告框，三项断言：① 停止调试服务器并释放 Debug 引擎后 `IsDebugServerActive` 能否跟着回落；② 过程枚举 + 逐个签名解析这两个只读 API 会不会把 Debug 引擎重新拉起来；③ 过程签名解析覆盖率（附过程目录下子目录数量，为 0 时明确标注"本项无法证伪子目录递归查找"，不让报告假装证明了没证明的事）。自检会先停服务器 + 释放引擎构造干净起点，跑完保持关闭状态，需要继续调试请重新点「启动调试服务器」。`HalconDebugViewModel` 构造函数由容器解析 `IVisionContextManager`（对应 `PF.Vision.Halcon` v1.0.2 起的破坏性变更，消费项目无需改动）。依赖 `PF.Core` 1.0.13+ 与 `PF.Vision.Halcon` 1.0.2+。

## 依赖关系

```
PF.Modules.Halcon（本包，UI 层）
    ↓
PF.Vision.Halcon（HDevEngine + 直调 HALCON SDK 的服务层，见其 README）
    ↓
PF.Core.Interfaces.Vision（IVisionService / IVisionResult 契约）
```

本版本需 `PF.Vision.Halcon` 1.1.0+。

## 注意事项

- 调试 UI 内的 `async void` 命令方法均已补充统一异常保护，单个命令抛异常不会导致进程崩溃（v1.0.1 起）。
- `HalconDashboardViewModel` 重写了 `KeepAlive => true`（配合 `PF.UI.Infrastructure` v1.0.2 起 `RegionMemberLifetime` 默认值变为 `false` 的行为变更），以保留内部区域（过程调试/管线运行）已选中的导航状态；自行编写新的、需要"导航离开后重新进入仍保留状态"的调试面板时，务必同时重写 `IsNavigationTarget` 和 `KeepAlive`，只写一个会被 Region 自动移除打断复用逻辑。
- `HalconRoiEditor` 顶部工具栏（操作模式/形状选择/预览检测范围/退出预览/清空 ROI 共 10 个按钮）自 v1.0.4 起由"emoji+文字"改为 `pf:PackIcon` 纯图标，说明文字移到 `ToolTip` 里——不再依赖字体对几何符号 emoji 的渲染支持。
- `HalconImageViewer` 自适应显示自 v1.1.0 起按窗口宽高比计算显示范围（整图完整显示、居中），此前第一次显示与窗口宽高比不同的图会被拉伸。
- 同类型编辑页之间切换时 WPF 会复用同一个视图实例、只换 DataContext（不触发 Loaded/Unloaded）：编辑页里挂 HALCON 查看器的代码后置要同时处理 `DataContextChanged`，参照 `Views/VisionPackageEditor/ImagePageView.xaml.cs`。

## 图标量所有权提示

本模块从 `IVisionResult.IconicOutputs`（装箱 `HObject`）取值显示时，务必遵守 `PF.Vision.Halcon` README 中记录的所有权契约——通过事件拿到的图标量仅在回调期间有效，需要长期持有必须自行 `HOperatorSet.CopyObj` 克隆。
