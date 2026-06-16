# PF.SecsGem.DataBase

PF.AutoFramework SECS/GEM 协议专用数据库层，基于 EF Core + SQLite，提供 SECS/GEM 配置数据的实体、DbContext 及仓储。主程序与后台 Windows 服务（`PF.SecsGem.Service`）**共享同一数据库文件**。

## 数据库文件位置

```
%APPDATA%\PFAutoFrameWork\SecsGemConfig.db
```

## 主要类型

| 类型 | 说明 |
|---|---|
| `SecsGemDbContext` | EF Core DbContext，管理 SECS/GEM 所有配置表 |
| `ISecsGemDataBase` | 数据库访问接口（供主程序和 Windows 服务共用） |
| `SecsGemRepository` | 泛型仓储实现 |

## DI 注册

```csharp
// 主程序 App.xaml.cs
containerRegistry.Register<SecsGemDbContext>();
containerRegistry.RegisterSingleton<ISecsGemDataBase, SecsGemDataBase>();
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
var config = await _secsGemDb.GetConnectionConfigAsync();

// 更新变量定义
await _secsGemDb.SaveVariableAsync(new SecsGemVariableEntity
{
    VariableId   = "SVID_1",
    Name         = "MachineName",
    DataType     = "A",
    DefaultValue = "AutoOCR"
});
```
