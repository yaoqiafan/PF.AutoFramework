using PF.Core.Interfaces.Production;
using PF.Modules.ProductionRecord.ViewModels;
using PF.Modules.ProductionRecord.Views;
using PF.UI.Infrastructure.Navigation;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.ProductionRecord
{
    /// <summary>
    /// 生产数据记录模块。
    /// 提供实时监控视图（ProductionMonitorView）和历史查询视图（ProductionHistoryView）。
    /// 与 SecsGem 模块完全解耦，适用于任何设备的生产数据记录场景。
    /// </summary>
    public class ProductionRecordModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ProductionMonitorView, ProductionMonitorViewModel>(
                "ProductionMonitorView");
            containerRegistry.RegisterForNavigation<ProductionHistoryView, ProductionHistoryViewModel>(
                "ProductionHistoryView");
        }

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
