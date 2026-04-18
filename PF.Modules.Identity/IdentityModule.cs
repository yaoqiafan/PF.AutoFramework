using PF.Core.Constants;
using PF.Modules.Identity.ViewModels;
using PF.Modules.Identity.Views;
using PF.UI.Infrastructure.Navigation;
using System.Reflection;

namespace PF.Modules.Identity
{
    /// <summary>身份认证与权限管理模块</summary>
    public class IdentityModule : IModule
    {
        /// <summary>模块初始化时注册导航菜单</summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

        }

        /// <summary>注册身份模块的视图和服务</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<LoginViewModel>();
            containerRegistry.RegisterForNavigation<LoginView>(NavigationConstants.Dialogs.LoginView);
            containerRegistry.RegisterForNavigation<PagePermissionView, PagePermissionViewModel>(NavigationConstants.Views.PagePermissionView);
            containerRegistry.RegisterForNavigation<UserManagementView, UserManagementViewModel>(NavigationConstants.Views.UserManagementView);
        }
    }
}