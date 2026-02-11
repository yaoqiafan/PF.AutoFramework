using PF.Modules.Parameter.Dialog;
using PF.Modules.Parameter.ViewModels;
using PF.Modules.Parameter.Views;
using PF.Core.Constants;
using PF.Modules.Parameter.Dialog.DialogViewModel;

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