using PF.Core.Constants;
using PF.Workstation.Demo.UI.ViewModels;
using PF.Workstation.Demo.UI.Views;

namespace PF.Workstation.Demo.UI
{
    public class UIModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<GantryMechanismView, GantryMechanismViewModel>("GantryMechanismView");

            // 取放工站调试子视图（由 StationDebugView 通过 [StationUIAttribute] 发现并导航）
            containerRegistry.RegisterForNavigation<PickPlaceStationDebugView, PickPlaceStationDebugViewModel>(
                NavigationConstants.Views.PickPlaceStationDebugView);
        }
    }
}