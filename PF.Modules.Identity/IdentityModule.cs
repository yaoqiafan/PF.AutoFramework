using PF.Core.Constants;
using PF.Modules.Identity.Helpers;
using PF.Modules.Identity.ViewModels;
using PF.Modules.Identity.Views;
using PF.UI.Infrastructure.Navigation;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.Identity
{
    public class IdentityModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            // 用所有已注册菜单的 Title 初始化 PermissionHelper 的动态中文名称映射
            PermissionHelper.Initialize(navMenuService);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<LoginViewModel>();
            containerRegistry.RegisterForNavigation<LoginView>(NavigationConstants.Dialogs.LoginView);
            containerRegistry.RegisterForNavigation<PagePermissionView, PagePermissionViewModel>(NavigationConstants.Views.PagePermissionView);
            containerRegistry.RegisterForNavigation<UserManagementView, UserManagementViewModel>(NavigationConstants.Views.UserManagementView);
        }
    }
}