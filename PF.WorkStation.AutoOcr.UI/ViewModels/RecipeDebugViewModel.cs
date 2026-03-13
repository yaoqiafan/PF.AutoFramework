using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Recipe;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    public class RecipeDebugViewModel : PFDialogViewModelBase
    {
        private readonly IHardwareManagerService _hardwareManager;
        private readonly IRecipeService<OCRRecipeParam> _recipeService;

        private readonly IAxis? _axisX;
        private readonly IAxis? _axisY;
        private readonly IAxis? _axisZ;
        private readonly IBarcodeScan? _scanner1;
        private readonly IBarcodeScan? _scanner2;
        private readonly IIntelligentCamera? _camera;

        private IAxis _axis;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;
        private OCRRecipeParam _currentRecipe;
       

        public RecipeDebugViewModel(
            IHardwareManagerService hardwareManager,
            IRecipeService<OCRRecipeParam> recipeService)
        {
            Title = "程式调试";
            _hardwareManager = hardwareManager;
            _recipeService = recipeService;

            // 获取三个固定 OCR 轴
            _axisX = hardwareManager.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            _axisY = hardwareManager.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            _axisZ = hardwareManager.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;

            // 获取两个扫码枪
            _scanner1 = hardwareManager.GetDevice(E_ScanCode.工位1扫码枪.ToString()) as IBarcodeScan;
            _scanner2 = hardwareManager.GetDevice(E_ScanCode.工位2扫码枪.ToString()) as IBarcodeScan;

            // 相机：从 ActiveDevices 中取第一个 IIntelligentCamera
            _camera = hardwareManager.ActiveDevices.OfType<IIntelligentCamera>().FirstOrDefault();

            // 初始化轴列表（只含三个 OCR 轴，过滤 null）
            AxisList = new ObservableCollection<IAxis?>(
                new[] { _axisX, _axisY, _axisZ }.Where(a => a != null));

            // 默认运动参数
            AbsVelocity = 50.0;
            RelVelocity = 50.0;
            JogVelocity = 10.0;
            RelativeDistance = 10.0;

            InitializeCommands();

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            //_pollingTimer.Tick += OnPollingTimerTick;
            SelectedAxis = AxisList.First();
        }

        #region Dialog 生命周期

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("CurrentRepice"))
                _currentRecipe = parameters.GetValue<OCRRecipeParam>("CurrentRepice");

            UpdateRecipePositionDisplay();
        }

        public override void OnDialogClosed()
        {
            _pollingTimer.Stop();
            _cts?.Cancel();
        }

        #endregion

        #region 实时状态属性

        private double _currentPosition;
        public double CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isMoving;
        public bool IsMoving { get => _isMoving; set => SetProperty(ref _isMoving, value); }

        private bool _isEnabled;
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private bool _isPositiveLimit;
        public bool IsPositiveLimit { get => _isPositiveLimit; set => SetProperty(ref _isPositiveLimit, value); }

        private bool _isNegativeLimit;
        public bool IsNegativeLimit { get => _isNegativeLimit; set => SetProperty(ref _isNegativeLimit, value); }

        private bool _isAlarm;
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        #endregion

        #region 运动参数属性

        private double _targetPosition;
        public double TargetPosition { get => _targetPosition; set => SetProperty(ref _targetPosition, value); }

        private double _absVelocity;
        public double AbsVelocity { get => _absVelocity; set => SetProperty(ref _absVelocity, value); }

        private double _relativeDistance;
        public double RelativeDistance { get => _relativeDistance; set => SetProperty(ref _relativeDistance, value); }

        private double _relVelocity;
        public double RelVelocity { get => _relVelocity; set => SetProperty(ref _relVelocity, value); }

        private double _jogVelocity;
        public double JogVelocity { get => _jogVelocity; set => SetProperty(ref _jogVelocity, value); }

        #endregion

        #region 轴/工位选择属性

        public ObservableCollection<IAxis?> AxisList { get; }

        private IAxis _selectedAxis;
        public IAxis SelectedAxis
        {
            get => _selectedAxis;
            set
            {
                if (SetProperty(ref _selectedAxis, value))
                    OnSelectedAxisChanged();
            }
        }

        

        private E_WorkSpace _currentStation = E_WorkSpace.工位1;
        public E_WorkSpace CurrentStation { get => _currentStation; set => SetProperty(ref _currentStation, value); }

        private string _currentRecipePosition = "(X=0.000, Y=0.000, Z=0.000)";
        public string CurrentRecipePosition { get => _currentRecipePosition; set => SetProperty(ref _currentRecipePosition, value); }

        private double _recipeAxisPosition;
        public double RecipeAxisPosition { get => _recipeAxisPosition; set => SetProperty(ref _recipeAxisPosition, value); }

        #endregion

        #region 命令定义

        public DelegateCommand SwitchRecipeCommand { get; private set; }
        public DelegateCommand TriggerScanCommand { get; private set; }
        public DelegateCommand TriggerOcrCommand { get; private set; }
        public DelegateCommand OpenScannerSoftwareCommand { get; private set; }
        public DelegateCommand OpenCameraSoftwareCommand { get; private set; }

        public DelegateCommand ConnectCommand { get; private set; }
        public DelegateCommand DisconnectCommand { get; private set; }
        public DelegateCommand EnableCommand { get; private set; }
        public DelegateCommand DisableCommand { get; private set; }
        public DelegateCommand HomeCommand { get; private set; }
        public DelegateCommand StopCommand { get; private set; }
        public DelegateCommand ResetCommand { get; private set; }

        public DelegateCommand MoveAbsoluteCommand { get; private set; }
        public DelegateCommand MoveRelativeCommand { get; private set; }
        public DelegateCommand JogPositiveCommand { get; private set; }
        public DelegateCommand JogNegativeCommand { get; private set; }

        public DelegateCommand SyncAdjustCommand { get; private set; }
        public DelegateCommand MoveToRecipePositionCommand { get; private set; }

        private void InitializeCommands()
        {
            // 顶部功能命令
            SwitchRecipeCommand = new DelegateCommand(ExecuteSwitchRecipe);
            TriggerScanCommand = new DelegateCommand(ExecuteTriggerScan);
            TriggerOcrCommand = new DelegateCommand(ExecuteTriggerOcr);
            OpenScannerSoftwareCommand = new DelegateCommand(ExecuteOpenScannerSoftware);
            OpenCameraSoftwareCommand = new DelegateCommand(ExecuteOpenCameraSoftware);

            // 底部对话框按钮
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
            ConfirmCommand = new DelegateCommand(ExecuteConfirm);

            // 硬件控制命令
            ConnectCommand = new DelegateCommand(async () =>
            {
                if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None);
            });
            DisconnectCommand = new DelegateCommand(async () =>
            {
                if (_baseDevice != null) await _baseDevice.DisconnectAsync();
            });
            EnableCommand = new DelegateCommand(async () =>
            {
                if (_axis != null) await _axis.EnableAsync();
            });
            DisableCommand = new DelegateCommand(async () =>
            {
                if (_axis != null) await _axis.DisableAsync();
            });
            HomeCommand = new DelegateCommand(async () =>
            {
                if (_axis != null) await _axis.HomeAsync(CancellationToken.None);
            });
            StopCommand = new DelegateCommand(async () =>
            {
                _cts?.Cancel();
                if (_axis != null) await _axis.StopAsync();
            });
            ResetCommand = new DelegateCommand(async () =>
            {
                if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None);
            });

            // 运动控制命令
            MoveAbsoluteCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveAbsoluteAsync(TargetPosition, AbsVelocity, AbsVelocity * 5, AbsVelocity * 5, 0.08, _cts.Token);
            });
            MoveRelativeCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveRelativeAsync(RelativeDistance, RelVelocity, RelVelocity * 5, RelVelocity * 5, 0.08, _cts.Token);
            });
            JogPositiveCommand = new DelegateCommand(async () =>
            {
                if (_axis != null) await _axis.JogAsync(JogVelocity, true, JogVelocity * 5, JogVelocity * 5);
            });
            JogNegativeCommand = new DelegateCommand(async () =>
            {
                if (_axis != null) await _axis.JogAsync(JogVelocity, false, JogVelocity * 5, JogVelocity * 5);
            });

            // 程式点位命令
            SyncAdjustCommand = new DelegateCommand(ExecuteSyncAdjust);
            MoveToRecipePositionCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveAbsoluteAsync(RecipeAxisPosition, AbsVelocity, AbsVelocity * 5, AbsVelocity * 5, 0.08, _cts.Token);
            });
        }

        #endregion

        #region 命令实现

        private async void ExecuteSwitchRecipe()
        {
            if (_currentRecipe == null) return;
            try
            {
                var recipeManger = _recipeService as IRecipeManger<OCRRecipeParam>;
                switch (CurrentStation)
                {
                    case E_WorkSpace.工位1:
                        await recipeManger?.ChangedStationRecipe(E_WorkSpace.工位2.ToString(), _currentRecipe);
                       CurrentStation = E_WorkSpace.工位2;
                        break;
                    case E_WorkSpace.工位2:
                        await recipeManger?.ChangedStationRecipe(E_WorkSpace.工位1.ToString(), _currentRecipe);
                        CurrentStation = E_WorkSpace.工位1;
                        break;
                    default:
                        break;
                }
                OnSelectedStationChanged();
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"切换程式失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteTriggerScan()
        {
            var scanner = CurrentStation == 0 ? _scanner1 : _scanner2;
            if (scanner == null)
            {
                MessageService.ShowMessage($"工位{CurrentStation + 1}扫码枪未连接", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var result = await scanner.Tigger();
                MessageService.ShowMessage($"扫码结果: {result}", "触发扫码", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"触发扫码失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void ExecuteTriggerOcr()
        {
            if (_camera == null)
            {
                MessageService.ShowMessage("OCR相机未连接", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                var result = await _camera.Tigger();
                MessageService.ShowMessage($"OCR结果: {result}", "触发OCR", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"触发OCR失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExecuteOpenScannerSoftware()
        {
            // TODO: 打开扫码枪管理软件
        }

        private void ExecuteOpenCameraSoftware()
        {
            // TODO: 打开相机软件
        }

        private void ExecuteConfirm()
        {





            RequestClose.Invoke(ButtonResult.OK);
        }

        private void ExecuteSyncAdjust()
        {
            SyncStation2ByStation1();
        }

        /// <summary>
        /// 根据工位1参数通过算法推导工位2参数。
        /// TODO: 算法待实现。
        /// </summary>
        private void SyncStation2ByStation1()
        {
            // TODO: 实现工位1→工位2同步调整算法
            MessageService.ShowMessage("同步调整算法待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        #region 轴/工位切换

        private void OnSelectedAxisChanged()
        {
            _pollingTimer.Stop();
            _axis = SelectedAxis;
            _baseDevice = _axis as BaseDevice;
            UpdateRecipePositionDisplay();
            if (_axis != null) _pollingTimer.Start();
        }

        private void OnSelectedStationChanged()
        {
            UpdateRecipePositionDisplay();
        }

        /// <summary>
        /// 根据当前工位和选中轴更新程式点位显示。
        /// </summary>
        private void UpdateRecipePositionDisplay()
        {
            if (_currentRecipe == null) return;

            double x=0, y = 0, z = 0;
            if (CurrentStation == 0)
            {
                x = _currentRecipe._1PosX;
                y = _currentRecipe._1PosY;
                z = _currentRecipe._1PosZ;
            }
            else
            {
                x = _currentRecipe._2PosX;
                y = _currentRecipe._2PosY;
                z = _currentRecipe._2PosZ;
            }

            CurrentRecipePosition = $"(X={x:F3}, Y={y:F3}, Z={z:F3})";

            // 根据当前选中的轴确定 RecipeAxisPosition
            if (_axis == null) return;
            if (_axis == _axisX) RecipeAxisPosition = x;
            else if (_axis == _axisY) RecipeAxisPosition = y;
            else if (_axis == _axisZ) RecipeAxisPosition = z;
        }

        #endregion

        #region 定时器轮询

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_axis == null) return;
            var io = _axis.AxisIOStatus;
            CurrentPosition = _axis.CurrentPosition ?? 0;
            IsMoving = io?.Moving ?? false;
            IsEnabled = io?.SVO ?? false;
            IsPositiveLimit = io?.PEL ?? false;
            IsNegativeLimit = io?.MEL ?? false;
            IsAlarm = _axis.HasAlarm;
        }

        private void RefreshCancellationToken()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
        }

        #endregion
    }
}
