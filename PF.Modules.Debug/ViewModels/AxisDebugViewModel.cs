using PF.Core.Interfaces.Hardware.Motor.Basic;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    public class AxisDebugViewModel : RegionViewModelBase
    {
        private IAxis _axis;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;

        public AxisDebugViewModel()
        {
            // 初始化默认的运动参数
            AbsVelocity = 50.0;
            RelVelocity = 50.0;
            JogVelocity = 10.0;
            RelativeDistance = 10.0;

            InitializeCommands();

            // 定时器初始化：工控上位机状态轮询通常 50ms 刷新一次界面
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            // 从 HardwareDebugViewModel 的 Navigate 参数中获取具体的轴对象
            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _axis = navigationContext.Parameters.GetValue<IAxis>("Device");
                _baseDevice = _axis as BaseDevice;

                if (_baseDevice != null)
                {
                    // 提取设备名称和描述，用于 UI 顶部横幅显示
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知轴设备";
                    DeviceDescription = "无法获取底层设备信息";
                }

                if (_axis != null)
                {
                    _pollingTimer.Start(); // 只有拿到实例才启动轮询
                }
            }
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);

            // 离开当前页面时停止轮询并取消可能正在进行的延时任务，释放内存
            _pollingTimer.Stop();
            _cts?.Cancel();
        }

        #endregion

        #region 【设备信息与状态属性】

        private string _deviceName = "未选中轴";
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private double _currentPosition;
        public double CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isMoving;
        public bool IsMoving { get => _isMoving; set => SetProperty(ref _isMoving, value); }

        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private bool _isPositiveLimit;
        public bool IsPositiveLimit { get => _isPositiveLimit; set => SetProperty(ref _isPositiveLimit, value); }

        private bool _isNegativeLimit;
        public bool IsNegativeLimit { get => _isNegativeLimit; set => SetProperty(ref _isNegativeLimit, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _isAlarm;
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        #endregion

        #region 【运动输入参数属性】

        private double _targetPosition;
        public double TargetPosition { get => _targetPosition; set => SetProperty(ref _targetPosition, value); }

        private double _absVelocity;
        public double AbsVelocity { get => _absVelocity; set => SetProperty(ref _absVelocity, value); }

        private double _relativeDistance;
        public double RelativeDistance { get => _relativeDistance; set => SetProperty(ref _relativeDistance, value); }

        private double _relVelocity;
        public double RelVelocity { get => _relVelocity; set => SetProperty(ref _relVelocity, value); }

        private double _jogVelocity;
        public double JogVelocity { get => _jogVelocity; set => SetProperty(ref _jogVelocity, value); }

        #endregion

        #region 【控制命令定义】

        public DelegateCommand ConnectCommand { get; private set; }
        public DelegateCommand DisconnectCommand { get; private set; }
        public DelegateCommand EnableCommand { get; private set; }
        public DelegateCommand DisableCommand { get; private set; }
        public DelegateCommand HomeCommand { get; private set; }
        public DelegateCommand StopCommand { get; private set; }
        public DelegateCommand ResetCommand { get; private set; }
        public DelegateCommand MoveAbsoluteCommand { get; private set; }
        public DelegateCommand MoveRelativeCommand { get; private set; }
        public DelegateCommand JogPositiveCommand { get; private set; }
        public DelegateCommand JogNegativeCommand { get; private set; }

        private void InitializeCommands()
        {
            ConnectCommand = new DelegateCommand(async () =>
            {
                if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None);
            });

            DisconnectCommand = new DelegateCommand(async () =>
            {
                if (_baseDevice != null) await _baseDevice.DisconnectAsync();
            });

            EnableCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.EnableAsync(); });
            DisableCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.DisableAsync(); });

            HomeCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.HomeAsync(CancellationToken.None); });

            StopCommand = new DelegateCommand(async () =>
            {
                _cts?.Cancel();
                if (_axis != null) await _axis.StopAsync();
            });

            ResetCommand = new DelegateCommand(async () =>
            {
                if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None);
            });

            MoveAbsoluteCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveAbsoluteAsync(TargetPosition, AbsVelocity, _cts.Token);
            });

            MoveRelativeCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveRelativeAsync(RelativeDistance, RelVelocity, _cts.Token);
            });

            JogPositiveCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.JogAsync(JogVelocity, true); });
            JogNegativeCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.JogAsync(JogVelocity, false); });
        }

        private void RefreshCancellationToken()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
        }

        #endregion

        #region 【定时器轮询更新】

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_axis == null) return;

            // 定期拉取底层硬件的状态同步到 UI
            CurrentPosition = _axis.CurrentPosition;
            IsMoving = _axis.IsMoving;
            IsEnabled = _axis.IsEnabled;
            IsPositiveLimit = _axis.IsPositiveLimit;
            IsNegativeLimit = _axis.IsNegativeLimit;

            // 如果 BaseDevice 有连接状态和报警状态，在这里更新
            // 例如：
            // if (_baseDevice != null)
            // {
            //     IsConnected = _baseDevice.IsConnected; 
            // }
        }

        #endregion
    }
}