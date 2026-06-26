using PF.Modules.Halcon.ViewModels;
using PF.Modules.Halcon.Views;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.Halcon;

/// <summary>Halcon 视觉调试 Prism 模块入口</summary>
public class HalconModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterForNavigation<HalconDashboardView,  HalconDashboardViewModel>(
            HalconNavigationConstants.Views.Dashboard);

        containerRegistry.RegisterForNavigation<ProcedureDebugView,   ProcedureDebugViewModel>(
            HalconNavigationConstants.Views.ProcedureDebug);

        containerRegistry.RegisterForNavigation<PipelineRunnerView,   PipelineRunnerViewModel>(
            HalconNavigationConstants.Views.PipelineRunner);

        containerRegistry.RegisterForNavigation<HalconDebugView,      HalconDebugViewModel>(
            HalconNavigationConstants.Views.HalconDebug);

        // ROI 编辑弹窗（通过 IDialogService 调用）
        containerRegistry.RegisterDialog<RoiEditorDialogView, RoiEditorDialogViewModel>(
            HalconNavigationConstants.Dialogs.RoiEditor);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // 扫描当前程序集，自动将 [ModuleNavigation] 标注的视图注册到侧边栏菜单
        var navMenuService = containerProvider.Resolve<INavigationMenuService>();
        navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
    }
}
