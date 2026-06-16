# PF.Modules.Debug

PF.AutoFramework 硬件调试面板 Prism 模块，提供设备连接 / 断开、轴手动运动、IO 状态读写的可视化操作界面。以插件 DLL 方式加载。

## 功能

- **设备面板**：列出所有已注册硬件，显示连接状态，支持手动连接 / 断开 / 复位
- **轴调试**：手动点动（+/-）、绝对 / 相对运动、原点复归、当前位置实时显示
- **IO 调试**：输入点实时状态、输出点手动强制置位 / 复位（需工程师权限）
- **机构面板**：通过 `[MechanismUI]` 特性自动注册各机构的自定义调试视图
- **工站面板**：通过 `[StationUI]` 特性自动注册各工站的自定义调试视图

## 接入步骤

### 1. 为机构 / 工站注册自定义调试视图

```csharp
// 在机构类上标注（ViewName 必须与 RegisterForNavigation key 一致）
[MechanismUI(ViewName = "FeedMechDebugView", Title = "送料机构")]
public class FeedMechanism : BaseMechanism { ... }

// 在工站类上标注
[StationUI(ViewName = "FeedStationDebugView", Title = "送料工站")]
public class FeedStation : StationBase<FeedMechanism> { ... }

// 在工站 UI 模块中注册视图
_regionManager.RegisterForNavigation<FeedMechDebugView,  FeedMechDebugViewModel>("FeedMechDebugView");
_regionManager.RegisterForNavigation<FeedStationDebugView, FeedStationDebugViewModel>("FeedStationDebugView");
```

### 2. 确保 DLL 在 Modules\ 目录

构建后 `PF.Modules.Debug.dll` 自动复制到 Shell 的 `Modules\` 目录（由 `IsModuleProject=true` 驱动）。

## 权限控制

IO 强制输出操作需 `UserLevel >= Engineer`，框架在执行前自动检查当前登录用户权限。

## 仿真模式提示

调试面板在仿真模式下显示"SIM"角标，IO 读写和轴运动均为虚拟操作，不驱动实际硬件。
