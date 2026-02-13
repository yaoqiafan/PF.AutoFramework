using PF.Core.Constants;
using PF.Modules.Parameter.Dialog;
using PF.Modules.Parameter.Dialog.DialogViewModel;
using PF.Modules.Parameter.ViewModels;
using PF.Modules.Parameter.Views;
using PF.UI.Infrastructure.Navigation;
using System.ComponentModel;
using System.Reflection;

namespace PF.Modules.Parameter
{
    public class ParameterModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 解析导航服务并扫描当前程序集自动注册菜单
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ParameterView, ParameterViewModel>(NavigationConstants.Views.ParameterView);

            containerRegistry.RegisterForNavigation<ParamChangeDialog_Common, CommonChangeParamDialogViewModel>(NavigationConstants.Dialogs.CommonChangeParamDialog);
        }
    }
}