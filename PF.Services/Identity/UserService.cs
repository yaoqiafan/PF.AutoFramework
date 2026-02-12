using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Data.Entity.Category;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json; // 使用 System.Text.Json
using System.Threading.Tasks;

namespace PF.Services.Identity
{
    public class UserService : IUserService
    {
        private readonly IParamService _paramService;
        private readonly ILogService _logService;

        public UserInfo? CurrentUser { get; private set; }

        public UserService(IParamService paramService, ILogService logService)
        {
            _paramService = paramService;
            _logService = logService;

            // 【关键】注册映射关系：告诉 ParamService，遇到 UserInfo 类型的数据，
            // 请存到 UserLoginParam 对应的数据库表中。
            _paramService.RegisterParamType<UserLoginParam, UserInfo>();
        }

        public async Task<bool> LoginAsync(string userName, string password)
        {
            try
            {
                // 1. 直接通过 ParamService 获取用户信息
                // GetParamAsync 内部会自动去 UserLoginParams 表查找 Name 为 userName 的记录并反序列化
                var user = await _paramService.GetParamAsync<UserInfo>(userName);

                if (user != null && user.Password == password)
                {
                    CurrentUser = user;
                    _logService.Info($"用户 {userName} 登录成功", "Identity");
                    return true;
                }

                // 2. 后备系统管理员通道 (防止数据库无数据时无法登录)
                if (userName == "System" && password == "admin")
                {
                    CurrentUser = UserInfo.SystemUser;
                    _logService.Info($"系统管理员 {userName} 登录成功 (后备通道)", "Identity");
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
            }
        }

        public bool IsAuthorized(UserLevel requiredLevel)
        {
            if (CurrentUser == null) return false;
            // 数值越大权限越高
            return (int)CurrentUser.Root >= (int)requiredLevel;
        }

        public async Task<ObservableCollection<UserInfo>> GetUserListAsync()
        {
            var users = new ObservableCollection<UserInfo>();
            try
            {
                // 1. 获取所有类型为 UserLoginParam 的参数信息
                var paramInfos = await _paramService.GetParamsByCategoryAsync<UserLoginParam>();

                // 2. 遍历并反序列化
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
                // 直接调用 SetParamAsync
                // Name = user.UserName (作为唯一键)
                // Value = user (会被序列化存入 Content/JsonValue)
                // UserInfo = CurrentUser (用于记录是谁修改的这个参数)
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
                // 直接调用 DeleteParamAsync，根据 Name 删除
                return await _paramService.DeleteParamAsync(user.UserName, CurrentUser);
            }
            catch (Exception ex)
            {
                _logService.Error($"删除用户失败: {user.UserName}", exception: ex);
                return false;
            }
        }
    }
}