using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using PF.Modules.Alarm.Dialogs;
using PF.Modules.Alarm.ViewModels;
using PF.Modules.Alarm.Views;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.Operation;
using System.Reflection;

namespace PF.Modules.Alarm
{
    /// <summary>
    /// 异常处理展示模块 (Alarm Management Module)。
    /// 提供活跃报警看板、历史查询、SOP排故面板。
    /// </summary>
    public class AlarmModule : IModule
    {
        private readonly IRegionManager _regionManager;
        private readonly CategoryLogger _uiLogger;

        /// <summary>初始化报警模块</summary>
        public AlarmModule(IRegionManager regionManager, ILogService logService)
        {
            _regionManager = regionManager;
            _uiLogger = CategoryLoggerFactory.UI(logService);
        }

        /// <summary>注册报警模块的视图和对话框</summary>
        public void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterForNavigation<AlarmCenterView, AlarmCenterViewModel>(
                NavigationConstantsAlarm.AlarmCenterView);

            // Operator 及以上均可访问报警中心（故障处理是基本权限）
            DefaultPermissions.RegisterViews(
                UserLevel.Operator,
                NavigationConstantsAlarm.AlarmCenterView);


            containerRegistry.RegisterDialogWindow<PFAlarmBaseWindow>(nameof(PFAlarmBaseWindow));

            containerRegistry.RegisterDialog<AlarmDetailCardView, AlarmDetailCardViewModel>(nameof(AlarmDetailCardView));
        }

        /// <summary>模块初始化时加载报警字典并注册菜单</summary>
        public async void OnInitialized(IContainerProvider containerProvider)
        {
            // 菜单与操作日志键必须同步注册、放在任何 await 之前：async void 在第一个真正让出的 await 处就返回，
            // 之后的代码可能晚于宿主的 PermissionHelper.Initialize / 主窗口菜单刷新执行，报警中心就不在菜单里
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
            OperationLogKeyRegistry.RegisterAssembly(Assembly.GetExecutingAssembly());

            try
            {
                // 宿主（PFApplicationBase）已在 Splash 第一步加载过字典，这里幂等兜底，供不继承 PFApplicationBase 的宿主使用
                var dictService = containerProvider.Resolve<IAlarmDictionaryService>();
                await dictService.InitializeAsync();

                _uiLogger.Info("报警模块初始化完成");
            }
            catch (Exception ex)
            {
                _uiLogger.Error("报警模块初始化失败", ex);
            }
        }
    }
}
