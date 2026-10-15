using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.IO;
using PF.Core.Interfaces.Device.Hardware.IO.Basic;
using PF.Core.Models.Device.Hardware.IO;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using Prism.Ioc;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// IO 调试视图模型
    /// 通过 IIOMappingService 实现业务层与通用 UI 的解耦
    /// </summary>
    public class IODebugViewModel : RegionViewModelBase
    {
        /// <summary>未标注 [Display(GroupName=)] 的信号归入的默认分组名</summary>
        private const string SharedCategoryName = "其他信号";

        private IIOController _ioController;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;
        private readonly IIOMappingService _ioMappingService;

        // 安全门监控（可选）：不是所有项目都有安全门，未注册时本页不做任何门锁处理
        private readonly IHardwareInputMonitor? _inputMonitor;
        // 当前板卡上由安全门监控驱动的门锁输出口 → 所属门名称；为空表示本卡没有门锁
        private Dictionary<int, string> _doorLockPorts = new();

        // 构造函数注入映射服务
        /// <summary>初始化 IO 调试 ViewModel</summary>
        public IODebugViewModel(IIOMappingService ioMappingService)
        {
            _ioMappingService = ioMappingService;

            var container = ContainerLocator.Container;
            if (container.IsRegistered<IHardwareInputMonitor>())
                _inputMonitor = container.Resolve<IHardwareInputMonitor>();

            ToggleLockOverrideCommand = new DelegateCommand(ToggleLockOverride,
                () => IsLockManualOverride || CanLockManualOverride)
                .ObservesProperty(() => IsLockManualOverride)
                .ObservesProperty(() => CanLockManualOverride);

            // 初始化命令
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(System.Threading.CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(System.Threading.CancellationToken.None); });
            SimulateAlarmCommand = new DelegateCommand(() =>
            {
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.IoModuleError, "调试页面手动模拟IO模块报警");
            });

            // 工控常用的 IO 刷新率通常在 50ms - 100ms
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        /// <summary>导航进入时加载 IO 设备数据</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _ioController = navigationContext.Parameters.GetValue<IIOController>("Device");
                _baseDevice = _ioController as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知 IO 设备";
                    DeviceDescription = "无法获取底层设备信息";
                }

                if (_ioController != null)
                {
                    InitializePorts();
                    _pollingTimer.Start();
                }
            }
        }

        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop(); // 离开页面务必停止轮询，防止内存泄漏

            // 手动接管只在本页有效：离开即交还安全门监控，锁输出按期望写回
            if (_inputMonitor?.IsLockManualOverride == true)
                _inputMonitor.TrySetLockManualOverride(false);
            IsLockManualOverride = false;
        }

        #endregion

        #region 【设备基础属性】

        private string _deviceName = "未选中IO板卡";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>模拟硬件报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; private set; }

        #endregion

        #region 【门锁手动接管】

        private bool _hasDoorLocks;
        /// <summary>当前板卡上是否有由安全门监控驱动的门锁输出（为 false 时本页不显示任何门锁相关内容）</summary>
        public bool HasDoorLocks { get => _hasDoorLocks; private set => SetProperty(ref _hasDoorLocks, value); }

        private bool _isLockManualOverride;
        /// <summary>门锁是否处于手动接管（接管期间门锁口可直接写出，监控不会写回）</summary>
        public bool IsLockManualOverride { get => _isLockManualOverride; private set => SetProperty(ref _isLockManualOverride, value); }

        private bool _canLockManualOverride;
        /// <summary>当前是否允许进入手动接管（机台初始化中/运行中持有上锁许可时不允许）</summary>
        public bool CanLockManualOverride { get => _canLockManualOverride; private set => SetProperty(ref _canLockManualOverride, value); }

        /// <summary>进入/退出门锁手动接管命令</summary>
        public DelegateCommand ToggleLockOverrideCommand { get; }

        private void ToggleLockOverride()
        {
            if (_inputMonitor == null) return;

            bool target = !IsLockManualOverride;
            if (!_inputMonitor.TrySetLockManualOverride(target))
            {
                MessageService.ShowMessage("机台处于初始化或运行中，门锁由安全门监控控制，不能手动接管。",
                    "门锁手动接管", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            IsLockManualOverride = _inputMonitor.IsLockManualOverride;
        }

        #endregion

        #region 【IO 端口分组与初始化】

        private List<IOPortGroup> _inputGroups = new();
        /// <summary>获取输入端口分组列表（按 [Display(GroupName=)] 分组，绑定到 UI 的外层 ItemsControl）</summary>
        public List<IOPortGroup> InputGroups { get => _inputGroups; private set => SetProperty(ref _inputGroups, value); }

        private List<IOPortGroup> _outputGroups = new();
        /// <summary>获取输出端口分组列表（按 [Display(GroupName=)] 分组，绑定到 UI 的外层 ItemsControl）</summary>
        public List<IOPortGroup> OutputGroups { get => _outputGroups; private set => SetProperty(ref _outputGroups, value); }

        private string _inputSearchText = string.Empty;
        /// <summary>获取或设置输入端口的搜索关键字</summary>
        public string InputSearchText
        {
            get => _inputSearchText;
            set
            {
                if (SetProperty(ref _inputSearchText, value))
                    foreach (var group in InputGroups) group.Refresh();
            }
        }

        private string _outputSearchText = string.Empty;
        /// <summary>获取或设置输出端口的搜索关键字</summary>
        public string OutputSearchText
        {
            get => _outputSearchText;
            set
            {
                if (SetProperty(ref _outputSearchText, value))
                    foreach (var group in OutputGroups) group.Refresh();
            }
        }

        private bool FilterInputPort(object obj) => MatchesSearch(obj, InputSearchText);
        private bool FilterOutputPort(object obj) => MatchesSearch(obj, OutputSearchText);

        private static bool MatchesSearch(object obj, string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText)) return true;
            if (obj is not IOPortModel port) return false;

            return port.PortName?.Contains(searchText, StringComparison.OrdinalIgnoreCase) == true
                || port.Index.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase);
        }

        private void InitializePorts()
        {
            string deviceId = _baseDevice?.DeviceId ?? "Default";

            // 只认安全门监控所管的那张卡：其他 IO 卡、或项目没有门锁时映射为空，本页行为与原来一致
            _doorLockPorts = (_inputMonitor?.GetDoorLockOutputs() ?? Array.Empty<DoorLockOutput>())
                .Where(o => o.DeviceId == deviceId)
                .GroupBy(o => o.Port)
                .ToDictionary(g => g.Key, g => g.First().DoorName);
            HasDoorLocks = _doorLockPorts.Count > 0;
            RefreshLockOverrideState();

            InputGroups = BuildGroups(_ioController.InputCount,
                i => _ioMappingService.GetInputInfo(deviceId, i), "DI", isOutput: false, FilterInputPort);
            OutputGroups = BuildGroups(_ioController.OutputCount,
                i => _ioMappingService.GetOutputInfo(deviceId, i), "DO", isOutput: true, FilterOutputPort);
        }

        // 过滤 [Browsable(false)] 引脚 → 按 [Display(Order=)] 排序 → 按 [Display(GroupName=)] 分组
        // （未指定顺序/分组的分别退化为物理索引排序、归入"其他信号"）
        private List<IOPortGroup> BuildGroups(int count, Func<int, IOMapInfo> getInfo, string prefix, bool isOutput, Predicate<object> filter)
        {
            var candidates = new List<(int Order, int Index, string Name, string Category)>();
            for (int i = 0; i < count; i++)
            {
                var ioInfo = getInfo(i);
                if (ioInfo != null && !ioInfo.IsBrowsable) continue;

                string showName = ioInfo?.Name ?? $"{prefix} {i:D2}";
                int order = ioInfo?.Order ?? int.MaxValue;
                candidates.Add((order == int.MaxValue ? i : order, i, showName, ioInfo?.Category));
            }

            var groups = new List<IOPortGroup>();
            foreach (var bucket in candidates.OrderBy(c => c.Order).ThenBy(c => c.Index)
                                              .GroupBy(c => c.Category ?? SharedCategoryName))
            {
                var group = new IOPortGroup(bucket.Key, filter);
                foreach (var c in bucket)
                {
                    var port = new IOPortModel { Index = c.Index, PortName = c.Name, IsOutput = isOutput };
                    if (isOutput)
                    {
                        port.ToggleCommand = new DelegateCommand<IOPortModel>(ToggleOutputPort);
                        if (_doorLockPorts.TryGetValue(c.Index, out var doorName)) port.DoorLockName = doorName;
                    }
                    group.Items.Add(port);
                }
                group.RecomputeVisibility();
                groups.Add(group);
            }
            return groups;
        }

        private void ToggleOutputPort(IOPortModel port)
        {
            if (port == null || _ioController == null) return;

            // 门锁口由安全门监控按机台状态驱动（并周期对账），未接管时直接写出会在 0.5s 内被写回
            if (port.IsDoorLock && _inputMonitor?.IsLockManualOverride != true)
            {
                MessageService.ShowMessage($"[{port.PortName}] 是安全门 [{port.DoorLockName}] 的门锁输出，由安全门监控控制。\n请先开启「门锁手动接管」再操作。",
                    "门锁手动接管", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            bool targetState = !port.State; // 取反

            // =========================================================================
            // ⚠️ TODO: 替换为你实际的 IO 写入方法 (例如 WriteDO, SetOutput 等)
            // =========================================================================
             _ioController.WriteOutput(port.Index, targetState);


        }

        #endregion

        #region 【定时器轮询状态】

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_ioController == null) return;

            // =========================================================================
            // ⚠️ TODO: 替换为你实际的读取状态方法 (例如 ReadDI, ReadDO 等)
            // 如果底层方法比较耗时，建议在底层维护好缓存，这里只读缓存状态以防阻塞 UI
            // =========================================================================

            // 1. 刷新输入端口 (DI)
            foreach (var group in InputGroups)
                foreach (var port in group.Items)
                    port.State = Convert.ToBoolean(_ioController.ReadInput(port.Index));

            // 2. 刷新输出端口 (DO) 的反馈状态
            foreach (var group in OutputGroups)
                foreach (var port in group.Items)
                    port.State = Convert.ToBoolean(_ioController.ReadOutput(port.Index));

            // 3. 刷新连接状态
             if (_baseDevice != null) IsConnected = _baseDevice.IsConnected;

            // 4. 刷新门锁接管状态（主控初始化/启动时会强制收回接管）
            RefreshLockOverrideState();
        }

        private void RefreshLockOverrideState()
        {
            if (!HasDoorLocks || _inputMonitor == null) return;
            IsLockManualOverride = _inputMonitor.IsLockManualOverride;
            CanLockManualOverride = !_inputMonitor.IsLockPermitted;
        }

        #endregion
    }

    /// <summary>
    /// IO 端口分组：承载同一分类（如"工位1"/"工位2"/"其他信号"）下的端口列表，
    /// 自带独立的搜索过滤视图与"过滤后是否还有可见项"标记（用于隐藏空分组的分割线）
    /// </summary>
    public class IOPortGroup : BindableBase
    {
        /// <summary>获取分组名称（对应 [Display(GroupName=)] 特性）</summary>
        public string CategoryName { get; }

        /// <summary>获取该分组下的端口集合（承载真实数据，含轮询刷新的 State）</summary>
        public ObservableCollection<IOPortModel> Items { get; } = new ObservableCollection<IOPortModel>();

        /// <summary>获取该分组的搜索过滤视图，绑定到 UI 的 ItemsControl</summary>
        public ICollectionView View { get; }

        private bool _hasVisibleItems = true;
        /// <summary>获取过滤后该分组是否还有可见端口；为 false 时应隐藏分割线与列表</summary>
        public bool HasVisibleItems { get => _hasVisibleItems; private set => SetProperty(ref _hasVisibleItems, value); }

        /// <summary>创建一个 IO 端口分组，并绑定其搜索过滤谓词</summary>
        public IOPortGroup(string categoryName, Predicate<object> filter)
        {
            CategoryName = categoryName;
            View = CollectionViewSource.GetDefaultView(Items);
            View.Filter = filter;
        }

        /// <summary>重新计算过滤后是否还有可见项</summary>
        public void RecomputeVisibility() => HasVisibleItems = View.Cast<object>().Any();

        /// <summary>搜索关键字变化时调用：刷新过滤视图并重新计算可见性</summary>
        public void Refresh()
        {
            View.Refresh();
            RecomputeVisibility();
        }
    }

    /// <summary>
    /// 单个 IO 端口的视图模型
    /// </summary>
    public class IOPortModel : BindableBase
    {
        /// <summary>获取或设置IO端口索引</summary>
        public int Index { get; set; }
        /// <summary>获取或设置端口名称</summary>
        public string PortName { get; set; }
        /// <summary>获取或设置是否为输出端口</summary>
        public bool IsOutput { get; set; }

        /// <summary>门锁输出所属的安全门名称；null 表示不是门锁口</summary>
        public string? DoorLockName { get; set; }
        /// <summary>是否为安全门门锁输出口（由安全门监控驱动）</summary>
        public bool IsDoorLock => DoorLockName != null;

        private bool _state;
        /// <summary>获取或设置端口状态</summary>
        public bool State
        {
            get => _state;
            set => SetProperty(ref _state, value);
        }

        /// <summary>切换端口状态命令</summary>
        public DelegateCommand<IOPortModel> ToggleCommand { get; set; }
    }
}
