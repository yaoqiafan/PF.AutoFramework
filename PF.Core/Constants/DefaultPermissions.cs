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
            
            var views = new List<string>
            {
                NavigationConstants.Views.MainView,
                NavigationConstants.Views.HomeView,
                NavigationConstants.Dialogs.LoginView,
                NavigationConstants.Views.LogManagementView,
            };

            if (level < UserLevel.Engineer)
                return views;

          
            views.AddRange(new[]
            {
                NavigationConstants.Views.ParameterView_SystemConfigParam,
                NavigationConstants.Views.HardwareDebugView,
                NavigationConstants.Views.UserManagementView,
            });

            if (level < UserLevel.Administrator)
                return views;

            
            views.AddRange(new[]
            {
                NavigationConstants.Views.MechanismDebugView,
                NavigationConstants.Views.StationDebugView,
                NavigationConstants.Views.PagePermissionView,
            });

            if (level < UserLevel.SuperUser)
                return views;

           
            views.AddRange(new[]
            {
                NavigationConstants.Views.CommonParamView
            });

            return views;
        }
    }
}
