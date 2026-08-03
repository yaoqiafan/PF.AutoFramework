# PF.SecsGem.DataBase

PF.AutoFramework SECS/GEM 协议专用数据库层，基于 EF Core + SQLite，提供 SECS/GEM 配置数据的实体、DbContext 及仓储。主程序与后台 Windows 服务（`PF.SecsGem.Service`）**共享同一数据库文件**。

## 数据库文件位置

```
D:\PFConfig\PFAutoFrameWork\{项目名}\SecsGemConfig.db
```

## 主要类型

| 类型 | 说明 |
|---|---|
| `SecsGemDbContext` | EF Core DbContext，管理 SECS/GEM 所有配置表 |
| `ISecsGemDataBase` | 数据库访问入口（供主程序和 Windows 服务共用），持有 `DbContextOptions` 单例 |
| `ISecsGemDbScope` | 工作单元作用域（`BeginScope()` 返回），`IDisposable` |
| `SecsGemDataBaseManger` | `ISecsGemDataBase` 实现 |

## ⚠️ 破坏性变更（v1.0.3）：工作单元(UoW)模式

`ISecsGemDataBase.GetRepository()`/`SaveChangesAsync()` 已删除，改为 `BeginScope()` 返回的作用域承担：每次 `BeginScope()` 创建一个独立的短生命周期 `DbContext` + 仓储缓存，多线程调用各自隔离，避免 `DbContext` 的线程不安全问题。

```csharp
// 旧用法（已删除，不再编译）
var repo = db.GetRepository<SecsGemVariableEntity>();
await repo.AddAsync(entity);
await db.SaveChangesAsync();

// 新用法：BeginScope 工作单元
using var scope = db.BeginScope();
var repo = scope.GetRepository<SecsGemVariableEntity>(SecsDbSet.Variables);
await repo.AddAsync(entity);
await scope.SaveChangesAsync();   // 一次性提交本作用域内的所有变更
```

`GetRepository<T>(SecsDbSet dbSet)` 需要传入 `SecsDbSet` 枚举指定目标表；同一作用域内多次调用返回绑定同一 `DbContext` 的仓储，共享同一个 ChangeTracker。

## DI 注册

```csharp
// 主程序 App.xaml.cs
containerRegistry.RegisterSingleton<ISecsGemDataBase, SecsGemDataBaseManger>();
```

## 主要实体

```csharp
// SECS/GEM 通信配置
public class SecsGemConnectionEntity : IEntity
{
    public int    Id          { get; set; }
    public string DeviceId    { get; set; } = "";
    public string IpAddress   { get; set; } = "";
    public int    Port        { get; set; }
    public bool   IsActive    { get; set; }
}

// 变量定义（SVID / DVVAL / ECID 等）
public class SecsGemVariableEntity : IEntity
{
    public int    Id          { get; set; }
    public string VariableId  { get; set; } = "";
    public string Name        { get; set; } = "";
    public string DataType    { get; set; } = "";
    public string DefaultValue{ get; set; } = "";
}
```

## 使用说明

主程序与 Windows 服务通过同一 SQLite 文件共享配置，因此：

- 主程序修改配置后，服务需重启或监听文件变更才能生效
- 写操作避免并发（SQLite WAL 模式支持一写多读，但写入需串行化）

```csharp
// 读取连接配置
using var readScope = _secsGemDb.BeginScope();
var configs = await readScope.GetRepository<SecsGemConnectionEntity>(SecsDbSet.Connections)
    .GetAllAsync();

// 新增变量定义
using var writeScope = _secsGemDb.BeginScope();
await writeScope.GetRepository<SecsGemVariableEntity>(SecsDbSet.Variables).AddAsync(new SecsGemVariableEntity
{
    VariableId   = "SVID_1",
    Name         = "MachineName",
    DataType     = "A",
    DefaultValue = "AutoOCR"
});
await writeScope.SaveChangesAsync();
```
