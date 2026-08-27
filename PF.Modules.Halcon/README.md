# PF.Modules.Halcon

PF.AutoFramework HALCON 视觉调试 Prism 模块（Layer 05，当前版本 1.0.7），提供过程调试、管线运行、ROI 编辑、ROI 形状模板建立/验证的可视化界面。以插件 DLL 方式加载，依赖 `PF.Vision.Halcon` 服务层（零 UI 依赖）完成实际的视觉引擎调用。

## 界面构成

```
HalconDashboardView（侧边栏根入口）
    └─ HalconContentRegion（内部区域，KeepAlive 保留导航状态）
         ├─ HalconDebugView      — 过程调试：过程列表 + HWindowControlWPF 图像查看器 + 单步执行 + 引擎耦合自检
         └─ PipelineRunnerView   — 管线运行：按顺序执行多步视觉管线，步骤间通过上下文黑板自动传图
```

三个独立弹窗（`IDialogService`，`HalconModule.RegisterTypes` 注册）：

| 弹窗 | 导航 key | 作用 |
|---|---|---|
| `RoiEditorDialogView` | `RoiEditorDialog` | 拖拽/绘制方式定义 `VisionRoiConfig`（矩形/旋转矩/圆/椭圆/扇形），画布控件用 `HalconRoiEditor` |
| `ShapeTemplateEditorDialogView` | `ShapeTemplateEditorDialog` | ROI 形状模板**建立/编辑**：选参考图 → 画 ROI → 保存（建模+写盘一步完成）；也能按名字加载已有模板微调 |
| `ShapeTemplateVerifyDialogView` | `ShapeTemplateVerifyDialog` | ROI 形状模板**只读验证**：按名字加载模板 → 在一张图上查找匹配 → 叠加显示命中轮廓 |

## ROI 形状模板匹配能力（v1.0.5 起新增，v1.0.6/v1.0.7 迭代）

框架侧独立、可复用的形状模板编辑/验证能力，消费项目不用自己重写这套界面，实际的建模/查找由 `PF.Vision.Halcon` 的 `ShapeTemplateService` 完成（不经 HDevEngine，直调 HALCON SDK）。

**建模弹窗 `ShapeTemplateEditorDialog`**：

- 打开方式互斥，调用方一开始就得选好——传 `"ImagePath"`（string）= 建新模板，用调用方指定的图当参考图（图片锁定）；传 `"LoadTemplateName"`（string）= 打开一个已有模板微调，按名字读回它自己存的参考图 + ROI；两者都不传就是空白开局，弹窗里"选参考图"/"加载模板文件"两个按钮都在。
- 画 ROI 复用 `HalconRoiEditor` 控件本体。v1.0.7 起「建立模板」不再是独立一步：填好名字、画好 ROI，点一次「保存并关闭」把建模型（`CreateShapeModel`）和写盘一起做了。
- 保存前两项校验（v1.0.6 起）：名称合法性（挡 Windows 文件名非法字符 `\ / : * ? " < > |`）与唯一性（撞已有模板名弹「确认覆盖」二次确认）。v1.0.7 起「改已有模板」时名称锁定不可改（`CanEditTemplateName` 仅新建时为真），从根上避免"到底是新建还是覆盖了原模板"变得含糊——因此撞名确认现在只可能发生在新建路径。
- 关闭（OK）时通过 `DialogParameters` 带回 `"Name"`，方便调用方（比如自动填进旁边的"验证模板"输入框）直接使用。
- 范围只到"存盘"为止，不含"在新图上查找/预览"——那是 `ShapeTemplateVerifyDialog` 或消费方自己的事。

**验证弹窗 `ShapeTemplateVerifyDialog`**：纯只读，不建模板也不改模板。打开时 `DialogParameters` 传 `"TemplateName"`（必需）+ `"ImagePath"`（可选，预填验证图）；图片来源按钮**始终可见**（不因预填而隐藏，验证场景本就需要随时换图测试鲁棒性）。在图上跑 `ShapeTemplateService.FindMatches`，通过 `pf:PropertyGrid` 直接编辑 `ShapeMatchOptions`（`MinScore`/`AngleExtent`/`NumLevels` 等），匹配结果列表可选中，选中即用 `GetMatchedContour` + `HalconImageViewer.DisplayOverlay` 叠加显示命中轮廓（lime green）。`ConfirmCommand = CancelCommand`，没有"确定"要提交，关闭即走。

模板文件是 `.roipk`（zip 打包）：`model.shm`（生产匹配用的 HALCON 形状模型）+ `rois.json`（ROI 绘制过程，供再次打开微调）+ `reference.png`（参考图）。生产路径的 `ShapeTemplateService.LoadTemplate` 只解压 `model.shm`，不受编辑弹窗改动影响。

> `FindMatches` 此前有一个必现 Bug：`NumLevels==0` 时被错误传成字符串 `"auto"`（抄自 `CreateShapeModel` 的惯例），但 `find_shape_model` 的 `NumLevels` 默认值是整数 0、不接受该字符串，导致查找模板必现 HALCON #1208。已在 `PF.Vision.Halcon` 1.0.3 修复为直接传整数，升级前"查找模板"功能实际上完全不可用。

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

## 注意事项

- 调试 UI 内的 `async void` 命令方法均已补充统一异常保护，单个命令抛异常不会导致进程崩溃（v1.0.1 起）。
- `HalconDashboardViewModel` 重写了 `KeepAlive => true`（配合 `PF.UI.Infrastructure` v1.0.2 起 `RegionMemberLifetime` 默认值变为 `false` 的行为变更），以保留内部区域（过程调试/管线运行）已选中的导航状态；自行编写新的、需要"导航离开后重新进入仍保留状态"的调试面板时，务必同时重写 `IsNavigationTarget` 和 `KeepAlive`，只写一个会被 Region 自动移除打断复用逻辑。
- `HalconRoiEditor` 顶部工具栏（操作模式/形状选择/预览检测范围/退出预览/清空 ROI 共 10 个按钮）自 v1.0.4 起由"emoji+文字"改为 `pf:PackIcon` 纯图标，说明文字移到 `ToolTip` 里——不再依赖字体对几何符号 emoji 的渲染支持。

## 图标量所有权提示

本模块从 `IVisionResult.IconicOutputs`（装箱 `HObject`）取值显示时，务必遵守 `PF.Vision.Halcon` README 中记录的所有权契约——通过事件拿到的图标量仅在回调期间有效，需要长期持有必须自行 `HOperatorSet.CopyObj` 克隆。
