using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Entities.Identity;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Infrastructure.Hardware;
using PF.Modules.Debug.Dialogs;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>轴调试 ViewModel</summary>
    public class AxisDebugViewModel : RegionViewModelBase
    {
        private IAxis _axis;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;
        private readonly IParamService _paramService;

        /// <summary>初始化轴调试 ViewModel</summary>
        public AxisDebugViewModel( IParamService paramService)
        {
            _paramService= paramService;
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

        /// <summary>导航进入时加载轴设备数据</summary>
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

        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
            _cts?.Cancel();
        }

        #endregion

        #region 【设备信息与状态属性】

        private string _deviceName = "未选中轴";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private int  _currentPosition;
        /// <summary>获取或设置当前位置</summary>
        public int  CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isMoving;
        /// <summary>获取或设置是否运动中</summary>
        public bool IsMoving { get => _isMoving; set => SetProperty(ref _isMoving, value); }

        private bool _isEnabled;
        /// <summary>获取或设置是否使能</summary>
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private bool _isPositiveLimit;
        /// <summary>获取或设置正限位状态</summary>
        public bool IsPositiveLimit { get => _isPositiveLimit; set => SetProperty(ref _isPositiveLimit, value); }

        private bool _isNegativeLimit;
        /// <summary>获取或设置负限位状态</summary>
        public bool IsNegativeLimit { get => _isNegativeLimit; set => SetProperty(ref _isNegativeLimit, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _isAlarm;
        /// <summary>获取或设置是否报警</summary>
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        private bool _isORG;

        /// <summary>获取或设置是否在原点</summary>
        public bool IsORG { get => _isORG; set => SetProperty(ref _isORG, value); }

        private bool _isHoming;

        /// <summary>获取或设置是否正在回原点</summary>
        public bool IsHoming { get => _isHoming; set => SetProperty(ref _isHoming, value); }


        #endregion

        #region 【运动输入参数属性】

        private double _targetPosition;
        /// <summary>获取或设置目标位置</summary>
        public double TargetPosition { get => _targetPosition; set => SetProperty(ref _targetPosition, value); }

        private double _absVelocity;
        /// <summary>获取或设置绝对运动速度</summary>
        public double AbsVelocity { get => _absVelocity; set => SetProperty(ref _absVelocity, value); }

        private double _relativeDistance;
        /// <summary>获取或设置相对运动距离</summary>
        public double RelativeDistance { get => _relativeDistance; set => SetProperty(ref _relativeDistance, value); }

        private double _relVelocity;
        /// <summary>获取或设置相对运动速度</summary>
        public double RelVelocity { get => _relVelocity; set => SetProperty(ref _relVelocity, value); }

        private double _jogVelocity;
        /// <summary>获取或设置点动速度</summary>
        public double JogVelocity { get => _jogVelocity; set => SetProperty(ref _jogVelocity, value); }

        #endregion

        #region 【点表管理属性】

        private ObservableCollection<AxisPoint> _pointTable = new ObservableCollection<AxisPoint>();
        /// <summary>获取或设置点位表</summary>
        public ObservableCollection<AxisPoint> PointTable
        {
            get => _pointTable;
            set => SetProperty(ref _pointTable, value);
        }

        private AxisPoint _selectedPoint;
        /// <summary>获取或设置选中的点位</summary>
        public AxisPoint SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        #endregion

        #region 【控制命令定义】

        // 基础控制命令
        /// <summary>显示轴参数对话框</summary>
        public DelegateCommand ShowAxisParamDialog { get; private set; }

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>使能命令</summary>
        public DelegateCommand EnableCommand { get; private set; }
        /// <summary>去使能命令</summary>
        public DelegateCommand DisableCommand { get; private set; }
        /// <summary>回原点命令</summary>
        public DelegateCommand HomeCommand { get; private set; }
        /// <summary>停止命令</summary>
        public DelegateCommand StopCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>绝对运动命令</summary>
        public DelegateCommand MoveAbsoluteCommand { get; private set; }
        /// <summary>相对运动命令</summary>
        public DelegateCommand MoveRelativeCommand { get; private set; }
        /// <summary>正向点动命令</summary>
        public DelegateCommand JogPositiveCommand { get; private set; }
        /// <summary>反向点动命令</summary>
        public DelegateCommand JogNegativeCommand { get; private set; }
        /// <summary>轴停止命令</summary>
        public DelegateCommand AxisStop { get; private set; }

        // 点表控制命令
        /// <summary>添加点位命令</summary>
        public DelegateCommand AddPointCommand { get; private set; }
        /// <summary>删除点位命令</summary>
        public DelegateCommand DeletePointCommand { get; private set; }
        /// <summary>保存点位命令</summary>
        public DelegateCommand SavePointsCommand { get; private set; }
        /// <summary>走到点位命令</summary>
        public DelegateCommand GoToPointCommand { get; private set; }

        private void InitializeCommands()
        {
            // ===== 基础硬件命令 =====
            ShowAxisParamDialog = new DelegateCommand(() =>
            {
                var param = new DialogParameters { { "Data", _axis.Param } };
                DialogService.ShowDialog(nameof(AxisParamDialog), param, ValueChangeCallBack);
            });


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
                await _axis.MoveAbsoluteAsync(TargetPosition, AbsVelocity, AbsVelocity * 5, AbsVelocity * 5, 0.08, _cts.Token);
            });

            MoveRelativeCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveRelativeAsync(RelativeDistance, RelVelocity, RelVelocity * 5, RelVelocity * 5, 0.08, _cts.Token);
            });

            JogPositiveCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.JogAsync(JogVelocity, true, JogVelocity * 5, JogVelocity * 5); });
            JogNegativeCommand = new DelegateCommand(async () => { if (_axis != null) await _axis.JogAsync(JogVelocity, false, JogVelocity * 5, JogVelocity * 5); });
            AxisStop = new DelegateCommand(async () => { if (_axis != null) await _axis.StopAsync(); });
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

        private async void ValueChangeCallBack(IDialogResult result)
        {
            if (result.Result == ButtonResult.Yes)
            {
                try
                {
                    var paramItem = result.Parameters.GetValue<AxisParam>("CallBackParamItem");
                    if (paramItem != null)
                    {
                        _axis.Param = paramItem;

                      var _config =  await _paramService.GetParamAsync<HardwareConfig>(_axis.DeviceId);
                        if (_config !=null )
                        {
                            if (_config .ConnectionParameters .ContainsKey ("AxisParam"))
                            {
                                _config.ConnectionParameters["AxisParam"] = System.Text.Json.JsonSerializer.Serialize(_axis.Param);
                               
                                await _paramService.SetParamAsync<HardwareConfig >(_config.DeviceId, _config);

                            }
                        }
                        
                      


                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"同步参数名称失败: {ex.Message}");
                }
            }
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
            var axisio = _axis.AxisIOStatus;
            IsConnected = _axis.IsConnected;
            CurrentPosition = (int )(_axis.CurrentPosition ?? 0);
            IsMoving = axisio?.Moving ?? false;
            IsEnabled = axisio?.SVO ?? false;
            IsPositiveLimit = axisio?.PEL ?? false;
            IsNegativeLimit = axisio?.MEL ?? false;
            IsORG = axisio?.ORG ?? false;
            IsHoming = axisio?.Homing ?? false;
            IsAlarm = axisio?.ALM ?? false;
        }

        #endregion
    }
}