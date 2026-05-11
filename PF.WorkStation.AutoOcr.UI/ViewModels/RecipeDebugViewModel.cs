using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Recipe;
using PF.Core.Models;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Commands;
using Prism.Ioc;
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
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
        private readonly IAxis? _axisStopperX1;
        private readonly IAxis? _axisStopperX2;
        private readonly IBarcodeScan? _scanner1;
        private readonly IBarcodeScan? _scanner2;
        private readonly IIntelligentCamera? _camera;
        private readonly ILightController? _lightconnter;

        private DispatcherTimer _pollingTimer;
        private CancellationTokenSource _cts;
        private OCRRecipeParam _currentRecipe;

        private IParamService _paramservice;

        private readonly WS1MaterialPullingModule? _ws1Module;
        private readonly WS2MaterialPullingModule? _ws2Module;
        private readonly WS1FeedingModel? _ws1FeedingModule;
        private readonly WS2FeedingModel? _ws2FeedingModule;

        private bool _isBusy;
        /// <summary>
        /// 获取或设置 IsBusy
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    TestPullOutCommand?.RaiseCanExecuteChanged();
                    TestPushBackCommand?.RaiseCanExecuteChanged();
                    TestFullFlowCommand?.RaiseCanExecuteChanged();
                    MoveToRecipeXYZCommand?.RaiseCanExecuteChanged();
                    RaiseStepCanExecuteChanged();
                }
            }
        }

        public RecipeDebugViewModel(
            IHardwareManagerService hardwareManager,
            IRecipeService<OCRRecipeParam> recipeService, IParamService paramService,
            IContainerProvider containerProvider)
        {
            Title = "程式调试";
            _hardwareManager = hardwareManager;
            _recipeService = recipeService;
            _paramservice = paramService;

            _ws1Module = containerProvider.Resolve<IMechanism>(nameof(WS1MaterialPullingModule)) as WS1MaterialPullingModule;
            _ws2Module = containerProvider.Resolve<IMechanism>(nameof(WS2MaterialPullingModule)) as WS2MaterialPullingModule;
            _ws1FeedingModule = containerProvider.Resolve<IMechanism>(nameof(WS1FeedingModel)) as WS1FeedingModel;
            _ws2FeedingModule = containerProvider.Resolve<IMechanism>(nameof(WS2FeedingModel)) as WS2FeedingModel;

            _axisX = hardwareManager.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            _axisY = hardwareManager.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            _axisZ = hardwareManager.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;
            _axisStopperX1 = hardwareManager.GetDevice(E_AxisName.工位1挡料X轴.ToString()) as IAxis;
            _axisStopperX2 = hardwareManager.GetDevice(E_AxisName.工位2挡料X轴.ToString()) as IAxis;

            _scanner1 = hardwareManager.GetDevice(E_ScanCode.工位1扫码枪.ToString()) as IBarcodeScan;
            _scanner2 = hardwareManager.GetDevice(E_ScanCode.工位2扫码枪.ToString()) as IBarcodeScan;

            _camera = hardwareManager.ActiveDevices.OfType<IIntelligentCamera>().FirstOrDefault();
            _lightconnter = hardwareManager.ActiveDevices.OfType<ILightController>().FirstOrDefault();

            InitializeCommands();

            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region Dialog 生命周期

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("CurrentRepice"))
                _currentRecipe = parameters.GetValue<OCRRecipeParam>("CurrentRepice");

            RaisePropertyChanged(nameof(RecipeWaferSizeText));
            InfraredLightValue = _currentRecipe?.LightChanel1Value ?? 0;
            WhiteLightValue = _currentRecipe?.LightChanel2Value ?? 0;

            UpdateRecipePositionDisplay();
            _pollingTimer.Start();
        }

        public override void OnDialogClosed()
        {
            _pollingTimer.Stop();
            _cts?.Cancel();
        }

        #endregion

        #region 三轴独立状态属性

        // -- 视觉X轴 --
        private double _xAxisCurrentPosition;
        public double XAxisCurrentPosition { get => _xAxisCurrentPosition; set => SetProperty(ref _xAxisCurrentPosition, value); }
        private bool _xAxisIsEnabled;
        public bool XAxisIsEnabled { get => _xAxisIsEnabled; set => SetProperty(ref _xAxisIsEnabled, value); }
        private bool _xAxisIsMoving;
        public bool XAxisIsMoving { get => _xAxisIsMoving; set => SetProperty(ref _xAxisIsMoving, value); }
        private bool _xAxisIsAlarm;
        public bool XAxisIsAlarm { get => _xAxisIsAlarm; set => SetProperty(ref _xAxisIsAlarm, value); }
        private bool _xAxisIsPosLimit;
        public bool XAxisIsPosLimit { get => _xAxisIsPosLimit; set => SetProperty(ref _xAxisIsPosLimit, value); }
        private bool _xAxisIsNegLimit;
        public bool XAxisIsNegLimit { get => _xAxisIsNegLimit; set => SetProperty(ref _xAxisIsNegLimit, value); }
        private double _xAxisJogVelocity = 10.0;
        public double XAxisJogVelocity { get => _xAxisJogVelocity; set => SetProperty(ref _xAxisJogVelocity, value); }

        // -- 视觉Y轴 --
        private double _yAxisCurrentPosition;
        public double YAxisCurrentPosition { get => _yAxisCurrentPosition; set => SetProperty(ref _yAxisCurrentPosition, value); }
        private bool _yAxisIsEnabled;
        public bool YAxisIsEnabled { get => _yAxisIsEnabled; set => SetProperty(ref _yAxisIsEnabled, value); }
        private bool _yAxisIsMoving;
        public bool YAxisIsMoving { get => _yAxisIsMoving; set => SetProperty(ref _yAxisIsMoving, value); }
        private bool _yAxisIsAlarm;
        public bool YAxisIsAlarm { get => _yAxisIsAlarm; set => SetProperty(ref _yAxisIsAlarm, value); }
        private bool _yAxisIsPosLimit;
        public bool YAxisIsPosLimit { get => _yAxisIsPosLimit; set => SetProperty(ref _yAxisIsPosLimit, value); }
        private bool _yAxisIsNegLimit;
        public bool YAxisIsNegLimit { get => _yAxisIsNegLimit; set => SetProperty(ref _yAxisIsNegLimit, value); }
        private double _yAxisJogVelocity = 10.0;
        public double YAxisJogVelocity { get => _yAxisJogVelocity; set => SetProperty(ref _yAxisJogVelocity, value); }

        // -- 视觉Z轴 --
        private double _zAxisCurrentPosition;
        public double ZAxisCurrentPosition { get => _zAxisCurrentPosition; set => SetProperty(ref _zAxisCurrentPosition, value); }
        private bool _zAxisIsEnabled;
        public bool ZAxisIsEnabled { get => _zAxisIsEnabled; set => SetProperty(ref _zAxisIsEnabled, value); }
        private bool _zAxisIsMoving;
        public bool ZAxisIsMoving { get => _zAxisIsMoving; set => SetProperty(ref _zAxisIsMoving, value); }
        private bool _zAxisIsAlarm;
        public bool ZAxisIsAlarm { get => _zAxisIsAlarm; set => SetProperty(ref _zAxisIsAlarm, value); }
        private bool _zAxisIsPosLimit;
        public bool ZAxisIsPosLimit { get => _zAxisIsPosLimit; set => SetProperty(ref _zAxisIsPosLimit, value); }
        private bool _zAxisIsNegLimit;
        public bool ZAxisIsNegLimit { get => _zAxisIsNegLimit; set => SetProperty(ref _zAxisIsNegLimit, value); }
        private double _zAxisJogVelocity = 10.0;
        public double ZAxisJogVelocity { get => _zAxisJogVelocity; set => SetProperty(ref _zAxisJogVelocity, value); }

        #endregion

        #region 工位选择属性

        private E_WorkSpace _currentStation = E_WorkSpace.工位1;
        public E_WorkSpace CurrentStation { get => _currentStation; set => SetProperty(ref _currentStation, value); }

        private string _currentRecipePosition = "(X=0.000, Y=0.000, Z=0.000)";
        public string CurrentRecipePosition { get => _currentRecipePosition; set => SetProperty(ref _currentRecipePosition, value); }

        private double _recipePosX;
        public double RecipePosX { get => _recipePosX; set => SetProperty(ref _recipePosX, value); }
        private double _recipePosY;
        public double RecipePosY { get => _recipePosY; set => SetProperty(ref _recipePosY, value); }
        private double _recipePosZ;
        public double RecipePosZ { get => _recipePosZ; set => SetProperty(ref _recipePosZ, value); }

        private string _scanResult = "等待扫码...";
        public string ScanResult { get => _scanResult; set => SetProperty(ref _scanResult, value); }

        private string _ocrResult = "等待OCR...";
        public string OcrResult { get => _ocrResult; set => SetProperty(ref _ocrResult, value); }

        #endregion

        #region 光源参数属性

        private double _infraredLightValue;
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

        #endregion

        #region 模组调试步骤属性

        private int _currentDebugStep;
        public int CurrentDebugStep
        {
            get => _currentDebugStep;
            private set
            {
                if (SetProperty(ref _currentDebugStep, value))
                {
                    RaisePropertyChanged(nameof(Step1Brush));
                    RaisePropertyChanged(nameof(Step2Brush));
                    RaisePropertyChanged(nameof(Step3Brush));
                    RaisePropertyChanged(nameof(Step4Brush));
                    RaisePropertyChanged(nameof(Step5Brush));
                    RaisePropertyChanged(nameof(Step6Brush));
                    RaiseStepCanExecuteChanged();
                }
            }
        }

        public string RecipeWaferSizeText => _currentRecipe != null ? _currentRecipe.WafeSize.ToString() : "—";

        private int _targetLayerNumber = 1;
        public int TargetLayerNumber
        {
            get => _targetLayerNumber;
            set => SetProperty(ref _targetLayerNumber, value < 1 ? 1 : value);
        }

        private string _debugStepMessage = "请从第1步开始，按顺序执行调试流程";
        public string DebugStepMessage
        {
            get => _debugStepMessage;
            set => SetProperty(ref _debugStepMessage, value);
        }

        private bool _step0Done;
        private bool _step0Busy;
        public Brush Step0Brush => _step0Done ? Brushes.MediumSeaGreen : Brushes.CornflowerBlue;
        public Brush Step1Brush => CurrentDebugStep >= 1 ? Brushes.MediumSeaGreen : Brushes.CornflowerBlue;
        public Brush Step2Brush => CurrentDebugStep >= 2 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 1 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        public Brush Step3Brush => CurrentDebugStep >= 3 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 2 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        public Brush Step4Brush => CurrentDebugStep >= 4 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 3 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        public Brush Step5Brush => CurrentDebugStep >= 5 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 4 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        public Brush Step6Brush => CurrentDebugStep >= 6 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 5 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);

        #endregion

        #region 迷你模式属性

        private int _miniCardIndex;
        public int MiniCardIndex
        {
            get => _miniCardIndex;
            set
            {
                if (SetProperty(ref _miniCardIndex, ((value % 7) + 7) % 7))
                {
                    RaisePropertyChanged(nameof(MiniCardTitle));
                    RaisePropertyChanged(nameof(MiniCardPageText));
                    RaisePropertyChanged(nameof(IsMiniAxisPage));
                    RaisePropertyChanged(nameof(MiniAxisPosition));
                    RaisePropertyChanged(nameof(MiniAxisIsEnabled));
                    RaisePropertyChanged(nameof(MiniAxisIsMoving));
                    RaisePropertyChanged(nameof(MiniAxisIsAlarm));
                    RaisePropertyChanged(nameof(MiniJogVelocity));
                }
            }
        }

        public string MiniCardTitle => MiniCardIndex switch
        {
            0 => "调试步骤", 1 => "视觉 X 轴", 2 => "视觉 Y 轴",
            3 => "视觉 Z 轴", 4 => "扫码 / OCR", 5 => "配方点位",
            _ => "光源控制"
        };
        public string MiniCardPageText => $"{MiniCardIndex + 1} / 7";
        public bool IsMiniAxisPage => MiniCardIndex >= 1 && MiniCardIndex <= 3;

        public double MiniAxisPosition => MiniCardIndex switch { 1 => XAxisCurrentPosition, 2 => YAxisCurrentPosition, _ => ZAxisCurrentPosition };
        public bool MiniAxisIsEnabled => MiniCardIndex switch { 1 => XAxisIsEnabled, 2 => YAxisIsEnabled, _ => ZAxisIsEnabled };
        public bool MiniAxisIsMoving => MiniCardIndex switch { 1 => XAxisIsMoving, 2 => YAxisIsMoving, _ => ZAxisIsMoving };
        public bool MiniAxisIsAlarm => MiniCardIndex switch { 1 => XAxisIsAlarm, 2 => YAxisIsAlarm, _ => ZAxisIsAlarm };

        public double MiniJogVelocity
        {
            get => MiniCardIndex switch { 1 => XAxisJogVelocity, 2 => YAxisJogVelocity, _ => ZAxisJogVelocity };
            set
            {
                switch (MiniCardIndex)
                {
                    case 1: XAxisJogVelocity = value; break;
                    case 2: YAxisJogVelocity = value; break;
                    default: ZAxisJogVelocity = value; break;
                }
                RaisePropertyChanged();
            }
        }

        #endregion

        #region 命令定义

        public DelegateCommand SwitchRecipeCommand { get; private set; }
        public DelegateCommand TriggerScanCommand { get; private set; }
        public DelegateCommand TriggerOcrCommand { get; private set; }
        public DelegateCommand OpenScannerSoftwareCommand { get; private set; }
        public DelegateCommand OpenCameraSoftwareCommand { get; private set; }

        public DelegateCommand TestPullOutCommand { get; private set; }
        public DelegateCommand TestPushBackCommand { get; private set; }
        public DelegateCommand TestFullFlowCommand { get; private set; }

        // ── 模组调试步骤命令 ──
        public DelegateCommand Step0MoveStopperToStandbyCommand { get; private set; }
        public DelegateCommand Step1SwitchProductionCommand { get; private set; }
        public DelegateCommand Step2SwitchLayerCommand { get; private set; }
        public DelegateCommand Step3PullMaterialCommand { get; private set; }
        public DelegateCommand Step4MoveToRecipeXYZCommand { get; private set; }
        public DelegateCommand Step5MoveToStandbyCommand { get; private set; }
        public DelegateCommand Step6PushMaterialCommand { get; private set; }
        public DelegateCommand ResetDebugStepsCommand { get; private set; }

        // ── 三轴独立 JOG 命令 ──
        public DelegateCommand XJogPositiveCommand { get; private set; }
        public DelegateCommand XJogNegativeCommand { get; private set; }
        public DelegateCommand XAxisStopCommand { get; private set; }
        public DelegateCommand YJogPositiveCommand { get; private set; }
        public DelegateCommand YJogNegativeCommand { get; private set; }
        public DelegateCommand YAxisStopCommand { get; private set; }
        public DelegateCommand ZJogPositiveCommand { get; private set; }
        public DelegateCommand ZJogNegativeCommand { get; private set; }
        public DelegateCommand ZAxisStopCommand { get; private set; }

        public DelegateCommand MoveToRecipeXYZCommand { get; private set; }
        public DelegateCommand GetAndUpdateRecipeXYZCommand { get; private set; }
        public DelegateCommand SyncAdjustCommand { get; private set; }

        // ── 迷你模式命令 ──
        public DelegateCommand MiniCardPrevCommand { get; private set; }
        public DelegateCommand MiniCardNextCommand { get; private set; }
        public DelegateCommand MiniJogPositiveCommand { get; private set; }
        public DelegateCommand MiniJogNegativeCommand { get; private set; }
        public DelegateCommand MiniAxisStopCommand { get; private set; }
        public DelegateCommand MiniAxisEnableCommand { get; private set; }
        public DelegateCommand MiniAxisDisableCommand { get; private set; }
        public DelegateCommand MiniAxisHomeCommand { get; private set; }
        public DelegateCommand MiniAxisResetCommand { get; private set; }

        private void InitializeCommands()
        {
            SwitchRecipeCommand = new DelegateCommand(ExecuteSwitchRecipe);
            TriggerScanCommand = new DelegateCommand(ExecuteTriggerScan);
            TriggerOcrCommand = new DelegateCommand(ExecuteTriggerOcr);
            OpenScannerSoftwareCommand = new DelegateCommand(ExecuteOpenScannerSoftware);
            OpenCameraSoftwareCommand = new DelegateCommand(ExecuteOpenCameraSoftware);

            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
            ConfirmCommand = new DelegateCommand(ExecuteConfirm);

            TestPullOutCommand = new DelegateCommand(async () => await ExecuteTestPullOutAsync(), () => !IsBusy);
            TestPushBackCommand = new DelegateCommand(async () => await ExecuteTestPushBackAsync(), () => !IsBusy);
            TestFullFlowCommand = new DelegateCommand(async () => await ExecuteTestFullFlowAsync(), () => !IsBusy);

            Step0MoveStopperToStandbyCommand = new DelegateCommand(async () => await ExecuteStep0Async(), () => !_step0Busy);
            Step1SwitchProductionCommand = new DelegateCommand(async () => await ExecuteDebugStep1Async(), () => !IsBusy && _step0Done);
            Step2SwitchLayerCommand = new DelegateCommand(async () => await ExecuteDebugStep2Async(), () => !IsBusy && CurrentDebugStep >= 1);
            Step3PullMaterialCommand = new DelegateCommand(async () => await ExecuteDebugStep3Async(), () => !IsBusy && CurrentDebugStep >= 2);
            Step4MoveToRecipeXYZCommand = new DelegateCommand(async () => await ExecuteDebugStep4Async(), () => !IsBusy && CurrentDebugStep >= 3);
            Step5MoveToStandbyCommand = new DelegateCommand(async () => await ExecuteDebugStep5Async(), () => !IsBusy && CurrentDebugStep >= 4);
            Step6PushMaterialCommand = new DelegateCommand(async () => await ExecuteDebugStep6Async(), () => !IsBusy && CurrentDebugStep >= 5);
            ResetDebugStepsCommand = new DelegateCommand(() =>
            {
                CurrentDebugStep = 0;
                _step0Done = false;
                RaisePropertyChanged(nameof(Step0Brush));
                Step1SwitchProductionCommand.RaiseCanExecuteChanged();
                DebugStepMessage = "已重置，请从第0步开始";
            });

            XJogPositiveCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.JogAsync(XAxisJogVelocity, true, XAxisJogVelocity * 5, XAxisJogVelocity * 5); });
            XJogNegativeCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.JogAsync(XAxisJogVelocity, false, XAxisJogVelocity * 5, XAxisJogVelocity * 5); });
            XAxisStopCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.StopAsync(); });
            YJogPositiveCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.JogAsync(YAxisJogVelocity, true, YAxisJogVelocity * 5, YAxisJogVelocity * 5); });
            YJogNegativeCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.JogAsync(YAxisJogVelocity, false, YAxisJogVelocity * 5, YAxisJogVelocity * 5); });
            YAxisStopCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.StopAsync(); });
            ZJogPositiveCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.JogAsync(ZAxisJogVelocity, true, ZAxisJogVelocity * 5, ZAxisJogVelocity * 5); });
            ZJogNegativeCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.JogAsync(ZAxisJogVelocity, false, ZAxisJogVelocity * 5, ZAxisJogVelocity * 5); });
            ZAxisStopCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.StopAsync(); });

            MoveToRecipeXYZCommand = new DelegateCommand(async () => await ExecuteMoveToRecipeXYZAsync(), () => !IsBusy);
            GetAndUpdateRecipeXYZCommand = new DelegateCommand(ExecuteGetAndUpdateRecipeXYZ);
            SyncAdjustCommand = new DelegateCommand(() =>
                MessageService.ShowMessage("同步调整算法待实现", "提示", MessageBoxButton.OK, MessageBoxImage.Information));

            MiniCardPrevCommand = new DelegateCommand(() => MiniCardIndex--);
            MiniCardNextCommand = new DelegateCommand(() => MiniCardIndex++);
            MiniJogPositiveCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, _ => _axisZ };
                if (axis != null) await axis.JogAsync(MiniJogVelocity, true, MiniJogVelocity * 5, MiniJogVelocity * 5);
            });
            MiniJogNegativeCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, _ => _axisZ };
                if (axis != null) await axis.JogAsync(MiniJogVelocity, false, MiniJogVelocity * 5, MiniJogVelocity * 5);
            });
            MiniAxisStopCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, _ => _axisZ };
                if (axis != null) await axis.StopAsync();
            });
            MiniAxisEnableCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, 3 => _axisZ, _ => null };
                if (axis != null) await axis.EnableAsync();
            });
            MiniAxisDisableCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, 3 => _axisZ, _ => null };
                if (axis != null) await axis.DisableAsync();
            });
            MiniAxisHomeCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, 3 => _axisZ, _ => null };
                if (axis != null) await axis.HomeAsync(CancellationToken.None);
            });
            MiniAxisResetCommand = new DelegateCommand(async () =>
            {
                IAxis? axis = MiniCardIndex switch { 1 => _axisX, 2 => _axisY, 3 => _axisZ, _ => null };
                if (axis is BaseDevice dev) await dev.ResetAsync(CancellationToken.None);
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

        #endregion

        #region 模组调试命令实现

        private async Task ExecuteTestPullOutAsync()
        {
            var module = GetCurrentModule();
            if (module == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                var closeResult = await FeedingSetThrustWasherAsync(false, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                await InternalTestPullOutAsync(module, _currentRecipe?.WafeSize ?? E_WafeSize._12寸, cts.Token);
                await MoveToRecipeXYZInternalAsync(cts.Token);

                var openResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!openResult.IsSuccess) throw new Exception($"打开凸片检测失败: {openResult.ErrorMessage}");

                MessageService.ShowMessage("拉料流程测试完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"拉料流程测试中断: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteTestPushBackAsync()
        {
            var module = GetCurrentModule();
            if (module == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                var closeResult = await FeedingSetThrustWasherAsync(false, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                await InternalTestPushBackAsync(module, cts.Token);

                var openResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!openResult.IsSuccess) throw new Exception($"打开凸片检测失败: {openResult.ErrorMessage}");

                MessageService.ShowMessage("推料流程测试完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"推料流程测试中断: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task ExecuteTestFullFlowAsync()
        {
            var module = GetCurrentModule();
            if (module == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                var closeResult = await FeedingSetThrustWasherAsync(false, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                await InternalTestPullOutAsync(module, _currentRecipe?.WafeSize ?? E_WafeSize._12寸, cts.Token);
                await MoveToRecipeXYZInternalAsync(cts.Token);

                var openResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!openResult.IsSuccess) throw new Exception($"打开凸片检测失败: {openResult.ErrorMessage}");

                await Task.Delay(1500, cts.Token);

                var closeResult2 = await FeedingSetThrustWasherAsync(false, cts.Token);
                if (!closeResult2.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult2.ErrorMessage}");

                await InternalTestPushBackAsync(module, cts.Token);

                var openResult2 = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!openResult2.IsSuccess) throw new Exception($"打开凸片检测失败: {openResult2.ErrorMessage}");

                MessageService.ShowMessage("完整拉送料闭环测试完成", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"完整闭环测试中断: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static async Task InternalTestPullOutAsync(IMechanism module, E_WafeSize wafesize, CancellationToken token)
        {
            if (module is WS1MaterialPullingModule ws1)
            {
                var resMove = await ws1.InitialMoveFeeding(token);
                if (!resMove.IsSuccess) throw new Exception($"移动到取料位失败: {resMove.ErrorMessage}");
                var resClose = await ws1.CloseWafeGipper(token);
                if (!resClose.IsSuccess) throw new Exception($"关闭夹爪失败: {resClose.ErrorMessage}");
                if (!await ws1.CheckStackedPieces(token)) throw new Exception("检测到叠料异常");
                var resDetect = await ws1.MoveDetection(wafesize, token);
                if (!resDetect.IsSuccess) throw new Exception($"拉出至检测位失败: {resDetect.ErrorMessage}");
            }
            else if (module is WS2MaterialPullingModule ws2)
            {
                var resMove = await ws2.InitialMoveFeeding(token);
                if (!resMove.IsSuccess) throw new Exception($"移动到取料位失败: {resMove.ErrorMessage}");
                var resClose = await ws2.CloseWafeGipper(token);
                if (!resClose.IsSuccess) throw new Exception($"关闭夹爪失败: {resClose.ErrorMessage}");
                if (!await ws2.CheckStackedPieces(token)) throw new Exception("检测到叠料异常");
                var resDetect = await ws2.MoveDetection(wafesize, token);
                if (!resDetect.IsSuccess) throw new Exception($"拉出至检测位失败: {resDetect.ErrorMessage}");
            }
        }

        private static async Task InternalTestPushBackAsync(IMechanism module, CancellationToken token)
        {
            if (module is WS1MaterialPullingModule ws1)
            {
                var resFeed = await ws1.FeedingMaterialToBox(token);
                if (!resFeed.IsSuccess) throw new Exception($"送料入料盒失败: {resFeed.ErrorMessage}");
                var resOpen = await ws1.OpenWafeGipper(token);
                if (!resOpen.IsSuccess) throw new Exception($"打开夹爪失败: {resOpen.ErrorMessage}");
                var resRetract = await ws1.PutOverMove(token);
                if (!resRetract.IsSuccess) throw new Exception($"退回待机避让位失败: {resRetract.ErrorMessage}");
                if (!await ws1.CheckGipperInsidePro(token)) throw new Exception("退回后夹爪内仍检测到残留带片");
            }
            else if (module is WS2MaterialPullingModule ws2)
            {
                var resFeed = await ws2.FeedingMaterialToBox(token);
                if (!resFeed.IsSuccess) throw new Exception($"送料入料盒失败: {resFeed.ErrorMessage}");
                var resOpen = await ws2.OpenWafeGipper(token);
                if (!resOpen.IsSuccess) throw new Exception($"打开夹爪失败: {resOpen.ErrorMessage}");
                var resRetract = await ws2.PutOverMove(token);
                if (!resRetract.IsSuccess) throw new Exception($"退回待机避让位失败: {resRetract.ErrorMessage}");
                if (!await ws2.CheckGipperInsidePro(token)) throw new Exception("退回后夹爪内仍检测到残留带片");
            }
        }

        private IMechanism? GetCurrentModule()
        {
            return CurrentStation == E_WorkSpace.工位1 ? _ws1Module : _ws2Module;
        }

        private void RaiseStepCanExecuteChanged()
        {
            Step1SwitchProductionCommand?.RaiseCanExecuteChanged();
            Step2SwitchLayerCommand?.RaiseCanExecuteChanged();
            Step3PullMaterialCommand?.RaiseCanExecuteChanged();
            Step4MoveToRecipeXYZCommand?.RaiseCanExecuteChanged();
            Step5MoveToStandbyCommand?.RaiseCanExecuteChanged();
            Step6PushMaterialCommand?.RaiseCanExecuteChanged();
        }

        #endregion

        #region 模组调试步骤命令实现

        private async Task ExecuteStep0Async()
        {
            var axis = CurrentStation == E_WorkSpace.工位1 ? _axisStopperX1 : _axisStopperX2;
            if (axis == null) { DebugStepMessage = "挡料X轴未就绪"; return; }
            _step0Busy = true;
            Step0MoveStopperToStandbyCommand.RaiseCanExecuteChanged();
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在移动挡料X轴到待机位...";
                await axis.MoveToPointAsync("待机位", cts.Token);
                _step0Done = true;
                RaisePropertyChanged(nameof(Step0Brush));
                Step1SwitchProductionCommand.RaiseCanExecuteChanged();
                DebugStepMessage = "Step0 完成：挡料X轴已到达待机位，可继续后续步骤";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"Step0 失败：{ex.Message}";
            }
            finally
            {
                _step0Busy = false;
                Step0MoveStopperToStandbyCommand.RaiseCanExecuteChanged();
            }
        }

        private async Task ExecuteMoveToRecipeXYZAsync()
        {
            if (_currentRecipe == null) return;
            IsBusy = true;
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            try
            {
                await MoveToRecipeXYZInternalAsync(_cts.Token);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"移动到配方点位失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsBusy = false; }
        }

        private async Task MoveToRecipeXYZInternalAsync(CancellationToken token)
        {
            if (_currentRecipe == null) return;

            double x = CurrentStation == E_WorkSpace.工位1 ? _currentRecipe._1PosX : _currentRecipe._2PosX;
            double y = CurrentStation == E_WorkSpace.工位1 ? _currentRecipe._1PosY : _currentRecipe._2PosY;
            double z = CurrentStation == E_WorkSpace.工位1 ? _currentRecipe._1PosZ : _currentRecipe._2PosZ;

            // 1. Z轴先退到待机位，防止XY移动时相机干涉
            if (_axisZ != null)
                await _axisZ.MoveToPointAsync("待机位", token);

            // 2. XY轴并行移动到目标位
            var xyTasks = new System.Collections.Generic.List<Task>();
            if (_axisX != null) xyTasks.Add(_axisX.MoveAbsoluteAsync(x, _axisX.Param.Vel, _axisX.Param.Acc, _axisX.Param.Dec, 0.08, token));
            if (_axisY != null) xyTasks.Add(_axisY.MoveAbsoluteAsync(y, _axisY.Param.Vel, _axisY.Param.Acc, _axisY.Param.Dec, 0.08, token));
            await Task.WhenAll(xyTasks);

            // 3. Z轴移动到配方检测位
            if (_axisZ != null)
                await _axisZ.MoveAbsoluteAsync(z, _axisZ.Param.Vel, _axisZ.Param.Acc, _axisZ.Param.Dec, 0.08, token);
        }

        private void ExecuteGetAndUpdateRecipeXYZ()
        {
            if (_currentRecipe == null) return;

            double x = (int)(_axisX?.CurrentPosition ?? RecipePosX);
            double y = (int)(_axisY?.CurrentPosition ?? RecipePosY);
            double z = (int)(_axisZ?.CurrentPosition ?? RecipePosZ);

            if (CurrentStation == E_WorkSpace.工位1)
            {
                _currentRecipe._1PosX = x;
                _currentRecipe._1PosY = y;
                _currentRecipe._1PosZ = z;
            }
            else
            {
                _currentRecipe._2PosX = x;
                _currentRecipe._2PosY = y;
                _currentRecipe._2PosZ = z;
            }

            UpdateRecipePositionDisplay();
        }

        private async Task ExecuteDebugStep1Async()
        {
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                var size = _currentRecipe?.WafeSize ?? E_WafeSize._8寸;
                DebugStepMessage = $"正在切换生产状态（{size}）...";
                var result = await FeedingSwitchProductionStateAsync(cts.Token);
                if (!result.IsSuccess) throw new Exception(result.ErrorMessage);
                CurrentDebugStep = 1;
                DebugStepMessage = $"第1步完成：已切换为 {size} 生产状态，可执行第2步";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"第1步失败：{ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteDebugStep2Async()
        {
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = $"正在切换至第 {TargetLayerNumber} 层...";
                var result = await FeedingSwitchToLayerAsync(TargetLayerNumber, cts.Token);
                if (!result.IsSuccess) throw new Exception(result.ErrorMessage);
                CurrentDebugStep = 2;
                DebugStepMessage = $"第2步完成：已定位至第 {TargetLayerNumber} 层，可执行第3步";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"第2步失败：{ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteDebugStep3Async()
        {
            var pullModule = GetCurrentModule();
            if (pullModule == null) { DebugStepMessage = "当前工位拉料模组未就绪"; return; }
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在关闭凸片检测...";
                var closeResult = await FeedingSetThrustWasherAsync(false, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                DebugStepMessage = "正在执行拉料流程...";
                await InternalTestPullOutAsync(pullModule, _currentRecipe?.WafeSize ?? E_WafeSize._12寸, cts.Token);

                DebugStepMessage = "正在打开凸片检测...";
                var openResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!openResult.IsSuccess) throw new Exception($"打开凸片检测失败: {openResult.ErrorMessage}");

                CurrentDebugStep = 3;
                DebugStepMessage = "第3步完成：拉料流程测试完成，可执行第4步";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"第3步失败：{ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteDebugStep4Async()
        {
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在移动相机到配方位置...";
                await MoveToRecipeXYZInternalAsync(cts.Token);
                CurrentDebugStep = 4;
                DebugStepMessage = "第4步完成：相机已移动到配方位置，可执行第5步";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"第4步失败：{ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteDebugStep5Async()
        {
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在移动相机到待机位...";
                if (_axisZ != null)
                    await _axisZ.MoveToPointAsync("待机位", cts.Token);
                var xyTasks = new System.Collections.Generic.List<Task>();
                if (_axisX != null) xyTasks.Add(_axisX.MoveToPointAsync("待机位", cts.Token));
                if (_axisY != null) xyTasks.Add(_axisY.MoveToPointAsync("待机位", cts.Token));
                await Task.WhenAll(xyTasks);
                CurrentDebugStep = 5;
                DebugStepMessage = "第5步完成：相机已回到待机位，可执行第6步";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"第5步失败：{ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task ExecuteDebugStep6Async()
        {
            var pullModule = GetCurrentModule();
            if (pullModule == null) { DebugStepMessage = "当前工位拉料模组未就绪"; return; }
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在关闭凸片检测...";
                var closeResult = await FeedingSetThrustWasherAsync(false, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                DebugStepMessage = "正在执行推料流程...";
                await InternalTestPushBackAsync(pullModule, cts.Token);

                DebugStepMessage = "正在打开凸片检测...";
                var openResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!openResult.IsSuccess) throw new Exception($"打开凸片检测失败: {openResult.ErrorMessage}");

                CurrentDebugStep = 6;
                DebugStepMessage = "第6步完成：推料流程测试完成，全流程调试结束";
            }
            catch (Exception ex)
            {
                DebugStepMessage = $"第6步失败：{ex.Message}";
            }
            finally { IsBusy = false; }
        }

        private async Task<MechResult> FeedingSwitchProductionStateAsync(CancellationToken token)
        {
            var size = _currentRecipe?.WafeSize ?? E_WafeSize._8寸;
            if (CurrentStation == E_WorkSpace.工位1)
            {
                if (_ws1FeedingModule == null) throw new InvalidOperationException("工位1上料模组未就绪");
                return await _ws1FeedingModule.SwitchProductionStateAsync(size, token);
            }
            if (_ws2FeedingModule == null) throw new InvalidOperationException("工位2上料模组未就绪");
            return await _ws2FeedingModule.SwitchProductionStateAsync(size, token);
        }

        private async Task<MechResult> FeedingSwitchToLayerAsync(int layer, CancellationToken token)
        {
            if (CurrentStation == E_WorkSpace.工位1)
            {
                if (_ws1FeedingModule == null) throw new InvalidOperationException("工位1上料模组未就绪");
                return await _ws1FeedingModule.SwitchToLayerAsync(layer, token);
            }
            if (_ws2FeedingModule == null) throw new InvalidOperationException("工位2上料模组未就绪");
            return await _ws2FeedingModule.SwitchToLayerAsync(layer, token);
        }

        private async Task<MechResult> FeedingSetThrustWasherAsync(bool open, CancellationToken token)
        {
            if (CurrentStation == E_WorkSpace.工位1)
            {
                if (_ws1FeedingModule == null) throw new InvalidOperationException("工位1上料模组未就绪");
                return await _ws1FeedingModule.SetThrustWasherAsync(open, token);
            }
            if (_ws2FeedingModule == null) throw new InvalidOperationException("工位2上料模组未就绪");
            return await _ws2FeedingModule.SetThrustWasherAsync(open, token);
        }

        #endregion

        #region 工位切换

        private void OnSelectedStationChanged()
        {
            UpdateRecipePositionDisplay();
        }

        private void UpdateRecipePositionDisplay()
        {
            if (_currentRecipe == null) return;

            double x, y, z;
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
            RecipePosX = x;
            RecipePosY = y;
            RecipePosZ = z;
        }

        #endregion

        #region 定时器轮询

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_axisX != null)
            {
                var io = _axisX.AxisIOStatus;
                XAxisCurrentPosition = (int)(_axisX.CurrentPosition ?? 0);
                XAxisIsEnabled = io?.SVO ?? false;
                XAxisIsMoving = io?.Moving ?? false;
                XAxisIsAlarm = io?.ALM ?? false;
                XAxisIsPosLimit = io?.PEL ?? false;
                XAxisIsNegLimit = io?.MEL ?? false;
            }
            if (_axisY != null)
            {
                var io = _axisY.AxisIOStatus;
                YAxisCurrentPosition = (int)(_axisY.CurrentPosition ?? 0);
                YAxisIsEnabled = io?.SVO ?? false;
                YAxisIsMoving = io?.Moving ?? false;
                YAxisIsAlarm = io?.ALM ?? false;
                YAxisIsPosLimit = io?.PEL ?? false;
                YAxisIsNegLimit = io?.MEL ?? false;
            }
            if (_axisZ != null)
            {
                var io = _axisZ.AxisIOStatus;
                ZAxisCurrentPosition = (int)(_axisZ.CurrentPosition ?? 0);
                ZAxisIsEnabled = io?.SVO ?? false;
                ZAxisIsMoving = io?.Moving ?? false;
                ZAxisIsAlarm = io?.ALM ?? false;
                ZAxisIsPosLimit = io?.PEL ?? false;
                ZAxisIsNegLimit = io?.MEL ?? false;
            }

            RaisePropertyChanged(nameof(MiniAxisPosition));
            RaisePropertyChanged(nameof(MiniAxisIsEnabled));
            RaisePropertyChanged(nameof(MiniAxisIsMoving));
            RaisePropertyChanged(nameof(MiniAxisIsAlarm));
        }

        #endregion
    }
}
