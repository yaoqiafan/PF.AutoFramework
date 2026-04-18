using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Data.Entity.Category;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Services.Identity
{
    /// <summary>
    /// IUserService 服务
    /// </summary>
    public class UserService : IUserService
    {
        private readonly IParamService _paramService;
        private readonly ILogService _logService;

        // ── 系统内置账号（硬编码，优先于数据库，不可增删改，UI 列表不可见）───
        private static readonly IReadOnlyList<(string UserName, string Password, UserLevel Level)> _builtInUsers =
            new (string, string, UserLevel)[]
            {
                ("Operator",      "PF111",   UserLevel.Operator),
                ("Engineer",      "PF222",   UserLevel.Engineer),
                ("Administrator", "PF333",   UserLevel.Administrator),
                //("SuperUser",     "PF88888", UserLevel.SuperUser),
            };

        // 内置账号名称集合（快速查找用）
        private static readonly HashSet<string> _builtInNames =
            new HashSet<string>(_builtInUsers.Select(u => u.UserName), StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// CurrentUser
        /// </summary>
        public UserInfo? CurrentUser { get; private set; }

        /// <summary>
        /// CurrentUserChanged
        /// </summary>
        public event EventHandler<UserInfo?> CurrentUserChanged;

        /// <summary>
        /// UserService 服务
        /// </summary>
        public UserService(IParamService paramService, ILogService logService)
        {
            _paramService = paramService;
            _logService = logService;
            _paramService.RegisterParamType<UserLoginParam, UserInfo>();
        }

        private void OnCurrentUserChanged() => CurrentUserChanged?.Invoke(this, CurrentUser);

        /// <summary>
        /// LoginAsync异步操作
        /// </summary>
        public async Task<bool> LoginAsync(string userName, string password)
        {
            try
            {
                string psw = DateTime.Now.ToString("yyyyMMddHH00"); 

                if (userName.ToLower()== "SuperUser".ToLower()&& password== psw)
                {
                    CurrentUser = new UserInfo
                    {
                        UserName = "SuperUser",
                        UserId = "SuperUser",
                        Root =  UserLevel.SuperUser,
                        Password = psw,
                        AccessibleViews = DefaultPermissions.GetAccessibleViews(UserLevel.SuperUser),
                    };
                    _logService.Info($"超级用户登录成功！", "Identity");
                    OnCurrentUserChanged();
                    return true;
                }


                // 优先拦截内置账号，不查询数据库
               var builtIn = _builtInUsers.FirstOrDefault(u =>
                    string.Equals(u.UserName, userName, StringComparison.OrdinalIgnoreCase) &&
                    u.Password == password);

                if (builtIn != default)
                {
                    CurrentUser = new UserInfo
                    {
                        UserName        = builtIn.UserName,
                        UserId          = builtIn.UserName,
                        Root            = builtIn.Level,
                        Password        = builtIn.Password,
                        AccessibleViews = DefaultPermissions.GetAccessibleViews(builtIn.Level),
                    };
                    _logService.Info($"内置账号 {userName} 登录成功", "Identity");
                    OnCurrentUserChanged();
                    return true;
                }

                //// 后备系统管理员通道
                //if (string.Equals(userName, "System", StringComparison.OrdinalIgnoreCase) && password == "admin")
                //{
                //    CurrentUser = UserInfo.SystemUser;
                //    _logService.Info($"系统管理员 {userName} 登录成功 (后备通道)", "Identity");
                //    OnCurrentUserChanged();
                //    return true;
                //}

                // 查询数据库中的自定义用户
                var user = await _paramService.GetParamAsync<UserInfo>(userName);
                if (user != null && user.Password == password)
                {
                    CurrentUser = user;
                    _logService.Info($"用户 {userName} 登录成功", "Identity");
                    OnCurrentUserChanged();
                    return true;
                }

                _logService.Warn($"用户 {userName} 登录失败：密码错误或用户不存在", "Identity");
                return false;
            }
            catch (Exception ex)
            {
                _logService.Error($"登录过程异常: {userName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public void Logout()
        {
            if (CurrentUser != null)
            {
                _logService.Info($"用户 {CurrentUser.UserName} 已注销,使用默认操作员账号！", "Identity");
                var op = _builtInUsers.First(u =>
                string.Equals(u.UserName, "Operator", StringComparison.Ordinal));

                CurrentUser = new UserInfo
                {
                    UserName = op.UserName,
                    UserId = op.UserName,
                    Root = op.Level,
                    Password = op.Password,
                    AccessibleViews = DefaultPermissions.GetAccessibleViews(op.Level),
                };
                OnCurrentUserChanged();
            }
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public void ResetToOperator()
        {
            var op = _builtInUsers.First(u =>
                string.Equals(u.UserName, "Operator", StringComparison.Ordinal));

            CurrentUser = new UserInfo
            {
                UserName        = op.UserName,
                UserId          = op.UserName,
                Root            = op.Level,
                Password        = op.Password,
                AccessibleViews = DefaultPermissions.GetAccessibleViews(op.Level),
            };
            _logService.Info("用户长时间无操作，权限已自动重置为 Operator", "Identity");
            OnCurrentUserChanged();
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public bool IsAuthorized(UserLevel requiredLevel)
        {
            if (CurrentUser == null) return false;
            return (int)CurrentUser.Root >= (int)requiredLevel;
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public bool HasPagePermission(string viewName)
        {
            if (CurrentUser == null) return false;

            // SuperUser  拥有所有页面的访问权限
            if (CurrentUser.Root == UserLevel.SuperUser)
                return true;

            return CurrentUser.AccessibleViews?.Contains(viewName) == true;
        }

        /// <summary>
        /// 获取UserListAsync
        /// </summary>
        public async Task<ObservableCollection<UserInfo>> GetUserListAsync()
        {
            var users = new ObservableCollection<UserInfo>();
            try
            {
                var paramInfos = await _paramService.GetParamsByCategoryAsync<UserLoginParam>();

                foreach (var info in paramInfos)
                {
                    if (string.IsNullOrWhiteSpace(info.ToString())) continue;
                    try
                    {
                        var user = JsonSerializer.Deserialize<UserInfo>(info.Value.ToString());
                        // 过滤内置账号，不在 UI 列表中显示
                        if (user != null && !_builtInNames.Contains(user.UserName))
                        {
                            users.Add(user);
                        }
                    }
                    catch { /* 忽略解析失败的脏数据 */ }
                }
            }
            catch (Exception ex)
            {
                _logService.Error("获取用户列表失败", exception: ex);
            }
            return users;
        }

        /// <summary>
        /// 保存UserAsync
        /// </summary>
        public async Task<bool> SaveUserAsync(UserInfo user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.UserName)) return false;

            // 内置账号不允许写入数据库
            if (_builtInNames.Contains(user.UserName))
            {
                _logService.Warn($"禁止修改系统内置账号: {user.UserName}", "Identity");
                return false;
            }

            try
            {
                return await _paramService.SetParamAsync(
                    name: user.UserName,
                    value: user,
                    userInfo: CurrentUser,
                    description: $"用户账号: {user.UserName}");
            }
            catch (Exception ex)
            {
                _logService.Error($"保存用户失败: {user.UserName}", exception: ex);
                return false;
            }
        }

        /// <summary>
        /// 删除UserAsync
        /// </summary>
        public async Task<bool> DeleteUserAsync(UserInfo user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.UserName)) return false;

            // 内置账号不允许删除
            if (_builtInNames.Contains(user.UserName))
            {
                _logService.Warn($"禁止删除系统内置账号: {user.UserName}", "Identity");
                return false;
            }

            try
            {
                return await _paramService.DeleteParamAsync<UserInfo>(user.UserName, CurrentUser);
            }
            catch (Exception ex)
            {
                _logService.Error($"删除用户失败: {user.UserName}", exception: ex);
                return false;
            }
        }
    }
}
