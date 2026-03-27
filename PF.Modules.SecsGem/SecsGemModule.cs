using Microsoft.EntityFrameworkCore;
using PF.Core.Constants;
using PF.Core.Interfaces.Communication.TCP;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.DataBase;
using PF.Core.Interfaces.SecsGem.Params;
using PF.Infrastructure.Communication.TCP;
using PF.Infrastructure.SecsGem;
using PF.Infrastructure.SecsGem.Command;
using PF.Infrastructure.SecsGem.Incentive;
using PF.Infrastructure.SecsGem.Param;
using PF.Infrastructure.SecsGem.Tools;
using PF.Modules.SecsGem.ViewModels;
using PF.Modules.SecsGem.Views;
using PF.SecsGem.DataBase;
using PF.UI.Infrastructure.Navigation;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.SecsGem
{
    public class SecsGemModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            

            // View + ViewModel 注册（支持 Prism 导航）
            containerRegistry.RegisterForNavigation<SecsGemDebugView, SecsGemDebugViewModel>();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 解析导航菜单服务，自动扫描当前程序集
            // 这样 DeviceDebugView 上的 [ModuleNavigation] 特性就会被识别，自动添加到系统侧边栏菜单中
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            Task.Run(async () =>
            {
                var db = containerProvider.Resolve<ISecsGemDataBase>();
                await db.InitializationDataBase();
                var mgr = containerProvider.Resolve<ISecsGemManger>();
                await mgr.InitializeAsync();
            });
        }
    }
}
