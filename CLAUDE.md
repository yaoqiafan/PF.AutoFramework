# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目定位

**PF.AutoFramework** 是面向工业自动化控制（半导体制造、多工位流水线 OCR 检测）的 .NET 8 WPF 企业框架。内置 Demo 工站 `PF.WorkStation.AutoOcr` 展示双工位五站流水线 + SECS/GEM 协议完整实现。

---

## 构建命令

```powershell
dotnet restore
dotnet build -c Release --no-restore
dotnet pack  -c Release --no-build -o nupkg   # 打包为 NuGet

publish-only.bat   # Clean → Restore → Build → Pack（三步自动化）
push-only.bat      # 推送 nupkg 到私有 BaGet 服务器 https://nuget.powerfocus.com.cn/api/v2/package（推送地址与 API Key 见脚本内 $PUSH_URL / $API_KEY，勿在此重复记录以免失步）
```

无单元测试项目，运行验证依赖 Shell 启动 + Debug/Logging 模块。

---

## 七层单向架构

```
/00. Global Config    — Directory.Build.props / Common.props / Directory.Packages.props
/01. Foundation       — PF.Core（42 接口，零外部依赖）/ PF.Infrastructure（硬件/模组/工站基类）
/02. UI               — PF.UI.Controls / PF.UI.Infrastructure / PF.UI.Resources / PF.UI.Shared
/03. Data             — PF.Data（EF Core + Repository）/ PF.SecsGem.DataBase
/04. Tools/Services   — PF.CommonTools / PF.Services（8 个服务实现）
/05. Modules          — Alarm / Debug / Identity / Logging / Parameter / ProductionRecord / SecsGem
/06. Application      — PF.Application.Shell（WPF 入口，全量 DI 注册）
/07. Workstation      — PF.WorkStation.AutoOcr + AutoOcr.UI（完整 Demo）
/08. External         — PF.SecsGem.Service（Windows 后台服务）
/09. MetaPackage      — PF.AutoFramework.Meta（NuGet 元包）
```

**关键约束**：`PF.Infrastructure` 零 Prism 依赖，Prism EventAggregator 桥接在 `App.xaml.cs` 中完成。上层只能依赖下层。

---

## 核心设计模式

### 三层硬件抽象（模板方法模式）

```
BaseMotionCard    ← 厂商 SDK 封装，21 个抽象成员（4 属性 + 17 方法）+ 3 个 BaseDevice 钩子
    ↓ AttachToCard()
BaseAxisDevice    ← 代理到 ParentCard，子类只需提供 AxisIndex / Param
BaseIODevice      ← 代理到 ParentCard，WaitInputAsync 以 20ms 轮询，泛型枚举重载
```

- **BaseDevice**：3 次重试连接（间隔 2s），健康监控循环（默认 1000ms，仿真模式 ×5），抽象钩子 `InternalConnectAsync` / `InternalDisconnectAsync` / `InternalResetAsync`
- **BaseMechanism**：聚合多硬件，`RegisterHardwareDevice()` 自动订阅报警聚合 + 批量复位，内置 `WaitAxisMoveDoneAsync`（50ms 轮询，30s 超时）/ `EnsurePointsExist<TEnum>(IAxis)`（点表自动补全 + 持久化）/ `CheckReady()`（防呆保护）
- **StationBase\<T\>**：8 状态机（Stateless）+ `SemaphoreSlim(1,1)` 状态锁 + `CancellationTokenSource _runCts` 取消式暂停，子类实现 `ProcessNormalLoopAsync` / `ProcessDryRunLoopAsync`
- **BaseMasterController**：编排所有 `IStation`，并行初始化（最大并发度 4），初始化超时 120s，复位超时 30s，防撕裂守卫（子站意外跌落 Uninitialized 时全局报警）

### 8 状态机（所有工站 + 主控共用）

```
Uninitialized ──(Initialize)──► Initializing ──(InitializeDone)──► Idle
                                      │(Error)                        │(Start)
                                   InitAlarm                        Running
                                      │(Reset)               (Pause)↕ ↕(Resume)
                                   Resetting ◄──(Reset)──── RunAlarm  Paused
                                      ├──(ResetDone)──────────────── Idle
                                      └──(ResetDoneUninitialized)── Uninitialized
```

触发器（10 个）：`Initialize` / `InitializeDone` / `Start` / `Pause` / `Resume` / `Stop` / `Error` / `Reset` / `ResetDone` / `ResetDoneUninitialized`

> **关键**：触发 `Start` / `Resume` 必须用 `await FireAsync()`，确保旧任务彻底终止后才启动新任务。

### 自动发现特性

| 特性 | 作用目标 | 消费者 |
|------|----------|--------|
| `[ModuleNavigation]` | 侧边栏菜单项（ViewName/Title/GroupName/Icon/Order） | `NavigationMenuService` |
| `[MechanismUI]` | 调试面板自动注册 | `MechanismDebugViewModel` |
| `[StationUI]` | 工站调试面板自动注册 | `StationDebugViewModel` |
| `[AlarmInfo]` | 报警码元数据（反射扫描） | `AlarmDictionaryService` |
| `[ParamView]` | 参数视图路由 | `ParameterModule` |

> **关键约束**：特性中的 `ViewName` 必须与 `RegisterForNavigation` 的 key 完全一致，否则导航失败。

---

## PF.Core 接口全览（42 个，零外部依赖）

**硬件设备**：`IHardwareDevice` / `IMotionCard` / `IAttachedDevice` / `IAxis` / `IIOController` / `ILightController` / `IBarcodeScan` / `IIntelligentCamera` / `IHardwareManagerService`

**机构 & 工站**：`IMechanism` / `IStation` / `IMasterController`

**服务**：`IParamService` / `ILogService` / `IAlarmService` / `IAlarmDictionaryService` / `IAlarmEventPublisher` / `IUserService` / `IProductionDataService` / `IRecipeService<T>` / `ITowerLightService` / `ITowerLightDoWriter` / `IAppTimerService` / `IStationSyncService`

**数据**：`IEntity` / `IGenericRepository<T>` / `IDataBase` / `IParamRepository`

**通信**：`IServer` / `IClient` / `IClientConnection`

**SECS/GEM**：`ISecsGemManager` / `ICommandManager` / `ISFCommand` / `ISecsGemDataBase` / `IParams` / `IinternalClient` / `ISecsGemMessageUpdater`

**关键枚举**：
- `MachineState`（8 个）/ `MachineTrigger`（10 个）/ `OperationMode`（Normal / DryRun）
- `HardwareCategory`（General / Axis / IOController / Camera / Robot / Scanner / Instrument / MotionCard / LightController）
- `UserLevel`（Null=-1 / Operator=0 / Engineer=1 / Administrator=2 / SuperUser=3）

---

## Shell 启动序列（App.xaml.cs）

1. `RunningInstance()` — 全局互斥锁，单实例保护
2. `CreateShell()` → `PerformInitializationAsync()` — 加载配置，Splash 进度驱动硬件初始化
3. `RegisterTypes()` — 全量 DI 注册（~450 行）：

   **硬件工厂**（6 种）：
   - `LTDMCMotionCard` — 雷赛运动控制卡（`CardIndex`）
   - `EtherCatAxis` — EtherCAT 伺服轴（`AxisIndex` / `AxisParam` JSON）
   - `EtherCatIO` — EtherCAT 数字 IO（`InPutCount` / `OutPutCount`）
   - `HKBarcodeScan` — 海康条码扫描仪（`IP` / `TiggerPort` / `UserPort` / `TimeOutMs`）
   - `KeyenceIntelligentCamera` — 基恩士 OCR 相机（`IP` / `TiggerPort` / `TimeOutms`）
   - `CTS_LightControoller` — CTS 三色灯（`COM` 串口）

   **机构**（7 个，DryIoc 多键注册）：`WS1FeedingModel` / `WS2FeedingModel` / `WSDetectionModule` / `WS1MaterialPullingModule` / `WS2MaterialPullingModule` / `WSDataModule` / `WSSecsGemModule`

   **工站**（5 个）：`WS1FeedingStation` / `WS2FeedingStation` / `WSDetectionStation` / `WS1MaterialPullingStation` / `WS2MaterialPullingStation`

   **单例服务**：`ILogService` / `IParamService` / `IHardwareManagerService` / `IAlarmService` / `IAlarmDictionaryService` / `IAlarmEventPublisher` / `IProductionDataService` / `IUserService` / `IAppTimerService` / `IStationSyncService` / `IHardwareInputMonitor` / `ITowerLightService` / `IMasterController`（`AutoOCRMachineController`）/ `IRecipeService<OCRRecipeParam>`

4. `ConfigureModuleCatalog()` — 8 个 Prism 模块：`AlarmModule` / `LoggingModule` / `ParameterModule` / `IdentityModule` / `DebugModule` / `AutoOcrUIModule` / `SecsGemModule` / `ProductionRecordModule`

5. `OnInitialized()` — 静默登录 SuperUser，Prism EventAggregator 桥接，启动 `AppTimerService`，注册日常定时任务（磁盘告警 08:00 / 图片清理 08:00）

**Prism 事件桥接**（解耦 Infrastructure 与 Prism）：
```
HardwareResetRequestedEvent → BaseMasterController.OnHardwareResetRequested()
SystemResetRequestedEvent   → IMasterController.RequestSystemResetAsync()
MasterStateChanged (event)  → MachineStateChangedEvent
ReinitializationRequired    → ReinitializeRequiredEvent
```

---

## 服务层关键细节

| 服务 | 关键特性 |
|------|---------|
| **HardwareManagerService** | `RegisterFactory(className, factory)` 注册，`LoadAndInitializeAsync()` 拓扑排序（父卡优先），`SetGlobalSimulationModeAsync()` 原子切换仿真模式，需配合 `ReloadAllAsync()` 热重载生效 |
| **ParamService** | `RegisterParamType<TEntity, TModel>()` 显式类型映射（注意泛型顺序：实体在前、模型在后）；值未变更跳过写库；`ParamChanged` 事件携带审计信息（旧值/新值/用户/时间）；泛型约束 `where T : class`，不可直接用值类型 |
| **AlarmService** | 复合键 `(Source, ErrorCode)` 幂等触发；`Channel<PersistJob>`（容量 10000）异步持久化；按年动态路由表 `AlarmRecord_{Year}` |
| **LogService** | 内存循环缓冲 1000 条；`Channel` 异步文件写入；按小时滚动，30 天自动清理 |
| **StationSyncService** | 命名信号量跨工站协同：`WaitAsync(name, token)` / `Release(name)` / `ResetSingleSignal(name, initialCount)` |
| **ProductionDataService** | 泛型 `RecordAsync<TData>()` POCO → JSON 非阻塞写入；`DataRecorded` 事件实时订阅；支持 Excel（NPOI）/ CSV 导出 |

---

## 配置路径与项目隔离

配置根目录 `D:\PFConfig\PFAutoFrameWork\`（`ConstGlobalParam.ConfigRoot`），实际配置全部落在**项目子目录** `{ConfigRoot}\{ProjectName}\`（`ConstGlobalParam.ConfigPath`）下。

```
D:\PFConfig\PFAutoFrameWork\
├─ PF.Application.Shell\        ← 项目 A（= 入口程序集名）
│   ├─ SystemParamsCollection.db / ProductionHistory.db / AlarmHistory.db / SecsGemConfig.db
│   ├─ user.config / timer_schedule.json / log_config.json / SecsGem*.xlsx
│   └─ Recipe\ AxisPoints\ DataMemory\ Vision\ StationMemoryParam\
└─ ProjectB.Shell\              ← 项目 B
```

**为什么要隔离**：`AppParamDbContext.EnsureDefaultParametersCreatedAsync` 会删除"不在本项目默认参数集里"的参数行（废弃参数清理）。同机换项目运行时，A 的参数在 B 眼里全是废弃行，四张表会被整批清空。`Recipe\` / `AxisPoints\` / `user.config` 同样存在同名互相覆盖问题。

**项目名来源**：
- 主程序：`PFApplicationBase.ProjectName` 虚属性，默认取入口程序集名，在**实例构造函数**中调用 `ConstGlobalParam.Initialize()`（必须早于任何触碰 `ConfigPath` 的代码）
- `PF.SecsGem.Service`：独立进程，从自身 `appsettings.json` 的 `ProjectName` 键读取，该键由 `Build-Installer.ps1` 在构建安装包时注入
- 安装器解析顺序（`Build-Installer.ps1` 第 1c 节）：`installer.conf` 的 `PROJECT_NAME` → **主程序源码里 `override string ProjectName => "xxx"` 的字面量** → `MAIN_EXE` 去掉 `.exe`。重写了 `ProjectName` 的项目**无需**再改 `installer.conf`；若两者都填且不一致，构建期直接失败并指出两边的值

**关键约束**：
- `ConstGlobalParam.ConfigPath` 未初始化时**直接抛 `InvalidOperationException`**，不回退根目录——漏调 `Initialize` 的宿主必须在首次运行时暴露
- 因此**禁止**在静态字段初始化器中捕获 `ConfigPath`（会先于 `Initialize` 求值抛 `TypeInitializationException`），必须写成 `=>` 表达式属性
- 项目若重写 `ProjectName`，必须同步设置 `installer.conf` 的 `PROJECT_NAME`，否则服务指向另一个目录的 `SecsGemConfig.db`（启动时 `VerifySecsServiceProjectBinding` 会弹窗告警）
- SECS 服务名固定为 `SecsGemService`，**不按项目区分**：本机通道端口 6800 在 `Worker.LocationServer` 与 `InternalClient` 两端硬编码，HSMS 被动端口也只有一个，全机只能有一个实例真正工作；按项目注册多个 `start= auto` 服务只会让它们开机争抢 6800。一机多项目时服务归属最后安装的项目，主程序启动时解析 SCM 中**实际注册**的 `ImagePath`（而非本项目安装目录）来校验归属
- 服务读不到 `ProjectName` 时**拒绝启动**并写 EventLog + `D:\PF_Logs\SecsGem\Service\startup-error.log`（不回退根目录：各设备 `SecsGemConfig.db` 内容不同，回退会让服务用空库与 Host 建链，外部看是"正常运行"）
- 老版本升级必须先执行 `Installer\Migrate-Config.ps1`，详见 `Installer\现场升级迁移说明.md`

**4 个独立 SQLite 库**（均位于项目子目录下）：

| 文件 | DbContext | 用途 |
|------|-----------|------|
| `SystemParamsCollection.db` | `AppParamDbContext` | 参数、用户凭证、硬件配置、通讯配置（JSON 字段） |
| `ProductionHistory.db` | `ProductionDbContext` | 生产数据记录（按 RecordType 索引） |
| `AlarmHistory.db` | `AlarmDbContext` | 报警历史（年度分表 `AlarmRecord_{YYYY}`，`AlarmModelCacheKeyFactory` 驱动 EF Core 模型缓存） |
| `SecsGemConfig.db` | `SecsGemDbContext` | SECS/GEM 配置（主程序 + Windows 服务共享同一文件） |

**日志路径**（与配置路径无关，不按项目隔离）：主程序 `D:\PF_Logs\`（`LogConfiguration.CreateDefault`）| SECS/GEM 服务 `D:\PF_Logs\SecsGem\Service\`（报文日志在其下 `Protocol\`）

---

## Demo 工站（AutoOCR）

**5 个工站 + 7 个模组，双工位流水线**：

| 工站 | 步序数 | 核心功能 |
|------|--------|---------|
| `WS1FeedingStation` | 44 步 | 批次验证 → 配方加载 → 晶圆盒检测 → 层位扫描 → 逐层取料 |
| `WS2FeedingStation` | 44 步 | 工位二对称实现 |
| `WSDetectionStation` | 28 步 | `Task.WhenAny` 双工位竞争 → 龙门运动 → OCR 拍照 → 数据比对 |
| `WS1MaterialPullingStation` | 40 步 | 取料 → 条码扫描 → 送检 → 回料 |
| `WS2MaterialPullingStation` | 40 步 | 工位二对称实现 |

**工站间同步信号**（`IStationSyncService` 命名信号量，枚举 `WorkstationSignals`）：
工位1/2 各含：启动按钮按下 / 允许拉料 / 拉料完成 / 允许退料 / 退料完成 / 允许检测 / 检测完成 / 人工下料完成 / 拉料复位完成，加上`检测模组复位完成`，共 19 个信号。

**主控**：`AutoOCRMachineController` 继承 `BaseMasterController`，协调五站七模组联动，内含安全门监控（`IHardwareInputMonitor`）+ 参数变更触发重初始化检测。

---

## NuGet 配置

两个包源（`nuget.config`）：`nuget.org`（公共）+ 私有 BaGet `https://nuget.powerfocus.com.cn/v3/index.json`（源名 `PowerFocus_NuGet`）。
**所有包版本在 `Directory.Packages.props` 中央管理，各 `.csproj` 不得指定版本号。**

核心版本：Prism 9.0.537（其容器实现 `DryIoc.dll` 5.4.3 由 Prism.DryIoc 传递引入，无需显式声明）/ EF Core 9.0.18 / Stateless 5.20.1 / log4net 3.3.2 / NPOI 2.7.6 / Microsoft.Extensions.* 10.0.10 / System.Drawing.Common、System.IO.Ports、System.IO.Hashing、System.ServiceProcess.ServiceController 10.0.10

两处版本天花板（升级时注意）：
- **EF Core 停在 9.x**：10.0.x 起 lib 仅 `net10.0`，当前 `net8.0` 目标无法引用，要升 EF 10 必须先改 TFM。
- **NPOI 停在 2.7.6**：2.8.0 起二进制包改用 OSMF EULA（年营收 ≥ 1 万美元的商业用户需按月付维护费，且须在 csproj 显式写 `AcceptNPOIOSMFLicense=true`），2.7.6 是最后的纯 Apache-2.0 稳定版。

全局编译配置（`Directory.Build.props`）：`net8.0` / `Nullable=enable` / `ImplicitUsings=enable` / 抑制 ~30 个 CS8xxx + CA1416 告警。WPF 项目额外继承 `Common.Desktop.props`（`net8.0-windows` / `UseWPF=true`）。

---

## 新建工站的最小步骤

1. 继承 `BaseMechanism`，实现 `InternalInitializeAsync` / `InternalStopAsync`，构造函数签名 `(string name, IHardwareManagerService, IParamService, ILogService)`；在 `InternalInitializeAsync` 中延迟解析设备（禁止在构造函数中调用 `GetDevice`）
2. 继承 `StationBase<T>`：
   - **必须**实现 `ProcessNormalLoopAsync`（正常循环）和 `ProcessDryRunLoopAsync`（空跑循环），均以 `while (!token.IsCancellationRequested)` 驱动
   - **推荐**重写 `OnInitializeAsync(token)` 和 `OnResetAsync(token)` 钩子（**不要**重写 `ExecuteInitializeAsync` / `ExecuteResetAsync`，**不要**在钩子内调用 `Fire`）
   - **推荐**重写 `GetMechanisms()` 返回 `IEnumerable<BaseMechanism>`，框架自动注入 `PauseCheckAsync`
   - **可选**重写 `OnPhysicalStopAsync()` 切断危险源
3. 继承 `BaseMasterController`，在构造函数中注册 `IStationSyncService` 信号量，重写 `OnAfterResetSuccess()` 重置信号量
4. 在 `App.xaml.cs` `RegisterTypes()` 中：注册硬件工厂 → 注册机构（DryIoc ServiceKey）→ 注册工站（Singleton）→ 注册主控
5. 在 `ConfigureModuleCatalog()` 中添加 UI 模块
6. 在 UI 模块中 `RegisterForNavigation<View, ViewModel>(key)` 注册视图，key 必须与 `[MechanismUI]` / `[StationUI]` 特性中的 `ViewName` 完全一致

---

## 并发安全规则

- 状态机转换：`SemaphoreSlim(1,1)` 保护所有 `Fire()` / `FireAsync()` 调用
- 工站循环暂停：`CancellationTokenSource _runCts` 取消式暂停，`CancelAndAwaitOldTaskAsync` 确保旧任务彻底终止
- 报警原子性：`Volatile.Write` + `Interlocked.Exchange` 防撕裂
- 主控硬件输入门：`SemaphoreSlim _hardwareOpGate` 非阻塞 `WaitAsync(0)` 防止并发硬件操作
- DbContext：Scoped 注册（每请求一个），工厂层 `ConcurrentDictionary` 缓存配置

---

## 常见开发陷阱

| 问题 | 原因 | 解决 |
|------|------|------|
| Start 后旧任务仍在运行 | 用了同步 `Fire()` 触发 Running | **必须用 `await FireAsync()`** |
| 机构初始化取不到设备 | 构造函数调用了 `GetDevice` | 在 `InternalInitializeAsync` 延迟解析 |
| View 导航失败 | key 不匹配 | 检查特性 ViewName 与 `RegisterForNavigation` key |
| 仿真模式切换未生效 | 仅改配置未重载 | 切换后调用 `ReloadAllAsync()` |
| 信号量死锁 | ResetAll 时线程未停 | 先 `StopAllAsync()` 再 `ResetAllAsync()` |
| 点表丢失 | 未调用 EnsurePointsExist | `InternalInitializeAsync` 末尾调用 `EnsurePointsExist<TEnum>(axis)` |
| `IParamService` 参数读取返回 null | 直接用值类型泛型参数 | 封装为 POCO 类（泛型约束 `where T : class`） |
| 工站抽象方法找不到 | API 已更新 | `ProcessLoopAsync` → `ProcessNormalLoopAsync` / `ProcessDryRunLoopAsync` |
| 启动抛 `TypeInitializationException` | 静态字段初始化器捕获了 `ConfigPath` | 改成 `=>` 表达式属性，延迟到 `Initialize` 之后求值 |
| `ConfigPath` 抛"尚未初始化" | 宿主没继承 `PFApplicationBase`（工具、测试宿主） | 显式调用 `ConstGlobalParam.Initialize(项目名)` |
| 换项目后参数全没了 | 老布局未迁移，或两个项目共用了同一个配置目录 | 执行 `Installer\Migrate-Config.ps1`，确认各项目 `ProjectName` 不同 |
| SecsGem 服务起不来 | `appsettings.json` 缺 `ProjectName` 键 | 看 `D:\PF_Logs\SecsGem\Service\startup-error.log`，补键后重启服务 |
| 重写了 `ExecuteInitializeAsync` 手动调 `Fire` | 应重写钩子不是入口 | 改为重写 `OnInitializeAsync` / `OnResetAsync`，基类自动管理状态机 |
| 复位后状态机不前进 | 在 `OnResetAsync` 里调了 `ResetAlarm()` / `Fire` | `OnResetAsync` 内只做硬件动作，基类自动路由 `ResetDone` / `ResetDoneUninitialized` |
| 暂停后循环无法被打断 | 使用了不存在的 `_pauseEvent` | 暂停是 CancellationToken 取消式，循环用 `while (!token.IsCancellationRequested)` |
| `GetMechanisms` 编译报错 | 返回类型是 `IEnumerable<BaseMechanism>` 非接口 | 改为 `IEnumerable<BaseMechanism>`，只能返回 `BaseMechanism` 子类 |
| `StationSyncService.Reset` 不存在 | API 已更新 | 用 `ResetSingleSignal` / `ResetScope` / `ResetAll` |
