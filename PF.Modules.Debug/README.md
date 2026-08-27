# PF.Modules.Debug

PF.AutoFramework 综合调试 Prism 模块（当前 v1.0.10），以插件 DLL 方式加载。覆盖硬件、模组、工站、通讯四条调试树，是现场排故的主入口。

## 四棵调试树

| 入口 | 内容 |
|---|---|
| **硬件综合调试**（`HardwareDebugView`） | 树根现为"运动控制卡 + 图像采集卡"两类宿主，子设备按 `ParentDevice` 分组显示；连接/断开/复位/仿真开关；点击设备按类型路由到对应叶子调试页 |
| **模组调试**（`MechanismDebugView`） | 通过 `[MechanismUI]` 特性自动发现并列出各机构的自定义调试视图 |
| **工站调试**（`StationDebugView`） | 通过 `[StationUI]` 特性自动发现并列出各工站的自定义调试视图 |
| **通讯综合调试**（`CommunicationDebugView`） | 按 `CommunicationCategory` / `CommunicationRole` 两级分类树展示所有 `ICommunication` 实例 |

### 硬件叶子调试页

| 设备类型 | 调试页 | 要点 |
|---|---|---|
| `IAxis` | `AxisDebugView` | 点动 / 绝对/相对运动 / 回零 / 实时位置 |
| `IIOController` | `IODebugView` | 输入实时状态、输出手动强制置位（需工程师权限） |
| `IMotionCard` | `CardDebugView` | 控制卡级状态与复位 |
| `IIntelligentCamera` | `CameraDebugView` | 智能相机连接、拍照测试 |
| `IBarcodeScan` | `BarcodeScanDebugView` | 条码扫描触发测试 |
| `ILightController` | `LightControllerDebugView` | 通道区域按设备 `ChannelCount`（PF.Core 1.0.14 起可配置）动态渲染，每通道一个带刻度滑块 + 可编辑文本框，二者绑定同一 `Value` 天然联动；亮度下发防抖(150ms)+串行化，"读取亮度"按钮/进页面自动读一次显示设备实测值，读回不做轮询 |
| `IFrameGrabberCard` | `FrameGrabberDebugView` | 采集卡独立成页（切帧发生在卡上，非相机附属面板）：帧控制下发（帧长/帧超时/帧触发源/残帧策略）、帧软触发、GenICam 属性树 |
| `ILineScanCamera` | `LineScanCameraDebugView` | 连接/断开/复位/模拟报警、枚举在线相机、配置下发（本体参数+行触发+编码器）、开流/停流、取一帧存盘（BMP/PNG/TIFF）、`pf:ImageViewer` 预览、GenICam 属性树。面板状态一律从设备读回，不回显输入值 |
| 标注 `[HardwareUI("视图名")]` 的自定义设备 | 项目自定义视图 | 优先于以上内置分发，导航参数键统一为 `"Device"` |

GenICam 属性树（线阵相机/采集卡两页共用）：一键枚举设备全部节点，按分类显示类型/权限/当前值 + 常用节点中文释义，支持单节点读写与命令执行。

### 通讯叶子调试页

| 通讯类型 | 调试页 |
|---|---|
| TCP 服务端 | `TcpServerDebugView` |
| TCP 客户端 | `TcpClientDebugView` |
| 文件传输通道 | `FileTransferDebugView` |
| Modbus RTU 主站 | `ModbusRtuDebugView`（8 个功能码读写测试、报文预览、原始 PDU 直发） |
| Modbus TCP 主站 | `ModbusTcpDebugView`（同上，另有事务号递增/固定切换） |
| 裸串口 | `SerialPortDebugView`（v1.0.9 起）：原始字节收发，收/发各带独立十六进制开关，十六进制输入容忍空格/逗号/连字符/0x前缀，位数非法只提示不发送 |

均带实时状态、测试收发、事件日志，以及"参数设置"入口（弹窗改参数 → 存库 → `ReloadAllAsync` 重新加载）。串口调试页要在树上出现，需项目侧先在 `RegisterCommunicationFactories` 注册 `"SerialPort"` 工厂并在参数/默认配置中加一条对应实例（建议 `AutoStart=false`，开口时机交给设备层或调试页）。

### 线扫检测模组调试页

`LineScanDetectionModuleDebugView`（v1.0.7 起）随框架提供，本体在 `PF.Infrastructure`，项目无需自己写。导航 key 与 `[MechanismUI]` 一致。功能：初始化/复位/停止、移动到扫描起点、执行一次完整扫描、中止；`ScanProfile` 参数经 `pf:PropertyGrid` 编辑，实时显示换算结果（帧长/行频/曝光上限/理论帧时间）与 `Validate()` 校验信息，校验不通过时"扫描"按钮禁用；结果用 `pf:ImageViewer` 预览。

## 接入步骤

### 1. 为机构 / 工站注册自定义调试视图

```csharp
// 在机构类上标注（ViewName 必须与 RegisterForNavigation key 一致）
[MechanismUI(ViewName = "FeedMechDebugView", Title = "送料机构")]
public class FeedMechanism : BaseMechanism { ... }

// 在工站类上标注
[StationUI(ViewName = "FeedStationDebugView", Title = "送料工站")]
public class FeedStation : StationBase<FeedMechanism> { ... }

// 在项目 UI 模块中注册视图路由
containerRegistry.RegisterForNavigation<FeedMechDebugView,  FeedMechDebugViewModel>("FeedMechDebugView");
containerRegistry.RegisterForNavigation<FeedStationDebugView, FeedStationDebugViewModel>("FeedStationDebugView");
```

### 2. 为项目自定义硬件设备接入调试页路由

```csharp
// 标注在设备实现类上，优先于内置类型硬编码分发
[HardwareUI("MyCustomDeviceDebugView")]
public class MyCustomDevice : BaseDevice, IMyDevice { ... }
```

### 3. 确保 DLL 在 Modules\ 目录

构建后 `PF.Modules.Debug.dll` 自动复制到 Shell 的 `Modules\` 目录（由 `IsModuleProject=true` 驱动）。

## 权限控制

IO 强制输出等危险操作需 `UserLevel >= Engineer`；通讯综合调试视图与硬件/工站调试一样，仅限工程师及以上权限可见（`DefaultPermissions.RegisterViews`）。

## 仿真模式提示

调试面板在仿真模式下显示"SIM"角标，IO 读写和轴运动均为虚拟操作，不驱动实际硬件。
