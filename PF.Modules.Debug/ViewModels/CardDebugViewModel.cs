using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 板卡级调试视图模型。
    /// 接收 HardwareDebugViewModel 通过 NavigationParameter("Device") 传入的 IMotionCard 实例，
    /// 展示板卡状态并提供基础测试命令。
    /// 与 AxisDebugViewModel / IODebugViewModel 策略一致。
    /// </summary>
    public class CardDebugViewModel : RegionViewModelBase
    {
        private IMotionCard _card;
        private BaseDevice _baseDevice;
        private readonly DispatcherTimer _pollingTimer;

        // ── 设备信息属性 ──────────────────────────────────────────────────────
        private string _deviceName = "未选中板卡";
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待板卡接入...";
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        // ── 状态属性 ──────────────────────────────────────────────────────────
        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _hasAlarm;
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }

        private string _connectionText = "——";
        public string ConnectionText { get => _connectionText; set => SetProperty(ref _connectionText, value); }

        private string _alarmText = "——";
        public string AlarmText { get => _alarmText; set => SetProperty(ref _alarmText, value); }

        private Brush _connectionBrush = GrayBrush;
        public Brush ConnectionBrush { get => _connectionBrush; set => SetProperty(ref _connectionBrush, value); }

        private Brush _alarmBrush = GrayBrush;
        public Brush AlarmBrush { get => _alarmBrush; set => SetProperty(ref _alarmBrush, value); }

        // ── 板卡规格属性 ──────────────────────────────────────────────────────
        private int _cardIndex;
        public int CardIndex { get => _cardIndex; set => SetProperty(ref _cardIndex, value); }

        private int _axisCount;
        public int AxisCount { get => _axisCount; set => SetProperty(ref _axisCount, value); }

        private int _inputCount;
        public int InputCount { get => _inputCount; set => SetProperty(ref _inputCount, value); }

        private int _outputCount;
        public int OutputCount { get => _outputCount; set => SetProperty(ref _outputCount, value); }

        // ── 命令 ──────────────────────────────────────────────────────────────
        public DelegateCommand ConnectCommand { get; }
        public DelegateCommand DisconnectCommand { get; }
        public DelegateCommand ResetCommand { get; }

        // ── 静态颜色 ──────────────────────────────────────────────────────────
        private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
        private static readonly Brush RedBrush   = new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44));
        private static readonly Brush GrayBrush  = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));

        public CardDebugViewModel()
        {
            ConnectCommand    = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand      = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None); });

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        // ── Prism 导航生命周期 ────────────────────────────────────────────────

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _card       = navigationContext.Parameters.GetValue<IMotionCard>("Device");
                _baseDevice = _card as BaseDevice;

                if (_card != null)
                {
                    DeviceName        = _card.DeviceName;
                    DeviceDescription = $"类别: {_card.Category}  |  槽位索引: {_card.CardIndex}  |  模拟模式: {_card.IsSimulated}";
                    CardIndex         = _card.CardIndex;
                    AxisCount         = _card.AxisCount;
                    InputCount        = _card.InputCount;
                    OutputCount       = _card.OutputCount;
                    _pollingTimer.Start();
                }
            }
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
        }

        // ── 定时轮询 ──────────────────────────────────────────────────────────

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_card == null) return;

            IsConnected = _card.IsConnected;
            HasAlarm    = _card.HasAlarm;

            ConnectionText  = _card.IsConnected ? "已连接" : "未连接";
            AlarmText       = _card.HasAlarm    ? "报警中" : "正常";
            ConnectionBrush = _card.IsConnected ? GreenBrush : RedBrush;
            AlarmBrush      = _card.HasAlarm    ? RedBrush   : GreenBrush;
        }
    }
}
