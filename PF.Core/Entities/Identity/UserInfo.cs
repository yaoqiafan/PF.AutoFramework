using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Identity
{
    public class UserInfo
    {
        /// <summary>
        /// 用户名
        /// </summary>
        public string UserName { get; set; } = "System";

        /// <summary>
        /// 用户ID
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// 用户角色
        /// </summary>
        public UserLevel Root { get; set; } = UserLevel.Null;

        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// 创建默认系统用户
        /// </summary>
        public static UserInfo SystemUser => new UserInfo
        {
            UserName = "System",
            UserId = "system",
            Root = UserLevel.Null,
        };
    }
}
