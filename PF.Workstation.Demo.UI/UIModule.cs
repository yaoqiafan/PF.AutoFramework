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
        }
    }
}