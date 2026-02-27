using PF.Core.Constants;
using PF.Modules.Debug.ViewModels;
using PF.Modules.Debug.Views;
using PF.UI.Infrastructure.Navigation;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.Debug
{
    /// <summary>
    /// 系统调试模块入口
    /// </summary>
    public class DebugModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 解析导航菜单服务，自动扫描当前程序集
            // 这样 DeviceDebugView 上的 [ModuleNavigation] 特性就会被识别，自动添加到系统侧边栏菜单中
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 1. 注册硬件调试视图
            containerRegistry.RegisterForNavigation<HardwareDebugView, HardwareDebugViewModel>(NavigationConstants.Views.HardwareDebugView);

            // 2. 注册模组调试视图
            containerRegistry.RegisterForNavigation<MechanismDebugView, MechanismDebugViewModel>(NavigationConstants.Views.MechanismDebugView);


            containerRegistry.RegisterForNavigation<AxisDebugView, AxisDebugViewModel>(NavigationConstants.Views.AxisDebugView);

            containerRegistry.RegisterForNavigation<IODebugView, IODebugViewModel>(NavigationConstants.Views.IODebugView);

            // 3. 注册工站调试容器视图（子工站调试视图由各工站 UI 模块自行注册）
            containerRegistry.RegisterForNavigation<StationDebugView, StationDebugViewModel>(NavigationConstants.Views.StationDebugView);
        }
    }
}