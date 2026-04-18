using PF.Core.Constants;
using PF.Core.Interfaces.Production;
using PF.Modules.Production.ViewModels;
using PF.Modules.Production.Views;
using PF.UI.Infrastructure.Navigation;
using System.Reflection;

namespace PF.Modules.Production
{
    /// <summary>
    /// 生产数据记录模块。
    /// 提供实时监控视图（ProductionMonitorView）和历史查询视图（ProductionHistoryView）。
    /// 与 SecsGem 模块完全解耦，适用于任何设备的生产数据记录场景。
    /// </summary>
    public class ProductionRecordModule : IModule
    {
        /// <summary>注册依赖类型</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ProductionMonitorView, ProductionMonitorViewModel>(
                NavigationConstants.Views.ProductionMonitorView);
         
        }

        /// <summary>模块初始化回调</summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 初始化生产数据服务（建表）
            var service = containerProvider.Resolve<IProductionDataService>();
            _ = service.InitializeAsync();

            // 扫描当前程序集自动注册菜单
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
