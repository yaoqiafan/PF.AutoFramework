using PF.Core.Constants;
using PF.Infrastructure.Station;
using PF.UI.Infrastructure.Navigation;
using PF.WorkStation.AutoOcr.Stations;
using PF.WorkStation.AutoOcr.UI.UserControls;
using PF.WorkStation.AutoOcr.UI.ViewModels;
using PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms;
using PF.WorkStation.AutoOcr.UI.ViewModels.WorkStations;
using PF.WorkStation.AutoOcr.UI.Views;
using PF.WorkStation.AutoOcr.UI.Views.Mechanisms;
using PF.WorkStation.AutoOcr.UI.Views.WorkStations;
using System.Reflection;

namespace PF.WorkStation.AutoOcr.UI
{
    /// <summary>
    /// AutoOcrUIModule
    /// </summary>
    public class AutoOcrUIModule : IModule
    {
        /// <summary>
        /// OnInitialized
        /// </summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());


            containerProvider.Resolve<IRegionManager>().RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.MainView);

            DefaultPermissions.RegisterViews(Core.Enums.UserLevel.Administrator, nameof(OcrRecipeManageView));

           
        }
        /// <summary>
        /// RegisterTypes
        /// </summary>

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<MainView, MainViewModel>(NavigationConstants.Views.MainView);
            containerRegistry.RegisterForNavigation<HomeView, HomeViewModel >(NavigationConstants.Views.HomeView);

            containerRegistry.RegisterForNavigation<OcrRecipeManageView, OcrRecipeManageViewModel>(nameof(OcrRecipeManageView));


            containerRegistry.RegisterForNavigation<RecipeDebugView, RecipeDebugViewModel>(nameof(RecipeDebugView));

           containerRegistry.RegisterForNavigation<ChangeLotView , ChangeLotViewModel>(nameof(ChangeLotView));

            containerRegistry.RegisterForNavigation<ProductionHistoryView, ProductionHistoryViewModel>(
             NavigationConstants.Views.ProductionHistoryView);


            containerRegistry.RegisterForNavigation<Workstation1FeedingModelDebugView, WS1FeedingModelDebugViewModel>(nameof(Workstation1FeedingModelDebugView));

            containerRegistry.RegisterForNavigation<WorkStationSecsGemModuleDebugView , WSSecsGemModuleDebugViewModel >(nameof(WorkStationSecsGemModuleDebugView));


            containerRegistry.RegisterForNavigation<WorkStationDetectionModuleDebugView, WSDetectionModuleDebugViewModel>(nameof(WorkStationDetectionModuleDebugView));

            containerRegistry.RegisterForNavigation<WorkStation1MaterialPullingModuleDebugView, WS1MaterialPullingModuleDebugViewModel>(nameof(WorkStation1MaterialPullingModuleDebugView));

            containerRegistry .RegisterForNavigation <WorkStationDataModuleDebugView , WSDataModuleDebugViewModel>(nameof(WorkStationDataModuleDebugView));

            containerRegistry.RegisterForNavigation<WorkStation1FeedingStationDebugView, WS1FeedingStationDebugViewModel>(
             nameof(WorkStation1FeedingStationDebugView));

            containerRegistry.RegisterForNavigation<WorkStationDetectionStationDebugView, WSDetectionStationDebugViewModel>(
             nameof(WorkStationDetectionStationDebugView));
            containerRegistry.RegisterForNavigation< WorkStation1MaterialPullingStationDebugView, WS1MaterialPullingStationDebugViewModel>(nameof(WorkStation1MaterialPullingStationDebugView));

            // ── 工位 2 调试视图注册 ──
            containerRegistry.RegisterForNavigation<Workstation2FeedingModelDebugView, WS2FeedingModelDebugViewModel>(nameof(Workstation2FeedingModelDebugView));
            containerRegistry.RegisterForNavigation<WorkStation2MaterialPullingModuleDebugView, WS2MaterialPullingModuleDebugViewModel>(nameof(WorkStation2MaterialPullingModuleDebugView));
            containerRegistry.RegisterForNavigation<WorkStation2FeedingStationDebugView, WS2FeedingStationDebugViewModel>(nameof(WorkStation2FeedingStationDebugView));
            containerRegistry.RegisterForNavigation<WorkStation2MaterialPullingStationDebugView, WS2MaterialPullingStationDebugViewModel>(nameof(WorkStation2MaterialPullingStationDebugView));

        }


        private void NavigationComplete(NavigationResult result)
        {
            if (result.Success == false && result.Exception != null)
            {
               
            }
        }
    }
}