using PF.Core.Enums;
using System.Collections.Generic;
using System.Linq;

namespace PF.Core.Constants
{
    /// <summary>
    /// 各权限等级的默认可访问页面（累积模型：高等级包含低等级所有页面）。
    /// 此类位于 PF.Core，可被 Service 层和 UI 层共同引用，确保权限列表只有一个来源。
    /// <para>
    /// 外部模块可通过 <see cref="RegisterViews"/> 在任意位置追加默认页面路由，
    /// 无需修改本文件。
    /// </para>
    /// </summary>
    public static class DefaultPermissions
    {
        // 各模块动态注册的额外页面路由：key = 所需最低权限等级
        private static readonly Dictionary<UserLevel, List<string>> _extraViews = new();

        /// <summary>
        /// 从任意模块注册默认可访问页面路由。
        /// <para>
        /// 示例（在 IModule.RegisterTypes 或 OnInitialized 中调用）：
        /// <code>DefaultPermissions.RegisterViews(UserLevel.Engineer, "MyCustomView", "AnotherView");</code>
        /// </para>
        /// </summary>
        /// <param name="minimumLevel">访问该页面所需的最低权限等级。</param>
        /// <param name="viewNames">一个或多个页面路由名称。</param>
        public static void RegisterViews(UserLevel minimumLevel, params string[] viewNames)
        {
            if (viewNames == null || viewNames.Length == 0) return;

            if (!_extraViews.TryGetValue(minimumLevel, out var list))
            {
                list = new List<string>();
                _extraViews[minimumLevel] = list;
            }
            list.AddRange(viewNames);
        }

        /// <summary>
        /// 根据权限等级返回默认可访问的页面路由名称列表。
        /// </summary>
        public static List<string> GetAccessibleViews(UserLevel level)
        {
            var views = new List<string>
            {
                NavigationConstants.Views.MainView,
                NavigationConstants.Views.HomeView,
                NavigationConstants.Dialogs.LoginView,
                NavigationConstants.Views.LogManagementView,
            };

            if (level >= UserLevel.Engineer)
            {
                views.AddRange(new[]
                {
                    NavigationConstants.Views.ParameterView_SystemConfigParam,
                    NavigationConstants.Views.HardwareDebugView,
                    NavigationConstants.Views.UserManagementView,
                });
            }

            if (level >= UserLevel.Administrator)
            {
                views.AddRange(new[]
                {
                    NavigationConstants.Views.MechanismDebugView,
                    NavigationConstants.Views.StationDebugView,
                    NavigationConstants.Views.PagePermissionView,
                });
            }

            if (level >= UserLevel.SuperUser)
            {
                views.AddRange(new[]
                {
                    NavigationConstants.Views.CommonParamView
                });
            }

            // 追加各模块动态注册的扩展页面路由
            foreach (var (minLevel, registeredViews) in _extraViews)
            {
                if (level >= minLevel)
                    views.AddRange(registeredViews.Where(v => !views.Contains(v)));
            }

            return views;
        }
    }
}

