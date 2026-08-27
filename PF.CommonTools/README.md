# PF.CommonTools

PF.AutoFramework 通用工具集，当前版本 1.0.3。提供枚举元数据扩展、单值 JSON 序列化、反射类型扫描/解析、Windows 服务生命周期管理、SECS/GEM 服务定位五类工具，供上层业务代码直接调用。零 UI 依赖（`net8.0-windows`，但不引用 WPF）。

> 早期版本 README 里的 `JsonHelper`/`ReflectionHelper`/`ServiceControlHelper`/`EnsurePointsExist` 等类名/方法名均已核实不存在——本包实际没有轴点表相关功能（那是 `PF.Infrastructure` 的 `BaseMechanism.EnsurePointsExist<TEnum>` 提供的），以下按仓库当前真实类名重写。

## 枚举元数据扩展（EnumParameterExtensions）

从标准 `System.ComponentModel` 特性（`[Description]`/`[Category]`/`[DefaultValue]`）读取枚举字段的附加信息，按枚举类型分桶缓存（`ConcurrentDictionary`，零装箱）：

```csharp
public enum FeedPoint
{
    [Description("取料等待位")] [Category("轴点位")] [DefaultValue(0.0)]
    WaitPos,
}

EnumParamInfo info = FeedPoint.WaitPos.GetParamInfo();   // Description / Category / DefaultValue / TypeFullName
string desc         = FeedPoint.WaitPos.GetDescription();
string cat           = FeedPoint.WaitPos.GetCategory();
object def           = FeedPoint.WaitPos.GetDefaultValue();
double defTyped      = FeedPoint.WaitPos.GetDefaultValueAs<FeedPoint, double>(fallback: 0.0);
```

字段未标注对应特性时分别兜底为 `ToString()`（Description）、`"未分类"`（Category）、`null`（DefaultValue）。

## JSON 单值序列化（JsonSingleValueHelper）

`System.Text.Json` 的薄封装，专用于把单个标量/对象值和 JSON 字符串互转（`ParamEntity.JsonValue` 这类"一列存一个值"的场景），不是通用对象序列化器：

```csharp
string json = JsonSingleValueHelper.SerializeSingleValue(500);        // "500"
int    val  = JsonSingleValueHelper.DeserializeSingleValue<int>(json);
object dyn  = JsonSingleValueHelper.DeserializeDynamic(json);          // 按 JsonValueKind 自动判断返回 string/int/double/bool/null
```

## 反射：类型解析与扫描

`TypeClassExtensions`（跨程序集按类型全名查找 `Type`，`ParamService.RegisterParamType` 等处用于还原 JSON 里记录的 `TypeFullName`）：

```csharp
Type? t  = TypeClassExtensions.GetTypeFromAnyAssembly("MyProject.Params.FeedParam");
Type? t2 = TypeClassExtensions.GetTypeWithAssembly("MyProject.Params.FeedParam", assemblyPath);
```

`TypeScanner<TBaseType>`（泛型批量扫描 + 实例化，`[AlarmInfo]`/`[ModuleNavigation]` 等自动发现特性背后用的就是它）：

```csharp
var options = new TypeScanner<IHardwareDevice>.TypeScanOptions
{
    IncludeAbstract = false,
    Namespaces      = new[] { "MyProject.Hardware" },
    CustomFilter    = t => t.GetCustomAttribute<HardwareUIAttribute>() != null,
};

List<Type> types = TypeScanner<IHardwareDevice>.GetAllTypes(options);       // 默认按 TypeScanOptions.CacheResults 缓存
var instances     = TypeScanner<IHardwareDevice>.CreateInstances(options);   // 无参构造
TypeScanner<IHardwareDevice>.ClearCache();
TypeScanner<IHardwareDevice>.WarmUp();                                       // 预热缓存，避免首次调用卡顿

// 非泛型场景的快捷扩展
List<Type> concrete = TypeScannerExtensions.GetAllConcreteTypes<IStation>();
```

## Windows 服务管理（ServerMangerTool）

管理 `PF.SecsGem.Service` 这类 Windows 服务的安装/启停（类名沿用代码里的拼写，非笔误校正）：

```csharp
ServerMangerTool.Initialize(logService);
bool isAdmin = ServerMangerTool.IsAdministrator();
ServerMangerTool.TryRestartAsAdministrator();                                     // 非管理员时提权重启自身

bool installed = ServerMangerTool.IsWindowsServiceInstalled("SecsGemService");
bool running   = ServerMangerTool.IsServiceRunning("SecsGemService");
ServerMangerTool.InstallService("SecsGemService", "SECS/GEM 通讯服务", exePath, startType: "auto");
ServerMangerTool.StartWindowsService("SecsGemService", timeoutSeconds: 30);
ServerMangerTool.UninstallService("SecsGemService");
```

## SECS/GEM 服务定位（ServicePathResolver，v1.0.3 新增）

一机可能装了多个基于本框架的项目，但 `PF.SecsGem.Service` 全机只能有一个真正在跑（本机通道端口 6800 两端硬编码）。本工具类统一解析"服务到底属于哪个项目"，供 `PF.Application.Base` 的启动期归属校验与 `PF.Modules.SecsGem` 的服务管理面板共用：

```csharp
string exePath = ServicePathResolver.ResolveSecsServiceExePath();
// 优先读 SCM 中【实际注册】的 ImagePath（HKLM\SYSTEM\CurrentControlSet\Services\SecsGemService），
// 而不是本项目的安装目录——一机多项目时服务可能是别的项目装的，只有读实际注册项才能查出归属不符。

string settingsPath = ServicePathResolver.GetSecsServiceSettingsPath(exePath);   // 服务 appsettings.json 路径
string? projectName = ServicePathResolver.TryReadServiceProjectName(settingsPath);
bool ok = ServicePathResolver.TryWriteServiceProjectName(settingsPath, "MyProject");   // 安装前把项目名写入，绕过安装器场景用
```

常量：`SecsServiceExeName = "PF.SecsGem.Service.exe"`、`SecsServiceSubDir = "SecsGemService"`、`SecsServiceName = "SecsGemService"`。
