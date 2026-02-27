using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using PF.Workstation.Demo;
using Prism.Commands;
using Prism.Mvvm;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Workstation.Demo.UI.ViewModels
{
    /// <summary>
    /// 取放工站调试面板 ViewModel
    ///
    /// 权限控制：
    ///   手动控制区域（初始化、启动、停止等）仅 SuperUser 级别可操作。
    ///   通过订阅 IUserService.CurrentUserChanged 事件，在用户切换时
    ///   自动刷新 CanManualControl 属性及所有命令的 CanExecute 状态。
    ///
    /// 数据刷新：
    ///   100ms 轮询定时器拉取工站状态（CurrentState、CurrentStepDescription、
    ///   CurrentMode、CycleCount），以应对从后台线程修改的状态值。
    /// </summary>
    public class PickPlaceStationDebugViewModel : BindableBase, IDisposable
    {
        private readonly PickPlaceStation _station;
        private readonly IUserService _userService;
        private readonly DispatcherTimer _pollTimer;

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

        private Brush _statusBrush = new SolidColorBrush(Color.FromRgb(0x2d, 0xb8, 0x4d));
        /// <summary>根据当前状态机状态返回状态指示灯画刷，供 XAML 直接绑定 Fill/Background</summary>
        public Brush StatusBrush
        {
            get => _statusBrush;
            private set => SetProperty(ref _statusBrush, value);
        }

        // ── 权限控制 ─────────────────────────────────────────────────────────

        /// <summary>
        /// 是否允许手动控制（仅 SuperUser 级别）。
        /// 驱动底部"手动控制" GroupBox 的 IsEnabled 绑定。
        /// </summary>
        public bool CanManualControl => _userService.IsAuthorized(UserLevel.SuperUser);

        // ── 命令 ─────────────────────────────────────────────────────────────

        public DelegateCommand InitializeCommand { get; }
        public DelegateCommand StartCommand      { get; }
        public DelegateCommand StopCommand       { get; }
        public DelegateCommand PauseCommand      { get; }
        public DelegateCommand ResumeCommand     { get; }
        public DelegateCommand ResetCommand      { get; }
        public DelegateCommand TriggerAlarmCommand { get; }

        public PickPlaceStationDebugViewModel(PickPlaceStation station, IUserService userService)
        {
            _station     = station ?? throw new ArgumentNullException(nameof(station));
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));

            // 订阅用户切换事件，刷新权限状态
            _userService.CurrentUserChanged += OnCurrentUserChanged;

            // 绑定命令，CanExecute = 权限 + 当前状态机允许
            InitializeCommand   = new DelegateCommand(ExecuteInitialize,   () => CanManualControl && _station.CurrentState == MachineState.Uninitialized);
            StartCommand        = new DelegateCommand(ExecuteStart,        () => CanManualControl && _station.CurrentState == MachineState.Idle);
            StopCommand         = new DelegateCommand(ExecuteStop,         () => CanManualControl && (_station.CurrentState == MachineState.Running || _station.CurrentState == MachineState.Paused));
            PauseCommand        = new DelegateCommand(ExecutePause,        () => CanManualControl && _station.CurrentState == MachineState.Running);
            ResumeCommand       = new DelegateCommand(ExecuteResume,       () => CanManualControl && _station.CurrentState == MachineState.Paused);
            ResetCommand        = new DelegateCommand(ExecuteReset,        () => CanManualControl && _station.CurrentState == MachineState.Alarm);
            TriggerAlarmCommand = new DelegateCommand(ExecuteTriggerAlarm, () => CanManualControl && _station.CurrentState != MachineState.Alarm);

            // 100ms 轮询定时器：从工站状态同步到 ViewModel 属性
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
            CurrentState            = _station.CurrentState;
            CurrentStepDescription  = _station.CurrentStepDescription;
            CurrentMode             = _station.CurrentMode;
            StatusBrush             = StateToBrush(_station.CurrentState);

            // 同步刷新所有命令的 CanExecute（因为状态机状态会后台变化）
            InitializeCommand.RaiseCanExecuteChanged();
            StartCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            ResumeCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
            TriggerAlarmCommand.RaiseCanExecuteChanged();
        }

        private static readonly Dictionary<MachineState, Brush> _stateBrushMap = new()
        {
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x2d, 0xb8, 0x4d)) }, // Success green
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) }, // Warning yellow
            { MachineState.Alarm,        new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) }, // Danger red
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) }, // Primary blue
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) }, // Info cyan
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) }, // Primary blue
        };
        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        private static Brush StateToBrush(MachineState state) =>
            _stateBrushMap.TryGetValue(state, out var brush) ? brush : _defaultBrush;

        // ── 权限事件处理 ──────────────────────────────────────────────────────

        private void OnCurrentUserChanged(object sender, Core.Entities.Identity.UserInfo? user)
        {
            // 刷新权限属性及所有依赖命令
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

        // ── 销毁 ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            _pollTimer.Stop();
            _userService.CurrentUserChanged -= OnCurrentUserChanged;
        }
    }
}
