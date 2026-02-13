using Microsoft.Extensions.DependencyInjection;
using PF.Application.Shell.CustomConfiguration.Logging;
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
using System.Windows.Input;

namespace PF.Application.Shell.ViewModels
{
    public class MainWindowViewModel : RegionViewModelBase
    {
        #region 私有字段
        private readonly IParamService _paramService;
        private readonly IUserService _userService;
        private readonly INavigationMenuService _navigationMenuService;
        private ILogService _logService;

        private CategoryLogger _dbLogger;
        private CategoryLogger _systemLogger;
        private CategoryLogger _custom;
        private CancellationTokenSource _cts;
        private Task _runningTask;
        #endregion

        #region 公共集合
        public ObservableCollection<NavigationItem> MenuItems { get; } = new ObservableCollection<NavigationItem>();
        #endregion

        #region 构造函数
        public MainWindowViewModel(IParamService paramService, IUserService userService, INavigationMenuService navigationMenuService)
        {
            _paramService = paramService;
            _userService = userService;
            _navigationMenuService = navigationMenuService;

            _userService.CurrentUserChanged += OnUserChanged;
            CurrentUser = _userService.CurrentUser ?? new UserInfo { Root = UserLevel.Null, AccessibleViews = new List<string>() };

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

            RefreshMenu();

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
        #endregion

        #region 菜单刷新与权限过滤
        private void RefreshMenu()
        {
            var allSystemMenus = _navigationMenuService.MenuItems;
            var filteredMenus = FilterMenuTree(allSystemMenus, CurrentUser);

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MenuItems.Clear();
                foreach (var item in filteredMenus)
                {
                    MenuItems.Add(item);
                }
            });
        }

        private bool IsWhiteListView(string viewName)
        {
            if (string.IsNullOrEmpty(viewName)) return false;
            if (NavigationConstantMapper.GetCategory(viewName) == nameof(NavigationConstants.Dialogs)) return true;
            return false;
        }

        private ObservableCollection<NavigationItem> FilterMenuTree(IEnumerable<NavigationItem> originalItems, UserInfo user)
        {
            var filteredCollection = new ObservableCollection<NavigationItem>();

            if (originalItems == null || !originalItems.Any()) return filteredCollection;

            user ??= new UserInfo { Root = UserLevel.Null, AccessibleViews = new List<string>() };

            bool isSuperAdmin = user.Root == UserLevel.SuperUser || user.Root == UserLevel.Administrator;
            var allowedViews = user.AccessibleViews ?? new List<string>();

            foreach (var item in originalItems)
            {
                var clonedItem = new NavigationItem
                {
                    ViewName = item.ViewName,
                    Title = item.Title,
                    Icon = item.Icon,
                    Order = item.Order,
                    NavigationParameter = item.NavigationParameter,
                    Children = new ObservableCollection<NavigationItem>()
                };

                if (item.Children != null && item.Children.Any())
                {
                    var filteredChildren = FilterMenuTree(item.Children, user);
                    if (filteredChildren.Any())
                    {
                        clonedItem.Children = filteredChildren;
                        filteredCollection.Add(clonedItem);
                    }
                }
                else
                {
                    if (isSuperAdmin || allowedViews.Contains(clonedItem.ViewName) || IsWhiteListView(clonedItem.ViewName))
                    {
                        filteredCollection.Add(clonedItem);
                    }
                }
            }
            return filteredCollection;
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

                    if (IsParameterView(viewName))
                    {
                        var parameters = new NavigationParameters();
                        parameters.Add("TargetParamType", viewName);
                        RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, NavigationConstants.Views.ParameterView, NavigationComplete, parameters);
                        return;
                    }

                    string category = NavigationConstantMapper.GetCategory(viewName);
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
                SoftWareName = $"{await _paramService.GetParamAsync<string>("SoftWareName")}";
                CoName = await _paramService.GetParamAsync<string>("COName");
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