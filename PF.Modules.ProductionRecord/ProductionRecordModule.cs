using PF.Core.Constants;
using PF.Core.Interfaces.Production;
using PF.Modules.Production.ViewModels;
using PF.Modules.Production.Views;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.Operation;
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

            containerRegistry.RegisterForNavigation<ProductionHistoryView, ProductionHistoryViewModel>(
               NavigationConstants.Views.ProductionHistoryView);

        }

        /// <summary>模块初始化回调</summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 宿主（PFApplicationBase）已在启动加载阶段初始化过，这里幂等兜底，供不继承 PFApplicationBase 的宿主使用。
            // 此前 fire-and-forget 且不观察结果：建表失败无任何痕迹，生产数据从此不落库
            var service = containerProvider.Resolve<IProductionDataService>();
            var logService = containerProvider.IsRegistered<PF.Core.Interfaces.Logging.ILogService>()
                ? containerProvider.Resolve<PF.Core.Interfaces.Logging.ILogService>()
                : null;
            _ = service.InitializeAsync().ContinueWith(t =>
                    logService?.Error(
                        $"生产数据服务初始化失败，生产数据将无法保存：{t.Exception?.GetBaseException().Message}",
                        "ProductionData", t.Exception?.GetBaseException()),
                TaskContinuationOptions.OnlyOnFaulted);

            // 扫描当前程序集自动注册菜单
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            OperationLogKeyRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
