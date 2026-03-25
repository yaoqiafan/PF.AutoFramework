using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 工站调试主视图 ViewModel
    ///
    /// 左侧面板集成主控状态（状态、模式、报警、控制指令）+ 可点击工站列表；
    /// 点击工站条目后，右侧 StationContentRegion 加载对应工站调试视图。
    /// </summary>
    public class StationDebugViewModel : RegionViewModelBase, IDisposable
    {
        private readonly IRegionManager _regionManager;
        private readonly IMasterController _controller;
        private readonly IStationSyncService _syncService;
        private readonly DispatcherTimer _pollTimer;

        // ── 主控状态属性 ─────────────────────────────────────────────────────

        private static readonly Dictionary<MachineState, Brush> _stateBrushMap = new()
        {
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x2d, 0xb8, 0x4d)) },
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) },
            { MachineState.Alarm,        new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) },
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) },
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
        };
        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        private MachineState _controllerState;
        public MachineState ControllerState
        {
            get => _controllerState;
            private set
            {
                SetProperty(ref _controllerState, value);
                RaisePropertyChanged(nameof(StatusBrush));
            }
        }

        private OperationMode _currentMode;
        public OperationMode CurrentMode
        {
            get => _currentMode;
            private set => SetProperty(ref _currentMode, value);
        }

        private string _lastAlarmMessage = "无";
        public string LastAlarmMessage
        {
            get => _lastAlarmMessage;
            private set => SetProperty(ref _lastAlarmMessage, value);
        }

        public Brush StatusBrush =>
            _stateBrushMap.TryGetValue(_controllerState, out var b) ? b : _defaultBrush;

        // ── 主控指令 ─────────────────────────────────────────────────────────

        public DelegateCommand InitializeAllCommand { get; }
        public DelegateCommand StartAllCommand      { get; }
        public DelegateCommand PauseAllCommand      { get; }
        public DelegateCommand ResumeAllCommand     { get; }
        public DelegateCommand StopAllCommand       { get; }
        public DelegateCommand ResetAllCommand      { get; }
        public DelegateCommand EmergencyStopCommand { get; }

        // ── 流水线信号量列表 ──────────────────────────────────────────────────

        public ObservableCollection<SignalStatusItem> SignalItems { get; } = new();

        // ── 工站导航列表 ─────────────────────────────────────────────────────

        public ObservableCollection<StationNavItem> NavItems { get; } = new();

        private StationNavItem _selectedItem;
        public StationNavItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                if (SetProperty(ref _selectedItem, value) && value != null)
                    NavigateTo(value.ViewName);
            }
        }

        public StationDebugViewModel(
            IEnumerable<StationBase> stations,
            IMasterController controller,
            IStationSyncService syncService,
            IRegionManager regionManager)
        {
            _regionManager = regionManager;
            _controller    = controller;
            _syncService   = syncService;

            _controller.MasterAlarmTriggered += OnMasterAlarmTriggered;

            BuildNavItems(stations);

            InitializeAllCommand = new DelegateCommand(
                async () => { try { await _controller.InitializeAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Uninitialized);

            StartAllCommand = new DelegateCommand(
                async () => { try { await _controller.StartAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Idle);

            PauseAllCommand = new DelegateCommand(
                () => _controller.PauseAll(),
                () => _controller.CurrentState == MachineState.Running);

            ResumeAllCommand = new DelegateCommand(
                async () => { try { await _controller.ResumeAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Paused);

            StopAllCommand = new DelegateCommand(
                () => _controller.StopAll(),
                () => _controller.CurrentState == MachineState.Running
                   || _controller.CurrentState == MachineState.Paused);

            ResetAllCommand = new DelegateCommand(
                async () => { try { await _controller.ResetAllAsync(); } catch (Exception ex) { Log(ex); } },
                () => _controller.CurrentState == MachineState.Alarm);

            EmergencyStopCommand = new DelegateCommand(
                () => _controller.EmergencyStop(),
                () => _controller.CurrentState != MachineState.Alarm
                   && _controller.CurrentState != MachineState.Uninitialized);

            _pollTimer = new DispatcherTimer(DispatcherPriority.DataBind)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        private void BuildNavItems(IEnumerable<StationBase<StationMemoryBaseParam>> stations)
        {
            var items = new List<(StationNavItem Item, int Order)>();

            foreach (var station in stations)
            {
                var attr = station.GetType().GetCustomAttribute<StationUIAttribute>();
                if (attr == null) continue;

                items.Add((new StationNavItem
                {
                    Title       = attr.Title,
                    ViewName    = attr.ViewName,
                    StationName = station.StationName,
                    Station     = station,
                }, attr.Order));
            }

            foreach (var (item, _) in items.OrderBy(x => x.Order))
                NavItems.Add(item);
        }

        private void NavigateTo(string viewName)
        {
            if (string.IsNullOrWhiteSpace(viewName)) return;

            _regionManager.RequestNavigate(
                NavigationConstants.Regions.StationContentRegion,
                viewName,
                result =>
                {
                    if (result.Success == false)
                        System.Diagnostics.Debug.WriteLine(
                            $"[StationDebug] 导航失败: {result.Exception?.Message}");
                });
        }

        private void OnPollTick(object sender, EventArgs e)
        {
            ControllerState = _controller.CurrentState;
            CurrentMode     = _controller.CurrentMode;

            foreach (var item in NavItems)
                item.Refresh();

            RefreshSignals();

            InitializeAllCommand.RaiseCanExecuteChanged();
            StartAllCommand.RaiseCanExecuteChanged();
            PauseAllCommand.RaiseCanExecuteChanged();
            ResumeAllCommand.RaiseCanExecuteChanged();
            StopAllCommand.RaiseCanExecuteChanged();
            ResetAllCommand.RaiseCanExecuteChanged();
            EmergencyStopCommand.RaiseCanExecuteChanged();
        }

        private void RefreshSignals()
        {
            var snapshot = _syncService.GetSnapshot();

            // 新增在快照中但不在列表中的条目
            foreach (var kv in snapshot)
            {
                var existing = SignalItems.FirstOrDefault(s => s.Name == kv.Key);
                if (existing == null)
                    SignalItems.Add(new SignalStatusItem { Name = kv.Key, InitialCount = kv.Value.InitialCount });
                else
                    existing.Update(kv.Value.CurrentCount);
            }

            // 移除已不存在于快照的条目（信号量注销场景）
            for (int i = SignalItems.Count - 1; i >= 0; i--)
            {
                if (!snapshot.ContainsKey(SignalItems[i].Name))
                    SignalItems.RemoveAt(i);
            }
        }

        private void OnMasterAlarmTriggered(object sender, string message) =>
            LastAlarmMessage = message;

        private static void Log(Exception ex) =>
            System.Diagnostics.Debug.WriteLine($"[StationDebug] 指令执行失败: {ex.Message}");

        public void Dispose()
        {
            _pollTimer.Stop();
            _controller.MasterAlarmTriggered -= OnMasterAlarmTriggered;
        }

        public override void Destroy() => Dispose();
    }

    /// <summary>流水线信号量状态条目</summary>
    public class SignalStatusItem : BindableBase
    {
        public string Name         { get; init; }
        public int    InitialCount { get; init; }

        private int _currentCount;
        public int CurrentCount
        {
            get => _currentCount;
            private set
            {
                SetProperty(ref _currentCount, value);
                RaisePropertyChanged(nameof(StatusText));
            }
        }

        /// <summary>显示文本：当前可用计数 / 最大计数</summary>
        public string StatusText => CurrentCount > 0 ? $"可用 ({CurrentCount})" : "阻塞 (0)";

        internal void Update(int currentCount) => CurrentCount = currentCount;
    }

    /// <summary>工站导航条目，持有实时状态供左侧列表展示</summary>
    public class StationNavItem : BindableBase
    {
        private static readonly Dictionary<MachineState, Brush> _brushMap = new()
        {
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x2d, 0xb8, 0x4d)) },
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) },
            { MachineState.Alarm,        new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) },
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) },
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
        };
        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        public string Title       { get; init; }
        public string ViewName    { get; init; }
        public string StationName { get; init; }
        internal StationBase Station { get; init; }

        private MachineState _state;
        public MachineState State
        {
            get => _state;
            private set
            {
                SetProperty(ref _state, value);
                RaisePropertyChanged(nameof(StateBrush));
            }
        }

        private string _stepDescription = string.Empty;
        public string StepDescription
        {
            get => _stepDescription;
            private set => SetProperty(ref _stepDescription, value);
        }

        public Brush StateBrush =>
            _brushMap.TryGetValue(_state, out var b) ? b : _defaultBrush;

        /// <summary>由定时器调用，从 StationBase 拉取最新状态</summary>
        internal void Refresh()
        {
            if (Station == null) return;
            State           = Station.CurrentState;
            StepDescription = Station.CurrentStepDescription;
        }
    }
}
