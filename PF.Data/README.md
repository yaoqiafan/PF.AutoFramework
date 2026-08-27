# PF.Data

PF.AutoFramework 通用数据访问层，基于 EF Core 9 + SQLite，提供参数分类实体、生产数据/报警两个独立数据库的 `DbContext`、通用仓储与参数仓储实现。当前版本 1.0.3，零 UI 依赖，仅被 `PF.Services` 引用。

> 参数库的 `AppParamDbContext`（聚合四张参数表）实际定义在 `PF.Application.Base`，不在本包内——本包只提供它所依赖的实体（`ParamEntity` 及四个子类）与仓储实现。`ProductionDbContext` / `AlarmDbContext` 两个业务数据库的 `DbContext` 则完整定义在本包。

## 三个物理数据库

| 文件（位于 `D:\PFConfig\PFAutoFrameWork\{项目名}\`） | DbContext | 定义位置 |
|---|---|---|
| `SystemParamsCollection.db` | `AppParamDbContext` | `PF.Application.Base` |
| `ProductionHistory.db` | `ProductionDbContext` | 本包 `PF.Data` |
| `AlarmHistory.db` | `AlarmDbContext` | 本包 `PF.Data` |

## 参数实体：`ParamEntity` 四张子表

四张参数表统一继承自 `ParamEntity`（`BasicEntity` → `IEntity`），主键 `ID` 是**字符串 Guid**（`[DatabaseGenerated(Identity)]`，非自增整数）：

```csharp
public abstract class ParamEntity : BasicEntity   // ID: string, CreateTime/UpdateTime: DateTime
{
    public string Name         { get; set; }   // 参数名（唯一索引，四表均在 AppParamDbContext 里加了 IsUnique）
    public string Description  { get; set; }
    public string? TypeFullName{ get; set; }   // 反射解析用的完整类型名
    public string JsonValue    { get; set; }   // 实际参数值的 JSON 序列化结果
    public string Category     { get; set; }
    public int    Version      { get; set; } = 1;
}
```

四个子类只是各自 `[Table("XxxParams")]` + 独立生成 `ID`，字段完全一致：

| 实体 | 表名 | 存储键（Name） | 用途 |
|---|---|---|---|
| `UserLoginParam` | `UserLoginParams` | 用户名 | 用户凭证 |
| `SystemConfigParam` | `SystemConfigParams` | 配置项名 | 系统配置 |
| `HardwareParam` | `HardwareParams` | `HardwareConfig.DeviceId` | 硬件配置（JSON） |
| `CommunicationParam` | `CommunicationParams` | `CommunicationConfig.InstanceId` | 通讯实例配置（JSON，`ICommunication` 用） |

`IDefaultParam` 是项目侧要实现的"默认参数集"契约（消费方通常叫 `DefaultParameters`），四个方法各自返回一张表的默认字典，`AppParamDbContext.EnsureDefaultParametersCreatedAsync` 据此做废弃参数清理（详见仓库根 `CLAUDE.md` 的"配置路径与项目隔离"一节）：

```csharp
public interface IDefaultParam
{
    Dictionary<string, UserLoginParam>     GetUsersDefaults();
    Dictionary<string, SystemConfigParam>  GetSystemDefaults();
    Dictionary<string, HardwareParam>      GetHardwareDefaults();
    Dictionary<string, CommunicationParam> GetCommunicationDefaults();
}
```

## 通用仓储 `IGenericRepository<T>`

```csharp
public interface IGenericRepository<T> where T : class, new()   // 不要求 IEntity！
{
    Task<T?>             GetByIdAsync(int id);                          // 见下方注意事项
    Task<IEnumerable<T>> GetAllAsync();
    Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);
    Task<T?>              SingleOrDefaultAsync(Expression<Func<T, bool>> predicate);
    Task<T>               AddAsync(T entity);
    Task                  AddRangeAsync(IEnumerable<T> entities);
    Task                  UpdateAsync(T entity);
    Task                  UpdateRangeAsync(IEnumerable<T> entities);
    Task                  RemoveAsync(T entity);
    Task                  RemoveRangeAsync(IEnumerable<T> entities);
    Task<int>             CountAsync();
    Task<bool>             AnyAsync(Expression<Func<T, bool>> predicate);
    Task<int>              SaveChangesAsync();   // 内部即 Context.SaveChangesAsync()
}
```

`GenericRepository<T>` 是唯一实现，查询类方法（`GetAllAsync`/`FindAsync`/`SingleOrDefaultAsync`）统一 `AsNoTracking()`。

> **注意**：`GetByIdAsync(int id)` 内部是 `DbSet.FindAsync(id)`——对 `ParamEntity` 系四张表（主键是字符串 Guid）**永远查不到任何行**，只对整型自增主键的实体（如 `AlarmRecordEntity.Id`）有意义。参数类实体请用下面的 `ParamRepository<T>.GetByNameAsync`。

## 参数仓储 `ParamRepository<T>`

`ParamRepository<T> : GenericRepository<T>, IParamRepository<T>`，约束 `where T : class, IEntity, new()`，在通用 CRUD 之上按 `Name`/`Category` 做了 EF Core 可翻译的查询（`EF.Property<string>` 而非手写表达式树）：

```csharp
public class ParamRepository<T> : GenericRepository<T>, IParamRepository<T>
{
    Task<T?>       GetByNameAsync(string name);
    Task<List<T>>  GetByCategoryAsync(string category);
    Task<bool>     ExistsAsync(string name);
    Task<int>      UpdateVersionAsync(string id, int version);   // 直接改 Version + UpdateTime 并 SaveChanges
}
```

框架内几乎不直接用它——上层通过 `PF.Services` 的 `IParamService`（见该包 README）统一读写，`ParamRepository<T>` 是 `ParamService` 内部使用的数据访问层。

## 生产数据库：`ProductionDbContext`

```csharp
public class ProductionDbContext : DbContext
{
    public DbSet<ProductionDataEntity> ProductionData { get; set; }
}
```

`OnModelCreating` 为 `ProductionDataEntity` 建了两个索引：`RecordTime`（时间范围查询）与 `RecordType`（按记录类型过滤），对应 `PF.Services` 的 `ProductionDataService.RecordAsync<TData>()` 写入路径。

## 报警数据库：`AlarmDbContext`（年度分表）

```csharp
public class AlarmDbContext : DbContext
{
    public int CurrentYear { get; }   // 构造函数传 year，不传则取 DateTime.Now.Year
    public DbSet<AlarmDefinitionEntity> AlarmDefinitions { get; set; }   // 共享字典表，不分年
    public DbSet<AlarmRecordEntity>     AlarmRecords     { get; set; }   // 物理表名 AlarmRecord_{CurrentYear}
}
```

- `AlarmDefinitionEntity`：报警字典（主键 `ErrorCode`），字段 `Category`/`Message`/`MessageEn`/`Severity`/`Solution`/`MessageID`/`MessageIDHex`，后三个（`MessageEn`/`MessageID`/`MessageIDHex`）是 v1.0.2 为 SECS/GEM S5F1 上传补的字段。数据库里的条目优先级高于代码内置的 `[AlarmInfo]` 特性定义。
- `AlarmRecordEntity`：报警流水（自增 `Id`），只存 `ErrorCode`/`Source`/`TriggerTime`/`ClearTime`/`IsActive`，不冗余存储 `Message` 等描述字段——查询时按 `ErrorCode` 联查字典表。
- 分表机制：`AlarmModelCacheKeyFactory : IModelCacheKeyFactory` 把 `(ContextType, CurrentYear, designTime)` 作为 EF Core 的模型缓存键，同一进程内不同年份的 `AlarmDbContext` 实例会各自编译出独立 Model，`ToTable($"AlarmRecord_{CurrentYear}")` 才能生效；跨年查询需显式传 `year` 构造对应年份的 `AlarmDbContext`。

## `DbContextFactory<TContext>`：静态泛型选项缓存

不是走 DI 容器的 `AddDbContext`，而是每个 `TContext` 类型一份的静态类，按连接字符串缓存 `DbContextOptions<TContext>`：

```csharp
DbContextFactory<AlarmDbContext>.Initialize(connectionString);      // 或 Configure(builder => ...) 自定义
var ctx = DbContextFactory<AlarmDbContext>.CreateDbContext();       // 走已缓存的连接字符串
var ctx2024 = DbContextFactory<AlarmDbContext>.CreateDbContext(otherConnStr);  // 带特定连接字符串
DbContextFactory<AlarmDbContext>.ClearCache();
```

`CreateDbContext()` 内部用 `Activator.CreateInstance(typeof(TContext), options)`，因此 `TContext` 必须有形如 `ctor(DbContextOptions<TContext> options)` 的构造函数（`AlarmDbContext` 的 `year` 参数走默认值，`Activator` 场景下始终取当前年份）。
