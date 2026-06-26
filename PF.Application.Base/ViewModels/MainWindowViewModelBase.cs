using PF.Application.Base.Configuration;
using PF.Application.Base.Models;
using PF.Application.Base.Services;
using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.SecsGem;
using PF.Core.Interfaces.Station;
using PF.Core.Models;
using PF.UI.Controls;
using PF.UI.Infrastructure.Navigation;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using Prism.Commands;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PF.Application.Base.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel 基类。
    /// 包含导航、权限拦截、报警联动、空闲降权、机台状态等全部框架逻辑。
    /// 子类通过重写 <see cref="PollDeviceStatuses"/> 向 <see cref="DeviceStatusItems"/> 填充项目特有硬件状态。
    /// </summary>
    public class MainWindowViewModelBase : RegionViewModelBase
    {
        #region 私有字段

        private readonly INavigationMenuService _navigationMenuService;
        private readonly IContainerProvider _containerProvider;
        private readonly CommonSettings _commonSettings;
        private readonly IAlarmService _alarmService;
        private readonly IMasterController _masterController;
        private readonly IdleMonitorService _idleMonitor;

        private CancellationTokenSource _cts;
        private Task _runningTask;
        private string _currentViewName = string.Empty;

        // 子类在 PollDeviceStatuses() 中使用，Splash 后由 OnLoading 延迟解析
        protected IHardwareManagerService? HardwareManager { get; private set; }
        protected ISecsGemManager? SecsGemManager { get; private set; }

        #endregion

        #region 公共集合

        public ObservableCollection<NavigationItem> MenuItems { get; } = new();

        /// <summary>
        /// 状态栏设备状态列表（数据驱动，子类在 PollDeviceStatuses 中更新）
        /// </summary>
        public ObservableCollection<DeviceStatusItem> DeviceStatusItems { get; } = new();

        #endregion

        #region 构造函数

        public MainWindowViewModelBase(
            INavigationMenuService navigationMenuService,
            IContainerProvider containerProvider,
            CommonSettings commonSettings,
            IAlarmService alarmService,
            IMasterController masterController)
        {
            _navigationMenuService = navigationMenuService;
            _containerProvider = containerProvider;
            _commonSettings = commonSettings;
            _alarmService = alarmService;
            _masterController = masterController;

            _idleMonitor = new IdleMonitorService(TimeSpan.FromSeconds(_commonSettings.NoUseTime));

            UserService.CurrentUserChanged += OnUserChanged;
            CurrentUser = UserService.CurrentUser ?? new UserInfo { Root = UserLevel.Null, AccessibleViews = new List<string>() };

            _idleMonitor.IdleTimeout += OnIdleTimeout;

            EventAggregator.GetEvent<AlarmTriggeredEvent>()
                .Subscribe(OnGlobalAlarmTriggered, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
            EventAggregator.GetEvent<AlarmClearedEvent>()
                .Subscribe(_ => RefreshAlarmStatus(), ThreadOption.UIThread, keepSubscriberReferenceAlive: true);

            LoadCommand = new DelegateCommand(OnLoading);
            SwitchItemCmd = new DelegateCommand<FunctionEventArgs<object>>(OnNavigated);
            ChangeExpandCmd = new DelegateCommand<string>(e =>
            {
                if (Enum.TryParse<ExpandMode>(e, out ExpandMode result))
                    Expand = result;
            });

            NavigateToAlarmCenterCmd = new DelegateCommand(() =>
                RegionManager.RequestNavigate(
                    NavigationConstants.Regions.SoftwareViewRegion,
                    NavigationConstants.Views.AlarmCenterView));
        }

        #endregion

        #region 用户变更

        private void OnUserChanged(object sender, UserInfo? newUser)
        {
            CurrentUser = newUser ?? new UserInfo { Root = UserLevel.Null, AccessibleViews = new List<string>() };

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
                    if (RegionManager.Regions.ContainsRegionWithName(NavigationConstants.Regions.SoftwareViewRegion))
                    {
                        var region = RegionManager.Regions[NavigationConstants.Regions.SoftwareViewRegion];
                        foreach (var view in region.Views.ToArray())
                            region.Remove(view);
                    }
                    SelectedMenuItem = null;
                });
            }
        }

        private void OnIdleTimeout(object? sender, EventArgs e)
        {
            LogService?.Info("检测到无操作超时，权限自动重置为 Operator", "IdleMonitor");
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                if (RegionManager.Regions.ContainsRegionWithName(NavigationConstants.Regions.SoftwareViewRegion))
                {
                    var region = RegionManager.Regions[NavigationConstants.Regions.SoftwareViewRegion];
                    foreach (var view in region.Views.ToArray())
                        region.Remove(view);
                }
                SelectedMenuItem = null;
            });
            UserService.ResetToOperator();
        }

        #endregion

        #region 报警联动

        private void OnGlobalAlarmTriggered(AlarmRecord record)
        {
            if (record.Severity >= AlarmSeverity.Warning)
            {
                var param = new DialogParameters
                {
                    { "Data", record },
                    { "ShowResetButton", record.Severity >= AlarmSeverity.Error }
                };
                DialogService.Show("AlarmDetailCardView", param, null, "PFAlarmBaseWindow");
            }
            RefreshAlarmStatus();
        }

        private void RefreshAlarmStatus()
        {
            var active = _alarmService.ActiveAlarms;
            ActiveAlarmCount = active.Count;
            HighestAlarmSeverity = active.Count == 0 ? null : active.Max(r => r.Severity);
        }

        #endregion

        #region 菜单

        private void RefreshMenu()
        {
            var filtered = FilterMenuForDisplay(_navigationMenuService.MenuItems);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                MenuItems.Clear();
                foreach (var item in filtered)
                    MenuItems.Add(item);
            });
        }

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

        #region 导航

        private void OnNavigated(FunctionEventArgs<object> args)
        {
            if (args?.Info is not SideMenuItem sideMenuItem) return;
            if (sideMenuItem.Tag is not NavigationItem navItem || string.IsNullOrEmpty(navItem.ViewName)) return;

            string viewName = navItem.ViewName;
            string category = NavigationConstantMapper.GetCategory(viewName);

            if (!UserService.HasPagePermission(viewName))
            {
                LogService?.Warn($"用户 [{CurrentUser?.UserName}] 尝试访问无权限页面: {viewName}", "Security");
                var displayName = PermissionHelper.GetViewDisplayName(viewName);
                MessageService.ShowMessage(
                    $"您无权访问「{displayName}」页面，请联系管理员在「权限管控 → 窗体权限更改」中配置相应权限。",
                    "权限不足", MessageBoxButton.OK, MessageBoxImage.Warning);
                SelectedMenuItem = null;
                return;
            }

            if (DefaultPermissions.IsProtectedView(viewName)
                && IsMachineLocked(_machineState)
                && CurrentUser?.Root < UserLevel.SuperUser)
            {
                LogService?.Warn($"用户 [{CurrentUser?.UserName}] 在设备{MachineStateText}期间尝试访问受保护页面: {viewName}", "Security");
                MessageService.ShowMessage(
                    $"设备{MachineStateText}期间，参数与调试页面已锁定，请停止设备后重试。",
                    "操作受限", MessageBoxButton.OK, MessageBoxImage.Warning);
                SelectedMenuItem = null;
                return;
            }

            if (IsParameterView(viewName))
            {
                _currentViewName = viewName;
                var parameters = new NavigationParameters();
                parameters.Add("TargetParamType", viewName);
                RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion,
                    NavigationConstants.Views.ParameterView, NavigationComplete, parameters);
                return;
            }

            switch (category)
            {
                case nameof(NavigationConstants.Views):
                    _currentViewName = viewName;
                    RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, viewName, NavigationComplete);
                    break;
                case nameof(NavigationConstants.Dialogs):
                    DialogService.ShowDialog(NavigationConstants.Dialogs.LoginView, _ => { });
                    SelectedMenuItem = null;
                    break;
                default:
                    if (_containerProvider.IsRegistered<object>(viewName))
                    {
                        _currentViewName = viewName;
                        RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion, viewName, NavigationComplete);
                    }
                    break;
            }
        }

        private static bool IsParameterView(string viewName) =>
            viewName == NavigationConstants.Views.ParameterView_SystemConfigParam ||
            viewName == NavigationConstants.Views.ParameterView_UserLoginParam ||
            viewName == NavigationConstants.Views.ParameterView_HardwareParam;

        private void NavigationComplete(NavigationResult result)
        {
            if (!result.Success && result.Exception != null)
                LogService?.Error($"导航失败: {result.Exception.Message}", "System", result.Exception);
        }

        private void OnMachineLockedStateEntered()
        {
            if (CurrentUser?.Root >= UserLevel.SuperUser) return;
            if (!DefaultPermissions.IsProtectedView(_currentViewName)) return;

            _currentViewName = NavigationConstants.Views.HomeView;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                RegionManager.RequestNavigate(NavigationConstants.Regions.SoftwareViewRegion,
                    NavigationConstants.Views.HomeView, _ => { });
                SelectedMenuItem = null;
                MessageService.ShowMessage(
                    $"设备已{MachineStateText}，已自动退出参数与调试页面。",
                    "操作受限", MessageBoxButton.OK, MessageBoxImage.Warning);
            }));
        }

        #endregion

        #region 加载与轮询

        private async void OnLoading()
        {
            HardwareManager = ContainerLocator.Container.Resolve<IHardwareManagerService>();
            SecsGemManager = ContainerLocator.Container.IsRegistered<ISecsGemManager>()
                ? ContainerLocator.Container.Resolve<ISecsGemManager>()
                : null;

            RefreshMenu();

            if (CurrentUser == null || CurrentUser.Root == UserLevel.Null)
                DialogService.ShowDialog(NavigationConstants.Dialogs.LoginView, _ => { });

            try
            {
                SoftWareName = _commonSettings.SoftWareName;
                CoName = _commonSettings.COName;
            }
            catch { }

            StartPolling();
        }

        private void StartPolling()
        {
            _cts = new CancellationTokenSource();
            _runningTask = Task.Factory.StartNew(
                () => WorkerMethod(_cts.Token),
                _cts.Token,
                TaskCreationOptions.LongRunning | TaskCreationOptions.DenyChildAttach,
                TaskScheduler.Default);
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
                    MachineState = _masterController.CurrentState;
                    PollDeviceStatuses();
                    await Task.Delay(500, ct);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception) { }
        }

        /// <summary>
        /// 子类重写此方法以更新 DeviceStatusItems 中各项的 IsConnected。
        /// 基类默认为空实现（无状态栏硬件条目）。
        /// </summary>
        protected virtual void PollDeviceStatuses() { }

        #endregion

        #region 公共属性

        private string _softWareName = string.Empty;
        public string SoftWareName
        {
            get => _softWareName;
            set => SetProperty(ref _softWareName, value);
        }

        private object _selectedMenuItem;
        public object SelectedMenuItem
        {
            get => _selectedMenuItem;
            set => SetProperty(ref _selectedMenuItem, value);
        }

        private string _coName = string.Empty;
        public string CoName
        {
            get => _coName;
            set => SetProperty(ref _coName, value);
        }

        private string _sysTime = string.Empty;
        public string SysTime
        {
            get => _sysTime;
            set => SetProperty(ref _sysTime, value);
        }

        private UserInfo _currentUser = new();
        public UserInfo CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        private ExpandMode _expandMode = ExpandMode.ShowAll;
        public ExpandMode Expand
        {
            get => _expandMode;
            set => SetProperty(ref _expandMode, value);
        }

        private MachineState _machineState = MachineState.Uninitialized;
        public MachineState MachineState
        {
            get => _machineState;
            set
            {
                var previous = _machineState;
                if (SetProperty(ref _machineState, value))
                {
                    RaisePropertyChanged(nameof(MachineStateBrush));
                    RaisePropertyChanged(nameof(MachineStateText));
                    if (!IsMachineLocked(previous) && IsMachineLocked(value))
                        OnMachineLockedStateEntered();
                }
            }
        }

        private static bool IsMachineLocked(MachineState state) =>
            state == MachineState.Running || state == MachineState.Initializing || state == MachineState.Resetting;

        public Brush MachineStateBrush => _machineState switch
        {
            MachineState.Running      => new SolidColorBrush(Color.FromRgb(0x02, 0xad, 0x8b)),
            MachineState.Paused       => new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)),
            MachineState.InitAlarm    => new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)),
            MachineState.RunAlarm     => new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)),
            MachineState.Initializing => new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)),
            MachineState.Resetting    => new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)),
            MachineState.Idle         => new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)),
            _ => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75))
        };

        public string MachineStateText => _machineState switch
        {
            MachineState.Uninitialized => "未初始化",
            MachineState.Initializing  => "初始化中",
            MachineState.Idle          => "待机",
            MachineState.Running       => "运行中",
            MachineState.Paused        => "已暂停",
            MachineState.InitAlarm     => "初始化报警",
            MachineState.RunAlarm      => "运行报警",
            MachineState.Resetting     => "复位中",
            _ => "未知"
        };

        private int _activeAlarmCount;
        public int ActiveAlarmCount
        {
            get => _activeAlarmCount;
            set
            {
                if (SetProperty(ref _activeAlarmCount, value))
                {
                    RaisePropertyChanged(nameof(HasActiveAlarms));
                    RaisePropertyChanged(nameof(AlarmStatusText));
                }
            }
        }

        public bool HasActiveAlarms => ActiveAlarmCount > 0;

        private AlarmSeverity? _highestAlarmSeverity;
        public AlarmSeverity? HighestAlarmSeverity
        {
            get => _highestAlarmSeverity;
            set
            {
                if (SetProperty(ref _highestAlarmSeverity, value))
                    RaisePropertyChanged(nameof(AlarmStatusBrush));
            }
        }

        public Brush AlarmStatusBrush => _highestAlarmSeverity switch
        {
            AlarmSeverity.Fatal       => new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)),
            AlarmSeverity.Error       => new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)),
            AlarmSeverity.Warning     => new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)),
            AlarmSeverity.Information => new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)),
            _ => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75))
        };

        public string AlarmStatusText => HasActiveAlarms ? $"报警 {ActiveAlarmCount}" : "正常";

        #endregion

        #region 命令

        public ICommand LoadCommand { get; }
        public ICommand SwitchItemCmd { get; }
        public ICommand ChangeExpandCmd { get; }
        public ICommand NavigateToAlarmCenterCmd { get; }

        #endregion
    }
}
