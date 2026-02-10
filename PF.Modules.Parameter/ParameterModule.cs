using PF.Common.Param.Dialog;
using PF.Common.Param.ViewModels;
using PF.Common.Param.Views;
using PF.Modules.Parameter.Dialog.DialogViewModel;
using PF.Modules.Parameter.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace PF.Modules.Parameter
{
    public class ParameterModule : IModule
    {
        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ParameterView, ParameterViewModel>(NavigationConstants.Views.ParameterView);

            containerRegistry.RegisterForNavigation<ParamChangeDialog_Common, CommonChangeParamDialogViewModel>(NavigationConstants.Dialogs.CommonChangeParamDialog);
        }
    }
}