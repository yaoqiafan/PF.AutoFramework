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
using System.Reflection;
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
        /// <summary>硬件管理服务（Splash 完成后延迟解析，供子类 PollDeviceStatuses 使用）。</summary>
        protected IHardwareManagerService? HardwareManager { get; private set; }
        /// <summary>SECS/GEM 管理服务（可选，未注册时为 null）。</summary>
        protected ISecsGemManager? SecsGemManager { get; private set; }

        #endregion

        #region 公共集合

        /// <summary>侧边栏导航菜单项集合（按权限过滤后绑定到 MainWindow）。</summary>
        public ObservableCollection<NavigationItem> MenuItems { get; } = new();

        /// <summary>
        /// 状态栏设备状态列表（数据驱动，子类在 PollDeviceStatuses 中更新）
        /// </summary>
        public ObservableCollection<DeviceStatusItem> DeviceStatusItems { get; } = new();

        #endregion

        #region 构造函数

        /// <summary>注入全部依赖并初始化命令、报警联动、空闲降权订阅。</summary>
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

            // 版本徽章：取入口程序集版本，原样显示（与 Splash 启动屏一致）
            SoftWareVersion = Assembly.GetEntryAssembly()?.GetName()?.Version?.ToString() ?? "1.0.0";

            // .NET 运行时版本（原样显示，如 8.0.x）
            RuntimeVersion = Environment.Version.ToString();

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

        /// <summary>停止设备状态轮询任务并释放 CancellationTokenSource。</summary>
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
        /// <summary>当前软件名称（绑定到主窗口标题栏）。</summary>
        public string SoftWareName
        {
            get => _softWareName;
            set => SetProperty(ref _softWareName, value);
        }

        private string _softWareVersion = string.Empty;
        /// <summary>
        /// 软件版本号（绑定到主窗口标题栏 V 徽章）。
        /// 取值来源于入口程序集的版本号，与 csproj 中的 &lt;Version&gt; 自动同步，
        /// 同时与 Splash 启动屏显示的版本保持一致。
        /// </summary>
        public string SoftWareVersion
        {
            get => _softWareVersion;
            set => SetProperty(ref _softWareVersion, value);
        }

        private string _runtimeVersion = string.Empty;
        /// <summary>
        /// 当前 .NET 运行时版本（绑定到主窗口标题栏 .net 徽章）。
        /// 取值来源于 <see cref="System.Environment.Version"/>，自动反映实际运行的 CLR 版本。
        /// </summary>
        public string RuntimeVersion
        {
            get => _runtimeVersion;
            set => SetProperty(ref _runtimeVersion, value);
        }

        private object _selectedMenuItem;
        /// <summary>当前选中的侧边栏菜单项。</summary>
        public object SelectedMenuItem
        {
            get => _selectedMenuItem;
            set => SetProperty(ref _selectedMenuItem, value);
        }

        private string _coName = string.Empty;
        /// <summary>公司名称（绑定到主窗口底部信息栏）。</summary>
        public string CoName
        {
            get => _coName;
            set => SetProperty(ref _coName, value);
        }

        /// <summary>
        /// 产品实物图片路径（消费项目在 Shell 层的 MainWindowViewModel 子类中重写，指向项目专属素材；
        /// 支持包 URI，如 "/项目UI程序集;component/Images/xxx.png"，也支持磁盘绝对路径）。
        /// 供框架主显界面（MainView）展示，未重写时为空。
        /// </summary>
        public virtual string ProductImagePath => string.Empty;

        /// <summary>产品/项目简介文字（子类可重写，显示在主显界面产品图片下方，可为空）。</summary>
        public virtual string ProductDescription => string.Empty;

        /// <summary>
        /// 用户使用说明书文件路径（子类可重写，指向磁盘上的 PDF/Word 等文档）。
        /// 为空或文件不存在时，主显界面的"打开说明书"按钮自动禁用。
        /// </summary>
        public virtual string UserManualPath => string.Empty;

        private string _sysTime = string.Empty;
        /// <summary>当前系统时间字符串（格式 yyyy-MM-dd HH:mm:ss，每 500ms 刷新）。</summary>
        public string SysTime
        {
            get => _sysTime;
            set => SetProperty(ref _sysTime, value);
        }

        private UserInfo _currentUser = new();
        /// <summary>当前已登录的用户信息（含权限级别和可访问视图列表）。</summary>
        public UserInfo CurrentUser
        {
            get => _currentUser;
            set => SetProperty(ref _currentUser, value);
        }

        private ExpandMode _expandMode = ExpandMode.ShowAll;
        /// <summary>侧边栏展开模式（全展开 / 全折叠 / 单展）。</summary>
        public ExpandMode Expand
        {
            get => _expandMode;
            set => SetProperty(ref _expandMode, value);
        }

        private MachineState _machineState = MachineState.Uninitialized;
        /// <summary>主控当前状态机状态（500ms 轮询更新）。</summary>
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

        /// <summary>对应当前机台状态的颜色画刷（绑定到状态指示器）。</summary>
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

        /// <summary>当前机台状态对应的中文文本描述。</summary>
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
        /// <summary>当前活跃报警数量。</summary>
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

        /// <summary>是否存在活跃报警（用于绑定报警指示器可见性）。</summary>
        public bool HasActiveAlarms => ActiveAlarmCount > 0;

        private AlarmSeverity? _highestAlarmSeverity;
        /// <summary>当前活跃报警中最高的严重级别（无报警时为 null）。</summary>
        public AlarmSeverity? HighestAlarmSeverity
        {
            get => _highestAlarmSeverity;
            set
            {
                if (SetProperty(ref _highestAlarmSeverity, value))
                    RaisePropertyChanged(nameof(AlarmStatusBrush));
            }
        }

        /// <summary>对应最高报警严重级别的颜色画刷（绑定到报警状态指示器）。</summary>
        public Brush AlarmStatusBrush => _highestAlarmSeverity switch
        {
            AlarmSeverity.Fatal       => new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)),
            AlarmSeverity.Error       => new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)),
            AlarmSeverity.Warning     => new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)),
            AlarmSeverity.Information => new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)),
            _ => new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75))
        };

        /// <summary>报警状态文本（有报警时显示数量，否则显示"正常"）。</summary>
        public string AlarmStatusText => HasActiveAlarms ? $"报警 {ActiveAlarmCount}" : "正常";

        #endregion

        #region 命令

        /// <summary>主窗口 Loaded 时触发，完成延迟资源解析并启动轮询。</summary>
        public ICommand LoadCommand { get; }
        /// <summary>侧边栏菜单项点击命令，触发 Prism 导航或权限拦截。</summary>
        public ICommand SwitchItemCmd { get; }
        /// <summary>切换侧边栏展开模式（ShowAll / HideAll / Single）。</summary>
        public ICommand ChangeExpandCmd { get; }
        /// <summary>导航至报警中心页面。</summary>
        public ICommand NavigateToAlarmCenterCmd { get; }

        #endregion
    }
}
