using PF.Core.Attributes;
using System.ComponentModel;

// 目录字段用 DescriptionAttribute 自说明，不重复写 XML 注释。
#pragma warning disable CS1591

namespace PF.Modules.Identity
{
    /// <summary>
    /// PF.Modules.Identity 界面的操作日志键目录。内层嵌套类名 = 页面名（对应 View 根节点的
    /// OperationLog.PageName），const 字段 = Key，字段上的 DescriptionAttribute = 默认描述。
    /// </summary>
    [OperationLogKeyCatalog]
    public static class OperationLogKeysIdentity
    {
        /// <summary>登录弹窗</summary>
        public static class LoginView
        {
            /// <summary>输入账号</summary>
            [Description("输入账号")]
            public const string UserName = nameof(UserName);

            /// <summary>登录</summary>
            [Description("登录")]
            [OperationLogCritical]
            public const string Login = nameof(Login);

            /// <summary>注销</summary>
            [Description("注销")]
            [OperationLogCritical]
            public const string Logout = nameof(Logout);

            /// <summary>取消</summary>
            [Description("取消")]
            public const string Cancel = nameof(Cancel);
        }

        /// <summary>页面权限配置视图</summary>
        public static class PagePermissionView
        {
            /// <summary>刷新用户列表</summary>
            [Description("刷新用户列表")]
            public const string RefreshUsers = nameof(RefreshUsers);

            /// <summary>选择要配置权限的用户</summary>
            [Description("选择要配置权限的用户")]
            public const string SelectUser = nameof(SelectUser);

            /// <summary>勾选/取消勾选某个页面的授权</summary>
            [Description("授权/取消授权页面")]
            [OperationLogCritical]
            public const string Authorize = nameof(Authorize);

            /// <summary>恢复等级默认权限</summary>
            [Description("恢复等级默认权限")]
            [OperationLogCritical]
            public const string ApplyDefaultPermissions = nameof(ApplyDefaultPermissions);

            /// <summary>保存权限配置</summary>
            [Description("保存权限配置")]
            [OperationLogCritical]
            public const string SavePermissions = nameof(SavePermissions);
        }

        /// <summary>用户管理视图</summary>
        public static class UserManagementView
        {
            /// <summary>刷新用户列表</summary>
            [Description("刷新用户列表")]
            public const string Refresh = nameof(Refresh);

            /// <summary>新增用户</summary>
            [Description("新增用户")]
            [OperationLogCritical]
            public const string Add = nameof(Add);

            /// <summary>修改用户名</summary>
            [Description("修改用户名")]
            public const string UserName = nameof(UserName);

            /// <summary>选择权限等级</summary>
            [Description("选择权限等级")]
            public const string SelectUserLevel = nameof(SelectUserLevel);

            /// <summary>保存用户</summary>
            [Description("保存用户")]
            [OperationLogCritical]
            public const string SaveUser = nameof(SaveUser);

            /// <summary>删除用户</summary>
            [Description("删除用户")]
            [OperationLogCritical]
            public const string DeleteUser = nameof(DeleteUser);
        }
    }
}
