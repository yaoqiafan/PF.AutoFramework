# PF.Modules.Identity

PF.AutoFramework 身份认证 Prism 模块，提供用户登录、角色管理、细粒度权限控制 UI。以插件 DLL 方式加载。

## 功能

- **登录界面**：用户名 + 密码登录，支持静默自动登录（SuperUser 级别）
- **用户管理**：创建 / 修改 / 删除用户，分配角色
- **权限控制**：4 个权限等级，UI 元素按等级自动显示 / 隐藏
- **操作审计**：关键操作记录操作人和时间戳

## 用户权限等级

```csharp
UserLevel.Null          // -1：未登录
UserLevel.Operator      //  0：操作员（运行、查看）
UserLevel.Engineer      //  1：工程师（参数调整、调试）
UserLevel.Administrator //  2：管理员（用户管理）
UserLevel.SuperUser     //  3：超级用户（所有权限）
```

## 接入步骤

### 1. 注册用户服务（App.xaml.cs）

```csharp
containerRegistry.RegisterSingleton<IUserService, UserService>();
```

### 2. 静默登录（OnInitialized 中，程序启动时）

```csharp
// 以 SuperUser 静默登录（不弹登录框）
await _userService.SilentLoginAsync(UserLevel.SuperUser);
```

### 3. 在 UI 中使用权限绑定

```csharp
// ViewModel 中检查权限
bool canEdit = _userService.CurrentUser.Level >= UserLevel.Engineer;

// XAML 中使用转换器隐藏控件（框架内置 UserLevelToVisibilityConverter）
```

```xml
<Button Content="参数编辑"
        Visibility="{Binding CurrentUserLevel,
                     Converter={StaticResource UserLevelToVisibilityConverter},
                     ConverterParameter=Engineer}" />
```

### 4. 登录 / 登出事件

```csharp
_userService.UserChanged += (sender, e) =>
{
    _log.Info($"用户切换：{e.PreviousUser?.Name} → {e.CurrentUser?.Name}");
};
```

## 密码存储

密码使用 SHA-256 Hash 存储于 `SystemParamsCollection.db`，不保存明文。
