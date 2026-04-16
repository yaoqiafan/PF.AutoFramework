using PF.Core.Constants;
using PF.Core.Entities.Identity;
using PF.Core.Enums;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Stations;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.WorkStations
{
    public class WorkStationDetectionStationDebugViewModel : RegionViewModelBase, IDisposable
    {
        private readonly WorkStationDetectionStation<StationMemoryBaseParam> _station;
        private readonly IStationSyncService _sync;
        private readonly IUserService _userService;
        private readonly IAlarmService _alarmService;

        // ── 看板只读属性 ────────────────────────────────────────────────────

        private MachineState _currentState;
        public MachineState CurrentState
        {
            get => _currentState;
            private set => SetProperty(ref _currentState, value);
        }

        private string _currentStepDescription = "就绪";
        public string CurrentStepDescription
        {
            get => _currentStepDescription;
            private set => SetProperty(ref _currentStepDescription, value);
        }

        private OperationMode _currentMode;
        public OperationMode CurrentMode
        {
            get => _currentMode;
            private set => SetProperty(ref _currentMode, value);
        }

        private Brush _statusBrush;
        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        // ── 权限控制 ─────────────────────────────────────────────────────────

        public bool CanManualControl => _userService.IsAuthorized(UserLevel.SuperUser);

        // ── 命令 ─────────────────────────────────────────────────────────────

        public DelegateCommand InitializeCommand              { get; }
        public DelegateCommand StartCommand                   { get; }
        public DelegateCommand StopCommand                    { get; }
        public DelegateCommand PauseCommand                   { get; }
        public DelegateCommand ResumeCommand                  { get; }
        public DelegateCommand ResetCommand                   { get; }
        public DelegateCommand TriggerAlarmCommand            { get; }
        public DelegateCommand TriggerStation1DetectionCommand { get; }
        public DelegateCommand TriggerStation2DetectionCommand { get; }

        /// <summary>向 AlarmService 注入测试报警，验证复合键链路与历史落盘</summary>
        public DelegateCommand TriggerTestAlarmCommand { get; }

        /// <summary>清除本工站所有活跃报警，验证 HardwareResetRequestedEvent 广播</summary>
        public DelegateCommand ClearTestAlarmCommand { get; }

        public WorkStationDetectionStationDebugViewModel(IContainerProvider containerProvider)
        {
            _station      = containerProvider.Resolve<WorkStationDetectionStation<StationMemoryBaseParam>>(nameof(WorkStationDetectionStation<StationMemoryBaseParam>));
            _sync         = containerProvider.Resolve<IStationSyncService>();
            _userService  = containerProvider.Resolve<IUserService>();
            _alarmService = containerProvider.Resolve<IAlarmService>();

            // 首次同步（防止 PropertyChanged 到来前 UI 显示默认值）
            _statusBrush            = StateToBrush(_station.CurrentState);
            _currentState           = _station.CurrentState;
            _currentStepDescription = _station.CurrentStepDescription;
            _currentMode            = _station.CurrentMode;

            _userService.CurrentUserChanged += OnCurrentUserChanged;

            // 被动订阅：由底层后台线程触发，必须通过 Dispatcher 派发到 UI 线程
            _station.PropertyChanged += OnStationPropertyChanged;

            InitializeCommand = new DelegateCommand(ExecuteInitialize,
                () => CanManualControl && _station.CurrentState == MachineState.Uninitialized);
            StartCommand = new DelegateCommand(ExecuteStart,
                () => CanManualControl && _station.CurrentState == MachineState.Idle);
            StopCommand = new DelegateCommand(ExecuteStop,
                () => CanManualControl && (_station.CurrentState == MachineState.Running || _station.CurrentState == MachineState.Paused));
            PauseCommand = new DelegateCommand(ExecutePause,
                () => CanManualControl && _station.CurrentState == MachineState.Running);
            ResumeCommand = new DelegateCommand(ExecuteResume,
                () => CanManualControl && _station.CurrentState == MachineState.Paused);
            ResetCommand = new DelegateCommand(ExecuteReset,
                () => CanManualControl && _station.CurrentState == MachineState.Alarm);
            TriggerAlarmCommand = new DelegateCommand(ExecuteTriggerAlarm,
                () => CanManualControl && _station.CurrentState != MachineState.Alarm);
            TriggerStation1DetectionCommand = new DelegateCommand(ExecuteTriggerStation1Detection,
                () => CanManualControl && _station.CurrentState == MachineState.Running);
            TriggerStation2DetectionCommand = new DelegateCommand(ExecuteTriggerStation2Detection,
                () => CanManualControl && _station.CurrentState == MachineState.Running);

            TriggerTestAlarmCommand = new DelegateCommand(ExecuteTriggerTestAlarm,
                () => CanManualControl);
            ClearTestAlarmCommand = new DelegateCommand(ExecuteClearTestAlarm,
                () => CanManualControl);
        }

        // ── 被动状态推送（替代 DispatcherTimer 轮询） ─────────────────────────

        /// <summary>
        /// 响应工站 PropertyChanged 通知。
        /// 由底层后台心跳线程触发，必须通过 Dispatcher.BeginInvoke 切回 UI 线程，
        /// 防止跨线程访问 ObservableCollection / BindableBase 引发 InvalidOperationException。
        /// </summary>
        private void OnStationPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                switch (e.PropertyName)
                {
                    case nameof(_station.CurrentState):
                        CurrentState = _station.CurrentState;
                        StatusBrush  = StateToBrush(_station.CurrentState);
                        RaiseAllCommandCanExecute();
                        break;

                    case nameof(_station.CurrentStepDescription):
                        CurrentStepDescription = _station.CurrentStepDescription;
                        break;

                    case nameof(_station.CurrentMode):
                        CurrentMode = _station.CurrentMode;
                        break;
                }
            });
        }

        private void RaiseAllCommandCanExecute()
        {
            InitializeCommand.RaiseCanExecuteChanged();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
            TriggerAlarmCommand.RaiseCanExecuteChanged();
            TriggerStation1DetectionCommand.RaiseCanExecuteChanged();
            TriggerStation2DetectionCommand.RaiseCanExecuteChanged();
            TriggerTestAlarmCommand.RaiseCanExecuteChanged();
            ClearTestAlarmCommand.RaiseCanExecuteChanged();
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

        private static Brush StateToBrush(MachineState state) =>
            _stateBrushMap.TryGetValue(state, out var brush) ? brush : _defaultBrush;

        // ── 权限事件处理 ──────────────────────────────────────────────────────

        private void OnCurrentUserChanged(object sender, UserInfo? user)
        {
            RaisePropertyChanged(nameof(CanManualControl));
            RaiseAllCommandCanExecute();
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

        private void ExecuteStop()   => _station.Stop();
        private void ExecutePause()  => _station.Pause();

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
            => _sync.Release(WorkstationSignals.工位1检测完成.ToString(),E_WorkStation.OCR检测工站.ToString());

        private void ExecuteTriggerStation2Detection()
            => _sync.Release(WorkstationSignals.工位2检测完成.ToString(), E_WorkStation.OCR检测工站.ToString());

        /// <summary>向 AlarmService 注入测试报警，验证复合键链路与历史落盘</summary>
        private void ExecuteTriggerTestAlarm()
            => _alarmService.TriggerAlarm(_station.StationName, AlarmCodes.System.StationSyncError);

        /// <summary>清除本工站所有活跃报警，触发 HardwareResetRequestedEvent 广播</summary>
        private void ExecuteClearTestAlarm()
            => _alarmService.ClearAlarm(_station.StationName);

        // ── 销毁 ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            _station.PropertyChanged       -= OnStationPropertyChanged;
            _userService.CurrentUserChanged -= OnCurrentUserChanged;
        }

        public override void Destroy()
        {
            Dispose();
        }
    }
}
