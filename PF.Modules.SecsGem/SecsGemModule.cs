using DryIoc;
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
            var container = containerRegistry.GetContainer();
            var filePath = System.IO.Path.Combine(ConstGlobalParam.ConfigPath, "SecsGemConfig.db");

            // DbContext
            var dbContextOptions = new DbContextOptionsBuilder<SecsGemDbContext>()
                .UseSqlite($"Data Source={filePath}")
                .Options;
            container.RegisterInstance<DbContextOptions<SecsGemDbContext>>(dbContextOptions);
            container.Register<SecsGemDbContext>(reuse: Reuse.Singleton);

            container.Register<ISecsGemDataBase, SecsGemDataBaseManger>(reuse: Reuse.Singleton);
            container.Register<ICommandManager, SecsGemCommandManger>(reuse: Reuse.Singleton);
            container.Register<SecsGemMessageProcessor>(reuse: Reuse.Singleton);
            container.Register<IParams, ParamsManger>(reuse: Reuse.Singleton);
            container.Register<IClient, TCPClient>(reuse: Reuse.Singleton);
            container.Register<IinternalClient, InternalClient>(reuse: Reuse.Singleton);
            container.Register<ISecsGemMessageUpdater, SecsGemMessageUpdater>(reuse: Reuse.Singleton);
            container.Register<ISecsGemManger, SecsGemManger>(reuse: Reuse.Singleton);

            containerRegistry.RegisterForNavigation<ViewA, ViewModels.ViewAViewModel>("SecsGemView");
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
