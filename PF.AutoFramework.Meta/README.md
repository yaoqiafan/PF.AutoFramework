# PF.AutoFramework.Meta

PF.AutoFramework 一键引用元包。本身不含任何代码，单个 `PackageReference` 即可把下面 15 个子包一次性带入项目，省去逐个声明版本号。

## 快速接入

```xml
<PackageReference Include="PF.AutoFramework.Meta" Version="2.0.0" />
```

## 包含的子包

打包时通过 `ProjectReference` 引用以下 15 个项目，`dotnet pack` 自动转换为对应的 `PackageId` + 各自 `.csproj` 中当前 `<Version>` 的 NuGet 依赖：

| 包 | 说明 |
|---|---|
| `PF.Core` | 核心契约层：接口 / 枚举 / 特性，零外部依赖 |
| `PF.Infrastructure` | 三层硬件抽象（`BaseDevice` / `BaseMechanism` / `StationBase<T>`）、通讯实现、SECS/GEM 工具 |
| `PF.Services` | 服务实现：`Param` / `Hardware` / `Communication` / `Alarm` / `Log` / `Production` / `Sync` / `Timer` 等 |
| `PF.Data` | EF Core + SQLite 数据访问层 |
| `PF.SecsGem.DataBase` | SECS/GEM 协议专用数据库层 |
| `PF.CommonTools` | 通用工具集（含 `ServicePathResolver` 等） |
| `PF.UI.Infrastructure` | `RegionViewModelBase` / 导航服务 / 对话框服务 |
| `PF.Application.Base` | `PFApplicationBase` 应用入口基类、`MainWindow`/`MainView`、公共服务 |
| `PF.Modules.Alarm` | 报警中心 Prism 模块 |
| `PF.Modules.Identity` | 身份认证 Prism 模块 |
| `PF.Modules.Logging` | 日志查看 Prism 模块 |
| `PF.Modules.Parameter` | 参数管理 Prism 模块 |
| `PF.Modules.Debug` | 硬件/通讯综合调试面板 Prism 模块 |
| `PF.Modules.ProductionRecord` | 生产数据 Prism 模块 |
| `PF.Modules.SecsGem` | SECS/GEM 通信 Prism 模块 |

> **不在元包中，需按需单独引用**：
> - `PF.Modules.Halcon` / `PF.Vision.Halcon` — HALCON 机器视觉调试 UI 与视觉引擎服务，不用视觉的项目没有理由带上 HALCON 运行时依赖。
> - `PF.UI.Resources`（及历史上的 `PF.UI.Controls` / `PF.UI.Shared`）— UI 控件库与资源已独立为外部 NuGet 包维护，`PF.UI.Infrastructure` 不传递引用，消费项目需自行 `<PackageReference Include="PF.UI.Resources" />`。

## 版本策略

- 2.0.0 起框架包统一主版本：除 `PF.Vendor.*` 外全部框架包与本元包同为 2.0.0（框架大版本）。之后子包各自升补丁 / 次版本号，元包版本号仅代表"这一次打包动作"。
- 各子包版本在各自 `.csproj` 中独立声明和递增，`dotnet pack` 时自动解析并锁定其打包当时的实际版本，元包不做也不需要手动对齐。
- **子包有实际改动并升级版本号后，本元包必须重新打包发布一次新版本**——否则已发布的旧版本元包会一直指向旧的子包版本；这也是元包版本号变化如此频繁的原因（几乎每次子包发版都跟着发一次）。
- 各包 `PackageReleaseNotes` 只记录 2.0.0 起的变更；2.0.0 之前的流水账（含元包的子包版本对照）在各项目目录下的 `历史版本记录.md`。

## 运行要求

- .NET 8 / `net8.0-windows`
- WPF 项目（`UseWPF=true`）
- Prism.DryIoc 9.0.537+

## 私有 BaGet 服务器

```xml
<!-- nuget.config -->
<packageSources>
  <add key="PowerFocus_NuGet" value="https://nuget.powerfocus.com.cn/v3/index.json" />
</packageSources>
```
