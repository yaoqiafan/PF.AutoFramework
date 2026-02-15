using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Data.Entity.Category;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace PF.Services.Identity
{
    public class UserService : IUserService
    {
        private readonly IParamService _paramService;
        private readonly ILogService _logService;

        public UserInfo? CurrentUser { get; private set; }

        // 实现用户变化事件
        public event EventHandler<UserInfo?> CurrentUserChanged;

        public UserService(IParamService paramService, ILogService logService)
        {
            _paramService = paramService;
            _logService = logService;

            // 注册映射关系
            _paramService.RegisterParamType<UserLoginParam, UserInfo>();
        }

        private void OnCurrentUserChanged()
        {
            CurrentUserChanged?.Invoke(this, CurrentUser);
        }

        public async Task<bool> LoginAsync(string userName, string password)
        {
            try
            {
                var user = await _paramService.GetParamAsync<UserInfo>(userName);

                if (user != null && user.Password == password)
                {
                    CurrentUser = user;
                    _logService.Info($"用户 {userName} 登录成功", "Identity");
                    OnCurrentUserChanged(); // 触发用户变化事件
                    return true;
                }

                // 后备系统管理员通道
                if (userName == "System" && password == "admin")
                {
                    CurrentUser = UserInfo.SystemUser;
                    _logService.Info($"系统管理员 {userName} 登录成功 (后备通道)", "Identity");
                    OnCurrentUserChanged(); // 触发用户变化事件
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

        public void Logout()
        {
            if (CurrentUser != null)
            {
                _logService.Info($"用户 {CurrentUser.UserName} 已注销", "Identity");
                CurrentUser = null;
                OnCurrentUserChanged(); // 触发用户变化事件
            }
        }

        public bool IsAuthorized(UserLevel requiredLevel)
        {
            if (CurrentUser == null) return false;
            return (int)CurrentUser.Root >= (int)requiredLevel;
        }

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
                        if (user != null)
                        {
                            users.Add(user);
                        }
                    }
                    catch
                    {
                        // 忽略解析失败的脏数据
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error("获取用户列表失败", exception: ex);
            }
            return users;
        }

        public async Task<bool> SaveUserAsync(UserInfo user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.UserName)) return false;

            try
            {
                return await _paramService.SetParamAsync(
                    name: user.UserName,
                    value: user,
                    userInfo: CurrentUser,
                    description: $"用户账号: {user.UserName}"
                );
            }
            catch (Exception ex)
            {
                _logService.Error($"保存用户失败: {user.UserName}", exception: ex);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(UserInfo user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.UserName)) return false;

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