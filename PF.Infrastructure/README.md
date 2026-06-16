# PF.Infrastructure

PF.AutoFramework 底层基础设施层，提供三层硬件抽象（运动卡 / 轴 / IO）、机构基类、工站状态机、主控编排基类及 TCP 通信基类。继承这些基类是构建新工站的核心步骤。

## 三层硬件抽象

```
BaseMotionCard    ← 厂商 SDK 封装（实现 IMotionCard）
    ↓ AttachToCard()
BaseAxisDevice    ← 伺服轴，代理到 ParentCard（实现 IAxis）
BaseIODevice      ← 数字 IO，代理到 ParentCard（实现 IIOController）
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

子类实现 21 个抽象成员（4 属性 + 17 方法），包括轴使能 / 归零 / 相对 / 绝对运动 / IO 读写。已内置实现：`LTDMCMotionCard`（雷赛）。

### BaseAxisDevice / BaseIODevice

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
        // 在此处（而非构造函数）延迟解析硬件
        _xAxis = GetDevice<IAxis>("XAxis");
        _io    = GetDevice<IIOController>("MainIO");

        await _xAxis.HomeAsync(token);
        // 自动补全点表，不存在的点位会写入持久化存储
        EnsurePointsExist<FeedPointEnum>(_xAxis);
    }

    protected override Task InternalStopAsync() { ... }
}
```

- `RegisterHardwareDevice()` 自动订阅报警聚合 + 批量复位
- `WaitAxisMoveDoneAsync(axis, timeout: 30s)` — 50 ms 轮询等待轴到位
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
