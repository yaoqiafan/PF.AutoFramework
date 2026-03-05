using PF.Core.Enums;
using System.Collections.Generic;

namespace PF.Core.Constants
{
    /// <summary>
    /// 各权限等级的默认可访问页面（累积模型：高等级包含低等级所有页面）。
    /// 此类位于 PF.Core，可被 Service 层和 UI 层共同引用，确保权限列表只有一个来源。
    /// </summary>
    public static class DefaultPermissions
    {
        /// <summary>
        /// 根据权限等级返回默认可访问的页面路由名称列表。
        /// </summary>
        public static List<string> GetAccessibleViews(UserLevel level)
        {
            // Operator：日志查看 + 基础参数
            var views = new List<string>
            {
                NavigationConstants.Views.LoggingListView,
                NavigationConstants.Views.ParameterView_CommonParam,
            };

            if (level < UserLevel.Engineer)
                return views;

            // Engineer：新增系统参数 + 硬件调试 + 机构/工站调试
            views.AddRange(new[]
            {
                NavigationConstants.Views.ParameterView_SystemConfigParam,
                NavigationConstants.Views.HardwareDebugView,
                NavigationConstants.Views.MechanismDebugView,
                NavigationConstants.Views.StationDebugView,
            });

            if (level < UserLevel.Administrator)
                return views;

            // Administrator：新增日志管理 + 硬件参数 + 页面权限管理
            views.AddRange(new[]
            {
                NavigationConstants.Views.LogManagementView,
                NavigationConstants.Views.ParameterView_HardwareParam,
                NavigationConstants.Views.PagePermissionView,
            });

            if (level < UserLevel.SuperUser)
                return views;

            // SuperUser：追加用户管理 + 用户参数
            views.AddRange(new[]
            {
                NavigationConstants.Views.UserManagementView,
                NavigationConstants.Views.ParameterView_UserLoginParam,
            });

            return views;
        }
    }
}
