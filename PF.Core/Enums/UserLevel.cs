using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    /// <summary>
    /// 用户权限等级
    /// </summary>
    public enum UserLevel
    {
        /// <summary>无权限</summary>
        Null = -1,
        /// <summary>操作员</summary>
        Operator,
        /// <summary>工程师</summary>
        Engineer,
        /// <summary>管理员</summary>
        Administrator,
        /// <summary>超级用户</summary>
        SuperUser
    }
}
