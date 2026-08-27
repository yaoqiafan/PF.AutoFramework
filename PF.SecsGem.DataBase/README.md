# PF.SecsGem.DataBase

PF.AutoFramework SECS/GEM 协议专用数据库层（当前版本 1.0.3），基于 EF Core + SQLite，提供 SECS/GEM 配置数据的实体、`DbContext` 及工作单元式仓储。主程序与后台 Windows 服务 `PF.SecsGem.Service` **共享同一数据库文件**。

## 数据库文件位置

```
D:\PFConfig\PFAutoFrameWork\{项目名}\SecsGemConfig.db
```

按项目名隔离；同一台机器装了多个项目时各自互不覆盖（详见仓库 `CLAUDE.md`「配置路径与项目隔离」一节）。

## DI 注册：由 `PFApplicationBase` 自动完成

`ISecsGemDataBase` 由 `PF.Application.Base`（`PFApplicationBase.RegisterSecsGemServices`）在应用启动时自动 `RegisterSingleton`，项目侧不需要在自己的 `App.xaml.cs` 里手写注册。不用 SECS/GEM 的项目重写 `UsesSecsGemService => false` 即可整体跳过这段 DI 与 `SecsGemConfig.db` 的创建。

## 核心类型：工作单元（UoW）模式

```
ISecsGemDataBase          // 单例入口，持有 DbContextOptions
  └─ BeginScope()  ───►   ISecsGemDbScope : IDisposable   // 短生命周期作用域，持有一个独立 DbContext
        ├─ GetRepository<T>(SecsDbSet dbSet)   // T 需 class + IEntity + 无参构造；多次调用共享同一 ChangeTracker
        └─ SaveChangesAsync()                   // 一次性提交本作用域内的所有变更
```

`SecsGemDataBaseManger`（`ISecsGemDataBase` 实现）本身不持有长生命周期 `DbContext`——每次 `BeginScope()` 都新建一个短生命周期 `DbContext` + 仓储缓存，多线程各自隔离，从根上避免 `DbContext` 的线程不安全问题。`InitializationDataBase()` 同样走独立短 `context`，不经共享作用域。

```csharp
using var scope = db.BeginScope();
var repo = scope.GetRepository<VIDEntity>(SecsDbSet.VIDs);
await repo.AddAsync(entity);
await scope.SaveChangesAsync();
```

## ⚠️ 破坏性变更（v1.0.3，2026-07-07）：从旧版直连仓储迁移到 UoW

旧接口 `ISecsGemDataBase.GetRepository()` / `SaveChangesAsync()` 已删除，不再编译；改为上面的 `BeginScope()` 工作单元模式。下游 `PF.Modules.SecsGem` 已同步。若二次开发时还留有旧调用，需要照上面的写法改造。此约定至今没有变化，是当前唯一的数据访问方式。

## `SecsDbSet` 枚举 ↔ 实体 ↔ 数据表

`GetRepository<T>` 的 `dbSet` 参数必须与实体类型 `T` 一一对应（枚举定义在 `PF.Core.Enums`）：

| `SecsDbSet` | 实体类型 | 说明 |
|---|---|---|
| `SystemConfigs` | `SecsGemSystemEntity` | 服务名/自启/T3~T8 超时/心跳间隔/IP·端口/DeviceID/MDLN/SOFTREV 等系统级配置，单条记录 |
| `VIDs` | `VIDEntity` | 变量定义：`Code`（VID）、`Description`、`Comment`、`Type`（`PF.Core.Enums.DataType` 的字符串形式）、`Value` |
| `CEIDs` | `CEIDEntity` | 事件定义：`Code`（CEID）、`LinkReportCode`（关联的 ReportID 数组）、`Key` |
| `ReportIDs` | `ReportIDEntity` | 报表定义：`Code`（ReportID）、`LinkVID`（关联的 VID 数组，`uint[]`） |
| `CommnadIDs`（枚举成员拼写如此，未订正） | `CommandIDEntity` | RCMD 命令定义：`Code`、`RCMD`、`LinkVID`、`Key` |
| `IncentiveCommands` | `IncentiveEntity` | 主动命令（S/F 报文模板）：`Stream`、`Function`、`Name`、`Key`、`JsonMessage`、`ResponseID` |
| `ResponseCommands` | `ResponseEntity` | 应答命令（S/F 报文模板）：`Stream`、`Function`、`Name`、`Key`、`JsonMessage` |

所有实体都继承自 `BasicEntity`：`ID`（`string`，默认 `Guid.NewGuid()`）、`CreateTime`、`UpdateTime`、`Remarks`。每个实体都配了一套 `ToXxx()` / `ToEntity()` 扩展方法，在 `PF.Core` 的运行时模型（`VID`/`CEID`/`ReportID`/`CommandID`/`SFCommand`/`SecsGemSystemParam`）与数据库实体之间转换；旧方法名 `GetXxxFormXxx()` 仍保留但已标 `[Obsolete]`，新代码请用 `ToXxx()`。

## 已知的历史坑（v1.0.2 修复过一次）

`ReportIDEntity.LinkVID` 若来自迁移前的旧数据可能是 `null`——EF Core 会把空数组序列化成 `NULL`，反序列化回来时不再是 `uint[0]`。`ToReportID()` 已加 null 保护，但如果你在仓储层之外直接读这个字段做校验，也要自己判空，避免构造 `ReportID` 时因 null 数组崩溃。

## 使用说明

- 主程序与 `PF.SecsGem.Service` 通过同一 SQLite 文件共享配置：主程序改了配置后，服务需要重启（或自行监听文件变更）才会生效。
- SQLite WAL 模式支持一写多读，但写入仍建议串行化，避免两侧同时写同一表。
- 通用仓储方法见 `IGenericRepository<T>`（`PF.Core`）：`GetByIdAsync` / `GetAllAsync` / `FindAsync(predicate)` / `SingleOrDefaultAsync(predicate)` / `AddAsync` / `AddRangeAsync` / `UpdateAsync` / `RemoveAsync` / `CountAsync` / `AnyAsync`，均是 `ISecsGemDbScope.GetRepository<T>()` 返回对象上的方法。
