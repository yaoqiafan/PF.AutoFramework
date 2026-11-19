using PF.Core.Constants;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Modules.SecsGem.Dialogs;
using PF.Modules.SecsGem.Dialogs.ViewModels;
using PF.Modules.SecsGem.ViewModels;
using PF.Modules.SecsGem.Views;
using PF.UI.Infrastructure.Dialog;
using PF.UI.Infrastructure.Dialog.ViewModels;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.Operation;
using System.Reflection;

namespace PF.Modules.SecsGem
{
    /// <summary>SecsGem 模块</summary>
    public class SecsGemModule : IModule
    {
        /// <summary>注册依赖类型</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {


            containerRegistry.RegisterDialog<CommandEditDialog,    CommandEditDialogViewModel>("CommandEditDialog");
            containerRegistry.RegisterDialog<SecsNodeConfigDialog, SecsNodeConfigDialogViewModel>("SecsNodeConfigDialog");
            containerRegistry.RegisterDialog<VidSelectDialog,      VidSelectDialogViewModel>("VidSelectDialog");

            // View + ViewModel 注册（支持 Prism 导航）
            containerRegistry.RegisterForNavigation<SecsGemDebugView, SecsGemDebugViewModel>(
                NavigationConstants.Views.SecsGemDebugView);


        }

        /// <summary>模块初始化回调</summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 注册导航菜单（扫描 [ModuleNavigation] 特性）
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            OperationLogKeyRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());

            // 宿主（PFApplicationBase）已在启动加载阶段、模组初始化之前建好库；这里幂等兜底
            // （EnsureCreated 对已存在的库是空操作），供不继承 PFApplicationBase 的宿主使用。
            // 此前结果不看：建表失败没有任何痕迹
            var logService = containerProvider.IsRegistered<PF.Core.Interfaces.Logging.ILogService>()
                ? containerProvider.Resolve<PF.Core.Interfaces.Logging.ILogService>()
                : null;
            Task.Run(async () =>
            {
                try
                {
                    var db = containerProvider.Resolve<ISecsGemDataBase>();
                    if (!await db.InitializationDataBase())
                        logService?.Error("SECS/GEM 数据库初始化失败（SecsGemConfig.db），SECS 功能可能不可用", "SecsGem");
                }
                catch (Exception ex)
                {
                    logService?.Error($"SECS/GEM 数据库初始化异常：{ex.Message}", "SecsGem", ex);
                }
            });
        }
    }
}
