using PF.Core.Constants;
using PF.UI.Infrastructure.Navigation;
using PF.Workstation.Demo.UI.ViewModels;
using PF.Workstation.Demo.UI.Views;
using System.Reflection;

namespace PF.Workstation.Demo.UI
{
    public class UIModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            //var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            //navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<GantryMechanismView, GantryMechanismViewModel>("GantryMechanismView");

            // 取放工站调试子视图（由 StationDebugView 通过 [StationUIAttribute] 发现并导航）
            containerRegistry.RegisterForNavigation<PickPlaceStationDebugView, PickPlaceStationDebugViewModel>(
                NavigationConstants.Views.PickPlaceStationDebugView);

            //containerRegistry.RegisterForNavigation<MainView, MainViewModel>(NavigationConstants.Views.MainView);
        }
    }
}