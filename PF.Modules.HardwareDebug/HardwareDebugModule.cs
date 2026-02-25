using PF.Core.Constants;
using PF.Modules.HardwareDebug.ViewModels;
using PF.Modules.HardwareDebug.Views;
using PF.UI.Infrastructure.Navigation;
using System.Reflection;

namespace PF.Modules.HardwareDebug
{
    public class HardwareDebugModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<AxisDebugView, AxisDebugViewModel>(
                NavigationConstants.Views.AxisDebugView);
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
