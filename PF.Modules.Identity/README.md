# PF.Modules.Identity

PF.AutoFramework 身份认证 Prism 模块（当前 v1.0.2），提供登录、用户管理、逐页面权限配置三个视图。以插件 DLL 方式加载。

## 三个视图

| 视图 | ViewModel | 说明 |
|---|---|---|
| `LoginView` | `LoginViewModel` | 用户名 + 密码登录对话框（`NavigationConstants.Dialogs.LoginView`） |
| `UserManagementView` | `UserManagementViewModel` | 用户增删改、分配等级 |
| `PagePermissionView` | `PagePermissionViewModel` | 逐用户勾选可访问页面（Per-User 精细权限），并可"应用默认权限"把某等级的默认页面列表一键套用到选中用户 |

## 用户权限等级

```csharp
UserLevel.Null          // -1：未登录
UserLevel.Operator      //  0：操作员
UserLevel.Engineer      //  1：工程师
UserLevel.Administrator //  2：管理员
UserLevel.SuperUser     //  3：超级用户
```

## 两层权限模型

1. **等级默认页（`DefaultPermissions`，位于 PF.Core，累积模型）**：任意模块在自己的 `IModule.RegisterTypes`/`OnInitialized` 中调用一次即可把某页面登记为"该等级及以上默认可见"：

   ```csharp
   DefaultPermissions.RegisterViews(UserLevel.Engineer, NavigationConstants.Views.CommunicationDebugView);
   ```

   另有一份内置的"机台运行期锁定页面"名单（参数视图、硬件/模组/工站调试视图、权限管理页等），运行/初始化/复位期间普通用户看不到，SuperUser 豁免。

2. **逐用户覆盖（`PagePermissionView`）**：管理员可为某个具体用户单独勾选/取消每一个已注册的导航页面，形成该用户的 `AccessibleViews` 列表，覆盖等级默认值。`IUserService.HasPagePermission(viewName)` 是最终判定入口：SuperUser / Administrator 默认拥有全部页面权限，其余等级严格比对 `AccessibleViews`。

## 接入步骤

### 1. 注册用户服务（`PFApplicationBase` 默认已注册，通常无需手写）

```csharp
containerRegistry.RegisterSingleton<IUserService, UserService>();
```

### 2. 登录 / 登出

```csharp
bool ok = await _userService.LoginAsync(userName, password);

_userService.Logout();

// 无操作超时后自动降级为内置 Operator 账号，无需重新登录
_userService.ResetToOperator();
```

### 3. 权限检查

```csharp
// 按等级检查
bool canEdit = _userService.IsAuthorized(UserLevel.Engineer);

// 按具体页面检查（PagePermissionView 配置的逐用户权限）
bool canOpen = _userService.HasPagePermission(NavigationConstants.Views.ParameterView);

var current = _userService.CurrentUser; // 当前登录用户，未登录为 null
```

本模块**没有**内置的"按等级控制 Visibility"转换器，控件显隐需在 ViewModel 里用 `IsAuthorized`/`HasPagePermission` 算出布尔属性再绑定。模块自带的转换器是三个展示类工具：`UserLevelToBrushConverter`（等级 → 品牌色画刷，头像背景/徽章用）、`UserLevelToDisplayConverter`（等级 → 中文名或 Emoji，`ConverterParameter="icon"` 切换）、`SystemUserToBoolConverter`（用户名是否为内置保护账号 SuperUser/System/admin，绑到删除按钮 `IsEnabled` 防止误删）、`ListToStringConverter`（页面路由名称列表 → 顿号分隔中文显示，`PagePermissionView` 里用）。

### 4. 登录 / 登出事件

```csharp
_userService.CurrentUserChanged += (sender, user) =>
{
    _log.Info($"当前用户变更为：{user?.Name ?? "（已登出）"}");
};
```

## 密码存储

密码使用 SHA-256 Hash 存储于 `SystemParamsCollection.db`，不保存明文。
