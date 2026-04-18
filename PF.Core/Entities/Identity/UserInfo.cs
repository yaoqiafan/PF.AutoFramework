using PF.Core.Enums;
using System.Collections.Generic;

namespace PF.Core.Entities.Identity
{
    /// <summary>
    /// 用户信息
    /// </summary>
    public class UserInfo
    {
        /// <summary>用户名</summary>
        public string UserName { get; set; } = "System";

        /// <summary>用户ID</summary>
        public string? UserId { get; set; }

        /// <summary>用户权限等级</summary>
        public UserLevel Root { get; set; } = UserLevel.Null;

        /// <summary>密码</summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 授权页面集合
        /// </summary>
        public List<string> AccessibleViews { get; set; } = new List<string>();

        /// <summary>系统内置用户</summary>
        public static UserInfo SystemUser => new UserInfo
        {
            UserName = "System",
            UserId = "system",
            Root = UserLevel.Null,
            AccessibleViews = new List<string>()
        };
    }
}