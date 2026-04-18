using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Logging;
using PF.Modules.Alarm.Dialogs;
using PF.Modules.Alarm.ViewModels;
using PF.Modules.Alarm.Views;
using PF.UI.Infrastructure.Navigation;
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
        private readonly ILogService _logService;

        /// <summary>初始化报警模块</summary>
        public AlarmModule(IRegionManager regionManager, ILogService logService)
        {
            _regionManager = regionManager;
            _logService    = logService;
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
            try
            {
                // 初始化报警字典（反射扫描 + 数据库加载）
                var dictService = containerProvider.Resolve<IAlarmDictionaryService>();
                await dictService.InitializeAsync();

                _logService.Info("报警模块初始化完成", "AlarmModule");
            }
            catch (Exception ex)
            {
                _logService.Error("报警模块初始化失败", "AlarmModule", ex);
            }

            // 扫描当前程序集，自动注册菜单导航项
            var navMenuService = containerProvider.Resolve<INavigationMenuService>();
            navMenuService.RegisterAssembly(Assembly.GetExecutingAssembly());
        }
    }
}
