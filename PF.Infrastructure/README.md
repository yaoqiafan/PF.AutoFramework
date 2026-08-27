# PF.Infrastructure

PF.AutoFramework 底层基础设施层（当前 v1.0.12），提供硬件三层抽象（挂载基类 / 轴 / IO / 相机 / 采集卡 / 光源）、机构基类、工站状态机、主控编排基类，以及 TCP/串口/文件传输/Modbus 通讯基类。继承这些基类是构建新工站的核心步骤。

## 硬件挂载抽象

```
AttachedDeviceBase<TParent>   ← 挂载基类（实现 IAttachedDevice<TParent>），统一 Parent / AttachTo / TryAttachTo
    ├─ BaseAxisDevice / BaseIODevice  ← : AttachedDeviceBase<IMotionCard>，代理运动/IO 指令到 ParentCard
    └─ BaseLineScanCamera             ← : AttachedDeviceBase<IFrameGrabberCard>，HasFrameGrabber 由 Parent 是否为空判定

BaseMotionCard      ← 厂商 SDK 封装（实现 IMotionCard），轴/IO 的父设备
BaseFrameGrabberCard ← 厂商 SDK 封装（实现 IFrameGrabberCard），线阵相机的父设备
```

`AttachedDeviceBase<TParent>` 把"挂载到父设备"的样板代码收敛到一处：子类继承时指定期望的父设备类型即可拿到类型安全的 `Parent` 属性，父设备类型不匹配时 `TryAttachTo` 返回 `false`（由 `HardwareManagerService` 记警告，不抛异常，避免一处配置写错父设备就让整机初始化中断）。

`BaseAxisDevice` / `BaseIODevice` 保留了 `ParentCard`（`=> Parent`）与 `AttachToCard(card)`（`=> AttachTo(card)`）两个领域别名，既有项目设备类重编译即可，无需改调用点：

```csharp
// 轴设备：只需提供 AxisIndex 和 AxisParam，运动命令全部代理到 ParentCard
public class MyAxis : BaseAxisDevice
{
    public override int AxisIndex => 0;
}

// IO 设备：提供 InputCount / OutputCount
public class MyIO : BaseIODevice
{
    public override int InPutCount  => 16;
    public override int OutPutCount => 16;
}
```

### BaseDevice — 所有硬件的根基类

```csharp
public class MyDevice : BaseDevice
{
    // 三个必须实现的钩子
    protected override Task<bool> InternalConnectAsync(CancellationToken token) { ... }
    protected override Task InternalDisconnectAsync() { ... }
    protected override Task InternalResetAsync(CancellationToken token) { ... }
}
```

- 自动 **3 次重试**连接（间隔 2 s）
- 内置**健康监控循环**（默认 1000 ms，仿真模式 ×5）
- `IsSimulation` 切换后调用 `ReloadAllAsync()` 生效

### BaseMotionCard — 运动控制卡

子类实现轴使能 / 归零 / 相对 / 绝对运动 / IO 读写等抽象成员。已内置实现：`LTDMCMotionCard`（雷赛）。

## 线扫视觉链路（线阵相机 + 图像采集卡）

把"线阵相机 + 图像采集卡"完整接进框架：契约在 `PF.Core`，厂商实现与编排模组在本层。

```
BaseFrameGrabberCard : BaseDevice, IFrameGrabberCard          — 与运动控制卡并列的宿主设备
    └─ HikFrameGrabberCard   — 海康采集卡：ApplyFrameControlAsync 下发帧长/帧超时/帧触发源/残帧策略；
                                DiscoverCamerasAsync 枚举挂在本卡上的相机

BaseLineScanCamera : AttachedDeviceBase<IFrameGrabberCard>, ILineScanCamera
    └─ HikLineScanCamera     — 海康线阵相机：ApplyConfigAsync 一次下发本体参数+行触发+编码器；
                                ArmAsync 开流武装、WaitFrameAsync 主动等帧、FrameReceived 连续预览
```

拓扑约定：采集卡的 `ParentDeviceId` 为空（第 1 层）；相机挂采集卡时 `ParentDeviceId` 填卡的 `DeviceId`（第 2 层，经采集卡取流）；相机 `ParentDeviceId` 留空即 GigE/USB 直连、帧控制回落到相机自身节点树——配置拓扑本身表达链路类型，不设额外的"链路类型"字段。

GenICam 支撑（`Hardware/Vision/Hikvision`）：`GenICamNodeAccessor`（节点读写/枚举/命令执行的统一实现，相机与采集卡共用）、`MvTypeMapper`（SDK 节点类型映射）、`GenICamNodeGlossary`（常用节点中文释义表，供调试面板属性树直接显示）、`MvSdkLifetime`（SDK 初始化/反初始化引用计数，多设备共存不会被先关闭的那个拖垮）。

`LineScanDetectionModule : BaseMechanism`（`[MechanismUI]`，框架级可复用模组，扫描轴与相机的 `DeviceId` 由注册处传入，一台设备多条扫描线时注册多个实例即可）：

```csharp
// ScanAsync 完成一次完整扫描：
// 校验配方 → 移到起点前的加速余量位 → 开流武装 → 启动扫描运动（不等待）
// → 到达扫描起点发帧触发 → 等一帧 → 等轴走完 → 停流
await lineScanModule.ScanAsync(profile, baseConfig, token);
```

开流必须早于轴运动（否则丢起始行），扫描区必须完整落在匀速段内（加减速段留在余量里）；编码器假定直连相机 IO，模组只保证"匀速走过扫描区"，不参与逐行同步。

> **部署要求**：本层引用海康工业相机 SDK（MVS）托管封装 `MvCameraControl.Net.dll`（随 DLL 目录分发，不打进 nupkg）。现场还需把 SDK `Libraries\win64` 下的原生库复制到输出目录；CameraLink/CXP/XoF 链路另装采集卡驱动 MVFG。不接线扫的项目不受影响——设备不在硬件配置里就不会被实例化，也不会加载 SDK。

## 光源控制器

```
BaseLightController : BaseDevice, ILightController
    ├─ CTSLightController     — 康视达（CtsAPI P/Invoke），SetLightValue / GetLightValue 均已实现
    ├─ HikComLightController  — 海康串口光源（基于 ISerialCommunication），写 S{通道字母}{4位亮度}#、读 S{通道字母}#
    └─ OPTLightController     — SetLightValue 已实现，GetLightValue 未实现（占位）
```

构造函数统一新增可选 `channelCount` 参数（默认 4），透传给基类的只读 `ChannelCount` 属性（实现 `ILightController.ChannelCount`）。`GetLightValue` 读取失败必须抛异常（不返回 0，避免调试页把滑块悄悄归零）；`SetLightValue` 失败只记 `Error` 日志、不外抛（工站流程里被直接 `await`，抛出会带停产线）。

## BaseMechanism — 机构基类

聚合多个硬件设备，统一管理生命周期。

```csharp
public class FeedMechanism : BaseMechanism
{
    private IAxis _xAxis;
    private IIOController _io;

    public FeedMechanism(string name, IHardwareManagerService hw,
                         IParamService param, ILogService log)
        : base(name, hw, param, log) { }

    protected override async Task InternalInitializeAsync(CancellationToken token)
    {
        // 在此处（而非构造函数）延迟解析硬件，按 DeviceId 取活跃设备再转型
        _xAxis = HardwareManagerService.GetDevice("XAxis") as IAxis;
        _io    = HardwareManagerService.GetDevice("MainIO") as IIOController;

        await _xAxis.HomeAsync(token);
        // 自动补全点表，不存在的点位会写入持久化存储
        EnsurePointsExist<FeedPointEnum>(_xAxis);
    }

    protected override Task InternalStopAsync() { ... }
}
```

- `RegisterHardwareDevice(device)` 自动订阅报警聚合 + 批量复位
- `WaitAxisMoveDoneAsync(axis, timeoutMs: 30_000)` — 50 ms 轮询等待轴到位
- `CheckReady()` — 防呆保护，未初始化时阻止动作

## StationBase\<T\> — 工站基类

内置 8 状态机（Stateless）+ `SemaphoreSlim(1,1)` 状态锁 + 取消式暂停。

```csharp
public class FeedStation : StationBase<FeedMechanism>
{
    // 正常生产循环（必须实现）
    protected override async Task ProcessNormalLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Step1_ScanBarcodeAsync(token);
            await Step2_PickMaterialAsync(token);
            await PauseCheckAsync(token);   // 检查暂停信号
        }
    }

    // 空跑循环（必须实现）
    protected override async Task ProcessDryRunLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(500, token);
        }
    }

    // 初始化钩子（推荐重写，不要重写 ExecuteInitializeAsync）
    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        await Mechanism.InitializeAsync(token);
    }

    // 复位钩子（只做硬件动作，基类自动路由 ResetDone / ResetDoneUninitialized）
    protected override async Task OnResetAsync(CancellationToken token)
    {
        await Mechanism.ResetAsync(token);
    }

    // 返回机构列表，框架自动注入 PauseCheckAsync
    protected override IEnumerable<BaseMechanism> GetMechanisms()
        => new[] { Mechanism };
}
```

> **关键**：触发 `Start` / `Resume` 必须用 `await FireAsync()`，确保旧任务彻底终止后才启动新任务。

### 机构报警自动桥接（WireMechanismAlarms）

无需再手写 `mechanism.AlarmTriggered += ...` 订阅样板：

```csharp
public MyStation(...) : base(...)
{
    _mechanism = ...;
    // 订阅 GetMechanisms() 返回的全部机构的 AlarmTriggered/AlarmAutoCleared，统一上抛为工站级报警
    WireMechanismAlarms();
}

// 如需把机构报警码转换为工站报警码，重写此钩子（默认原样透传）
protected override string? MapMechanismAlarmCode(MechanismAlarmEventArgs e) => e.ErrorCode;
```

幂等，子类构造函数末尾调用一次即可；`Dispose`/`DisposeAsync` 会自动对称解绑，无需手动清理。

## 通讯管理（ICommunication）

`TcpServer` / `TCPClient` / `FileTransferChannel` / `SerialPortCommunication`（基于 `System.IO.Ports`）/ `ModbusRtuMaster` / `ModbusTcpMaster` 均实现 `ICommunication`，接入统一的调试与管理体系（配合 `PF.Services.CommunicationManagerService` 与 `PF.Modules.Debug` 的通讯调试面板）。大文件传输通道支持多 Lane 并行、CRC32+xxHash64 校验、断线重连。

### Modbus RTU/TCP 主站

`ModbusRtuMaster`（直接持有 `SerialPort`）/ `ModbusTcpMaster`（直接持有 `TcpClient`/`NetworkStream`）共用内部 `ModbusMasterBase` 实现 8 个标准功能码；协议编解码（PDU 构建/解析、CRC16、MBAP 头）下沉在 `Internal` 命名空间。

- **断线自动重连**：`AutoReconnect`（默认 true）/ `ReconnectIntervalMs`（默认 5000 ms），CAS 防重入保证只有一个重连循环；主动 `CloseAsync`/`DisconnectAsync` 会先取消重连再拆连接。
- **TCP 帧失步自愈**：MBAP 头校验 `ProtocolId==0` 且 `Length∈[2,254]`，不满足则滑动窗口向前扫描重新对齐（最多 64 字节），不会因为一次错位就直接断连。
- **RTU 静默判帧**：无长度前置声明时按报文间隔判定一帧结束（窗口取 10 个字符时间与 30 ms 的较大者），响应完整性由 CRC 兜底。
- `SendRawAsync` / `BuildFrame`：原始 PDU 透传（不校验功能码回显、异常响应原样返回）与报文预览（与实发帧逐字节一致），供框架未覆盖的功能码或调试面板复用。

## 硬件 SDK 集成

已内置封装基恩士（Keyence）智能相机 SDK、海康（Hikvision）扫码枪 SDK、海康工业相机 SDK（MVS，线阵相机 + 图像采集卡）的设备实现，可直接通过 `IHardwareManagerService.RegisterFactory` 注册使用。

海康扫码枪有两条实现路径，新开发请选 `MvCodeReaderBarcodeScan`：

| 实现 | 路径 | 状态 |
|---|---|---|
| `MvCodeReaderBarcodeScan` | `Hardware/BarcodeScan/Hikvision/` | **推荐**——官方 MvCodeReaderSDK.Net 托管封装，支持图像采集 |
| `HKBarcodeScan` | `Hardware/BarcodeScan/HKRobot/` | 已弃用（`[Obsolete]`，仅警告不报错）——TCP 透传协议版，现网已部署配置继续可用，不建议新项目使用 |

## BaseMasterController — 主控编排基类

```csharp
public class MyController : BaseMasterController
{
    public MyController(IEnumerable<IStation> stations, ...)
        : base(stations, ...)
    {
        // 注册跨工站同步信号量
        _syncService.Register("AllowFeed",    initialCount: 0);
        _syncService.Register("FeedComplete", initialCount: 0);
    }

    protected override void OnAfterResetSuccess()
    {
        // 复位完成后重置所有信号量
        _syncService.ResetAll(initialCount: 0);
    }
}
```

- 并行初始化所有工站（最大并发 4，超时 120 s）
- 防撕裂守卫：子站意外跌落 Uninitialized 时全局报警
