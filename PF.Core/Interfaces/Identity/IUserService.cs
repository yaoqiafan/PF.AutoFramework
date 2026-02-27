using PF.Core.Entities.Identity;
using PF.Core.Enums;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Identity
{
    public interface IUserService
    {
        /// <summary>
        /// 当前登录用户
        /// </summary>
        UserInfo? CurrentUser { get; }

        /// <summary>
        /// 当登录用户发生变化时触发（登录、注销）
        /// </summary>
        event EventHandler<UserInfo?> CurrentUserChanged;

        /// <summary>
        /// 登录
        /// </summary>
        Task<bool> LoginAsync(string userName, string password);

        /// <summary>
        /// 注销
        /// </summary>
        void Logout();

        /// <summary>
        /// 权限检查（基于用户等级）
        /// </summary>
        bool IsAuthorized(UserLevel requiredLevel);

        /// <summary>
        /// 精确到具体用户的页面权限校验（Per-User）。
        /// SuperUser / Administrator 默认拥有全部页面权限；
        /// 其他等级严格比对当前用户 AccessibleViews 列表。
        /// </summary>
        bool HasPagePermission(string viewName);

        /// <summary>
        /// 获取所有用户列表
        /// </summary>
        Task<ObservableCollection<UserInfo>> GetUserListAsync();

        /// <summary>
        /// 保存用户（新增或修改）
        /// </summary>
        Task<bool> SaveUserAsync(UserInfo user);

        /// <summary>
        /// 删除用户
        /// </summary>
        Task<bool> DeleteUserAsync(UserInfo user);
    }
}