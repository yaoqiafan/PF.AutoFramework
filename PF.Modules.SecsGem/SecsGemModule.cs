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
using System.Reflection;

namespace PF.Modules.SecsGem
{
    public class SecsGemModule : IModule
    {
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {


            containerRegistry.RegisterDialog<CommandEditDialog,    CommandEditDialogViewModel>("CommandEditDialog");
            containerRegistry.RegisterDialog<SecsNodeConfigDialog, SecsNodeConfigDialogViewModel>("SecsNodeConfigDialog");
            containerRegistry.RegisterDialog<VidSelectDialog,      VidSelectDialogViewModel>("VidSelectDialog");

            // View + ViewModel 注册（支持 Prism 导航）
            containerRegistry.RegisterForNavigation<SecsGemDebugView, SecsGemDebugViewModel>(
                NavigationConstants.Views.SecsGemDebugView);


        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 注册导航菜单（扫描 [ModuleNavigation] 特性）
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            Task.Run(async () =>
            {
                var db = containerProvider.Resolve<ISecsGemDataBase>();
                await db.InitializationDataBase();
            });
        }
    }
}
