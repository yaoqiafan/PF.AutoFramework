using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.Demo.Mechanisms;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PF.Workstation.Demo.UI.ViewModels
{
    public class GantryMechanismViewModel : RegionViewModelBase, IDisposable
    {
        private readonly GantryMechanism _mechanism;
        private readonly DispatcherTimer _statusTimer;
        private CancellationTokenSource _cts;

        public GantryMechanismViewModel(GantryMechanism mechanism)
        {
            _mechanism = mechanism ?? throw new ArgumentNullException(nameof(mechanism));

            // 初始化命令 (传入 CancellationToken)
            InitializeCommand = new DelegateCommand(async () => await ExecuteOperationAsync(token => _mechanism.InitializeAsync(token)), CanOperate);
            ResetCommand = new DelegateCommand(async () => await ExecuteOperationAsync(token => _mechanism.ResetAsync(token)), CanOperate);
            PickCommand = new DelegateCommand(async () => await ExecuteOperationAsync(_mechanism.PickAsync), CanOperatePickPlace);
            PlaceCommand = new DelegateCommand(async () => await ExecuteOperationAsync(_mechanism.PlaceAsync), CanOperatePickPlace);

            // 手动控制命令
            MoveToCommand = new DelegateCommand(async () => await ExecuteOperationAsync(token => _mechanism.XAxis.MoveAbsoluteAsync(TargetPosition, 100,1000,1000,0.08, token)), CanOperate);
            ToggleVacuumCommand = new DelegateCommand(() =>
            {
                // 直接反转真空输出状态
                _mechanism.VacuumIO.WriteOutput(_mechanism.VacuumValvePort, !IsVacuumOn);
            }, CanOperate);

            StopCommand = new DelegateCommand(async () =>
            {
                _cts?.Cancel(); // 取消当前的异步操作
                await _mechanism.StopAsync();
            });

            // 轮询定时器，用于更新无法通过事件通知的底层硬件状态
            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _statusTimer.Tick += StatusTimer_Tick;
            _statusTimer.Start();

            StatusMessage = "准备就绪";
        }

        #region 绑定的属性

        public string MechanismName => _mechanism.MechanismName;

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set => SetProperty(ref _isBusy, value, RefreshCommands);
        }

        private bool _isInitialized;
        public bool IsInitialized
        {
            get => _isInitialized;
            set => SetProperty(ref _isInitialized, value, RefreshCommands);
        }

        private bool _hasAlarm;
        public bool HasAlarm
        {
            get => _hasAlarm;
            set => SetProperty(ref _hasAlarm, value);
        }

        private double _currentPosition;
        public double CurrentPosition
        {
            get => _currentPosition;
            set => SetProperty(ref _currentPosition, value);
        }

        private double _targetPosition;
        public double TargetPosition
        {
            get => _targetPosition;
            set => SetProperty(ref _targetPosition, value);
        }

        private bool _isVacuumOn;
        public bool IsVacuumOn
        {
            get => _isVacuumOn;
            set => SetProperty(ref _isVacuumOn, value);
        }

        private string _statusMessage;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        #endregion

        #region 命令

        public DelegateCommand InitializeCommand { get; }
        public DelegateCommand ResetCommand { get; }
        public DelegateCommand PickCommand { get; }
        public DelegateCommand PlaceCommand { get; }
        public DelegateCommand MoveToCommand { get; }
        public DelegateCommand ToggleVacuumCommand { get; }
        public DelegateCommand StopCommand { get; }

        #endregion

        #region 私有方法

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            // 轮询更新模组状态
            IsInitialized = _mechanism.IsInitialized;
            HasAlarm = _mechanism.HasAlarm;

            // 轮询更新硬件状态
            if (_mechanism.XAxis != null)
                CurrentPosition = _mechanism.XAxis.CurrentPosition.Value ;

            if (_mechanism.VacuumIO != null)
                IsVacuumOn = _mechanism.VacuumIO.ReadOutput(_mechanism.VacuumValvePort).Value ;
        }

        private bool CanOperate() => !IsBusy;
        private bool CanOperatePickPlace() => !IsBusy && IsInitialized && !HasAlarm;

        private void RefreshCommands()
        {
            InitializeCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();
            PickCommand.RaiseCanExecuteChanged();
            PlaceCommand.RaiseCanExecuteChanged();
            MoveToCommand.RaiseCanExecuteChanged();
            ToggleVacuumCommand.RaiseCanExecuteChanged();
        }

        private async Task ExecuteOperationAsync(Func<CancellationToken, Task> operation)
        {
            if (IsBusy) return;

            IsBusy = true;
            _cts = new CancellationTokenSource();
            try
            {
                StatusMessage = "正在执行操作...";
                await operation(_cts.Token);
                StatusMessage = "操作成功完成";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已被手动中止";
            }
            catch (Exception ex)
            {
                StatusMessage = $"操作失败: {ex.Message}";
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
                IsBusy = false;
            }
        }

        public void Dispose()
        {
            _statusTimer?.Stop();
            _cts?.Cancel();
            _cts?.Dispose();
        }

        #endregion
    }
}