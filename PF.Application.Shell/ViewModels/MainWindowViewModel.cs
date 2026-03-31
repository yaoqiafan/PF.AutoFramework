using DryIoc.ImTools;
using log4net.Core;
using Microsoft.Extensions.DependencyInjection;
using PF.Application.Shell.CustomConfiguration.Param;
using PF.Application.Shell.Services;
using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using PF.UI.Controls;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using Prism.Navigation.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace PF.Application.Shell.ViewModels
{
    public class MainWindowViewModel : RegionViewModelBase
    {
        #region 私有字段
        private readonly IContainerProvider _containerProvider;
        private readonly IParamService _paramService;
        private readonly IUserService _userService;
        private readonly INavigationMenuService _navigationMenuService;
        private ILogService _logService;
        private CommonSettings _commonSettings;

        private CategoryLogger _dbLogger;
        private CategoryLogger _systemLogger;
        private CategoryLogger _custom;
        private CancellationTokenSource _cts;
        private Task _runningTask;

        // 无操作自动降权计时器（60 秒无鼠标/键盘操作 → 重置为 Operator）
        private readonly IdleMonitorService _idleMonitor =
            new IdleMonitorService(TimeSpan.FromSeconds(600000));
        #endregion

        #region 公共集合
        public ObservableCollection<NavigationItem> MenuItems { get; } = new ObservableCollection<NavigationItem>();
        #endregion

        #region 构造函数
        public MainWindowViewModel(IParamService paramService, IUserService userService, INavigationMenuService navigationMenuService, IContainerProvider containerProvider,CommonSettings commonSettings)
        {
            _paramService = paramService;
            _userService = userService;
            _navigationMenuService = navigationMenuService;
            _containerProvider = containerProvider;
            _commonSettings = commonSettings;

            _userService.CurrentUserChanged += OnUserChanged;
            CurrentUser = _userService.CurrentUser ?? new UserInfo { Root = UserLevel.Null, AccessibleViews = new List<string>() };

            _idleMonitor.IdleTimeout += OnIdleTimeout;

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
        #endregion

        #region 用户变更处理
        private void OnUserChanged(object sender, UserInfo? newUser)
        {
            CurrentUser = newUser ?? new UserInfo { Root = UserLevel.Null, AccessibleViews = new List<string>() };

            // 有真实权限时启动空闲计时；Null（已完全注销）时停止
            if (CurrentUser.Root > UserLevel.Operator)
                _idleMonitor.Start();
            else
                _idleMonitor.Stop();

            RefreshMenu();
            EventAggregator.GetEvent<UserChangedEvent>().Publish(CurrentUser);
            if (CurrentUser.Root == UserLevel.Null)
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    // 重置主显示区域，确保注销后屏幕不留敏感数据
                    if (RegionManager.Regions.ContainsRegionWithName(NavigationConstants.Regions.SoftwareViewRegion))
                    {
                        var region = RegionManager.Regions[NavigationConstants.Regions.SoftwareViewRegion];

                        foreach (var view in region.Views.ToArray())
                        {
                            region.Remove(view);
                        }
                    }
                    SelectedMenuItem = null;
                });
            }
        }

        /// <summary>
        /// 空闲超时回调：将当前权限降级为内置 Operator，并清空当前页面内容。
        /// </summary>
        private void OnIdleTimeout(object? sender, EventArgs e)
        {
            _logService?.Info("检测到 60 秒无操作，权限自动重置为 Operator", "IdleMonitor");

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // 重置主显示区域，确保注销后屏幕不留敏感数据
                if (RegionManager.Regions.ContainsRegionWithName(NavigationConstants.Regions.SoftwareViewRegion))
                {
                    var region = RegionManager.Regions[NavigationConstants.Regions.SoftwareViewRegion];

                    foreach (var view in region.Views.ToArray())
                    {
                        region.Remove(view);
                    }
                }

                SelectedMenuItem = null;
            });

            _userService.ResetToOperator();
        }
        #endregion

        #region 菜单刷新
        private void RefreshMenu()
        {
            var filtered = FilterMenuForDisplay(_navigationMenuService.MenuItems);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MenuItems.Clear();
                foreach (var item in filtered)
                {
                    MenuItems.Add(item);
                }
            });
            RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.MainView, NavigationComplete);
        }

        /// <summary>
        /// Administrator 及以下等级的页面始终在菜单中显示；
        /// SuperUser 专属页面仅当当前登录用户为 SuperUser 时才显示。
        /// </summary>
        private ObservableCollection<NavigationItem> FilterMenuForDisplay(IEnumerable<NavigationItem> items)
        {
            var result = new ObservableCollection<NavigationItem>();
            if (items == null) return result;

            bool isSuperUser = CurrentUser?.Root == UserLevel.SuperUser;
            var adminViews = DefaultPermissions.GetAccessibleViews(UserLevel.Administrator);

            foreach (var item in items)
            {
                var cloned = new NavigationItem
                {
                    ViewName = item.ViewName,
                    Title = item.Title,
                    Icon = item.Icon,
                    Order = item.Order,
                    NavigationParameter = item.NavigationParameter,
                    Children = new ObservableCollection<NavigationItem>()
                };

                if (item.Children?.Any() == true)
                {
                    var filteredChildren = FilterMenuForDisplay(item.Children);
                    if (filteredChildren.Any())
                    {
                        cloned.Children = filteredChildren;
                        result.Add(cloned);
                    }
                }
                else
                {
                    bool isAdminVisible = adminViews.Contains(item.ViewName) || IsWhiteListView(item.ViewName);
                    if (isAdminVisible || isSuperUser)
                        result.Add(cloned);
                }
            }
            return result;
        }

        private bool IsWhiteListView(string viewName)
        {
            if (string.IsNullOrEmpty(viewName)) return false;
            if (NavigationConstantMapper.GetCategory(viewName) == nameof(NavigationConstants.Dialogs)) return true;
            if (viewName == NavigationConstants.Views.MainView || viewName == NavigationConstants.Views.HomeView) return true;
            return false;
        }
        #endregion

        #region 导航处理
        private void OnNavigated(FunctionEventArgs<object> args)
        {
            if (args != null && args.Info is SideMenuItem sideMenuItem)
            {
                if (sideMenuItem.Tag is NavigationItem navItem && !string.IsNullOrEmpty(navItem.ViewName))
                {
                    string viewName = navItem.ViewName;
                    string category = NavigationConstantMapper.GetCategory(viewName);

                    // ── 页面权限拦截（唯一检查点）────────────────────────────────
                    if ( !_userService.HasPagePermission(viewName))
                    {
                        _logService?.Warn($"用户 [{CurrentUser?.UserName}] 尝试访问无权限页面: {viewName}", "Security");
                        var displayName = PermissionHelper.GetViewDisplayName(viewName);
                        MessageService.ShowMessage(
                            $"您无权访问「{displayName}」页面，请联系管理员在「权限管控 → 窗体权限更改」中配置相应权限。",
                            "权限不足",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        SelectedMenuItem = null;
                        return;
                    }
                    // ── 权限拦截结束 ─────────────────────────────────────────────

                    if (IsParameterView(viewName))
                    {
                        var parameters = new NavigationParameters();
                        parameters.Add("TargetParamType", viewName);
                        RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.ParameterView, NavigationComplete, parameters);
                        return;
                    }

                    switch (category)
                    {
                        case nameof(NavigationConstants.Views):
                            RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, viewName, NavigationComplete);
                            break;

                        case nameof(NavigationConstants.Dialogs):
                            DialogService.ShowDialog(NavigationConstants.Dialogs.LoginView, OnLoginOverCallback);
                            SelectedMenuItem = null;
                            break;
                    }

                    if (category==null)
                    {
                        bool isRegistered = _containerProvider.IsRegistered<object>(viewName);

                        if (isRegistered)
                        {
                            RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, viewName, NavigationComplete);
                        }
                    }
                }
            }
        }

        private bool IsParameterView(string viewName)
        {
            return viewName == NavigationConstants.Views.ParameterView_SystemConfigParam ||
                   viewName == NavigationConstants.Views.ParameterView_UserLoginParam ||
                   viewName == NavigationConstants.Views.ParameterView_HardwareParam;
        }

        private void OnLoginOverCallback() { }

        private void NavigationComplete(NavigationResult result)
        {
            if (result.Success == false && result.Exception != null)
            {
                _logService?.Error($"导航失败: {result.Exception.Message}", "System", result.Exception);
            }
        }
        #endregion

        #region 公共属性
        private string _SoftWareName = string.Empty;
        public string SoftWareName
        {
            get { return _SoftWareName; }
            set { SetProperty(ref _SoftWareName, value); }
        }

        private object _selectedMenuItem;
        public object SelectedMenuItem
        {
            get { return _selectedMenuItem; }
            set { SetProperty(ref _selectedMenuItem, value); }
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
        #endregion

        #region 命令属性
        public ICommand LoadCommand { get; set; }
        public ICommand SwitchItemCmd { get; set; }
        public ICommand ChangeExpandCmd { get; set; }
        #endregion

        #region 加载初始化
        private async void OnLoading()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();
            _dbLogger = CategoryLoggerFactory.Database(_logService);
            _systemLogger = CategoryLoggerFactory.System(_logService);
            _custom = CategoryLoggerFactory.Custom(_logService);

            RefreshMenu();

            if (CurrentUser == null || CurrentUser.Root == UserLevel.Null)
            {
                DialogService.ShowDialog(NavigationConstants.Dialogs.LoginView, OnLoginOverCallback);
            }

            try
            {
                SoftWareName = $"{_commonSettings.SoftWareName}";
                CoName =  _commonSettings.COName;
            }
            catch { }

            UPdataTime();
        }
        #endregion

        #region 公共时间刷新
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
            if (_runningTask != null) await _runningTask;
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
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }
        #endregion
    }
}