using PF.Core.Constants;
using PF.Core.Enums;
using PF.Modules.Debug.Dialogs;
using PF.Modules.Debug.ViewModels;
using PF.Modules.Debug.Views;
using PF.Modules.Debug.Views.Communication;
using PF.Modules.Debug.Views.Hardware;
using PF.Modules.Debug.Views.Mechanisms;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.Operation;
using Prism.Ioc;
using Prism.Modularity;
using System.Reflection;

namespace PF.Modules.Debug
{
    /// <summary>
    /// 系统调试模块入口
    /// </summary>
    public class DebugModule : IModule
    {
        /// <summary>模块初始化时注册导航菜单</summary>
        public void OnInitialized(IContainerProvider containerProvider)
        {
            // 解析导航菜单服务，自动扫描当前程序集
            // 这样 DeviceDebugView 上的 [ModuleNavigation] 特性就会被识别，自动添加到系统侧边栏菜单中
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());

            OperationLogKeyRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());

            // 通讯综合调试视图跟硬件/工站调试一样，仅限工程师及以上权限可见
            DefaultPermissions.RegisterViews(UserLevel.Engineer, NavigationConstants.Views.CommunicationDebugView);
        }

        /// <summary>注册调试模块的视图和对话框</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            // 1. 注册硬件调试视图
            containerRegistry.RegisterForNavigation<HardwareDebugView, HardwareDebugViewModel>(NavigationConstants.Views.HardwareDebugView);

            // 2. 注册模组调试视图
            containerRegistry.RegisterForNavigation<MechanismDebugView, MechanismDebugViewModel>(NavigationConstants.Views.MechanismDebugView);

            // 线扫检测模组是框架级模组（在 PF.Infrastructure 里），其调试视图随框架一起提供，
            // 不必每个项目自己写一份。key 必须与 [MechanismUI] 的 MechanismViewName 完全一致。
            containerRegistry.RegisterForNavigation<LineScanDetectionModuleDebugView, LineScanDetectionModuleDebugViewModel>(
                nameof(LineScanDetectionModuleDebugView));


            containerRegistry.RegisterForNavigation<AxisDebugView, AxisDebugViewModel>(NavigationConstants.Views.AxisDebugView);

            containerRegistry.RegisterForNavigation<IODebugView, IODebugViewModel>(NavigationConstants.Views.IODebugView);

            containerRegistry.RegisterForNavigation<BarcodeScanDebugView, BarcodeScanDebugViewModel>(NavigationConstants.Views.BarcodeScanDebugView);



            // 3. 注册工站调试容器视图（子工站调试视图由各工站 UI 模块自行注册）
            containerRegistry.RegisterForNavigation<StationDebugView, StationDebugViewModel>(NavigationConstants.Views.StationDebugView);

            // 4. 注册控制卡级调试视图
            containerRegistry.RegisterForNavigation<CardDebugView, CardDebugViewModel>(NavigationConstants.Views.CardDebugView);

            containerRegistry.RegisterForNavigation<CameraDebugView, CameraDebugViewModel>(NavigationConstants.Views.CameraDebugView);

            containerRegistry.RegisterForNavigation<LightControllerDebugView, LightControllerDebugViewModel >(NavigationConstants.Views.LightControllerDebugView);

            // 线阵相机与其采集卡各自一个调试页：采集卡是独立设备（切帧发生在卡上），不是相机的附属面板
            containerRegistry.RegisterForNavigation<LineScanCameraDebugView, LineScanCameraDebugViewModel>(NavigationConstants.Views.LineScanCameraDebugView);
            containerRegistry.RegisterForNavigation<FrameGrabberDebugView, FrameGrabberDebugViewModel>(NavigationConstants.Views.FrameGrabberDebugView);

            containerRegistry.RegisterDialog<AxisParamDialog, AxisParamDialogViewModel>(nameof(AxisParamDialog));

            // 5. 注册通讯综合调试容器视图 + 三个叶子调试视图
            containerRegistry.RegisterForNavigation<CommunicationDebugView, CommunicationDebugViewModel>(NavigationConstants.Views.CommunicationDebugView);
            containerRegistry.RegisterForNavigation<TcpServerDebugView, TcpServerDebugViewModel>(NavigationConstants.Views.TcpServerDebugView);
            containerRegistry.RegisterForNavigation<TcpClientDebugView, TcpClientDebugViewModel>(NavigationConstants.Views.TcpClientDebugView);
            containerRegistry.RegisterForNavigation<FileTransferDebugView, FileTransferDebugViewModel>(NavigationConstants.Views.FileTransferDebugView);
            containerRegistry.RegisterForNavigation<ModbusRtuDebugView, ModbusRtuDebugViewModel>(NavigationConstants.Views.ModbusRtuDebugView);
            containerRegistry.RegisterForNavigation<ModbusTcpDebugView, ModbusTcpDebugViewModel>(NavigationConstants.Views.ModbusTcpDebugView);
            containerRegistry.RegisterForNavigation<SerialPortDebugView, SerialPortDebugViewModel>(NavigationConstants.Views.SerialPortDebugView);

            // 6. 注册三个通讯实例参数修改对话框
            containerRegistry.RegisterDialog<TcpServerParamDialog, TcpServerParamDialogViewModel>(nameof(TcpServerParamDialog));
            containerRegistry.RegisterDialog<TcpClientParamDialog, TcpClientParamDialogViewModel>(nameof(TcpClientParamDialog));
            containerRegistry.RegisterDialog<FileTransferParamDialog, FileTransferParamDialogViewModel>(nameof(FileTransferParamDialog));
            containerRegistry.RegisterDialog<ModbusRtuParamDialog, ModbusRtuParamDialogViewModel>(nameof(ModbusRtuParamDialog));
            containerRegistry.RegisterDialog<ModbusTcpParamDialog, ModbusTcpParamDialogViewModel>(nameof(ModbusTcpParamDialog));
            containerRegistry.RegisterDialog<SerialPortParamDialog, SerialPortParamDialogViewModel>(nameof(SerialPortParamDialog));
        }
    }
}