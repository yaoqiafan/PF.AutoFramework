using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Hardware;
using PF.Core.Interfaces.Hardware.Motor.Basic;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows.Threading;

namespace PF.Modules.HardwareDebug.ViewModels
{
    /// <summary>
    /// 轴调试界面 ViewModel
    ///
    /// 功能：
    ///   · 从 IHardwareManagerService 获取所有轴设备列表（ComboBox 选择）
    ///   · 实时刷新轴状态（200ms DispatcherTimer）
    ///   · 运动控制：绝对定位、相对定位、点动、回原点、急停、使能/断使能
    ///   · 点表管理：添加空行、删除选中行、保存到文件、快速移动到选中点
    /// </summary>
    public class AxisDebugViewModel : RegionViewModelBase
    {
        private readonly IHardwareManagerService _hwManager;
        private readonly DispatcherTimer _statusTimer;
        private CancellationTokenSource _moveCts = new();

        // ── 轴选择 ──────────────────────────────────────────────────────────────

        private ObservableCollection<IAxis> _availableAxes = new();
        public ObservableCollection<IAxis> AvailableAxes
        {
            get => _availableAxes;
            set => SetProperty(ref _availableAxes, value);
        }

        private IAxis? _selectedAxis;
        public IAxis? SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (SetProperty(ref _selectedAxis, value))
                    OnAxisChanged();
            }
        }

        // ── 实时状态（轮询刷新）────────────────────────────────────────────────

        private double _currentPosition;
        public double CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isMoving;
        public bool IsMoving { get => _isMoving; set => SetProperty(ref _isMoving, value); }

        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private bool _isConnected;
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _hasAlarm;
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }

        private bool _isPositiveLimit;
        public bool IsPositiveLimit { get => _isPositiveLimit; set => SetProperty(ref _isPositiveLimit, value); }

        private bool _isNegativeLimit;
        public bool IsNegativeLimit { get => _isNegativeLimit; set => SetProperty(ref _isNegativeLimit, value); }

        // ── 运动控制参数 ────────────────────────────────────────────────────────

        private double _moveTargetPosition;
        public double MoveTargetPosition
        {
            get => _moveTargetPosition;
            set => SetProperty(ref _moveTargetPosition, value);
        }

        private double _moveRelativeDistance;
        public double MoveRelativeDistance
        {
            get => _moveRelativeDistance;
            set => SetProperty(ref _moveRelativeDistance, value);
        }

        private double _moveVelocity = 100.0;
        public double MoveVelocity
        {
            get => _moveVelocity;
            set => SetProperty(ref _moveVelocity, value);
        }

        private double _jogVelocity = 50.0;
        public double JogVelocity
        {
            get => _jogVelocity;
            set => SetProperty(ref _jogVelocity, value);
        }

        private string _statusMessage = "就绪";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // ── 点表管理 ────────────────────────────────────────────────────────────

        private ObservableCollection<AxisPoint> _pointTable = new();
        public ObservableCollection<AxisPoint> PointTable
        {
            get => _pointTable;
            set => SetProperty(ref _pointTable, value);
        }

        private AxisPoint? _selectedPoint;
        public AxisPoint? SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        // ── 命令 ─────────────────────────────────────────────────────────────────

        public DelegateCommand MoveAbsoluteCommand  { get; }
        public DelegateCommand MoveRelativeForwardCommand  { get; }
        public DelegateCommand MoveRelativeBackwardCommand { get; }
        public DelegateCommand HomeCommand          { get; }
        public DelegateCommand StopCommand          { get; }
        public DelegateCommand EnableCommand        { get; }
        public DelegateCommand DisableCommand       { get; }
        public DelegateCommand JogForwardCommand    { get; }
        public DelegateCommand JogBackwardCommand   { get; }

        public DelegateCommand AddPointCommand      { get; }
        public DelegateCommand DeletePointCommand   { get; }
        public DelegateCommand SavePointsCommand    { get; }
        public DelegateCommand GoToPointCommand     { get; }

        // ── 构造 ─────────────────────────────────────────────────────────────────

        public AxisDebugViewModel(IHardwareManagerService hwManager)
        {
            _hwManager = hwManager;

            // 运动命令
            MoveAbsoluteCommand         = new DelegateCommand(async () => await ExecuteMoveAbsoluteAsync(), CanExecuteMotion);
            MoveRelativeForwardCommand  = new DelegateCommand(async () => await ExecuteMoveRelativeAsync(+1), CanExecuteMotion);
            MoveRelativeBackwardCommand = new DelegateCommand(async () => await ExecuteMoveRelativeAsync(-1), CanExecuteMotion);
            HomeCommand    = new DelegateCommand(async () => await ExecuteHomeAsync(), CanExecuteMotion);
            StopCommand    = new DelegateCommand(ExecuteStop);
            EnableCommand  = new DelegateCommand(async () => await ExecuteEnableAsync(),  () => SelectedAxis != null);
            DisableCommand = new DelegateCommand(async () => await ExecuteDisableAsync(), () => SelectedAxis != null);
            JogForwardCommand  = new DelegateCommand(async () => await ExecuteJogAsync(true),  CanExecuteMotion);
            JogBackwardCommand = new DelegateCommand(async () => await ExecuteJogAsync(false), CanExecuteMotion);

            // 点表命令
            AddPointCommand    = new DelegateCommand(ExecuteAddPoint,    () => SelectedAxis != null);
            DeletePointCommand = new DelegateCommand(ExecuteDeletePoint, () => SelectedAxis != null && SelectedPoint != null);
            SavePointsCommand  = new DelegateCommand(ExecuteSavePoints,  () => SelectedAxis != null);
            GoToPointCommand   = new DelegateCommand(async () => await ExecuteGoToPointAsync(),
                () => SelectedAxis != null && SelectedPoint != null && !IsMoving);

            // 200ms 轮询刷新轴状态
            _statusTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _statusTimer.Tick += OnStatusTick;
        }

        // ── 导航生命周期 ───────────────────────────────────────────────────────

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            // 加载所有 IAxis 设备
            var axes = _hwManager.ActiveDevices.OfType<IAxis>().ToList();
            AvailableAxes = new ObservableCollection<IAxis>(axes);

            // 若导航参数中携带 DeviceId，则自动选中对应轴
            if (navigationContext.Parameters.TryGetValue("DeviceId", out string? deviceId))
                SelectedAxis = AvailableAxes.FirstOrDefault(a => a.DeviceId == deviceId);
            else if (AvailableAxes.Any())
                SelectedAxis = AvailableAxes[0];

            _statusTimer.Start();
        }

        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            _statusTimer.Stop();
            CancelCurrentMove();
        }

        public override void Destroy()
        {
            _statusTimer.Stop();
            _moveCts.Dispose();
        }

        // ── 状态轮询 ───────────────────────────────────────────────────────────

        private void OnStatusTick(object? sender, EventArgs e)
        {
            if (_selectedAxis == null) return;
            CurrentPosition  = _selectedAxis.CurrentPosition;
            IsMoving         = _selectedAxis.IsMoving;
            IsEnabled        = _selectedAxis.IsEnabled;
            IsConnected      = _selectedAxis.IsConnected;
            HasAlarm         = _selectedAxis.HasAlarm;
            IsPositiveLimit  = _selectedAxis.IsPositiveLimit;
            IsNegativeLimit  = _selectedAxis.IsNegativeLimit;
            RaiseCanExecuteChanged();
        }

        private void OnAxisChanged()
        {
            if (_selectedAxis == null)
            {
                PointTable = new ObservableCollection<AxisPoint>();
                return;
            }
            PointTable = new ObservableCollection<AxisPoint>(_selectedAxis.PointTable);
            StatusMessage = $"已选择: {_selectedAxis.DeviceName}";
            RaiseCanExecuteChanged();
        }

        private void RaiseCanExecuteChanged()
        {
            MoveAbsoluteCommand.RaiseCanExecuteChanged();
            MoveRelativeForwardCommand.RaiseCanExecuteChanged();
            MoveRelativeBackwardCommand.RaiseCanExecuteChanged();
            HomeCommand.RaiseCanExecuteChanged();
            EnableCommand.RaiseCanExecuteChanged();
            DisableCommand.RaiseCanExecuteChanged();
            JogForwardCommand.RaiseCanExecuteChanged();
            JogBackwardCommand.RaiseCanExecuteChanged();
            AddPointCommand.RaiseCanExecuteChanged();
            DeletePointCommand.RaiseCanExecuteChanged();
            SavePointsCommand.RaiseCanExecuteChanged();
            GoToPointCommand.RaiseCanExecuteChanged();
        }

        private bool CanExecuteMotion() => SelectedAxis != null && !IsMoving;

        // ── 运动命令实现 ───────────────────────────────────────────────────────

        private async Task ExecuteMoveAbsoluteAsync()
        {
            if (SelectedAxis == null) return;
            ResetMoveCts();
            StatusMessage = $"绝对定位 → {MoveTargetPosition:F2} mm";
            await SafeExecuteAsync(() =>
                SelectedAxis.MoveAbsoluteAsync(MoveTargetPosition, MoveVelocity, _moveCts.Token));
        }

        private async Task ExecuteMoveRelativeAsync(double direction)
        {
            if (SelectedAxis == null) return;
            ResetMoveCts();
            var distance = MoveRelativeDistance * direction;
            StatusMessage = $"相对移动 {(direction > 0 ? "+" : "")}{distance:F2} mm";
            await SafeExecuteAsync(() =>
                SelectedAxis.MoveRelativeAsync(distance, MoveVelocity, _moveCts.Token));
        }

        private async Task ExecuteHomeAsync()
        {
            if (SelectedAxis == null) return;
            ResetMoveCts();
            StatusMessage = "回原点中...";
            await SafeExecuteAsync(() => SelectedAxis.HomeAsync(_moveCts.Token));
        }

        private void ExecuteStop()
        {
            CancelCurrentMove();
            SelectedAxis?.StopAsync();
            StatusMessage = "急停";
        }

        private async Task ExecuteEnableAsync()
        {
            if (SelectedAxis == null) return;
            await SelectedAxis.EnableAsync();
            StatusMessage = "伺服使能 ON";
        }

        private async Task ExecuteDisableAsync()
        {
            if (SelectedAxis == null) return;
            await SelectedAxis.DisableAsync();
            StatusMessage = "伺服使能 OFF";
        }

        private async Task ExecuteJogAsync(bool isPositive)
        {
            if (SelectedAxis == null) return;
            await SelectedAxis.JogAsync(JogVelocity, isPositive);
        }

        // ── 点表命令实现 ───────────────────────────────────────────────────────

        private void ExecuteAddPoint()
        {
            if (SelectedAxis == null) return;

            // 用当前位置作默认值，排序号自动取最大+10
            int nextOrder = PointTable.Any() ? PointTable.Max(p => p.SortOrder) + 10 : 10;
            var newPoint = new AxisPoint
            {
                Name              = $"新点位_{DateTime.Now:HHmmss}",
                TargetPosition    = CurrentPosition,
                SuggestedVelocity = MoveVelocity,
                SortOrder         = nextOrder
            };
            PointTable.Add(newPoint);
            SelectedAxis.AddOrUpdatePoint(newPoint);
            SelectedPoint = newPoint;
            StatusMessage = $"已添加点位 '{newPoint.Name}'（未保存）";
        }

        private void ExecuteDeletePoint()
        {
            if (SelectedAxis == null || SelectedPoint == null) return;
            var name = SelectedPoint.Name;
            PointTable.Remove(SelectedPoint);
            SelectedAxis.DeletePoint(name);
            SelectedPoint = null;
            StatusMessage = $"已删除点位 '{name}'（未保存）";
        }

        private void ExecuteSavePoints()
        {
            if (SelectedAxis == null) return;

            // 将 DataGrid 中可能已内联编辑的点位同步回设备
            foreach (var p in PointTable)
                SelectedAxis.AddOrUpdatePoint(p);

            SelectedAxis.SavePointTable();
            StatusMessage = $"点表已保存（{PointTable.Count} 条）";
        }

        private async Task ExecuteGoToPointAsync()
        {
            if (SelectedAxis == null || SelectedPoint == null) return;
            ResetMoveCts();
            StatusMessage = $"移动到 '{SelectedPoint.Name}' ({SelectedPoint.TargetPosition:F2} mm)...";
            await SafeExecuteAsync(() =>
                SelectedAxis.MoveToPointAsync(SelectedPoint.Name, _moveCts.Token));
        }

        // ── 私有工具 ────────────────────────────────────────────────────────────

        private async Task SafeExecuteAsync(Func<Task<bool>> action)
        {
            try
            {
                var ok = await action();
                StatusMessage = ok ? "完成" : "执行失败";
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "操作已取消";
            }
            catch (Exception ex)
            {
                StatusMessage = $"错误: {ex.Message}";
            }
        }

        private void ResetMoveCts()
        {
            _moveCts.Cancel();
            _moveCts.Dispose();
            _moveCts = new CancellationTokenSource();
        }

        private void CancelCurrentMove()
        {
            _moveCts.Cancel();
        }
    }
}
