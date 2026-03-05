using PF.Core.Constants;
using PF.Core.Enums;
using PF.UI.Infrastructure.Navigation;
using System.Collections.Generic;

namespace PF.UI.Infrastructure.Navigation
{
    /// <summary>
    /// 权限辅助工具类 — 统一管理默认访问页面列表与页面名称的中文描述。
    /// 中文描述通过 INavigationMenuService 动态获取，无需硬编码字典。
    /// </summary>
    public static class PermissionHelper
    {
        // 由 Initialize 从 INavigationMenuService 动态构建的路由名称 → 中文标题映射
        private static Dictionary<string, string> _viewDisplayNames = new();

        /// <summary>
        /// 在模块初始化阶段调用，从已注册的导航菜单服务中提取所有 ViewName→Title 映射。
        /// 需在 all RegisterAssembly 完成后调用，确保所有模块的菜单已注册。
        /// </summary>
        public static void Initialize(INavigationMenuService navMenuService)
        {
            if (navMenuService == null) return;
            var map = new Dictionary<string, string>();
            ExtractTitles(navMenuService.MenuItems, map);
            _viewDisplayNames = map;
        }

        private static void ExtractTitles(IEnumerable<NavigationItem> items, Dictionary<string, string> map)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                if (item == null) continue;
                if (!string.IsNullOrEmpty(item.ViewName) && !string.IsNullOrEmpty(item.Title))
                    map[item.ViewName] = item.Title;
                ExtractTitles(item.Children, map);
            }
        }

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
                NavigationConstants.Views.MainView,
                NavigationConstants.Views.HomeView,
                NavigationConstants.Dialogs.LoginView,
                NavigationConstants.Views.LogManagementView,
            };

            if (level < UserLevel.Engineer)
                return views;

            // Engineer：新增系统参数 + 硬件调试 + 机构/工站调试
            views.AddRange(new[]
            {
                NavigationConstants.Views.ParameterView_SystemConfigParam,
                NavigationConstants.Views.HardwareDebugView,
                NavigationConstants.Views.UserManagementView,
            });

            if (level < UserLevel.Administrator)
                return views;

            // Administrator：新增日志管理 + 硬件参数 + 页面权限管理
            views.AddRange(new[]
            {
                NavigationConstants.Views.MechanismDebugView,
                NavigationConstants.Views.StationDebugView,
                NavigationConstants.Views.PagePermissionView,
            });

            if (level < UserLevel.SuperUser)
                return views;

            // SuperUser：追加用户管理 + 用户参数
            views.AddRange(new[]
            {
                NavigationConstants.Views.ParameterView_CommonParam
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
