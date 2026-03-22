using PF.Core.Constants;
using PF.UI.Infrastructure.Navigation;
using PF.WorkStation.AutoOcr.UI.UserControls;
using PF.WorkStation.AutoOcr.UI.ViewModels;
using PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms;
using PF.WorkStation.AutoOcr.UI.Views;
using PF.WorkStation.AutoOcr.UI.Views.Mechanisms;
using System.Reflection;

namespace PF.WorkStation.AutoOcr.UI
{
    public class AutoOcrUIModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());


            containerProvider.Resolve<IRegionManager>().RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.MainView);
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MainView, MainViewModel>(NavigationConstants.Views.MainView);
            containerRegistry.RegisterForNavigation<HomeView, MainViewModel>(NavigationConstants.Views.HomeView);

            containerRegistry.RegisterForNavigation<OcrRecipeManageView, OcrRecipeManageViewModel>(nameof(OcrRecipeManageView));


            containerRegistry.RegisterForNavigation<RecipeDebugView, RecipeDebugViewModel>(nameof(RecipeDebugView));



            containerRegistry.RegisterForNavigation<Workstation1FeedingModelDebugView, Workstation1FeedingModelDebugViewModel>(nameof(Workstation1FeedingModelDebugView));

            containerRegistry.RegisterForNavigation<WorkStationDetectionModuleDebugView, WorkStationDetectionModuleDebugViewModel>(nameof(WorkStationDetectionModuleDebugView));

        }
    }
}