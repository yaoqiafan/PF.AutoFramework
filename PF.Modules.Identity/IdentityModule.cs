using PF.Core.Constants;
using PF.Modules.Identity.ViewModels;
using PF.Modules.Identity.Views;
using Prism.Ioc;
using Prism.Modularity;

namespace PF.Modules.Identity
{
    public class IdentityModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 注册登录弹窗
            containerRegistry.RegisterDialog<LoginView, LoginViewModel>(NavigationConstants.Dialogs.LoginView);

            // 注册管理页面用于导航
            containerRegistry.RegisterForNavigation<UserManagementView, UserManagementViewModel>(NavigationConstants.Views.UserManagementView);
            containerRegistry.RegisterForNavigation<PagePermissionView, PagePermissionViewModel>(NavigationConstants.Views.PagePermissionView);
        }
    }
}