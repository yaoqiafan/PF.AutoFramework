using PF.Core.Enums;
using System.Collections.Generic;

namespace PF.Core.Entities.Identity
{
    public class UserInfo
    {
        public string UserName { get; set; } = "System";

        public string? UserId { get; set; }

        public UserLevel Root { get; set; } = UserLevel.Null;

        public string Password { get; set; } = string.Empty;

        // 新增：用于直接持久化存入数据库的授权页面集合
        public List<string> AccessibleViews { get; set; } = new List<string>();

        public static UserInfo SystemUser => new UserInfo
        {
            UserName = "System",
            UserId = "system",
            Root = UserLevel.Null,
            AccessibleViews = new List<string>()
        };
    }
}