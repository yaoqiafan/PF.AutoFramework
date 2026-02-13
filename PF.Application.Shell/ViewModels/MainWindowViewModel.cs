using Microsoft.Extensions.DependencyInjection;
using PF.Application.Shell.CustomConfiguration.Logging;
using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using PF.UI.Controls;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using System;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PF.Application.Shell.ViewModels
{
    public class MainWindowViewModel : RegionViewModelBase
    {
        private readonly IParamService _paramService;
        private readonly IUserService _userService;
        private readonly INavigationMenuService _navigationMenuService;
        private ILogService _logService;

        private CategoryLogger _dbLogger;
        private CategoryLogger _systemLogger;
        private CategoryLogger _custom;
        private CancellationTokenSource _cts;
        private Task _runningTask;

        public ObservableCollection<NavigationItem> MenuItems => _navigationMenuService.MenuItems;

        public MainWindowViewModel(IParamService paramService, IUserService userService, INavigationMenuService navigationMenuService)
        {
            _paramService = paramService;
            _userService = userService;
            _navigationMenuService = navigationMenuService; // 注入导航服务

            _userService.CurrentUserChanged += OnUserChanged;
            CurrentUser = _userService.CurrentUser ?? new UserInfo();

            LoadCommand = new DelegateCommand(OnLoading);
            SwitchItemCmd = new DelegateCommand<FunctionEventArgs<object>>(OnNavigated);
            ChangeExpandCmd = new DelegateCommand<string>((e) =>
            {
                if (Enum.TryParse<ExpandMode>(e, out ExpandMode result))
                {
                    Expand = result;
                }
            });
        }

        // 当 UserService 中的登录用户发生变化时触发
        private void OnUserChanged(object sender, UserInfo? newUser)
        {
            CurrentUser = newUser ?? new UserInfo();
        }

        //private void OnNavigated(FunctionEventArgs<object> args)
        //{
        //    if (args != null && args.Info is SideMenuItem sideMenuItem)
        //    {
        //        if (sideMenuItem.Tag != null)
        //        {
        //            string viewName = sideMenuItem.Tag.ToString();

        //            if (IsParameterView(viewName))
        //            {
        //                var parameters = new NavigationParameters();
        //                parameters.Add("TargetParamType", viewName);

        //                RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.ParameterView, NavigationComplete, parameters);
        //                return;
        //            }

        //            string category = NavigationConstantMapper.GetCategory(viewName);
        //            switch (category)
        //            {
        //                case nameof(NavigationConstants.Views):
        //                    RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, viewName, NavigationComplete);
        //                    break;
        //                case nameof(NavigationConstants.Dialogs):
        //                    // 打开登录弹窗，无需再手动赋值，因为有全局事件 OnUserChanged 在监听
        //                    DialogService.ShowDialog(NavigationConstants.Dialogs.LoginView, OnLoginOverCallback);
        //                    break;
        //            }
        //        }
        //    }
        //}


        // 在 MainWindowViewModel.cs 中
        private void OnNavigated(FunctionEventArgs<object> args)
        {
            if (args != null && args.Info is SideMenuItem sideMenuItem)
            {
                if (sideMenuItem.Tag is NavigationItem navItem && !string.IsNullOrEmpty(navItem.ViewName))
                {
                    if (navItem.IsDialog)
                    {
                        // 打开登录弹窗
                        DialogService.ShowDialog(navItem.ViewName, OnLoginOverCallback);
                    }
                    else
                    {
                        // 页面跳转，同时支持携带参数
                        var parameters = new NavigationParameters();
                        if (!string.IsNullOrEmpty(navItem.NavigationParameter))
                        {
                            parameters.Add("TargetParamType", navItem.NavigationParameter);
                        }

                        RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, navItem.ViewName, NavigationComplete, parameters);
                    }
                }
            }
        }

        private bool IsParameterView(string viewName)
        {
            return viewName == NavigationConstants.Views.ParameterView_SystemConfigParam ||
                   viewName == NavigationConstants.Views.ParameterView_CommonParam ||
                   viewName == NavigationConstants.Views.ParameterView_UserLoginParam ||
                   viewName == NavigationConstants.Views.ParameterView_HardWareParam;
        }

        private void OnLoginOverCallback()
        {
            // 由于通过事件驱动了，这里可以留空，或者处理特定的弹窗关闭逻辑
        }

        private void NavigationComplete(NavigationResult result)
        {
            if (result.Success == false && result.Exception != null)
            {
                _logService?.Error($"导航失败: {result.Exception.Message}", "System", result.Exception);
            }
        }

        private string _SoftWareName = string.Empty;
        public string SoftWareName
        {
            get { return _SoftWareName; }
            set { SetProperty(ref _SoftWareName, value); }
        }

        private string _CoName = string.Empty;
        public string CoName
        {
            get { return _CoName; }
            set { SetProperty(ref _CoName, value); }
        }

        private string _sysTime = string.Empty;
        public string SysTime
        {
            get { return _sysTime; }
            set { SetProperty(ref _sysTime, value); }
        }

        private UserInfo _currentUser = new UserInfo();
        public UserInfo CurrentUser
        {
            get { return _currentUser; }
            set { SetProperty(ref _currentUser, value); }
        }

        private ExpandMode _ExpandMode = ExpandMode.ShowAll;
        public ExpandMode Expand
        {
            get { return _ExpandMode; }
            set { SetProperty(ref _ExpandMode, value); }
        }

        public ICommand LoadCommand { get; set; }
        public ICommand SwitchItemCmd { get; set; }
        public ICommand ChangeExpandCmd { get; set; }

        private async void OnLoading()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();

            _dbLogger = CategoryLoggerFactory.Database(_logService);
            _systemLogger = CategoryLoggerFactory.System(_logService);
            _custom = CategoryLoggerFactory.Custom(_logService);
            RaisePropertyChanged(nameof(MenuItems));
            try
            {
                string name = $"{await _paramService.GetParamAsync<string>("SoftWareName")}";
                SoftWareName = name;

                name = await _paramService.GetParamAsync<string>("COName");
                CoName = name;
            }
            catch
            {
            }

            UPdataTime();
        }

        #region 公共
        public void UPdataTime()
        {
            _cts = new CancellationTokenSource();
            _runningTask = Task.Factory.StartNew(
                () => WorkerMethod(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default
            );
        }

        public async Task StopAsync()
        {
            _cts?.Cancel();
            if (_runningTask != null)
            {
                await _runningTask;
            }
            _cts?.Dispose();
        }

        private async Task WorkerMethod(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    SysTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    await Task.Delay(500, ct);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
            }
        }
        #endregion
    }
}