using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Stations;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.WorkStations
{
    /// <summary>
    /// WorkStationDetectionStationDebugViewModel
    /// </summary>
    public class WorkStationDetectionStationDebugViewModel : RegionViewModelBase, IDisposable
    {
        private readonly WorkStationDetectionStation<StationMemoryBaseParam> _station;
        private readonly IStationSyncService _sync;
        private readonly IUserService _userService;
        private readonly DispatcherTimer _pollTimer;

        // ── 看板只读属性 ────────────────────────────────────────────────────

        private MachineState _currentState;
        /// <summary>
        /// 成员
        /// </summary>
        public MachineState CurrentState
        {
            get => _currentState;
            private set => SetProperty(ref _currentState, value);
        }

        private string _currentStepDescription = "就绪";
        /// <summary>
        /// 成员
        /// </summary>
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            private set => SetProperty(ref _currentStepDescription, value);
        }

        private OperationMode _currentMode;
        /// <summary>
        /// 获取或设置 CurrentMode
        /// </summary>
        public OperationMode CurrentMode
        {
            get => _currentMode;
            private set => SetProperty(ref _currentMode, value);
        }

        private Brush _statusBrush;
        /// <summary>
        /// 成员
        /// </summary>
        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        // ── 权限控制 ─────────────────────────────────────────────────────────
        /// <summary>
        /// 获取或设置 CanManualControl
        /// </summary>

        public bool CanManualControl => _userService.IsAuthorized(UserLevel.SuperUser);

        // ── 命令 ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Initialize 命令
        /// </summary>

        public DelegateCommand InitializeCommand              { get; }
        /// <summary>
        /// Start 命令
        /// </summary>
        public DelegateCommand StartCommand                   { get; }
        /// <summary>
        /// Stop 命令
        /// </summary>
        public DelegateCommand StopCommand                    { get; }
        /// <summary>
        /// Pause 命令
        /// </summary>
        public DelegateCommand PauseCommand                   { get; }
        /// <summary>
        /// Resume 命令
        /// </summary>
        public DelegateCommand ResumeCommand                  { get; }
        /// <summary>
        /// Reset 命令
        /// </summary>
        public DelegateCommand ResetCommand                   { get; }
        /// <summary>
        /// TriggerAlarm 命令
        /// </summary>
        public DelegateCommand TriggerAlarmCommand            { get; }
        /// <summary>
        /// TriggerStation1Detection 命令
        /// </summary>
        public DelegateCommand TriggerStation1DetectionCommand { get; }
        /// <summary>
        /// TriggerStation2Detection 命令
        /// </summary>
        public DelegateCommand TriggerStation2DetectionCommand { get; }
        /// <summary>
        /// WorkStationDetectionStationDebugViewModel 构造函数
        /// </summary>

        public WorkStationDetectionStationDebugViewModel(IContainerProvider containerProvider)
        {
            _station      = containerProvider.Resolve<WorkStationDetectionStation<StationMemoryBaseParam>>(nameof(WorkStationDetectionStation<StationMemoryBaseParam>));
            _sync         = containerProvider.Resolve<IStationSyncService>();
            _userService  = containerProvider.Resolve<IUserService>();

            _statusBrush = StateToBrush(MachineState.Uninitialized);

            _userService.CurrentUserChanged += OnCurrentUserChanged;

            InitializeCommand = new DelegateCommand(ExecuteInitialize,
                () => CanManualControl && (_station.CurrentState == MachineState.Uninitialized
                                        || _station.CurrentState == MachineState.Idle));
            StartCommand = new DelegateCommand(ExecuteStart,
                () => CanManualControl && _station.CurrentState == MachineState.Idle);
            StopCommand = new DelegateCommand(ExecuteStop,
                () => CanManualControl && (_station.CurrentState == MachineState.Idle
                                        || _station.CurrentState == MachineState.Running
                                        || _station.CurrentState == MachineState.Paused));
            PauseCommand = new DelegateCommand(ExecutePause,
                () => CanManualControl && _station.CurrentState == MachineState.Running);
            ResumeCommand = new DelegateCommand(ExecuteResume,
                () => CanManualControl && _station.CurrentState == MachineState.Paused);
            ResetCommand = new DelegateCommand(ExecuteReset,
                () => CanManualControl && (_station.CurrentState == MachineState.InitAlarm
                                        || _station.CurrentState == MachineState.RunAlarm));
            TriggerAlarmCommand = new DelegateCommand(ExecuteTriggerAlarm,
                () => CanManualControl && _station.CurrentState != MachineState.InitAlarm
                                       && _station.CurrentState != MachineState.RunAlarm);
            TriggerStation1DetectionCommand = new DelegateCommand(ExecuteTriggerStation1Detection,
                () => CanManualControl && _station.CurrentState == MachineState.Running);
            TriggerStation2DetectionCommand = new DelegateCommand(ExecuteTriggerStation2Detection,
                () => CanManualControl && _station.CurrentState == MachineState.Running);

            _pollTimer = new DispatcherTimer(DispatcherPriority.DataBind)
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        // ── 轮询刷新 ─────────────────────────────────────────────────────────

        private void OnPollTick(object sender, EventArgs e)
        {
            CurrentState = _station.CurrentState;
            CurrentStepDescription = _station.CurrentStepDescription;
            CurrentMode = _station.CurrentMode;
            StatusBrush = StateToBrush(_station.CurrentState);

            InitializeCommand.RaiseCanExecuteChanged();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
            TriggerAlarmCommand.RaiseCanExecuteChanged();
            TriggerStation1DetectionCommand.RaiseCanExecuteChanged();
            TriggerStation2DetectionCommand.RaiseCanExecuteChanged();
        }

        private static readonly Dictionary<MachineState, Brush> _stateBrushMap = new()
        {
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x2d, 0xb8, 0x4d)) },
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) },
            { MachineState.InitAlarm,    new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)) },
            { MachineState.RunAlarm,     new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) },
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) },
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
        };
        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        private static Brush StateToBrush(MachineState state) =>
            _stateBrushMap.TryGetValue(state, out var brush) ? brush : _defaultBrush;

        // ── 权限事件处理 ──────────────────────────────────────────────────────

        private void OnCurrentUserChanged(object sender, UserInfo? user)
        {
            RaisePropertyChanged(nameof(CanManualControl));
            InitializeCommand.RaiseCanExecuteChanged();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
            TriggerAlarmCommand.RaiseCanExecuteChanged();
        }

        // ── 命令实现 ─────────────────────────────────────────────────────────

        private async void ExecuteInitialize()
        {
            try { await _station.ExecuteInitializeAsync(CancellationToken.None); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StationDebug] 初始化失败: {ex.Message}"); }
        }

        private async void ExecuteStart()
        {
            try { await _station.StartAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StationDebug] 启动失败: {ex.Message}"); }
        }

        private async void ExecuteStop()
        {
            try { await _station.StopAsync(); }
            catch { }
        }
        private void ExecutePause() { try { _station.Pause(); } catch { } }

        private async void ExecuteResume()
        {
            try { await _station.ResumeAsync(); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StationDebug] 恢复失败: {ex.Message}"); }
        }

        private async void ExecuteReset()
        {
            try { await _station.ExecuteResetAsync(CancellationToken.None); }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[StationDebug] 复位失败: {ex.Message}"); }
        }

        private void ExecuteTriggerAlarm() => _station.TriggerAlarm();

        private void ExecuteTriggerStation1Detection()
            => _sync.Release(WorkstationSignals.工位1检测完成.ToString(), E_WorkStation.OCR检测工站.ToString());

        private void ExecuteTriggerStation2Detection()
            => _sync.Release(WorkstationSignals.工位2检测完成.ToString(), E_WorkStation.OCR检测工站.ToString());

        // ── 销毁 ─────────────────────────────────────────────────────────────
        /// <summary>
        /// Dispose
        /// </summary>

        public void Dispose()
        {
            _pollTimer.Stop();
            _userService.CurrentUserChanged -= OnCurrentUserChanged;
        }
        /// <summary>
        /// Destroy
        /// </summary>

        public override void Destroy()
        {
            Dispose();
        }
    }
}
