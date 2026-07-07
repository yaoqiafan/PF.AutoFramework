# PF.Modules.Halcon

PF.AutoFramework HALCON 视觉调试 Prism 模块（Layer 05），提供过程列表导航、图像查看、ROI 编辑、管线运行的可视化界面。以插件 DLL 方式加载，依赖 `PF.Vision.Halcon` 服务层（零 UI 依赖）完成实际的视觉引擎调用。

## 功能

- **HalconDashboardViewModel**：驱动内部区域在「过程调试」（`HalconDebugView`）与「管线运行」（`PipelineRunnerView`）之间导航，首次进入默认导航到过程调试。
- **HalconDebugView / HalconDebugViewModel**：过程列表导航面板 + `HWindowControlWPF` 图像查看器，单步执行 `.hdev` 过程并显示控制量/图标量输出。
- **RoiEditorDialogView / RoiEditorDialogViewModel**：ROI 编辑弹窗，拖拽/绘制方式定义 `VisionRoiConfig`。
- **PipelineRunnerView / PipelineRunnerViewModel**：按顺序执行多步视觉管线，步骤间通过上下文黑板自动传递图像。

## 依赖关系

```
PF.Modules.Halcon（本包，UI 层）
    ↓
PF.Vision.Halcon（HDevEngine 服务层，见其 README）
    ↓
PF.Core.Interfaces.Vision（IVisionService / IVisionResult 契约）
```

## 注意事项（v1.0.1 起）

- 调试 UI 内的 `async void` 命令方法均已补充统一异常保护，单个命令抛异常不会再导致进程崩溃（此前的已知问题）。
- `HalconDashboardViewModel` 重写了 `KeepAlive => true`（配合 `PF.UI.Infrastructure` v1.0.2 起 `RegionMemberLifetime` 默认值变为 `false` 的行为变更），以保留内部区域（过程调试/管线运行）已选中的导航状态；自行编写新的、需要"导航离开后重新进入仍保留状态"的调试面板时，务必同时重写 `IsNavigationTarget` 和 `KeepAlive`，只写一个会被 Region 自动移除打断复用逻辑。

## 图标量所有权提示

本模块从 `IVisionResult.IconicOutputs`（装箱 `HObject`）取值显示时，务必遵守 `PF.Vision.Halcon` README 中记录的所有权契约——通过事件拿到的图标量仅在回调期间有效，需要长期持有必须自行 `HOperatorSet.CopyObj` 克隆。
