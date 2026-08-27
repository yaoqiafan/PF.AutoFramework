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
        // 根页面（侧边栏入口）
        containerRegistry.RegisterForNavigation<HalconDashboardView, HalconDashboardViewModel>(
            HalconNavigationConstants.Views.Dashboard);

        // 子页面（在 HalconContentRegion 内导航）
        containerRegistry.RegisterForNavigation<HalconDebugView,    HalconDebugViewModel>(
            HalconNavigationConstants.Views.HalconDebug);

        containerRegistry.RegisterForNavigation<PipelineRunnerView, PipelineRunnerViewModel>(
            HalconNavigationConstants.Views.PipelineRunner);

        // ROI 编辑弹窗
        containerRegistry.RegisterDialog<RoiEditorDialogView, RoiEditorDialogViewModel>(
            HalconNavigationConstants.Dialogs.RoiEditor);

        // ROI 形状模板编辑弹窗（画 ROI → 建模板 → 按名字存盘，框架侧独立能力，消费方直接复用）
        containerRegistry.RegisterDialog<ShapeTemplateEditorDialogView, ShapeTemplateEditorDialogViewModel>(
            HalconNavigationConstants.Dialogs.ShapeTemplateEditor);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var navMenuService = containerProvider.Resolve<INavigationMenuService>();
        navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
    }
}
