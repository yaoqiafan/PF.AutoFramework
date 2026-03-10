using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    public class BarcodeScanDebugViewModel : RegionViewModelBase
    {
        private IBarcodeScan _scanner;
        private BaseDevice _baseDevice;
        private readonly DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;

        public BarcodeScanDebugViewModel()
        {
            InitializeCommands();

            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _scanner = navigationContext.Parameters.GetValue<IBarcodeScan>("Device");
                _baseDevice = _scanner as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }

                if (_scanner != null)
                {
                    // 刷新接口特有的网络属性
                    RaisePropertyChanged(nameof(IpAddress));
                    RaisePropertyChanged(nameof(TriggerPort));
                    RaisePropertyChanged(nameof(UserPort));
                    RaisePropertyChanged(nameof(TimeOutMs));

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

        private string _deviceName = "未选中扫码枪";
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _hasAlarm;
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }

        // --- IBarcodeScan 接口专有属性 ---
        public string IpAddress => _scanner?.IPAdress ?? "0.0.0.0";
        public int TriggerPort => _scanner?.TiggerPort ?? 0;
        public int UserPort => _scanner?.UserPort ?? 0;
        public int TimeOutMs => _scanner?.TimeOutMs ?? 0;

        private bool _isScanning;
        public bool IsScanning { get => _isScanning; set => SetProperty(ref _isScanning, value); }

        private string _lastBarcode = "等待扫码...";
        public string LastBarcode { get => _lastBarcode; set => SetProperty(ref _lastBarcode, value); }

        private string _userInfoInput = "1"; // 默认参数集测试值
        public string UserInfoInput { get => _userInfoInput; set => SetProperty(ref _userInfoInput, value); }

        public ObservableCollection<ScanRecord> ScanHistory { get; } = new ObservableCollection<ScanRecord>();

        #endregion

        #region 【控制命令定义】

        public DelegateCommand ConnectCommand { get; private set; }
        public DelegateCommand DisconnectCommand { get; private set; }
        public DelegateCommand ResetCommand { get; private set; }

        public DelegateCommand TriggerScanCommand { get; private set; }
        public DelegateCommand ChangeUserParamCommand { get; private set; }
        public DelegateCommand ClearHistoryCommand { get; private set; }

        private void InitializeCommands()
        {
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None); });

            TriggerScanCommand = new DelegateCommand(async () =>
            {
                if (_scanner == null) return;
                RefreshCancellationToken();
                IsScanning = true;
                try
                {
                    // 调用接口定义的 Tigger 方法
                    string result = await _scanner.Tigger(_cts.Token);
                    UpdateScanResult(result);
                }
                catch (TaskCanceledException) { }
                catch (Exception ex)
                {
                    UpdateScanResult($"扫码异常: {ex.Message}");
                }
                finally
                {
                    IsScanning = false;
                }
            });

            ChangeUserParamCommand = new DelegateCommand(async () =>
            {
                if (_scanner == null || string.IsNullOrWhiteSpace(UserInfoInput)) return;
                RefreshCancellationToken();
                try
                {
                    bool success = await _scanner.ChangeUserParam(UserInfoInput, _cts.Token);
                    UpdateScanResult($"切换参数集 [ {UserInfoInput} ] : {(success ? "成功" : "失败")}");
                }
                catch (Exception ex)
                {
                    UpdateScanResult($"切换参数异常: {ex.Message}");
                }
            });

            ClearHistoryCommand = new DelegateCommand(() => ScanHistory.Clear());
        }

        private void RefreshCancellationToken()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
        }

        #endregion

        #region 【数据处理与定时轮询】

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_baseDevice == null) return;
            IsConnected = _baseDevice.IsConnected;
            HasAlarm = _baseDevice.HasAlarm;
        }

        private void UpdateScanResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result)) return;

            LastBarcode = result;
            ScanHistory.Insert(0, new ScanRecord
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Barcode = result
            });

            if (ScanHistory.Count > 100)
            {
                ScanHistory.RemoveAt(ScanHistory.Count - 1);
            }
        }

        #endregion
    }

    public class ScanRecord
    {
        public string Time { get; set; }
        public string Barcode { get; set; }
    }
}