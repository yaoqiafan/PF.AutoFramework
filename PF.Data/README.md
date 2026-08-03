# PF.Data

PF.AutoFramework 通用数据访问层，基于 EF Core + SQLite，提供参数、生产数据、报警三个独立数据库的 DbContext 工厂、实体定义及泛型仓储实现。

## 数据库文件位置

| 文件 | DbContext | 用途 |
|---|---|---|
| `D:\PFConfig\PFAutoFrameWork\{项目名}\SystemParamsCollection.db` | `AppParamDbContext` | 参数、用户凭证、硬件配置（JSON 字段） |
| `D:\PFConfig\PFAutoFrameWork\{项目名}\ProductionHistory.db` | `ProductionDbContext` | 生产数据记录 |
| `D:\PFConfig\PFAutoFrameWork\{项目名}\AlarmHistory.db` | `AlarmDbContext` | 报警历史（年度分表 `AlarmRecord_{YYYY}`） |

## DbContext 工厂

框架通过 `ConcurrentDictionary` 缓存 `DbContextOptions`，避免重复配置开销。

```csharp
// DI 注册（Scoped — 每请求一个实例）
containerRegistry.Register<AppParamDbContext>();
containerRegistry.Register<ProductionDbContext>();
containerRegistry.Register<AlarmDbContext>();
```

## 泛型仓储（GenericRepository\<T\>）

```csharp
public interface IGenericRepository<T> where T : class, IEntity
{
    Task<T?>               GetByIdAsync(int id);
    Task<IEnumerable<T>>   GetAllAsync();
    Task<IEnumerable<T>>   FindAsync(Expression<Func<T, bool>> predicate);
    Task                   AddAsync(T entity);
    Task                   UpdateAsync(T entity);
    Task                   DeleteAsync(T entity);
}
```

使用示例：

```csharp
// 查询生产记录
var records = await _productionRepo.FindAsync(r => r.RecordTime >= DateTime.Today);

// 新增参数实体
await _paramRepo.AddAsync(new FeedParamEntity { ... });
```

## 参数仓储（IParamRepository）

专为 `ParamService` 提供 JSON 字段存储，支持强类型参数 POCO 的序列化 / 反序列化。

```csharp
// 注册参数类型映射
_paramRepository.RegisterType<FeedParam, FeedParamEntity>();

// 读取参数
var param = await _paramRepository.GetAsync<FeedParam>();

// 保存参数（自动 JSON 序列化）
await _paramRepository.SaveAsync(new FeedParam { PickSpeed = 500 });
```

## 报警年度分表

`AlarmDbContext` 使用 `AlarmModelCacheKeyFactory` 驱动 EF Core 模型缓存，按年动态路由到 `AlarmRecord_{YYYY}` 表：

```csharp
// 查询当年报警（自动定向到 AlarmRecord_2025）
var alarms = await _alarmRepo.GetAllAsync();

// 跨年查询需分别实例化对应年份的 DbContext
using var ctx2024 = alarmDbFactory.Create(2024);
var history = await ctx2024.AlarmRecords.ToListAsync();
```

## 实体基接口

所有实体须实现 `IEntity`：

```csharp
public interface IEntity
{
    int Id { get; set; }
}

// 示例实体
public class FeedParamEntity : IEntity
{
    public int    Id         { get; set; }
    public string ParamJson  { get; set; } = "";  // JSON 序列化的参数内容
    public string ParamType  { get; set; } = "";  // 类型名（用于路由）
}
```
