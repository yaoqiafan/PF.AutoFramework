using PF.Core.Constants;
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
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<LoginViewModel>();
            containerRegistry.RegisterForNavigation<LoginView>(NavigationConstants.Dialogs.LoginView);
            containerRegistry.RegisterForNavigation<PagePermissionView, PagePermissionViewModel>(NavigationConstants.Views.PagePermissionView);
        }
    }
}