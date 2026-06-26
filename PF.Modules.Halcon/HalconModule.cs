using PF.Modules.Halcon.ViewModels;
using PF.Modules.Halcon.Views;
using PF.UI.Infrastructure.Navigation;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.Halcon;

/// <summary>Halcon 视觉调试 Prism 模块入口</summary>
public class HalconModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        // 根页面：Dashboard 内嵌 HalconDebugView + PipelineRunnerView，无需独立 ViewModel
        containerRegistry.RegisterForNavigation<HalconDashboardView>(
            HalconNavigationConstants.Views.Dashboard);

        // 子视图 ViewModel 注册（AutoWireViewModel 从容器解析）
        containerRegistry.Register<HalconDebugViewModel>();
        containerRegistry.Register<PipelineRunnerViewModel>();

        // ROI 编辑弹窗（通过 IDialogService 调用）
        containerRegistry.RegisterDialog<RoiEditorDialogView, RoiEditorDialogViewModel>(
            HalconNavigationConstants.Dialogs.RoiEditor);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        var navMenuService = containerProvider.Resolve<INavigationMenuService>();
        navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
    }
}
