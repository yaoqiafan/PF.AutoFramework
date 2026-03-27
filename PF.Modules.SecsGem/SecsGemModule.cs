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
using Prism.Ioc;
using Prism.Modularity;

namespace PF.Modules.SecsGem
{
    public class SecsGemModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            var filePath = System.IO.Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");

            var dbContextOptions = new DbContextOptionsBuilder<SecsGemDbContext>()
                .UseSqlite($"Data Source={filePath}")
                .Options;
            containerRegistry.RegisterInstance<DbContextOptions<SecsGemDbContext>>(dbContextOptions);
            containerRegistry.RegisterSingleton<SecsGemDbContext>();

            containerRegistry.RegisterSingleton<ISecsGemDataBase, SecsGemDataBaseManger>();
            containerRegistry.RegisterSingleton<ICommandManager, SecsGemCommandManger>();
            containerRegistry.RegisterSingleton<SecsGemMessageProcessor>();
            containerRegistry.RegisterSingleton<IParams, ParamsManger>();
            containerRegistry.RegisterSingleton<IClient, TCPClient>();
            containerRegistry.RegisterSingleton<IinternalClient, InternalClient>();
            containerRegistry.RegisterSingleton<ISecsGemMessageUpdater, SecsGemMessageUpdater>();
            containerRegistry.RegisterSingleton<ISecsGemManger, SecsGemManger>();

            // View + ViewModel 注册（支持 Prism 导航）
            containerRegistry.RegisterForNavigation<SecsGemDebugView, SecsGemDebugViewModel>();
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
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
