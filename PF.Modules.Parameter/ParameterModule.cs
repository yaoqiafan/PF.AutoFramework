using PF.Core.Constants;
using PF.Modules.Parameter.Dialog;
using PF.Modules.Parameter.Dialog.Base;
using PF.Modules.Parameter.Dialog.DialogViewModel;
using PF.Modules.Parameter.Dialog.Mappers;
using PF.Modules.Parameter.Dialog.Mappers.Hardware;
using PF.Modules.Parameter.ViewModels;
using PF.Modules.Parameter.ViewModels.Models.Hardware;
using PF.Modules.Parameter.Views;
using PF.UI.Infrastructure.Navigation;
using System.Reflection;

namespace PF.Modules.Parameter
{
    /// <summary>参数管理模块</summary>
    public class ParameterModule : IModule
    {
        /// <summary>模块初始化时注册导航菜单，并预加载参数视图程序���</summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 此时所有模块程序集均已加载完毕，PreloadAssemblies 扫描结果最完整
            ViewFactory.PreloadAssemblies();

            // 解析导航服务并扫描当前程序集自动注册菜单
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }

        /// <summary>注册参数模块的视图、对话框及硬件配置视图路���</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<ParameterView, ParameterViewModel>(
                NavigationConstants.Views.ParameterView);

            containerRegistry.RegisterForNavigation<ParamChangeDialog_Common, CommonChangeParamDialogViewModel>(
                NavigationConstants.Dialogs.CommonChangeParamDialog);

            // 硬件配置参数视图路由（按 HardwareConfig.ImplementationClassName 分发）
            // 所有 View/Mapper 类型均属于本模块，注册责任归属于此
            ViewFactory.RegisterHardwareConfigType<LTDMCMotionCardParamView,          LTDMCMotionCardParamViewMapper>         ("LTDMCMotionCard");
            ViewFactory.RegisterHardwareConfigType<EtherCatAxisParamView,             EtherCatAxisParamViewMapper>            ("EtherCatAxis");
            ViewFactory.RegisterHardwareConfigType<EtherCatIOParamView,               EtherCatIOParamViewMapper>              ("EtherCatIO");
            ViewFactory.RegisterHardwareConfigType<HKBarcodeScanParamView,            HKBarcodeScanParamViewMapper>           ("HKBarcodeScan");
            ViewFactory.RegisterHardwareConfigType<KeyenceIntelligentCameraParamView, KeyenceIntelligentCameraParamViewMapper>("KeyenceIntelligentCamera");
            ViewFactory.RegisterHardwareConfigType<CTSLightControllerParamView,       CTSLightControllerParamViewMapper>      ("CTS_LightControoller");
        }
    }
}