using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.BarcodeScan;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
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
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// 程式调试对话框 ViewModel。
    /// <para>
    /// 功能职责：
    /// <list type="bullet">
    ///   <item>视觉相机三轴（XYZ）手动点动 (JOG) 控制，含使能/失能、回零、复位、急停</item>
    ///   <item>扫码枪触发与 OCR 智能相机触发，结果实时回显</item>
    ///   <item>配方点位采集（示教）、移动到配方点位、双工位同步调整（X 轴镜像偏移）</item>
    ///   <item>上下料 + 拉料模组完整调试步序（Step0~Step6），逐步顺序门控（前一步未完成则后续禁用）</item>
    ///   <item>50ms 定时轮询硬件 IO 状态，驱动 UI 实时刷新位置、限位、报警信号</item>
    ///   <item>迷你模式（7 页卡片切换）：适配窄屏或紧凑布局场景</item>
    /// </list>
    /// </para>
    /// <para>线程模型：所有运动均为 async/await，CancellationTokenSource 在对话框关闭时统一取消，防止后台任务泄漏。</para>
    /// </summary>
    public class RecipeDebugViewModel : PFDialogViewModelBase
    {
        #region 依赖服务与硬件字段

        /// <summary>硬件管理服务，按设备名解析具体硬件实例（轴、扫码枪、相机、光源等）。</summary>
        private readonly IHardwareManagerService _hardwareManager;

        /// <summary>OCR 配方服务，用于读取/持久化 <see cref="OCRRecipeParam"/>。</summary>
        private readonly IRecipeService<OCRRecipeParam> _recipeService;

        /// <summary>用户服务，用于权限判断（如超级用户才能编辑同步公差）。</summary>
        private readonly IUserService _userService;

        /// <summary>视觉 X 轴（相机水平方向移动）。可能为 null：硬件未配置或未启用。</summary>
        private readonly IAxis? _axisX;

        /// <summary>视觉 Y 轴（相机进深方向移动）。可能为 null。</summary>
        private readonly IAxis? _axisY;

        /// <summary>视觉 Z 轴（相机升降，控制焦距）。可能为 null。</summary>
        private readonly IAxis? _axisZ;

        /// <summary>工位1挡料 X 轴，Step0 用于移至待机位以让出拉料通道。</summary>
        private readonly IAxis? _axisStopperX1;

        /// <summary>工位2挡料 X 轴，Step0 用于移至待机位。</summary>
        private readonly IAxis? _axisStopperX2;

        /// <summary>工位1扫码枪。</summary>
        private readonly IBarcodeScan? _scanner1;

        /// <summary>工位2扫码枪。</summary>
        private readonly IBarcodeScan? _scanner2;

        /// <summary>OCR 智能相机（共用，通过运动 XY 切换工位采图）。</summary>
        private readonly IIntelligentCamera? _camera;

        /// <summary>光源控制器，支持多通道亮度调节（通道1=红外，通道2=白光）。</summary>
        private readonly ILightController? _lightconnter;

        #endregion

        #region 内部状态字段

        /// <summary>50ms UI 轮询定时器，驱动三轴 IO 状态实时刷新到 UI。</summary>
        private DispatcherTimer _pollingTimer;

        /// <summary>当前运动任务的取消令牌源；对话框关闭或新任务启动时取消旧任务。</summary>
        private CancellationTokenSource _cts;

        /// <summary>对话框打开时通过参数注入的当前配方对象，本 VM 内对其点位/光源进行修改。</summary>
        private OCRRecipeParam _currentRecipe;

        /// <summary>参数服务（如全局参数、设备参数）。</summary>
        private IParamService _paramservice;

        /// <summary>工位1拉料模组（夹爪取片、检测、推料入盒）。</summary>
        private readonly WS1MaterialPullingModule? _ws1Module;

        /// <summary>工位2拉料模组。</summary>
        private readonly WS2MaterialPullingModule? _ws2Module;

        /// <summary>工位1上料模组（料盒升降、层切换、凸片检测开关）。</summary>
        private readonly WS1FeedingModel? _ws1FeedingModule;

        /// <summary>工位2上料模组。</summary>
        private readonly WS2FeedingModel? _ws2FeedingModule;

        #endregion

        #region 全局忙碌标志

        private bool _isBusy;
        /// <summary>
        /// 全局忙碌标志。为 true 时禁用拉/推料测试、移动到配方点位、调试步序等长耗时命令，
        /// 防止用户在运动过程中重复触发命令导致冲突。
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    // IsBusy 变化时主动刷新所有相关命令的 CanExecute，使按钮即时启用/禁用
                    TestPullOutCommand?.RaiseCanExecuteChanged();
                    TestPushBackCommand?.RaiseCanExecuteChanged();
                    MoveToRecipeXYZCommand?.RaiseCanExecuteChanged();
                    RaiseStepCanExecuteChanged();
                }
            }
        }

        #endregion

        /// <summary>
        /// 构造调试 ViewModel：通过 Prism 容器解析两个工位的上料/拉料模组，
        /// 通过硬件管理器按设备名（枚举字符串）解析三轴、挡料轴、扫码枪、相机和光源实例。
        /// </summary>
        /// <param name="hardwareManager">硬件管理服务（按名称查询设备）。</param>
        /// <param name="recipeService">OCR 配方服务。</param>
        /// <param name="paramService">参数服务。</param>
        /// <param name="containerProvider">Prism 容器，用于按命名解析机构模组。</param>
        public RecipeDebugViewModel(
            IHardwareManagerService hardwareManager,
            IRecipeService<OCRRecipeParam> recipeService, IParamService paramService,
            IContainerProvider containerProvider)
        {
            Title = "程式调试";
            _hardwareManager = hardwareManager;
            _recipeService = recipeService;
            _paramservice = paramService;
            _userService = containerProvider.Resolve<IUserService>();

            // 按命名注册解析两个工位的拉料/上料模组（IMechanism 接口，as 兜底）
            _ws1Module = containerProvider.Resolve<IMechanism>(nameof(WS1MaterialPullingModule)) as WS1MaterialPullingModule;
            _ws2Module = containerProvider.Resolve<IMechanism>(nameof(WS2MaterialPullingModule)) as WS2MaterialPullingModule;
            _ws1FeedingModule = containerProvider.Resolve<IMechanism>(nameof(WS1FeedingModel)) as WS1FeedingModel;
            _ws2FeedingModule = containerProvider.Resolve<IMechanism>(nameof(WS2FeedingModel)) as WS2FeedingModel;

            // 按设备名（E_AxisName 枚举的 ToString）解析硬件实例；找不到或类型不匹配则为 null
            _axisX = hardwareManager.GetDevice(E_AxisName.视觉X轴.ToString()) as IAxis;
            _axisY = hardwareManager.GetDevice(E_AxisName.视觉Y轴.ToString()) as IAxis;
            _axisZ = hardwareManager.GetDevice(E_AxisName.视觉Z轴.ToString()) as IAxis;
            _axisStopperX1 = hardwareManager.GetDevice(E_AxisName.工位1挡料X轴.ToString()) as IAxis;
            _axisStopperX2 = hardwareManager.GetDevice(E_AxisName.工位2挡料X轴.ToString()) as IAxis;

            _scanner1 = hardwareManager.GetDevice(E_ScanCode.工位1扫码枪.ToString()) as IBarcodeScan;
            _scanner2 = hardwareManager.GetDevice(E_ScanCode.工位2扫码枪.ToString()) as IBarcodeScan;

            // 相机与光源通常全局唯一，从激活设备集合中 OfType 取首个
            _camera = hardwareManager.ActiveDevices.OfType<IIntelligentCamera>().FirstOrDefault();
            _lightconnter = hardwareManager.ActiveDevices.OfType<ILightController>().FirstOrDefault();

            InitializeCommands();

            // 50ms 轮询间隔：兼顾 UI 流畅度（>20Hz）与 CPU/IO 开销
            _pollingTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region Dialog 生命周期

        /// <summary>
        /// 对话框打开回调。
        /// <para>从 <paramref name="parameters"/> 中提取当前配方，初始化光源亮度（通道1红外/通道2白光）、
        /// 同步公差（按晶圆尺寸 8寸/12寸 区分默认值），刷新点位显示并启动状态轮询定时器。</para>
        /// </summary>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("CurrentRepice"))
                _currentRecipe = parameters.GetValue<OCRRecipeParam>("CurrentRepice");

            RaisePropertyChanged(nameof(RecipeWaferSizeText));
            // 同步公差默认值按物料尺寸取经验值（um 单位）：8 寸 641000，12 寸 535000
            SyncTolerance = _currentRecipe?.WafeSize == E_WafeSize._8寸 ? 641000 : 535000;
            RaisePropertyChanged(nameof(SyncToleranceLabel));
            InfraredLightValue = _currentRecipe?.LightChanel1Value ?? 0;
            WhiteLightValue = _currentRecipe?.LightChanel2Value ?? 0;

            UpdateRecipePositionDisplay();
            _pollingTimer.Start();
        }

        /// <summary>
        /// 对话框关闭回调。停止 UI 轮询定时器并取消所有未完成的运动任务，防止后台任务泄漏到主程序生命周期。
        /// </summary>
        public override void OnDialogClosed()
        {
            _pollingTimer.Stop();
            _cts?.Cancel();
        }

        #endregion

        #region 三轴独立状态属性
        // 每个轴有 9 个独立属性：CurrentPosition / IsEnabled / IsMoving / IsAlarm
        // IsPosLimit (正限位 PEL) / IsNegLimit (负限位 MEL) / IsHomed (回零完成) / IsORG (原点信号) / JogVelocity
        // 由 OnPollingTimerTick 每 50ms 从硬件 IO 状态批量刷新

        // -- 视觉X轴 --
        private double _xAxisCurrentPosition;
        /// <summary>视觉 X 轴当前位置（编码器反馈，单位 um 或 mm，依硬件驱动定义）。</summary>
        public double XAxisCurrentPosition { get => _xAxisCurrentPosition; set => SetProperty(ref _xAxisCurrentPosition, value); }
        private bool _xAxisIsEnabled;
        /// <summary>X 轴使能状态（SVO 信号）。</summary>
        public bool XAxisIsEnabled { get => _xAxisIsEnabled; set => SetProperty(ref _xAxisIsEnabled, value); }
        private bool _xAxisIsMoving;
        /// <summary>X 轴运动中标志。</summary>
        public bool XAxisIsMoving { get => _xAxisIsMoving; set => SetProperty(ref _xAxisIsMoving, value); }
        private bool _xAxisIsAlarm;
        /// <summary>X 轴报警状态（ALM 信号）。</summary>
        public bool XAxisIsAlarm { get => _xAxisIsAlarm; set => SetProperty(ref _xAxisIsAlarm, value); }
        private bool _xAxisIsPosLimit;
        /// <summary>X 轴正限位触发（PEL，硬限位）。</summary>
        public bool XAxisIsPosLimit { get => _xAxisIsPosLimit; set => SetProperty(ref _xAxisIsPosLimit, value); }
        private bool _xAxisIsNegLimit;
        /// <summary>X 轴负限位触发（MEL，硬限位）。</summary>
        public bool XAxisIsNegLimit { get => _xAxisIsNegLimit; set => SetProperty(ref _xAxisIsNegLimit, value); }
        private bool _xAxisIsHomed;
        /// <summary>X 轴回零完成标志（HomeDone）。</summary>
        public bool XAxisIsHomed { get => _xAxisIsHomed; set => SetProperty(ref _xAxisIsHomed, value); }
        private bool _xAxisIsORG;
        /// <summary>X 轴原点信号（ORG）。</summary>
        public bool XAxisIsORG { get => _xAxisIsORG; set => SetProperty(ref _xAxisIsORG, value); }
        private double _xAxisJogVelocity = 10.0;
        /// <summary>X 轴 JOG 速度（用户可在 UI 调节，加减速默认按 5 倍速度推导）。</summary>
        public double XAxisJogVelocity { get => _xAxisJogVelocity; set => SetProperty(ref _xAxisJogVelocity, value); }

        // -- 视觉Y轴 --
        private double _yAxisCurrentPosition;
        /// <summary>视觉 Y 轴当前位置。</summary>
        public double YAxisCurrentPosition { get => _yAxisCurrentPosition; set => SetProperty(ref _yAxisCurrentPosition, value); }
        private bool _yAxisIsEnabled;
        /// <summary>Y 轴使能状态。</summary>
        public bool YAxisIsEnabled { get => _yAxisIsEnabled; set => SetProperty(ref _yAxisIsEnabled, value); }
        private bool _yAxisIsMoving;
        /// <summary>Y 轴运动中标志。</summary>
        public bool YAxisIsMoving { get => _yAxisIsMoving; set => SetProperty(ref _yAxisIsMoving, value); }
        private bool _yAxisIsAlarm;
        /// <summary>Y 轴报警状态。</summary>
        public bool YAxisIsAlarm { get => _yAxisIsAlarm; set => SetProperty(ref _yAxisIsAlarm, value); }
        private bool _yAxisIsPosLimit;
        /// <summary>Y 轴正限位触发。</summary>
        public bool YAxisIsPosLimit { get => _yAxisIsPosLimit; set => SetProperty(ref _yAxisIsPosLimit, value); }
        private bool _yAxisIsNegLimit;
        /// <summary>Y 轴负限位触发。</summary>
        public bool YAxisIsNegLimit { get => _yAxisIsNegLimit; set => SetProperty(ref _yAxisIsNegLimit, value); }
        private bool _yAxisIsHomed;
        /// <summary>Y 轴回零完成标志。</summary>
        public bool YAxisIsHomed { get => _yAxisIsHomed; set => SetProperty(ref _yAxisIsHomed, value); }
        private bool _yAxisIsORG;
        /// <summary>Y 轴原点信号。</summary>
        public bool YAxisIsORG { get => _yAxisIsORG; set => SetProperty(ref _yAxisIsORG, value); }
        private double _yAxisJogVelocity = 10.0;
        /// <summary>Y 轴 JOG 速度。</summary>
        public double YAxisJogVelocity { get => _yAxisJogVelocity; set => SetProperty(ref _yAxisJogVelocity, value); }

        // -- 视觉Z轴 --
        private double _zAxisCurrentPosition;
        /// <summary>视觉 Z 轴当前位置。</summary>
        public double ZAxisCurrentPosition { get => _zAxisCurrentPosition; set => SetProperty(ref _zAxisCurrentPosition, value); }
        private bool _zAxisIsEnabled;
        /// <summary>Z 轴使能状态。</summary>
        public bool ZAxisIsEnabled { get => _zAxisIsEnabled; set => SetProperty(ref _zAxisIsEnabled, value); }
        private bool _zAxisIsMoving;
        /// <summary>Z 轴运动中标志。</summary>
        public bool ZAxisIsMoving { get => _zAxisIsMoving; set => SetProperty(ref _zAxisIsMoving, value); }
        private bool _zAxisIsAlarm;
        /// <summary>Z 轴报警状态。</summary>
        public bool ZAxisIsAlarm { get => _zAxisIsAlarm; set => SetProperty(ref _zAxisIsAlarm, value); }
        private bool _zAxisIsPosLimit;
        /// <summary>Z 轴正限位触发。</summary>
        public bool ZAxisIsPosLimit { get => _zAxisIsPosLimit; set => SetProperty(ref _zAxisIsPosLimit, value); }
        private bool _zAxisIsNegLimit;
        /// <summary>Z 轴负限位触发。</summary>
        public bool ZAxisIsNegLimit { get => _zAxisIsNegLimit; set => SetProperty(ref _zAxisIsNegLimit, value); }
        private bool _zAxisIsHomed;
        /// <summary>Z 轴回零完成标志。</summary>
        public bool ZAxisIsHomed { get => _zAxisIsHomed; set => SetProperty(ref _zAxisIsHomed, value); }
        private bool _zAxisIsORG;
        /// <summary>Z 轴原点信号。</summary>
        public bool ZAxisIsORG { get => _zAxisIsORG; set => SetProperty(ref _zAxisIsORG, value); }
        private double _zAxisJogVelocity = 10.0;
        /// <summary>Z 轴 JOG 速度。</summary>
        public double ZAxisJogVelocity { get => _zAxisJogVelocity; set => SetProperty(ref _zAxisJogVelocity, value); }

        #endregion

        #region 工位选择与点位属性

        private E_WorkSpace _currentStation = E_WorkSpace.工位1;
        /// <summary>
        /// 当前选中工位（工位1 / 工位2）。切换时通过 <see cref="OnSelectedStationChanged"/> 刷新点位显示。
        /// </summary>
        public E_WorkSpace CurrentStation { get => _currentStation; set => SetProperty(ref _currentStation, value); }

        private string _currentRecipePosition = "(X=0.000, Y=0.000, Z=0.000)";
        /// <summary>当前工位的配方点位字符串显示（格式化后用于 UI 标签）。</summary>
        public string CurrentRecipePosition { get => _currentRecipePosition; set => SetProperty(ref _currentRecipePosition, value); }

        private double _recipePosX;
        /// <summary>当前工位的配方 X 点位（UI 双向绑定，用于直接编辑）。</summary>
        public double RecipePosX { get => _recipePosX; set => SetProperty(ref _recipePosX, value); }
        private double _recipePosY;
        /// <summary>当前工位的配方 Y 点位。</summary>
        public double RecipePosY { get => _recipePosY; set => SetProperty(ref _recipePosY, value); }
        private double _recipePosZ;
        /// <summary>当前工位的配方 Z 点位。</summary>
        public double RecipePosZ { get => _recipePosZ; set => SetProperty(ref _recipePosZ, value); }

        private string _scanResult = "等待扫码...";
        /// <summary>扫码结果回显文本（含进行中/失败/成功状态）。</summary>
        public string ScanResult { get => _scanResult; set => SetProperty(ref _scanResult, value); }

        private string _ocrResult = "等待OCR...";
        /// <summary>OCR 识别结果回显文本。</summary>
        public string OcrResult { get => _ocrResult; set => SetProperty(ref _ocrResult, value); }

        /// <summary>
        /// 是否为超级用户。绑定到同步公差编辑控件的可见性 —— 普通用户不可修改公差，仅可查看。
        /// </summary>
        public bool IsSuperUser => _userService.IsAuthorized(UserLevel.SuperUser);

        private double _syncTolerance;
        /// <summary>
        /// 同步公差（单位 um）：工位1 → 工位2 X 轴偏移量。
        /// 默认值按物料尺寸区分（8 寸 = 641000，12 寸 = 535000），代表两工位机械中心距。
        /// </summary>
        public double SyncTolerance
        {
            get => _syncTolerance;
            set => SetProperty(ref _syncTolerance, value);
        }

        /// <summary>同步公差标签文本（根据当前配方晶圆尺寸动态切换 8 寸/12 寸提示）。</summary>
        public string SyncToleranceLabel => _currentRecipe?.WafeSize == E_WafeSize._8寸
            ? "同步公差（8寸）："
            : "同步公差（12寸）：";

        #endregion

        #region 光源参数属性

        private double _infraredLightValue;
        /// <summary>
        /// 红外光源亮度（通道 1）。setter 同时下发到硬件并回写配方，保持 UI / 硬件 / 配方三方一致。
        /// </summary>
        public double InfraredLightValue
        {
            get => _infraredLightValue;
            set
            {
                if (value != _infraredLightValue)
                {
                    SetProperty(ref _infraredLightValue, (int)value);
                    UpdateLihtValue(1, (int)value);                       // 立即下发到硬件
                    _currentRecipe.LightChanel1Value = (int)value;        // 同步回写到配方对象（待 Confirm 才会持久化）
                }
            }
        }

        private double _whiteLightValue;
        /// <summary>白光光源亮度（通道 2）。setter 行为同 <see cref="InfraredLightValue"/>。</summary>
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

        /// <summary>
        /// 下发指定通道的亮度到硬件光源控制器。
        /// </summary>
        /// <param name="chanel">通道号（1=红外，2=白光）。</param>
        /// <param name="vale">亮度值（0-255 或 0-100，依硬件定义）。</param>
        private void UpdateLihtValue(int chanel, int vale)
        {
            _lightconnter?.SetLightValue(chanel, vale);
        }

        #endregion

        #region 模组调试步骤属性

        private int _currentDebugStep;
        /// <summary>
        /// 当前已完成的调试步骤编号（0~6）。
        /// 仅当 CurrentDebugStep >= N-1 时第 N 步才可执行（严格顺序门控）。
        /// 变化时主动通知所有 StepNBrush 与各步骤命令的 CanExecute。
        /// </summary>
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

        /// <summary>晶圆尺寸文本（8寸/12寸），用于 UI 标题显示，配方为空时显示 "—"。</summary>
        public string RecipeWaferSizeText => _currentRecipe != null ? _currentRecipe.WafeSize.ToString() : "—";

        private int _targetLayerNumber = 1;
        /// <summary>
        /// Step2 切换层时使用的目标层号（料盒中的物料层数，从 1 开始）。
        /// setter 内做钳位（最小 1），避免下层负数请求。
        /// </summary>
        public int TargetLayerNumber
        {
            get => _targetLayerNumber;
            set => SetProperty(ref _targetLayerNumber, value < 1 ? 1 : value);
        }

        private string _debugStepMessage = "请从第1步开始，按顺序执行调试流程";
        /// <summary>调试步序的当前提示消息，每步执行前/中/后/失败均会刷新此文本。</summary>
        public string DebugStepMessage
        {
            get => _debugStepMessage;
            set => SetProperty(ref _debugStepMessage, value);
        }

        /// <summary>Step0（挡料 X 轴到待机位）完成标志，作为 Step1 的前置门控条件。</summary>
        private bool _step0Done;

        /// <summary>Step0 执行中标志，防止用户重复触发 Step0 按钮。</summary>
        private bool _step0Busy;

        /// <summary>Step0 按钮颜色：完成时绿色，未完成时蓝色（可点击）。</summary>
        public Brush Step0Brush => _step0Done ? Brushes.MediumSeaGreen : Brushes.CornflowerBlue;

        /// <summary>Step1 颜色：CurrentDebugStep >= 1 时绿色（已完成），否则蓝色（可执行，因 Step0 通过即解锁）。</summary>
        public Brush Step1Brush => CurrentDebugStep >= 1 ? Brushes.MediumSeaGreen : Brushes.CornflowerBlue;

        /// <summary>Step2 颜色：绿色=已完成，蓝色=前一步已完成可执行，灰色=未解锁。</summary>
        public Brush Step2Brush => CurrentDebugStep >= 2 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 1 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        /// <summary>Step3 颜色：同上规则。</summary>
        public Brush Step3Brush => CurrentDebugStep >= 3 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 2 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        /// <summary>Step4 颜色：同上规则。</summary>
        public Brush Step4Brush => CurrentDebugStep >= 4 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 3 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        /// <summary>Step5 颜色：同上规则。</summary>
        public Brush Step5Brush => CurrentDebugStep >= 5 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 4 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);
        /// <summary>Step6 颜色：同上规则，全流程终点。</summary>
        public Brush Step6Brush => CurrentDebugStep >= 6 ? Brushes.MediumSeaGreen : (CurrentDebugStep >= 5 ? Brushes.CornflowerBlue : Brushes.LightSlateGray);

        #endregion

        #region 迷你模式属性
        // 迷你模式将完整调试页面拆分为 7 张卡片轮播：
        //   0=调试步骤, 1=视觉X轴, 2=视觉Y轴, 3=视觉Z轴, 4=扫码/OCR, 5=配方点位, 6=光源控制
        // 用 MiniCardIndex 切换页面，1~3 页（轴卡片）通过 IsMiniAxisPage 复用同一组轴控件 UI

        private int _miniCardIndex;
        /// <summary>
        /// 迷你模式当前卡片索引（0~6），setter 内做环形取模（支持向左/右无限循环）。
        /// 变化时刷新所有依赖 MiniCardIndex 的派生属性和命令。
        /// </summary>
        public int MiniCardIndex
        {
            get => _miniCardIndex;
            set
            {
                // ((value % 7) + 7) % 7 处理负数取模，C# 的 % 对负数返回负值，需做正数化
                if (SetProperty(ref _miniCardIndex, ((value % 7) + 7) % 7))
                {
                    RaisePropertyChanged(nameof(MiniCardTitle));
                    RaisePropertyChanged(nameof(MiniCardPageText));
                    RaisePropertyChanged(nameof(IsMiniAxisPage));
                    RaisePropertyChanged(nameof(MiniAxisPosition));
                    RaisePropertyChanged(nameof(MiniAxisIsEnabled));
                    RaisePropertyChanged(nameof(MiniAxisIsMoving));
                    RaisePropertyChanged(nameof(MiniAxisIsHomed));
                    RaisePropertyChanged(nameof(MiniAxisIsPosLimit));
                    RaisePropertyChanged(nameof(MiniAxisIsORG));
                    RaisePropertyChanged(nameof(MiniAxisIsNegLimit));
                    RaisePropertyChanged(nameof(MiniAxisIsAlarm));
                    RaisePropertyChanged(nameof(MiniJogVelocity));
                    RaisePropertyChanged(nameof(MiniJogPositiveCommand));
                    RaisePropertyChanged(nameof(MiniJogNegativeCommand));
                    RaisePropertyChanged(nameof(MiniAxisStopCommand));
                    RaisePropertyChanged(nameof(MiniAxisEnableCommand));
                    RaisePropertyChanged(nameof(MiniAxisDisableCommand));
                    RaisePropertyChanged(nameof(MiniAxisHomeCommand));
                    RaisePropertyChanged(nameof(MiniAxisResetCommand));
                }
            }
        }

        /// <summary>当前卡片标题（按 MiniCardIndex 映射）。</summary>
        public string MiniCardTitle => MiniCardIndex switch
        {
            0 => "调试步骤",
            1 => "视觉 X 轴",
            2 => "视觉 Y 轴",
            3 => "视觉 Z 轴",
            4 => "扫码 / OCR",
            5 => "配方点位",
            _ => "光源控制"
        };
        /// <summary>分页指示文本（如 "2 / 7"）。</summary>
        public string MiniCardPageText => $"{MiniCardIndex + 1} / 7";
        /// <summary>当前卡片是否为轴控制页（1~3），用于 UI 切换显示轴控件区域。</summary>
        public bool IsMiniAxisPage => MiniCardIndex >= 1 && MiniCardIndex <= 3;

        // 以下 MiniAxis* 属性按当前选中轴（X/Y/Z）路由到对应轴的状态属性，实现 UI 复用
        /// <summary>迷你模式当前选中轴的位置。</summary>
        public double MiniAxisPosition => MiniCardIndex switch { 1 => XAxisCurrentPosition, 2 => YAxisCurrentPosition, _ => ZAxisCurrentPosition };
        /// <summary>迷你模式当前选中轴的使能状态。</summary>
        public bool MiniAxisIsEnabled => MiniCardIndex switch { 1 => XAxisIsEnabled, 2 => YAxisIsEnabled, _ => ZAxisIsEnabled };
        /// <summary>迷你模式当前选中轴是否运动中。</summary>
        public bool MiniAxisIsMoving => MiniCardIndex switch { 1 => XAxisIsMoving, 2 => YAxisIsMoving, _ => ZAxisIsMoving };
        /// <summary>迷你模式当前选中轴是否回零完成。</summary>
        public bool MiniAxisIsHomed => MiniCardIndex switch { 1 => XAxisIsHomed, 2 => YAxisIsHomed, _ => ZAxisIsHomed };
        /// <summary>迷你模式当前选中轴正限位状态。</summary>
        public bool MiniAxisIsPosLimit => MiniCardIndex switch { 1 => XAxisIsPosLimit, 2 => YAxisIsPosLimit, _ => ZAxisIsPosLimit };
        /// <summary>迷你模式当前选中轴原点信号。</summary>
        public bool MiniAxisIsORG => MiniCardIndex switch { 1 => XAxisIsORG, 2 => YAxisIsORG, _ => ZAxisIsORG };
        /// <summary>迷你模式当前选中轴负限位状态。</summary>
        public bool MiniAxisIsNegLimit => MiniCardIndex switch { 1 => XAxisIsNegLimit, 2 => YAxisIsNegLimit, _ => ZAxisIsNegLimit };
        /// <summary>迷你模式当前选中轴报警状态。</summary>
        public bool MiniAxisIsAlarm => MiniCardIndex switch { 1 => XAxisIsAlarm, 2 => YAxisIsAlarm, _ => ZAxisIsAlarm };

        /// <summary>
        /// 迷你模式当前选中轴的 JOG 速度。读写均路由到对应轴的 JogVelocity 字段。
        /// </summary>
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

        /// <summary>切换当前工位（工位1 ↔ 工位2），并刷新点位显示。</summary>
        public DelegateCommand SwitchRecipeCommand { get; private set; }
        /// <summary>触发当前工位扫码枪一次扫码。</summary>
        public DelegateCommand TriggerScanCommand { get; private set; }
        /// <summary>触发 OCR 相机一次识别。</summary>
        public DelegateCommand TriggerOcrCommand { get; private set; }
        /// <summary>打开扫码枪厂商管理软件（外部进程，待实现）。</summary>
        public DelegateCommand OpenScannerSoftwareCommand { get; private set; }
        /// <summary>打开相机厂商管理软件（外部进程，待实现）。</summary>
        public DelegateCommand OpenCameraSoftwareCommand { get; private set; }

        /// <summary>独立测试"拉料"全流程（不依赖步序门控）。</summary>
        public DelegateCommand TestPullOutCommand { get; private set; }
        /// <summary>独立测试"推料"全流程。</summary>
        public DelegateCommand TestPushBackCommand { get; private set; }

        // ── 模组调试步骤命令（必须按顺序执行）──
        /// <summary>Step0：挡料 X 轴移动到待机位（解锁拉料通道，门控 Step1）。</summary>
        public DelegateCommand Step0MoveStopperToStandbyCommand { get; private set; }
        /// <summary>Step1：切换生产状态（按晶圆尺寸 8/12 寸切换上料模组配置）。</summary>
        public DelegateCommand Step1SwitchProductionCommand { get; private set; }
        /// <summary>Step2：上料模组切换到目标层（料盒升降到指定层）。</summary>
        public DelegateCommand Step2SwitchLayerCommand { get; private set; }
        /// <summary>Step3：执行拉料流程（夹爪取片 + 移到检测位）。</summary>
        public DelegateCommand Step3PullMaterialCommand { get; private set; }
        /// <summary>Step4：相机移动到当前工位的配方 XYZ 点位。</summary>
        public DelegateCommand Step4MoveToRecipeXYZCommand { get; private set; }
        /// <summary>Step5：相机三轴回到待机位（让出空间）。</summary>
        public DelegateCommand Step5MoveToStandbyCommand { get; private set; }
        /// <summary>Step6：执行推料流程（送回料盒 + 退回避让位）。</summary>
        public DelegateCommand Step6PushMaterialCommand { get; private set; }
        /// <summary>重置所有调试步骤进度（回到 Step0 未完成状态）。</summary>
        public DelegateCommand ResetDebugStepsCommand { get; private set; }

        // ── 三轴独立 JOG 命令（共 7 × 3 = 21 个命令）──
        /// <summary>X 轴正向点动。</summary>
        public DelegateCommand XJogPositiveCommand { get; private set; }
        /// <summary>X 轴负向点动。</summary>
        public DelegateCommand XJogNegativeCommand { get; private set; }
        /// <summary>X 轴停止运动。</summary>
        public DelegateCommand XAxisStopCommand { get; private set; }
        /// <summary>X 轴使能。</summary>
        public DelegateCommand XAxisEnableCommand { get; private set; }
        /// <summary>X 轴失能。</summary>
        public DelegateCommand XAxisDisableCommand { get; private set; }
        /// <summary>X 轴回零。</summary>
        public DelegateCommand XAxisHomeCommand { get; private set; }
        /// <summary>X 轴报警复位。</summary>
        public DelegateCommand XAxisResetCommand { get; private set; }
        /// <summary>Y 轴正向点动。</summary>
        public DelegateCommand YJogPositiveCommand { get; private set; }
        /// <summary>Y 轴负向点动。</summary>
        public DelegateCommand YJogNegativeCommand { get; private set; }
        /// <summary>Y 轴停止运动。</summary>
        public DelegateCommand YAxisStopCommand { get; private set; }
        /// <summary>Y 轴使能。</summary>
        public DelegateCommand YAxisEnableCommand { get; private set; }
        /// <summary>Y 轴失能。</summary>
        public DelegateCommand YAxisDisableCommand { get; private set; }
        /// <summary>Y 轴回零。</summary>
        public DelegateCommand YAxisHomeCommand { get; private set; }
        /// <summary>Y 轴报警复位。</summary>
        public DelegateCommand YAxisResetCommand { get; private set; }
        /// <summary>Z 轴正向点动。</summary>
        public DelegateCommand ZJogPositiveCommand { get; private set; }
        /// <summary>Z 轴负向点动。</summary>
        public DelegateCommand ZJogNegativeCommand { get; private set; }
        /// <summary>Z 轴停止运动。</summary>
        public DelegateCommand ZAxisStopCommand { get; private set; }
        /// <summary>Z 轴使能。</summary>
        public DelegateCommand ZAxisEnableCommand { get; private set; }
        /// <summary>Z 轴失能。</summary>
        public DelegateCommand ZAxisDisableCommand { get; private set; }
        /// <summary>Z 轴回零。</summary>
        public DelegateCommand ZAxisHomeCommand { get; private set; }
        /// <summary>Z 轴报警复位。</summary>
        public DelegateCommand ZAxisResetCommand { get; private set; }

        /// <summary>独立命令：移动相机到当前工位配方 XYZ 点位（防干涉三步序）。</summary>
        public DelegateCommand MoveToRecipeXYZCommand { get; private set; }
        /// <summary>读取三轴当前实际位置，写回当前工位的配方点位（示教采点）。</summary>
        public DelegateCommand GetAndUpdateRecipeXYZCommand { get; private set; }
        /// <summary>同步调整：以当前工位点位为基准，按同步公差镜像生成另一工位点位。</summary>
        public DelegateCommand SyncAdjustCommand { get; private set; }

        // ── 迷你模式命令 ──
        /// <summary>迷你模式：切换到上一张卡片。</summary>
        public DelegateCommand MiniCardPrevCommand { get; private set; }
        /// <summary>迷你模式：切换到下一张卡片。</summary>
        public DelegateCommand MiniCardNextCommand { get; private set; }
        /// <summary>迷你模式当前选中轴的正向点动命令（路由到 X/Y/Z 三轴对应命令）。</summary>
        public DelegateCommand MiniJogPositiveCommand => MiniCardIndex switch { 1 => XJogPositiveCommand, 2 => YJogPositiveCommand, _ => ZJogPositiveCommand };
        /// <summary>迷你模式当前选中轴的负向点动命令。</summary>
        public DelegateCommand MiniJogNegativeCommand => MiniCardIndex switch { 1 => XJogNegativeCommand, 2 => YJogNegativeCommand, _ => ZJogNegativeCommand };
        /// <summary>迷你模式当前选中轴的停止命令。</summary>
        public DelegateCommand MiniAxisStopCommand => MiniCardIndex switch { 1 => XAxisStopCommand, 2 => YAxisStopCommand, _ => ZAxisStopCommand };
        /// <summary>迷你模式当前选中轴的使能命令。</summary>
        public DelegateCommand MiniAxisEnableCommand => MiniCardIndex switch { 1 => XAxisEnableCommand, 2 => YAxisEnableCommand, _ => ZAxisEnableCommand };
        /// <summary>迷你模式当前选中轴的失能命令。</summary>
        public DelegateCommand MiniAxisDisableCommand => MiniCardIndex switch { 1 => XAxisDisableCommand, 2 => YAxisDisableCommand, _ => ZAxisDisableCommand };
        /// <summary>迷你模式当前选中轴的回零命令。</summary>
        public DelegateCommand MiniAxisHomeCommand => MiniCardIndex switch { 1 => XAxisHomeCommand, 2 => YAxisHomeCommand, _ => ZAxisHomeCommand };
        /// <summary>迷你模式当前选中轴的复位命令。</summary>
        public DelegateCommand MiniAxisResetCommand => MiniCardIndex switch { 1 => XAxisResetCommand, 2 => YAxisResetCommand, _ => ZAxisResetCommand };

        /// <summary>
        /// 初始化所有命令绑定。包括调试步序命令（带 CanExecute 门控）、三轴 JOG/Home/Reset 命令、
        /// 配方点位操作命令和迷你模式翻页命令。
        /// </summary>
        private void InitializeCommands()
        {
            SwitchRecipeCommand = new DelegateCommand(ExecuteSwitchRecipe);
            TriggerScanCommand = new DelegateCommand(ExecuteTriggerScan);
            TriggerOcrCommand = new DelegateCommand(ExecuteTriggerOcr);
            OpenScannerSoftwareCommand = new DelegateCommand(ExecuteOpenScannerSoftware);
            OpenCameraSoftwareCommand = new DelegateCommand(ExecuteOpenCameraSoftware);

            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
            ConfirmCommand = new DelegateCommand(ExecuteConfirm);

            // 拉/推料独立测试命令，依 IsBusy 门控（CanExecute = !IsBusy）
            TestPullOutCommand = new DelegateCommand(async () => await ExecuteTestPullOutAsync(), () => !IsBusy);
            TestPushBackCommand = new DelegateCommand(async () => await ExecuteTestPushBackAsync(), () => !IsBusy);

            // 调试步序命令：CanExecute 同时校验 IsBusy 与 CurrentDebugStep 顺序门控
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

            // 三轴 JOG 命令：加减速默认按速度的 5 倍（经验值，可在轴参数页定义后期重构）
            XJogPositiveCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.JogAsync(XAxisJogVelocity, true, XAxisJogVelocity * 5, XAxisJogVelocity * 5); });
            XJogNegativeCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.JogAsync(XAxisJogVelocity, false, XAxisJogVelocity * 5, XAxisJogVelocity * 5); });
            XAxisStopCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.StopAsync(); });
            XAxisEnableCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.EnableAsync(); });
            XAxisDisableCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.DisableAsync(); });
            XAxisHomeCommand = new DelegateCommand(async () => { if (_axisX != null) await _axisX.HomeAsync(CancellationToken.None); });
            // ResetAsync 定义在 BaseDevice 抽象类上，需 as 转型后才能调用
            XAxisResetCommand = new DelegateCommand(async () => { if (_axisX is BaseDevice devX) await devX.ResetAsync(CancellationToken.None); });
            YJogPositiveCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.JogAsync(YAxisJogVelocity, true, YAxisJogVelocity * 5, YAxisJogVelocity * 5); });
            YJogNegativeCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.JogAsync(YAxisJogVelocity, false, YAxisJogVelocity * 5, YAxisJogVelocity * 5); });
            YAxisStopCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.StopAsync(); });
            YAxisEnableCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.EnableAsync(); });
            YAxisDisableCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.DisableAsync(); });
            YAxisHomeCommand = new DelegateCommand(async () => { if (_axisY != null) await _axisY.HomeAsync(CancellationToken.None); });
            YAxisResetCommand = new DelegateCommand(async () => { if (_axisY is BaseDevice devY) await devY.ResetAsync(CancellationToken.None); });
            ZJogPositiveCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.JogAsync(ZAxisJogVelocity, true, ZAxisJogVelocity * 5, ZAxisJogVelocity * 5); });
            ZJogNegativeCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.JogAsync(ZAxisJogVelocity, false, ZAxisJogVelocity * 5, ZAxisJogVelocity * 5); });
            ZAxisStopCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.StopAsync(); });
            ZAxisEnableCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.EnableAsync(); });
            ZAxisDisableCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.DisableAsync(); });
            ZAxisHomeCommand = new DelegateCommand(async () => { if (_axisZ != null) await _axisZ.HomeAsync(CancellationToken.None); });
            ZAxisResetCommand = new DelegateCommand(async () => { if (_axisZ is BaseDevice devZ) await devZ.ResetAsync(CancellationToken.None); });

            MoveToRecipeXYZCommand = new DelegateCommand(async () => await ExecuteMoveToRecipeXYZAsync(), () => !IsBusy);
            GetAndUpdateRecipeXYZCommand = new DelegateCommand(ExecuteGetAndUpdateRecipeXYZ);
            SyncAdjustCommand = new DelegateCommand(ExecuteSyncAdjust);

            MiniCardPrevCommand = new DelegateCommand(() => MiniCardIndex--);
            MiniCardNextCommand = new DelegateCommand(() => MiniCardIndex++);
        }

        #endregion

        #region 通用命令实现

        /// <summary>
        /// 切换当前工位（工位1 ↔ 工位2），并通知点位显示刷新。
        /// 注意：方法签名为 async void，但内部无 await 调用，建议后续重构去掉 async 关键字。
        /// </summary>
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

        /// <summary>
        /// 触发当前工位的扫码枪一次扫码，将结果回显到 <see cref="ScanResult"/>。
        /// ⚠️ Bug：CurrentStation 是枚举 E_WorkSpace（值 1 / 2），与 0 比较恒为 false，工位2 永远走 _scanner2 分支。
        /// 应改为 CurrentStation == E_WorkSpace.工位1。
        /// </summary>
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

        /// <summary>
        /// 触发 OCR 智能相机一次识别，将结果回显到 <see cref="OcrResult"/>。
        /// </summary>
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

        /// <summary>TODO: 启动扫码枪厂商管理软件（外部进程）。</summary>
        private void ExecuteOpenScannerSoftware()
        {
            // TODO: 打开扫码枪管理软件
        }

        /// <summary>TODO: 启动相机厂商管理软件。</summary>
        private void ExecuteOpenCameraSoftware()
        {
            // TODO: 打开相机软件
        }

        /// <summary>
        /// 确认关闭对话框，将修改后的 _currentRecipe 通过 DialogParameters 回传给调用方。
        /// </summary>
        private void ExecuteConfirm()
        {
            var param = new DialogParameters() { { "CallBackRecipe", _currentRecipe } };
            RequestClose.Invoke(param, ButtonResult.OK);
        }

        #endregion

        #region 模组调试命令实现（独立测试入口，不受步序门控）

        /// <summary>
        /// 独立测试拉料全流程：
        /// 1. 关闭凸片检测 → 2. 拉料 + 移到检测位 → 3. 移动相机到配方 XYZ → 4. 重开凸片检测。
        /// </summary>
        private async Task ExecuteTestPullOutAsync()
        {
            var module = GetCurrentModule();
            if (module == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                // 关闭凸片检测（避免拉料过程中误触发料盒凸片传感器报警）
                var closeResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                await InternalTestPullOutAsync(module, _currentRecipe?.WafeSize ?? E_WafeSize._12寸, cts.Token);
                await MoveToRecipeXYZInternalAsync(cts.Token);

                // 测试完成后恢复凸片检测，保证下次正常生产时安全互锁有效
                var openResult = await FeedingSetThrustWasherAsync(false, cts.Token);
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

        /// <summary>
        /// 独立测试推料全流程：关闭凸片检测 → 送料入盒 + 打开夹爪 + 退回避让位 → 恢复凸片检测。
        /// </summary>
        private async Task ExecuteTestPushBackAsync()
        {
            var module = GetCurrentModule();
            if (module == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                var closeResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                await InternalTestPushBackAsync(module, cts.Token);

                var openResult = await FeedingSetThrustWasherAsync(false, cts.Token);
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

        /// <summary>
        /// 内部拉料动作序列（共两个工位分支共用同一动作模板）：
        /// 移动到取料位 → 关闭夹爪夹紧带片 → 叠料检测（异常立即中断）→ 拉出到检测位。
        /// </summary>
        /// <param name="module">当前工位的拉料模组（WS1 或 WS2）。</param>
        /// <param name="wafesize">当前晶圆尺寸，决定检测位的具体坐标。</param>
        /// <param name="token">取消令牌。</param>
        private static async Task InternalTestPullOutAsync(IMechanism module, E_WafeSize wafesize, CancellationToken token)
        {
            // 注：WS1 / WS2 模组类型不同但接口签名一致，受限于现有抽象层级只能通过 is 分支处理
            // TODO 重构：将 InitialMoveFeeding/CloseWafeGipper/CheckStackedPieces/MoveDetection 提到共同接口或基类
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

        /// <summary>
        /// 内部推料动作序列：送料入盒 → 打开夹爪 → 退回避让位 → 检查残留（夹爪内不应有带片）。
        /// </summary>
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
                // 残留检测：退回后夹爪内仍有带片说明上一步未送达，需人工排查
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

        /// <summary>
        /// 按当前选中工位返回对应拉料模组：工位1 → WS1, 工位2 → WS2。
        /// </summary>
        private IMechanism? GetCurrentModule()
        {
            return CurrentStation == E_WorkSpace.工位1 ? _ws1Module : _ws2Module;
        }

        /// <summary>
        /// 通知所有调试步序命令重新计算 CanExecute。
        /// 在 IsBusy 切换或 CurrentDebugStep 推进后调用，使按钮即时启用/禁用。
        /// </summary>
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

        #region 模组调试步骤命令实现（Step0 ~ Step6 顺序流程）

        /// <summary>
        /// Step0：将当前工位的挡料 X 轴移至"待机位"（让出拉料模组运动通道）。
        /// 完成后置 _step0Done = true，门控 Step1 可用。
        /// </summary>
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

        /// <summary>
        /// 独立命令：移动相机到当前工位配方 XYZ 点位。封装了"取消旧任务 + 创建新 CTS"的安全调用流程。
        /// </summary>
        private async Task ExecuteMoveToRecipeXYZAsync()
        {
            if (_currentRecipe == null) return;
            IsBusy = true;
            // 取消上一次未完成的运动，避免重复触发时叠加运动指令
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

        /// <summary>
        /// 防干涉三步序移动到配方点位（核心算法）：
        /// <para>1. Z 轴先退至"待机位"（升高相机），防止 XY 移动过程中相机与料盒/治具碰撞</para>
        /// <para>2. XY 轴并行移动到配方点位（<see cref="Task.WhenAll(Task[])"/>，两轴同时启动以减少周期时间）</para>
        /// <para>3. Z 轴下降到配方检测位（焦距对齐）</para>
        /// 每一步都执行"发送指令 + 等待到位 + 检查报警"三段校验，任一环节失败立即抛出 InvalidOperationException。
        /// </summary>
        /// <param name="token">取消令牌，对话框关闭或新任务发起时触发取消。</param>
        private async Task MoveToRecipeXYZInternalAsync(CancellationToken token)
        {
            if (_currentRecipe == null) return;

            // 按当前工位选择 X/Y/Z 三个目标坐标
            double x = CurrentStation == E_WorkSpace.工位1 ? _currentRecipe._1PosX : _currentRecipe._2PosX;
            double y = CurrentStation == E_WorkSpace.工位1 ? _currentRecipe._1PosY : _currentRecipe._2PosY;
            double z = CurrentStation == E_WorkSpace.工位1 ? _currentRecipe._1PosZ : _currentRecipe._2PosZ;

            // 校验各轴参数已配置（Vel = 0 时运动控制器会报错或运动无效）
            if (_axisX != null && (_axisX.Param == null || _axisX.Param.Vel <= 0))
                throw new InvalidOperationException("视觉X轴运动参数未配置，请先在轴参数页设置速度/加速度");
            if (_axisY != null && (_axisY.Param == null || _axisY.Param.Vel <= 0))
                throw new InvalidOperationException("视觉Y轴运动参数未配置，请先在轴参数页设置速度/加速度");
            if (_axisZ != null && (_axisZ.Param == null || _axisZ.Param.Vel <= 0))
                throw new InvalidOperationException("视觉Z轴运动参数未配置，请先在轴参数页设置速度/加速度");

            // ─── 步骤1：Z 轴退到待机位 ───
            if (_axisZ != null)
            {
                if (!await _axisZ.MoveToPointAsync("待机位", token))
                    throw new InvalidOperationException("Z轴退至待机位指令失败，请检查轴状态");
                if (!await WaitAxisMoveDoneAsync(_axisZ, token: token))
                    throw new InvalidOperationException("Z轴退至待机位超时或报警");
            }

            // ─── 步骤2：XY 并行运动 ───
            // 并行发送：先收集启动任务，统一 WhenAll 等待启动结果
            var xyStartTasks = new System.Collections.Generic.List<Task<bool>>();
            if (_axisX != null) xyStartTasks.Add(_axisX.MoveAbsoluteAsync(x, _axisX.Param.Vel, _axisX.Param.Acc, _axisX.Param.Dec, 0.08, token));
            if (_axisY != null) xyStartTasks.Add(_axisY.MoveAbsoluteAsync(y, _axisY.Param.Vel, _axisY.Param.Acc, _axisY.Param.Dec, 0.08, token));
            bool[] xyStarted = await Task.WhenAll(xyStartTasks);
            if (System.Array.Exists(xyStarted, r => !r))
                throw new InvalidOperationException("XY轴运动指令发送失败，请检查轴使能状态及软限位设置");

            // 并行等待 XY 到位
            var xyWaitTasks = new System.Collections.Generic.List<Task<bool>>();
            if (_axisX != null) xyWaitTasks.Add(WaitAxisMoveDoneAsync(_axisX, token: token));
            if (_axisY != null) xyWaitTasks.Add(WaitAxisMoveDoneAsync(_axisY, token: token));
            bool[] xyDone = await Task.WhenAll(xyWaitTasks);
            if (System.Array.Exists(xyDone, r => !r))
                throw new InvalidOperationException("XY轴等待到位超时或报警");

            // ─── 步骤3：Z 轴下降到配方检测位 ───
            if (_axisZ != null)
            {
                if (!await _axisZ.MoveAbsoluteAsync(z, _axisZ.Param.Vel, _axisZ.Param.Acc, _axisZ.Param.Dec, 0.08, token))
                    throw new InvalidOperationException("Z轴移动到配方检测位指令失败，请检查轴使能状态及软限位设置");
                if (!await WaitAxisMoveDoneAsync(_axisZ, token: token))
                    throw new InvalidOperationException("Z轴移动到配方检测位超时或报警");
            }
        }

        /// <summary>
        /// 调试页面专用的轴到位等待：50ms 轮询 MoveDone &amp;&amp; !Moving 标志，遇到报警即返回 false。
        /// <para>仿真模式（IsSimulated）直接返回 true，便于无硬件环境下调试。</para>
        /// <para>与生产流程的等待逻辑解耦：本方法不触发模组报警事件，仅返回布尔结果，由调用方决定后续行为。</para>
        /// </summary>
        /// <param name="axis">待等待的轴。</param>
        /// <param name="timeoutMs">超时阈值（默认 30 秒）。</param>
        /// <param name="token">外部取消令牌，与超时取消通过 LinkedTokenSource 合并。</param>
        /// <returns>true = 正常到位；false = 超时 / 报警 / 取消。</returns>
        private static async Task<bool> WaitAxisMoveDoneAsync(IAxis axis, int timeoutMs = 30_000, CancellationToken token = default)
        {
            if ((axis as IHardwareDevice)?.IsSimulated == true) return true;

            using var timeoutCts = new CancellationTokenSource(timeoutMs);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            try
            {
                while (true)
                {
                    await Task.Delay(50, linked.Token).ConfigureAwait(false);
                    var status = axis.AxisIOStatus;
                    if (status != null && status.MoveDone && !status.Moving)
                        return true;
                    if (status?.ALM == true)
                        return false;
                }
            }
            catch (OperationCanceledException)
            {
                return false;   // 超时或外部取消均归一化为 false
            }
        }

        /// <summary>
        /// 示教采点：读取三轴当前实际位置并截断为整数（um），写回当前工位的配方点位。
        /// 用于操作员手动 JOG 到目标位置后一键固化坐标。
        /// </summary>
        private void ExecuteGetAndUpdateRecipeXYZ()
        {
            if (_currentRecipe == null) return;

            // (int) 截断小数 —— 配方点位以 um 为单位，亚 um 精度无意义
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

        /// <summary>
        /// Step1：切换生产状态（按晶圆尺寸 8/12 寸切换上料模组配置，包括压脚、料盒规格、检测窗口等）。
        /// </summary>
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

        /// <summary>
        /// Step2：料盒升降至目标层（<see cref="TargetLayerNumber"/>），从料盒中暴露指定层的物料。
        /// </summary>
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

        /// <summary>
        /// Step3：完整拉料流程（关闭凸片检测 → 内部拉料动作 → 重开凸片检测）。
        /// 与 <see cref="ExecuteTestPullOutAsync"/> 的区别：本方法不移动相机，只完成"取片到检测位"。
        /// </summary>
        private async Task ExecuteDebugStep3Async()
        {
            var pullModule = GetCurrentModule();
            if (pullModule == null) { DebugStepMessage = "当前工位拉料模组未就绪"; return; }
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在关闭凸片检测...";
                var closeResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                DebugStepMessage = "正在执行拉料流程...";
                await InternalTestPullOutAsync(pullModule, _currentRecipe?.WafeSize ?? E_WafeSize._12寸, cts.Token);

                DebugStepMessage = "正在打开凸片检测...";
                var openResult = await FeedingSetThrustWasherAsync(false, cts.Token);
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

        /// <summary>
        /// Step4：调用核心防干涉运动函数，将相机三轴移动到当前工位的配方 XYZ 点位（拉料后视觉对焦）。
        /// </summary>
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

        /// <summary>
        /// Step5：相机三轴回到待机位。Z 轴先单独上升避免碰撞，再 XY 并行回零位。
        /// </summary>
        private async Task ExecuteDebugStep5Async()
        {
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在移动相机到待机位...";
                // 注：这里 Z 轴等待是隐式的（MoveToPointAsync 自身阻塞），未显式调用 WaitAxisMoveDoneAsync
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

        /// <summary>
        /// Step6：完整推料流程（关闭凸片检测 → 内部推料动作 → 重开凸片检测），全流程调试终点。
        /// </summary>
        private async Task ExecuteDebugStep6Async()
        {
            var pullModule = GetCurrentModule();
            if (pullModule == null) { DebugStepMessage = "当前工位拉料模组未就绪"; return; }
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugStepMessage = "正在关闭凸片检测...";
                var closeResult = await FeedingSetThrustWasherAsync(true, cts.Token);
                if (!closeResult.IsSuccess) throw new Exception($"关闭凸片检测失败: {closeResult.ErrorMessage}");

                DebugStepMessage = "正在执行推料流程...";
                await InternalTestPushBackAsync(pullModule, cts.Token);

                DebugStepMessage = "正在打开凸片检测...";
                var openResult = await FeedingSetThrustWasherAsync(false, cts.Token);
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

        /// <summary>
        /// 按当前工位调用对应上料模组的"切换生产状态"接口（晶圆尺寸 8/12 寸切换）。
        /// </summary>
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

        /// <summary>
        /// 按当前工位调用对应上料模组的"切换到指定层"接口。
        /// </summary>
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

        /// <summary>
        /// 按当前工位调用对应上料模组的"凸片检测开关"接口。
        /// </summary>
        /// <param name="open">true=关闭凸片检测（调试期屏蔽），false=打开（恢复正常）。
        /// ⚠️ 命名与含义反向：参数 open=true 实际是"屏蔽/关闭"检测，建议后续重构改名。</param>
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

        #region 同步调整

        /// <summary>
        /// 同步调整：以当前工位的配方点位为基准，按同步公差镜像生成另一工位的 X 轴点位。
        /// <para>规则：Y 轴、Z 轴坐标不变，仅 X 轴按同步公差偏移（两工位机械中心距固定）。</para>
        /// <list type="bullet">
        ///   <item>当前工位1 → 工位2.X = 工位1.X + 同步公差（工位2 在工位1 的 +X 方向）</item>
        ///   <item>当前工位2 → 工位1.X = 工位2.X - 同步公差</item>
        /// </list>
        /// <para>典型场景：操作员只示教一个工位，另一工位通过同步调整一键复制，减少示教工作量。</para>
        /// </summary>
        private void ExecuteSyncAdjust()
        {
            if (_currentRecipe == null) return;

            // 用户确认弹窗，明确告知镜像目标和偏移量，避免误操作覆盖已示教点位
            var confirm = MessageService.ShowSystemMessage(
                $"将以当前工位（{CurrentStation}）的配方点位为基准，\n" +
                $"按同步公差 {SyncTolerance} um 镜像计算另一工位的 X 轴点位。\n" +
                "Y 轴、Z 轴保持一致。\n\n确认执行同步调整？",
                "同步调整确认",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.OK) return;

            if (CurrentStation == E_WorkSpace.工位1)
            {
                // 工位1 → 工位2：X 加上同步公差，Y/Z 直接镜像
                _currentRecipe._2PosX = _currentRecipe._1PosX + SyncTolerance;
                _currentRecipe._2PosY = _currentRecipe._1PosY;
                _currentRecipe._2PosZ = _currentRecipe._1PosZ;

                MessageService.ShowMessage(
                    $"同步调整完成。\n工位2 点位：X={_currentRecipe._2PosX}, Y={_currentRecipe._2PosY}, Z={_currentRecipe._2PosZ}",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                // 工位2 → 工位1：X 减去同步公差，Y/Z 直接镜像
                _currentRecipe._1PosX = _currentRecipe._2PosX - SyncTolerance;
                _currentRecipe._1PosY = _currentRecipe._2PosY;
                _currentRecipe._1PosZ = _currentRecipe._2PosZ;

                MessageService.ShowMessage(
                    $"同步调整完成。\n工位1 点位：X={_currentRecipe._1PosX}, Y={_currentRecipe._1PosY}, Z={_currentRecipe._1PosZ}",
                    "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            // 刷新当前工位点位显示（被同步的另一工位的点位下次切换工位时再显示）
            UpdateRecipePositionDisplay();
        }

        #endregion

        #region 工位切换

        /// <summary>
        /// 工位切换钩子：当前预留扩展点，目前仅刷新点位显示。
        /// 后续可在此扩展硬件切换逻辑（如自动切换扫码枪/相机焦点）。
        /// </summary>
        private void OnSelectedStationChanged()
        {
            UpdateRecipePositionDisplay();
        }

        /// <summary>
        /// 按当前工位从 <see cref="_currentRecipe"/> 读取 XYZ 点位，更新到 RecipePosX/Y/Z 绑定属性，
        /// 并刷新格式化的坐标显示字符串 <see cref="CurrentRecipePosition"/>。
        /// </summary>
        private void UpdateRecipePositionDisplay()
        {
            if (_currentRecipe == null) return;

            double x, y, z;
            // ⚠️ 与 ExecuteTriggerScan 相同的隐患：E_WorkSpace 枚举值与 0 比较，依赖枚举底层 int 值
            // 若 E_WorkSpace.工位1 的 int 值不为 0，此处会错配
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

        /// <summary>
        /// 50ms 定时器回调：批量读取三轴 IO 状态并刷新绑定属性，驱动 UI 实时显示位置、限位和报警信号。
        /// <para>位置取整为 int（um 精度）以减少不必要的属性刷新。</para>
        /// <para>每个 IO 字段用 ?? false 兜底，避免 AxisIOStatus 为 null 时的 NullReferenceException。</para>
        /// <para>末尾批量 RaisePropertyChanged Mini* 派生属性，使迷你模式 UI 也同步刷新。</para>
        /// </summary>
        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            // 视觉 X 轴：6 个 IO 状态 + 当前位置
            if (_axisX != null)
            {
                var io = _axisX.AxisIOStatus;
                XAxisCurrentPosition = (int)(_axisX.CurrentPosition ?? 0);
                XAxisIsEnabled = io?.SVO ?? false;
                XAxisIsMoving = io?.Moving ?? false;
                XAxisIsHomed = io?.HomeDone ?? false;
                XAxisIsPosLimit = io?.PEL ?? false;
                XAxisIsORG = io?.ORG ?? false;
                XAxisIsNegLimit = io?.MEL ?? false;
                XAxisIsAlarm = io?.ALM ?? false;
            }
            // 视觉 Y 轴
            if (_axisY != null)
            {
                var io = _axisY.AxisIOStatus;
                YAxisCurrentPosition = (int)(_axisY.CurrentPosition ?? 0);
                YAxisIsEnabled = io?.SVO ?? false;
                YAxisIsMoving = io?.Moving ?? false;
                YAxisIsHomed = io?.HomeDone ?? false;
                YAxisIsPosLimit = io?.PEL ?? false;
                YAxisIsORG = io?.ORG ?? false;
                YAxisIsNegLimit = io?.MEL ?? false;
                YAxisIsAlarm = io?.ALM ?? false;
            }
            // 视觉 Z 轴
            if (_axisZ != null)
            {
                var io = _axisZ.AxisIOStatus;
                ZAxisCurrentPosition = (int)(_axisZ.CurrentPosition ?? 0);
                ZAxisIsEnabled = io?.SVO ?? false;
                ZAxisIsMoving = io?.Moving ?? false;
                ZAxisIsHomed = io?.HomeDone ?? false;
                ZAxisIsPosLimit = io?.PEL ?? false;
                ZAxisIsORG = io?.ORG ?? false;
                ZAxisIsNegLimit = io?.MEL ?? false;
                ZAxisIsAlarm = io?.ALM ?? false;
            }

            // 迷你模式派生属性需在主属性更新后显式通知，否则当前页停留时 UI 不刷新
            RaisePropertyChanged(nameof(MiniAxisPosition));
            RaisePropertyChanged(nameof(MiniAxisIsEnabled));
            RaisePropertyChanged(nameof(MiniAxisIsMoving));
            RaisePropertyChanged(nameof(MiniAxisIsHomed));
            RaisePropertyChanged(nameof(MiniAxisIsPosLimit));
            RaisePropertyChanged(nameof(MiniAxisIsORG));
            RaisePropertyChanged(nameof(MiniAxisIsNegLimit));
            RaisePropertyChanged(nameof(MiniAxisIsAlarm));
        }

        #endregion
    }
}