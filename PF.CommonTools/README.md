# PF.CommonTools

PF.AutoFramework 通用工具集，提供枚举参数扩展、JSON 序列化辅助及反射工具，供上层业务代码直接调用。

## 枚举参数扩展（EnumParameterExtensions）

用于将枚举值与机构点表、配方参数绑定，简化轴点位管理。

```csharp
public enum FeedPoint
{
    [Description("取料等待位")] WaitPos,
    [Description("取料位")]     PickPos,
    [Description("放料位")]     PlacePos,
}

// 在机构 InternalInitializeAsync 中
EnsurePointsExist<FeedPoint>(_xAxis);
// → 若数据库中无该枚举项的点位记录，自动补全并持久化（默认值 0.0）

// 读取点位
double pos = _paramService.GetAxisPoint<FeedPoint>(FeedPoint.PickPos, _xAxis);

// 移动到枚举点位
await _xAxis.MoveAbsAsync(FeedPoint.PickPos.GetPosition(_paramService), token);
```

## JSON 序列化辅助

封装 `System.Text.Json`，统一缩进、中文编码配置。

```csharp
// 序列化（自动缩进，中文不转义）
string json = JsonHelper.Serialize(myObject);

// 反序列化
var obj = JsonHelper.Deserialize<MyClass>(json);

// 深拷贝
var copy = JsonHelper.DeepClone(original);
```

## 反射辅助工具

用于框架内自动发现特性（`[AlarmInfo]`、`[ModuleNavigation]` 等扫描逻辑）。

```csharp
// 扫描程序集中所有带 [AlarmInfo] 特性的类型
var alarmTypes = ReflectionHelper.GetTypesWithAttribute<AlarmInfoAttribute>(assembly);

// 获取属性值
var value = ReflectionHelper.GetPropertyValue(obj, "PropertyName");
```

## Windows 服务工具（ServiceControlHelper）

```csharp
// 检查 Windows 服务状态
bool running = ServiceControlHelper.IsRunning("PF.SecsGem.Service");

// 启动 / 停止服务
await ServiceControlHelper.StartAsync("PF.SecsGem.Service");
await ServiceControlHelper.StopAsync("PF.SecsGem.Service");
```
