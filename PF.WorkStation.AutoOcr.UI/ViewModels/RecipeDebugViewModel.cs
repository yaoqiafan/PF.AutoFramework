using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.LightController;
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
    /// <summary>
    /// RecipeDebugViewModel
    /// </summary>
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


        private readonly ILightController? _lightconnter;

        private IAxis _axis;
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;
        private OCRRecipeParam _currentRecipe;

        private IParamService _paramservice;
        /// <summary>
        /// RecipeDebugViewModel 构造函数
        /// </summary>


        public RecipeDebugViewModel(
            IHardwareManagerService hardwareManager,
            IRecipeService<OCRRecipeParam> recipeService, IParamService paramService)
        {
            Title = "程式调试";
            _hardwareManager = hardwareManager;
            _recipeService = recipeService;
            _paramservice = paramService;

            // 获取三个固定 OCR 轴
            _axisX = hardwareManager.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            _axisY = hardwareManager.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            _axisZ = hardwareManager.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;

            // 获取两个扫码枪
            _scanner1 = hardwareManager.GetDevice(E_ScanCode.工位1扫码枪.ToString()) as IBarcodeScan;
            _scanner2 = hardwareManager.GetDevice(E_ScanCode.工位2扫码枪.ToString()) as IBarcodeScan;

            // 相机：从 ActiveDevices 中取第一个 IIntelligentCamera
            _camera = hardwareManager.ActiveDevices.OfType<IIntelligentCamera>().FirstOrDefault();


            _lightconnter = hardwareManager.ActiveDevices.OfType<ILightController>().FirstOrDefault();

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
            _pollingTimer.Tick += OnPollingTimerTick;
            SelectedAxis = AxisList.First();
        }

        #region Dialog 生命周期
        /// <summary>
        /// OnDialogOpened
        /// </summary>

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("CurrentRepice"))
                _currentRecipe = parameters.GetValue<OCRRecipeParam>("CurrentRepice");

            // 从配方初始化光源值
            InfraredLightValue = _currentRecipe?.LightChanel1Value ?? 0;
            WhiteLightValue = _currentRecipe?.LightChanel2Value ?? 0;

            UpdateRecipePositionDisplay();

            if (_axis != null) _pollingTimer.Start();
        }
        /// <summary>
        /// OnDialogClosed
        /// </summary>

        public override void OnDialogClosed()
        {
            _pollingTimer.Stop();
            _cts?.Cancel();
        }

        #endregion

        #region 实时状态属性

        private double _currentPosition;
        /// <summary>
        /// 获取或设置 CurrentPosition
        /// </summary>
        public double CurrentPosition { get => _currentPosition; set => SetProperty(ref _currentPosition, value); }

        private bool _isMoving;
        /// <summary>
        /// 获取或设置 IsMoving
        /// </summary>
        public bool IsMoving { get => _isMoving; set => SetProperty(ref _isMoving, value); }

        private bool _isEnabled;
        /// <summary>
        /// 获取或设置 IsEnabled
        /// </summary>
        public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

        private bool _isPositiveLimit;
        /// <summary>
        /// 获取或设置 IsPositiveLimit
        /// </summary>
        public bool IsPositiveLimit { get => _isPositiveLimit; set => SetProperty(ref _isPositiveLimit, value); }

        private bool _isNegativeLimit;
        /// <summary>
        /// 获取或设置 IsNegativeLimit
        /// </summary>
        public bool IsNegativeLimit { get => _isNegativeLimit; set => SetProperty(ref _isNegativeLimit, value); }

        private bool _isAlarm;
        /// <summary>
        /// 获取或设置 IsAlarm
        /// </summary>
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        #endregion

        #region 运动参数属性

        private double _targetPosition;
        /// <summary>
        /// 获取或设置 TargetPosition
        /// </summary>
        public double TargetPosition { get => _targetPosition; set => SetProperty(ref _targetPosition, value); }

        private double _absVelocity;
        /// <summary>
        /// 获取或设置 AbsVelocity
        /// </summary>
        public double AbsVelocity { get => _absVelocity; set => SetProperty(ref _absVelocity, value); }

        private double _relativeDistance;
        /// <summary>
        /// 获取或设置 RelativeDistance
        /// </summary>
        public double RelativeDistance { get => _relativeDistance; set => SetProperty(ref _relativeDistance, value); }

        private double _relVelocity;
        /// <summary>
        /// 获取或设置 RelVelocity
        /// </summary>
        public double RelVelocity { get => _relVelocity; set => SetProperty(ref _relVelocity, value); }

        private double _jogVelocity;
        /// <summary>
        /// 获取或设置 JogVelocity
        /// </summary>
        public double JogVelocity { get => _jogVelocity; set => SetProperty(ref _jogVelocity, value); }

        #endregion

        #region 轴/工位选择属性
        /// <summary>
        /// 获取或设置 AxisList
        /// </summary>

        public ObservableCollection<IAxis?> AxisList { get; }

        private IAxis _selectedAxis;
        /// <summary>
        /// 成员
        /// </summary>
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
        /// <summary>
        /// 获取或设置 CurrentStation
        /// </summary>
        public E_WorkSpace CurrentStation { get => _currentStation; set => SetProperty(ref _currentStation, value); }

        private string _currentRecipePosition = "(X=0.000, Y=0.000, Z=0.000)";
        /// <summary>
        /// 获取或设置 CurrentRecipePosition
        /// </summary>
        public string CurrentRecipePosition { get => _currentRecipePosition; set => SetProperty(ref _currentRecipePosition, value); }

        private double _recipeAxisPosition;
        /// <summary>
        /// 获取或设置 RecipeAxisPosition
        /// </summary>
        public double RecipeAxisPosition { get => _recipeAxisPosition; set => SetProperty(ref _recipeAxisPosition, value); }

        private string _scanResult = "等待扫码...";
        /// <summary>
        /// 获取或设置 ScanResult
        /// </summary>
        public string ScanResult { get => _scanResult; set => SetProperty(ref _scanResult, value); }

        private string _ocrResult = "等待OCR...";
        /// <summary>
        /// 获取或设置 OcrResult
        /// </summary>
        public string OcrResult { get => _ocrResult; set => SetProperty(ref _ocrResult, value); }

        #endregion

        #region 光源参数属性

        private double _infraredLightValue;
        /// <summary>
        /// 成员
        /// </summary>

        public double InfraredLightValue
        {
            get => _infraredLightValue;
            set
            {
                if (value != _infraredLightValue)
                {
                    SetProperty(ref _infraredLightValue, (int)value);
                    UpdateLihtValue(1, (int)value);
                    _currentRecipe.LightChanel1Value = (int)value;
                }
            }
        }

        private double _whiteLightValue;
        /// <summary>
        /// 成员
        /// </summary>

        public double WhiteLightValue
        {
            get => _whiteLightValue;
            set
            {
                if (value != _whiteLightValue)
                {
                    SetProperty(ref _whiteLightValue, (int)value);
                    UpdateLihtValue(2, (int)value);
                    _currentRecipe.LightChanel2Value = (int)value;
                }
            }
        }



        private void UpdateLihtValue(int chanel, int vale)
        {
            _lightconnter?.SetLightValue(chanel, vale);
        }

        #endregion 光源参数属性

        #region 命令定义
        /// <summary>
        /// SwitchRecipe 命令
        /// </summary>

        public DelegateCommand SwitchRecipeCommand { get; private set; }
        /// <summary>
        /// TriggerScan 命令
        /// </summary>
        public DelegateCommand TriggerScanCommand { get; private set; }
        /// <summary>
        /// TriggerOcr 命令
        /// </summary>
        public DelegateCommand TriggerOcrCommand { get; private set; }
        /// <summary>
        /// OpenScannerSoftware 命令
        /// </summary>
        public DelegateCommand OpenScannerSoftwareCommand { get; private set; }
        /// <summary>
        /// OpenCameraSoftware 命令
        /// </summary>
        public DelegateCommand OpenCameraSoftwareCommand { get; private set; }
        /// <summary>
        /// Connect 命令
        /// </summary>

        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>
        /// Disconnect 命令
        /// </summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>
        /// Enable 命令
        /// </summary>
        public DelegateCommand EnableCommand { get; private set; }
        /// <summary>
        /// Disable 命令
        /// </summary>
        public DelegateCommand DisableCommand { get; private set; }
        /// <summary>
        /// Home 命令
        /// </summary>
        public DelegateCommand HomeCommand { get; private set; }
        /// <summary>
        /// Stop 命令
        /// </summary>
        public DelegateCommand StopCommand { get; private set; }
        /// <summary>
        /// Reset 命令
        /// </summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>
        /// MoveAbsolute 命令
        /// </summary>

        public DelegateCommand MoveAbsoluteCommand { get; private set; }
        /// <summary>
        /// MoveRelative 命令
        /// </summary>
        public DelegateCommand MoveRelativeCommand { get; private set; }
        /// <summary>
        /// JogPositive 命令
        /// </summary>
        public DelegateCommand JogPositiveCommand { get; private set; }
        /// <summary>
        /// JogNegative 命令
        /// </summary>
        public DelegateCommand JogNegativeCommand { get; private set; }
        /// <summary>
        /// 轴停止命令
        /// </summary>
        public DelegateCommand AxisStopCommand { get; private set; }
        /// <summary>
        /// SyncAdjust 命令
        /// </summary>

        public DelegateCommand SyncAdjustCommand { get; private set; }
        /// <summary>
        /// MoveToRecipePosition 命令
        /// </summary>
        public DelegateCommand MoveToRecipePositionCommand { get; private set; }
        /// <summary>
        /// GetCurrentPosition 命令
        /// </summary>
        public DelegateCommand GetCurrentPositionCommand { get; private set; }
        /// <summary>
        /// UpdateRecipePosition 命令
        /// </summary>
        public DelegateCommand UpdateRecipePositionCommand { get; private set; }

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
            AxisStopCommand = new DelegateCommand(async () =>
            {
                if (_axis != null) await _axis.StopAsync();
            });

            // 程式点位命令
            SyncAdjustCommand = new DelegateCommand(ExecuteSyncAdjust);
            MoveToRecipePositionCommand = new DelegateCommand(async () =>
            {
                if (_axis == null) return;
                RefreshCancellationToken();
                await _axis.MoveAbsoluteAsync(RecipeAxisPosition, AbsVelocity, AbsVelocity * 5, AbsVelocity * 5, 0.08, _cts.Token);
            });
            GetCurrentPositionCommand = new DelegateCommand(() =>
            {
                if (_axis == null) return;
                RecipeAxisPosition = (int)(_axis.CurrentPosition ?? 0);
            });
            UpdateRecipePositionCommand = new DelegateCommand(() =>
            {
                if (_currentRecipe == null || _axis == null) return;
                if (_axis == _axisX)
                {
                    if (CurrentStation == E_WorkSpace.工位1)
                    {
                        _currentRecipe._1PosX = RecipeAxisPosition;
                        if (_currentRecipe .WafeSize == E_WafeSize._8寸 )
                        {
                            _currentRecipe._2PosX = RecipeAxisPosition + _paramservice.GetParamAsync<double>(E_Params.OCRStationDistance_8 .ToString ()).GetAwaiter().GetResult();
                        }
                        else
                        {
                            _currentRecipe._2PosX = RecipeAxisPosition + _paramservice.GetParamAsync<double>(E_Params.OCRStationDistance_12.ToString()).GetAwaiter().GetResult();
                        }
                        
                    }
                    else
                    {
                        _currentRecipe._2PosX = RecipeAxisPosition;
                        if (_currentRecipe.WafeSize == E_WafeSize._8寸)
                        {
                            _currentRecipe._2PosX = RecipeAxisPosition - _paramservice.GetParamAsync<double>(E_Params.OCRStationDistance_8.ToString()).GetAwaiter().GetResult();
                        }
                        else
                        {
                            _currentRecipe._2PosX = RecipeAxisPosition - _paramservice.GetParamAsync<double>(E_Params.OCRStationDistance_12.ToString()).GetAwaiter().GetResult();
                        }
                    }
                }
                else if (_axis == _axisY)
                {
                    if (CurrentStation == E_WorkSpace.工位1) _currentRecipe._1PosY = RecipeAxisPosition;
                    else _currentRecipe._2PosY = RecipeAxisPosition;
                }
                else if (_axis == _axisZ)
                {
                    if (CurrentStation == E_WorkSpace.工位1) _currentRecipe._1PosZ = RecipeAxisPosition;
                    else _currentRecipe._2PosZ = RecipeAxisPosition;
                }
                UpdateRecipePositionDisplay();
            });
        }

        #endregion

        #region 命令实现

        private async void ExecuteSwitchRecipe()
        {
            if (_currentRecipe == null) return;
            try
            {
                CurrentStation = CurrentStation == E_WorkSpace.工位1
                    ? E_WorkSpace.工位2
                    : E_WorkSpace.工位1;
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
                ScanResult = $"工位{CurrentStation + 1}扫码枪未连接";
                return;
            }
            try
            {
                ScanResult = "扫码中...";
                var result = await scanner.Tigger();
                ScanResult = result ?? "(空)";
            }
            catch (Exception ex)
            {
                ScanResult = $"扫码失败: {ex.Message}";
            }
        }

        private async void ExecuteTriggerOcr()
        {
            if (_camera == null)
            {
                OcrResult = "OCR相机未连接";
                return;
            }
            try
            {
                OcrResult = "OCR识别中...";
                var result = await _camera.Tigger();
                OcrResult = result ?? "(空)";
            }
            catch (Exception ex)
            {
                OcrResult = $"OCR失败: {ex.Message}";
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


            var param = new DialogParameters() { { "CallBackRecipe", _currentRecipe } };


            RequestClose.Invoke(param, ButtonResult.OK);
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

            double x = 0, y = 0, z = 0;
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

            CurrentRecipePosition = $"(X={x}, Y={y}, Z={z})";

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
            var axisio = _axis.AxisIOStatus;
            //IsConnected = _axis.IsConnected;
            CurrentPosition = (int)(_axis.CurrentPosition ?? 0);
            IsMoving = axisio?.Moving ?? false;
            IsEnabled = axisio?.SVO ?? false;
            IsPositiveLimit = axisio?.PEL ?? false;
            IsNegativeLimit = axisio?.MEL ?? false;
            //IsORG = axisio?.ORG ?? false;
            //IsHoming = axisio?.Homing ?? false;
            IsAlarm = axisio?.ALM ?? false;
        }

        private void RefreshCancellationToken()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
        }

        #endregion
    }
}
