# PF.Modules.SecsGem

PF.AutoFramework SECS/GEM 通信 Prism 模块（当前版本 1.0.5）。以插件 DLL 方式加载，提供**纯 UI 层**：报文命令管理、系统参数/VID/CEID/ReportID/CommandID 浏览、以及 `PF.SecsGem.Service` 这个 Windows 服务的安装/启停管控界面。本模块不包含任何 SECS/GEM 通信逻辑，只消费 `PF.Core`/`PF.Infrastructure` 已实现好的服务。

## 架构说明

真正的通信在独立 Windows 后台服务 `PF.SecsGem.Service` 里，本模块只是它的管理台：

```
主程序（WPF，本模块所在进程）
    ↓ 本机回环 TCP:6800（IinternalClient ↔ Worker.LocationServer）
PF.SecsGem.Service（Windows 服务，全机唯一实例）
    ↓ SECS/GEM (HSMS)
半导体设备主机（Host）
```

`ISecsGemManager` / `ICommandManager` / `IinternalClient` 三个核心服务由 `PF.Application.Base`（`PFApplicationBase.RegisterSecsGemServices`）在应用启动时**自动注册**，项目侧不需要在自己的 `App.xaml.cs` 里手写 `RegisterSingleton`。不用 SECS/GEM 的项目应重写 `UsesSecsGemService => false` 整体关掉这部分 DI 与本模块的加载（详见 `PF.Application.Base` README 与仓库 `CLAUDE.md`）。

## 界面：`SecsGemDebugView`（三个页签 + 常驻连接工具栏）

`SecsGemDebugViewModel` 聚合五个子 ViewModel，构成一个页面：

| 子 ViewModel | 承载位置 | 作用 |
|---|---|---|
| `SecsConnectionViewModel` | 页面顶部常驻工具栏（不在页签里） | HSMS 连接状态灯 + 初始化/断开按钮；数据库为空时给出提示 |
| `SecsCommandBuilderViewModel` | 页签「报文命令管理」 | 左侧命令库树（主动命令/应答命令两个子树，右键增删）+ 中间报文编辑器（S/F 号、Item 节点树，支持 LIST 嵌套）+ 右侧实时收发日志 |
| `SecsParameterViewModel` | 页签「参数管理」 | 系统参数 / VID / CEID / ReportID / CommandID 五个子页签的表格化浏览与编辑 |
| `SecsServiceManagerViewModel` | 页签「外围服务管理」 | `PF.SecsGem.Service` 的安装/卸载/启动、状态刷新 |
| `SecsLogViewModel` | 报文编辑器旁的日志面板 | 收发报文与系统操作的滚动日志 |

配套 3 个弹窗（`RegisterDialog` 注册，`IDialogService` 弹出）：

- `CommandEditDialog` — 新建/编辑一条 `SFCommand`（Stream/Function/方向/描述）
- `SecsNodeConfigDialog` — 编辑报文里某个 Item 节点（类型、值、LIST 子节点）
- `VidSelectDialog` — 从 VID 库里选一个变量插入报文节点

`ISFCommand`（`PF.Core`）不是"消息处理器"接口，而是**命令库的仓储契约**——`FindCommand`/`AddCommand`/`GetCommandsByStream`/`ValidateCommand` 等，`SecsCommandBuilderViewModel` 通过它增删改查 `SFCommand` 定义并落库；真正收发报文、触发 Collection Event 走的是 `ISecsGemManager`/`ISecsGemMessageUpdater`（`PF.Core`/`PF.Infrastructure`），本模块不重复封装，二次开发时直接注入这两个接口即可。

## ⚠️ 破坏性变更（v1.0.3 起）：数据访问迁移至 UoW 模式

`SecsParameterViewModel` / `SecsCommandBuilderViewModel` / `SecsConnectionViewModel` 已迁移到 `PF.SecsGem.DataBase` v1.0.3 引入的 `BeginScope()` 工作单元模式；旧的 `ISecsGemDataBase.GetRepository()` / `SaveChangesAsync()` 已删除。依赖 `PF.SecsGem.DataBase >= 1.0.3`，新旧用法对比详见该包 README。若二次开发时直接调用了旧接口，需要同步改造为：

```csharp
using var scope = db.BeginScope();
var repo = scope.GetRepository<T>();
// ...
await scope.SaveChangesAsync();
```

此约定至今没有变化，是当前唯一的数据访问方式。

## 「外围服务管理」页签：服务安装与启停

`SecsServiceManagerViewModel` 通过 `PF.CommonTools.ServeTool.ServicePathResolver` 解析服务 exe 路径与服务名（同一份实现也供 `PFApplicationBase` 的启动期项目归属校验共用），通过 `ServerMangerTool` 做实际的安装/卸载/启停：

- 服务名固定为 `SecsGemService`（不按项目区分，全机只能有一个实例真正工作，见 `CLAUDE.md`）。
- 「安装服务」按钮会先把当前主程序的 `ConstGlobalParam.ProjectName` 写入服务目录下 `appsettings.json` 的 `ProjectName` 键，再注册服务——这条路径绕过了安装器，服务缺这个键会直接拒绝启动。
- 安装/卸载/启动均要求管理员权限，否则弹窗提示。

`PF.SecsGem.Service` 本身（2026-08 起）新增了跟随主程序进程接入/断开而开关的本机 HSMS 监听（`HsmsListener`），但那是服务内部行为，本模块的管理界面不涉及。

## 日志路径

SECS/GEM 通信报文日志由 `PF.SecsGem.Service` 写出，与主程序配置路径无关、不按项目隔离：

```
D:\PF_Logs\SecsGem\Service\
    └── Protocol\      ← 报文收发日志，按小时滚动
```

服务自身的启动失败日志：`D:\PF_Logs\SecsGem\Service\startup-error.log`（同时写 Windows EventLog）。
