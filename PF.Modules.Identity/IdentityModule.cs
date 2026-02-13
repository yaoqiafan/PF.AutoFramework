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
            containerRegistry.RegisterSingleton<LoginViewModel>();
            containerRegistry.RegisterForNavigation<LoginView>(NavigationConstants.Dialogs.LoginView);
            containerRegistry.RegisterForNavigation<PagePermissionView, PagePermissionViewModel>(NavigationConstants.Views.PagePermissionView);
        }
    }
}