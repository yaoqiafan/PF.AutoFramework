using PF.Core.Constants;
using PF.Core.Enums.Hardware;
using PF.Core.Interfaces.Device.Hardware.Encoder.Basic;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 辅助编码器调试视图模型。
    /// 不区分底层是挂在运动控制卡下单独使用，还是挂在某根轴下随轴分组——
    /// 调试页只认 IAuxEncoder 契约本身。
    /// </summary>
    public class AuxEncoderDebugViewModel : RegionViewModelBase
    {
        private IAuxEncoder _encoder;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;

        /// <summary>初始化辅助编码器调试 ViewModel</summary>
        public AuxEncoderDebugViewModel()
        {
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(); });
            SimulateAlarmCommand = new DelegateCommand(() =>
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.AuxEncoderCardDisconnected, "调试页面手动模拟辅助编码器所在板卡断开"));

            SetPositionCommand = new DelegateCommand(async () =>
            {
                if (_encoder == null || !int.TryParse(TargetPosition, out var pos)) return;
                await _encoder.SetPositionAsync(pos);
            });

            SetModeCommand = new DelegateCommand(async () =>
            {
                if (_encoder == null || !int.TryParse(ModeMulit, out var mulit)) return;
                var ok = await _encoder.SetMode(SelectedMode, mulit);
                ModeResult = ok ? "设置成功" : "设置失败";
            });

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        /// <summary>导航进入时加载辅助编码器数据</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _encoder = navigationContext.Parameters.GetValue<IAuxEncoder>("Device");
                _baseDevice = _encoder as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知辅助编码器";
                    DeviceDescription = "无法获取底层设备信息";
                }

                Channel = _encoder?.Channel.ToString() ?? "-";
                Multiplier = _encoder?.Multiplier.ToString("G") ?? "-";

                _pollingTimer.Start();
            }
        }

        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
        }

        #endregion

        #region 【设备基础属性】

        private string _deviceName = "未选中辅助编码器";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private string _channel = "-";
        /// <summary>获取或设置通道号（只读展示）</summary>
        public string Channel { get => _channel; set => SetProperty(ref _channel, value); }

        private string _multiplier = "-";
        /// <summary>获取或设置编码器倍率（只读展示，改值请到硬件参数配置界面）</summary>
        public string Multiplier { get => _multiplier; set => SetProperty(ref _multiplier, value); }

        private string _currentPosition = "-";
        /// <summary>获取或设置当前位置（轮询刷新）</summary>
        public string CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; }
        /// <summary>模拟硬件报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; }

        #endregion

        #region 【写位置】

        private string _targetPosition = "0";
        /// <summary>获取或设置待写入的目标位置</summary>
        public string TargetPosition { get => _targetPosition; set => SetProperty(ref _targetPosition, value); }

        /// <summary>写入位置命令</summary>
        public DelegateCommand SetPositionCommand { get; }

        #endregion

        #region 【编码器模式设置】

        /// <summary>可选的编码器输入模式列表（供下拉框绑定）</summary>
        public IReadOnlyList<EmcoderModeEnum> AvailableModes { get; } =
            (EmcoderModeEnum[])Enum.GetValues(typeof(EmcoderModeEnum));

        private EmcoderModeEnum _selectedMode = EmcoderModeEnum.AB相;
        /// <summary>获取或设置待下发的编码器输入模式</summary>
        public EmcoderModeEnum SelectedMode { get => _selectedMode; set => SetProperty(ref _selectedMode, value); }

        private string _modeMulit = "1";
        /// <summary>获取或设置待下发的编码器计数模式（倍率）</summary>
        public string ModeMulit { get => _modeMulit; set => SetProperty(ref _modeMulit, value); }

        private string _modeResult = "-";
        /// <summary>获取或设置最近一次设置模式的结果</summary>
        public string ModeResult { get => _modeResult; set => SetProperty(ref _modeResult, value); }

        /// <summary>设置编码器模式命令</summary>
        public DelegateCommand SetModeCommand { get; }

        #endregion

        #region 【定时器轮询状态】

        private async void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_encoder == null) return;

            var pos = await _encoder.GetPositionAsync();
            CurrentPosition = pos?.ToString("F3") ?? "读取失败";

            if (_baseDevice != null) IsConnected = _baseDevice.IsConnected;
        }

        #endregion
    }
}
