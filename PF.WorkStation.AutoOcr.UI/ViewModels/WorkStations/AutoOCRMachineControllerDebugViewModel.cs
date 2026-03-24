using PF.Core.Enums;
using PF.Core.Interfaces.Station;
using PF.Infrastructure.Station.Basic;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.WorkStations
{
    /// <summary>
    /// 机台子工站状态条目（供 UI 列表绑定）
    /// </summary>
    public class StationStatusItem : BindableBase
    {
        public string StationName { get; set; } = string.Empty;

        private MachineState _state;
        public MachineState State
        {
            get => _state;
            set
            {
                SetProperty(ref _state, value);
                RaisePropertyChanged(nameof(StateBrush));
            }
        }

        private string _stepDescription = "就绪";
        public string StepDescription
        {
            get => _stepDescription;
            set => SetProperty(ref _stepDescription, value);
        }

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

        public Brush StateBrush =>
            _brushMap.TryGetValue(_state, out var b) ? b : _defaultBrush;
    }

    /// <summary>
    /// 流水线信号量状态条目（供 UI 列表绑定，待 WorkstationSignals 定义后扩展）
    /// </summary>
    public class SignalStatusItem : BindableBase
    {
        public string SignalName { get; set; } = string.Empty;
        public int InitialCount { get; set; }

        private string _statusText = "未知";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }
    }

    /// <summary>
    /// AutoOCR 机台主控监控 ViewModel
    ///
    /// 展示：
    ///   · 机台主控（IMasterController）全局状态与运行模式
    ///   · 各子工站实时状态与步序描述
    ///   · 流水线信号量占位面板（待 WorkstationSignals 有内容后填充）
    ///
    /// 数据刷新：200ms 轮询定时器，从工站状态同步到 ViewModel 属性。
    /// </summary>
    public class AutoOCRMachineControllerDebugViewModel : RegionViewModelBase, IDisposable
    {
        private readonly IMasterController _controller;
        private readonly IEnumerable<StationBase> _subStations;
        private readonly DispatcherTimer _pollTimer;

        // ── 主控状态属性 ─────────────────────────────────────────────────────

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

        public Brush StatusBrush =>
            _stateBrushMap.TryGetValue(_controllerState, out var b) ? b : _defaultBrush;

        // ── 子工站状态列表 ────────────────────────────────────────────────────

        public ObservableCollection<StationStatusItem> SubStationItems { get; } = new();

        // ── 信号量状态列表 ────────────────────────────────────────────────────

        public ObservableCollection<SignalStatusItem> SignalItems { get; } = new();

        // ── 命令 ─────────────────────────────────────────────────────────────

        public DelegateCommand InitializeAllCommand { get; }
        public DelegateCommand StartAllCommand { get; }
        public DelegateCommand PauseAllCommand { get; }
        public DelegateCommand ResumeAllCommand { get; }
        public DelegateCommand StopAllCommand { get; }
        public DelegateCommand ResetAllCommand { get; }
        public DelegateCommand EmergencyStopCommand { get; }

        public AutoOCRMachineControllerDebugViewModel(IContainerProvider containerProvider)
        {
            _controller = containerProvider.Resolve<IMasterController>();
            _subStations = containerProvider.Resolve<IEnumerable<StationBase>>();

            // 订阅主控报警事件
            _controller.MasterAlarmTriggered += OnMasterAlarmTriggered;

            // 初始化子工站条目
            foreach (var station in _subStations)
            {
                SubStationItems.Add(new StationStatusItem
                {
                    StationName = station.StationName,
                    State = station.CurrentState,
                    StepDescription = station.CurrentStepDescription
                });
            }

            // 绑定命令
            InitializeAllCommand = new DelegateCommand(async () =>
            {
                try { await _controller.InitializeAllAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerDebug] 全线初始化失败: {ex.Message}"); }
            }, () => _controller.CurrentState == MachineState.Uninitialized);

            StartAllCommand = new DelegateCommand(async () =>
            {
                try { await _controller.StartAllAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerDebug] 全线启动失败: {ex.Message}"); }
            }, () => _controller.CurrentState == MachineState.Idle);

            PauseAllCommand = new DelegateCommand(
                () => _controller.PauseAll(),
                () => _controller.CurrentState == MachineState.Running);

            ResumeAllCommand = new DelegateCommand(async () =>
            {
                try { await _controller.ResumeAllAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerDebug] 全线恢复失败: {ex.Message}"); }
            }, () => _controller.CurrentState == MachineState.Paused);

            StopAllCommand = new DelegateCommand(
                () => _controller.StopAll(),
                () => _controller.CurrentState == MachineState.Running || _controller.CurrentState == MachineState.Paused);

            ResetAllCommand = new DelegateCommand(async () =>
            {
                try { await _controller.ResetAllAsync(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[ControllerDebug] 全线复位失败: {ex.Message}"); }
            }, () => _controller.CurrentState == MachineState.Alarm);

            EmergencyStopCommand = new DelegateCommand(
                () => _controller.EmergencyStop(),
                () => _controller.CurrentState != MachineState.Alarm && _controller.CurrentState != MachineState.Uninitialized);

            _pollTimer = new DispatcherTimer(DispatcherPriority.DataBind)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        // ── 轮询刷新 ─────────────────────────────────────────────────────────

        private void OnPollTick(object sender, EventArgs e)
        {
            ControllerState = _controller.CurrentState;
            CurrentMode = _controller.CurrentMode;

            // 刷新子工站列表
            var stations = new List<StationBase>(_subStations);
            for (int i = 0; i < SubStationItems.Count && i < stations.Count; i++)
            {
                SubStationItems[i].State = stations[i].CurrentState;
                SubStationItems[i].StepDescription = stations[i].CurrentStepDescription;
            }

            // 刷新命令可用性
            InitializeAllCommand.RaiseCanExecuteChanged();
            StartAllCommand.RaiseCanExecuteChanged();
            PauseAllCommand.RaiseCanExecuteChanged();
            ResumeAllCommand.RaiseCanExecuteChanged();
            StopAllCommand.RaiseCanExecuteChanged();
            ResetAllCommand.RaiseCanExecuteChanged();
            EmergencyStopCommand.RaiseCanExecuteChanged();
        }

        private void OnMasterAlarmTriggered(object sender, string message)
        {
            LastAlarmMessage = message;
        }

        // ── 销毁 ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            _pollTimer.Stop();
            _controller.MasterAlarmTriggered -= OnMasterAlarmTriggered;
        }

        public override void Destroy()
        {
            Dispose();
        }
    }
}
