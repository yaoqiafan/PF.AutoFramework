using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Card;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 控制卡级调试视图模型：左侧板卡列表 + 右侧状态详情及基础测试命令
    /// </summary>
    public class CardDebugViewModel : RegionViewModelBase
    {
        private readonly IHardwareManagerService _hardwareManager;
        private readonly DispatcherTimer _pollingTimer;

        // ── 左侧板卡列表 ─────────────────────────────────────────────────────────
        public ObservableCollection<CardNavItem> Cards { get; } = new();

        private CardNavItem _selectedCard;
        public CardNavItem SelectedCard
        {
            get => _selectedCard;
            set
            {
                if (SetProperty(ref _selectedCard, value))
                    RefreshDetail();
            }
        }

        // ── 右侧详情属性 ─────────────────────────────────────────────────────────
        private string _cardName = "——";
        public string CardName { get => _cardName; set => SetProperty(ref _cardName, value); }

        private string _deviceId = "——";
        public string DeviceId { get => _deviceId; set => SetProperty(ref _deviceId, value); }

        private int _cardIndex;
        public int CardIndex { get => _cardIndex; set => SetProperty(ref _cardIndex, value); }

        private int _axisCount;
        public int AxisCount { get => _axisCount; set => SetProperty(ref _axisCount, value); }

        private int _inputCount;
        public int InputCount { get => _inputCount; set => SetProperty(ref _inputCount, value); }

        private int _outputCount;
        public int OutputCount { get => _outputCount; set => SetProperty(ref _outputCount, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _hasAlarm;
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }

        private string _connectionText = "未知";
        public string ConnectionText { get => _connectionText; set => SetProperty(ref _connectionText, value); }

        private string _alarmText = "未知";
        public string AlarmText { get => _alarmText; set => SetProperty(ref _alarmText, value); }

        private Brush _connectionBrush = GrayBrush;
        public Brush ConnectionBrush { get => _connectionBrush; set => SetProperty(ref _connectionBrush, value); }

        private Brush _alarmBrush = GrayBrush;
        public Brush AlarmBrush { get => _alarmBrush; set => SetProperty(ref _alarmBrush, value); }

        // ── 命令 ─────────────────────────────────────────────────────────────────
        public DelegateCommand ReconnectCommand { get; }
        public DelegateCommand ResetCommand { get; }

        // ── 静态 Brush 资源 ───────────────────────────────────────────────────────
        private static readonly Brush GreenBrush = new SolidColorBrush(Color.FromRgb(0x2d, 0xb8, 0x4d));
        private static readonly Brush RedBrush   = new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40));
        private static readonly Brush GrayBrush  = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        public CardDebugViewModel(IHardwareManagerService hardwareManager)
        {
            _hardwareManager = hardwareManager;

            ReconnectCommand = new DelegateCommand(
                async () =>
                {
                    if (SelectedCard?.Card == null) return;
                    await SelectedCard.Card.DisconnectAsync();
                    await SelectedCard.Card.ConnectAsync(CancellationToken.None);
                    RefreshDetail();
                },
                () => SelectedCard != null)
                .ObservesProperty(() => SelectedCard);

            ResetCommand = new DelegateCommand(
                async () =>
                {
                    if (SelectedCard?.Card == null) return;
                    await SelectedCard.Card.ResetAsync(CancellationToken.None);
                    RefreshDetail();
                },
                () => SelectedCard != null)
                .ObservesProperty(() => SelectedCard);

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _pollingTimer.Tick += (_, _) => RefreshDetail();
        }

        // ── Prism 导航生命周期 ────────────────────────────────────────────────────

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            LoadCards();
            _pollingTimer.Start();
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop();
        }

        // ── 私有方法 ──────────────────────────────────────────────────────────────

        private void LoadCards()
        {
            var previousId = SelectedCard?.Card?.DeviceId;
            Cards.Clear();

            foreach (var device in _hardwareManager.ActiveDevices)
            {
                if (device is IMotionCard card)
                    Cards.Add(new CardNavItem { Title = card.DeviceName, Card = card });
            }

            // 尝试恢复之前选中的板卡，否则默认选第一张
            CardNavItem restored = null;
            if (previousId != null)
                foreach (var item in Cards)
                    if (item.Card.DeviceId == previousId) { restored = item; break; }

            SelectedCard = restored ?? (Cards.Count > 0 ? Cards[0] : null);
        }

        private void RefreshDetail()
        {
            var card = SelectedCard?.Card;
            if (card == null)
            {
                CardName = "——";
                DeviceId = "——";
                IsConnected = false;
                HasAlarm = false;
                ConnectionText = "未知";
                AlarmText = "未知";
                ConnectionBrush = GrayBrush;
                AlarmBrush = GrayBrush;
                return;
            }

            CardName    = card.DeviceName;
            DeviceId    = card.DeviceId;
            CardIndex   = card.CardIndex;
            AxisCount   = card.AxisCount;
            InputCount  = card.InputCount;
            OutputCount = card.OutputCount;
            IsConnected = card.IsConnected;
            HasAlarm    = card.HasAlarm;

            ConnectionText  = card.IsConnected ? "已连接" : "未连接";
            AlarmText       = card.HasAlarm    ? "报警中" : "正常";
            ConnectionBrush = card.IsConnected ? GreenBrush : RedBrush;
            AlarmBrush      = card.HasAlarm    ? RedBrush   : GreenBrush;
        }
    }

    public class CardNavItem : BindableBase
    {
        public string Title { get; set; }
        public IMotionCard Card { get; set; }
    }
}
