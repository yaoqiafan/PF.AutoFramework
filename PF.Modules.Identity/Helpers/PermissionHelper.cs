using PF.Core.Constants;
using PF.Core.Enums;
using System.Collections.Generic;

namespace PF.Modules.Identity.Helpers
{
    /// <summary>
    /// 权限辅助工具类 — 统一管理默认访问页面列表与页面名称的中文映射。
    /// </summary>
    public static class PermissionHelper
    {
        // ── 路由名称 → 中文显示名称映射表 ────────────────────────────────────
        private static readonly Dictionary<string, string> _viewDisplayNames = new()
        {
            [NavigationConstants.Views.LoggingListView]              = "日志查看",
            [NavigationConstants.Views.LogManagementView]            = "日志管理",
            [NavigationConstants.Views.ParameterView]                = "参数管理",
            [NavigationConstants.Views.ParameterView_CommonParam]    = "基础参数",
            [NavigationConstants.Views.ParameterView_SystemConfigParam] = "系统参数",
            [NavigationConstants.Views.ParameterView_HardwareParam]  = "硬件参数",
            [NavigationConstants.Views.ParameterView_UserLoginParam] = "用户参数",
            [NavigationConstants.Views.PagePermissionView]           = "页面权限管理",
            [NavigationConstants.Views.UserManagementView]           = "用户管理",
            [NavigationConstants.Views.HardwareDebugView]            = "硬件调试",
            [NavigationConstants.Views.MechanismDebugView]           = "机构调试",
            [NavigationConstants.Views.AxisDebugView]                = "轴调试",
            [NavigationConstants.Views.IODebugView]                  = "IO调试",
            [NavigationConstants.Views.StationDebugView]             = "工站调试",
            [NavigationConstants.Views.PickPlaceStationDebugView]    = "取放工站调试",
            [NavigationConstants.Views.CardDebugView]                = "运动卡调试",
            [NavigationConstants.Views.MainView]                     = "主界面",
            [NavigationConstants.Views.HomeView]                     = "首页",
        };

        /// <summary>
        /// 将路由名称转换为中文显示名称；未找到映射时原样返回路由名称。
        /// </summary>
        public static string GetViewDisplayName(string viewName)
        {
            if (string.IsNullOrEmpty(viewName)) return viewName;
            return _viewDisplayNames.TryGetValue(viewName, out var name) ? name : viewName;
        }

        /// <summary>
        /// 根据权限等级返回该角色默认可访问的页面列表（累积模型：高等级包含低等级页面）。
        /// </summary>
        public static List<string> GetDefaultAccessibleViews(UserLevel level)
        {
            // Operator：日志查看 + 基础参数
            var views = new List<string>
            {
                NavigationConstants.Views.LoggingListView,
                NavigationConstants.Views.ParameterView_CommonParam,
            };

            if (level < UserLevel.Engineer)
                return views;

            // Engineer：新增硬件调试 + 系统参数 + 机构/工站调试
            views.AddRange(new[]
            {
                NavigationConstants.Views.ParameterView_SystemConfigParam,
                NavigationConstants.Views.HardwareDebugView,
                NavigationConstants.Views.MechanismDebugView,
                NavigationConstants.Views.StationDebugView,
            });

            if (level < UserLevel.Administrator)
                return views;

            // Administrator：新增日志管理 + 硬件参数 + 权限查看
            views.AddRange(new[]
            {
                NavigationConstants.Views.LogManagementView,
                NavigationConstants.Views.ParameterView_HardwareParam,
                NavigationConstants.Views.PagePermissionView,
            });

            if (level < UserLevel.SuperUser)
                return views;

            // SuperUser：完整权限（追加用户管理 + 用户参数）
            views.AddRange(new[]
            {
                NavigationConstants.Views.UserManagementView,
                NavigationConstants.Views.ParameterView_UserLoginParam,
            });

            return views;
        }

        // 系统内置账号名称集合（登录优先拦截，UI 列表过滤）
        public static readonly IReadOnlySet<string> BuiltInUserNames = new HashSet<string>
        {
            "Operator", "Engineer", "Administrator", "SuperUser", "System"
        };
    }
}
