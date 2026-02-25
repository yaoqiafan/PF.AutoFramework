using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

            // 定时器初始化：50ms 刷新一次界面
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

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _axis = navigationContext.Parameters.GetValue<IAxis>("Device");
                _baseDevice = _axis as BaseDevice;

                if (_baseDevice != null)
                {
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
                    // 加载该轴的点表数据
                    PointTable = new ObservableCollection<AxisPoint>(_axis.PointTable);
                    _pollingTimer.Start();
                }
            }
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
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

        #region 【点表管理属性】

        private ObservableCollection<AxisPoint> _pointTable = new ObservableCollection<AxisPoint>();
        public ObservableCollection<AxisPoint> PointTable
        {
            get => _pointTable;
            set => SetProperty(ref _pointTable, value);
        }

        private AxisPoint _selectedPoint;
        public AxisPoint SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        #endregion

        #region 【控制命令定义】

        // 基础控制命令
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

        // 点表控制命令
        public DelegateCommand AddPointCommand { get; private set; }
        public DelegateCommand DeletePointCommand { get; private set; }
        public DelegateCommand SavePointsCommand { get; private set; }
        public DelegateCommand GoToPointCommand { get; private set; }

        private void InitializeCommands()
        {
            // ===== 基础硬件命令 =====
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            EnableCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.EnableAsync(); });
            DisableCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.DisableAsync(); });
            HomeCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.HomeAsync(CancellationToken.None); });
            StopCommand = new DelegateCommand(async () =>
            {
                _cts?.Cancel();
                if (_axis != null) await _axis.StopAsync();
            });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None); });

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

            // ===== 点表管理命令 =====
            AddPointCommand = new DelegateCommand(() =>
            {
                if (_axis == null) return;
                int nextOrder = PointTable.Any() ? PointTable.Max(p => p.SortOrder) + 10 : 10;
                var newPoint = new AxisPoint
                {
                    Name = $"新点位_{DateTime.Now:HHmmss}",
                    TargetPosition = CurrentPosition, // 默认记录当前位置
                    Speed = AbsVelocity,
                    SortOrder = nextOrder
                };
                PointTable.Add(newPoint);
                _axis.AddOrUpdatePoint(newPoint);
                SelectedPoint = newPoint;
            });

            DeletePointCommand = new DelegateCommand(() =>
            {
                if (_axis == null || SelectedPoint == null) return;
                var name = SelectedPoint.Name;
                PointTable.Remove(SelectedPoint);
                _axis.DeletePoint(name);
                SelectedPoint = null;
            });

            SavePointsCommand = new DelegateCommand(() =>
            {
                if (_axis == null) return;
                foreach (var p in PointTable) _axis.AddOrUpdatePoint(p);
                _axis.SavePointTable();
            });

            GoToPointCommand = new DelegateCommand(async () =>
            {
                if (_axis == null || SelectedPoint == null) return;
                RefreshCancellationToken();
                await _axis.MoveToPointAsync(SelectedPoint.Name, _cts.Token);
            });
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

            CurrentPosition = _axis.CurrentPosition;
            IsMoving = _axis.IsMoving;
            IsEnabled = _axis.IsEnabled;
            IsPositiveLimit = _axis.IsPositiveLimit;
            IsNegativeLimit = _axis.IsNegativeLimit;
        }

        #endregion
    }
}