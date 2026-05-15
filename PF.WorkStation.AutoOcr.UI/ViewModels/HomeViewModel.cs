using PF.Core.Enums;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.Station;
using PF.Core.Interfaces.Sync;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr;
using PF.WorkStation.AutoOcr.CostParam;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.Stations;
using PF.WorkStation.AutoOcr.UI.UserControls;
using Prism.Events;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// HomeViewModel — 操作员主界面，集成设备总控按钮与工位1/2操作面板
    /// </summary>
    public class HomeViewModel : RegionViewModelBase
    {
        private readonly WSDataModule? _dataModule;
        private readonly IUserService _userService;
        private readonly IRecipeService<OCRRecipeParam> _recipeService;
        private readonly IMasterController _controller;
        private readonly DispatcherTimer _pollTimer;
        private readonly IEventAggregator _eventAggregator;
        private readonly IStationSyncService _sync;
        private readonly IHardwareInputMonitor? _hardwareInputMonitor;

        private readonly IParamService _paramService;

        #region 重初始化提醒

        private bool _needsReinitialize;
        /// <summary>工位屏蔽参数已变更，需要重新初始化设备才能生效</summary>
        public bool NeedsReinitialize
        {
            get => _needsReinitialize;
            private set => SetProperty(ref _needsReinitialize, value);
        }

        #endregion

        #region 设备总控状态

        private static readonly Dictionary<MachineState, Brush> _stateBrushMap = new()
        {
            { MachineState.Running,      new SolidColorBrush(Color.FromRgb(0x02, 0xad, 0x8b)) },
            { MachineState.Paused,       new SolidColorBrush(Color.FromRgb(0xe9, 0xaf, 0x20)) },
            { MachineState.InitAlarm,    new SolidColorBrush(Color.FromRgb(0xff, 0x8f, 0x00)) },
            { MachineState.RunAlarm,     new SolidColorBrush(Color.FromRgb(0xdb, 0x33, 0x40)) },
            { MachineState.Initializing, new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
            { MachineState.Resetting,    new SolidColorBrush(Color.FromRgb(0x00, 0xbc, 0xd4)) },
            { MachineState.Idle,         new SolidColorBrush(Color.FromRgb(0x32, 0x6c, 0xf3)) },
        };

        private static readonly Brush _defaultBrush = new SolidColorBrush(Color.FromRgb(0x75, 0x75, 0x75));

        private MachineState _currentState = MachineState.Uninitialized;
        /// <summary>获取设备当前运行状态</summary>
        public MachineState CurrentState
        {
            get => _currentState;
            private set
            {
                SetProperty(ref _currentState, value);
                RaisePropertyChanged(nameof(StatusBrush));
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(SmartStartText));
            }
        }

        /// <summary>获取设备状态对应的颜色画刷</summary>
        public Brush StatusBrush =>
            _stateBrushMap.TryGetValue(_currentState, out var b) ? b : _defaultBrush;

        /// <summary>获取设备状态的中文描述</summary>
        public string StatusText => _currentState switch
        {
            MachineState.Uninitialized => "未初始化",
            MachineState.Initializing => "初始化中",
            MachineState.Idle => "待机",
            MachineState.Running => "运行中",
            MachineState.Paused => "已暂停",
            MachineState.InitAlarm => "初始化报警",
            MachineState.RunAlarm => "运行报警",
            MachineState.Resetting => "复位中",
            _ => "未知"
        };

        /// <summary>获取 SmartStart 按钮的动态文本</summary>
        public string SmartStartText => _currentState == MachineState.Paused ? "▶ 恢复" : "▶ 启动";

        #endregion

        #region 工位 1/2 派生属性

        private string _station1InternalBatches = "NONE";
        /// <summary>获取或设置工位1内批号</summary>
        public string Station1InternalBatches
        {
            get => _station1InternalBatches;
            set => SetProperty(ref _station1InternalBatches, value);
        }

        private E_DetectionStatus _station1DetStatus = E_DetectionStatus.检测中;
        /// <summary>获取或设置工位1检测状态</summary>
        public E_DetectionStatus Station1DetStatus
        {
            get => _station1DetStatus;
            set => SetProperty(ref _station1DetStatus, value);
        }

        private E_DetectionStatus _station2DetStatus = E_DetectionStatus.检测中;
        /// <summary>获取或设置工位2检测状态</summary>
        public E_DetectionStatus Station2DetStatus
        {
            get => _station2DetStatus;
            set => SetProperty(ref _station2DetStatus, value);
        }

        private string _station2InternalBatches = "NONE";
        /// <summary>获取或设置工位2内批号</summary>
        public string Station2InternalBatches
        {
            get => _station2InternalBatches;
            set => SetProperty(ref _station2InternalBatches, value);
        }

        private bool _station1UnloadMaskVisible;
        /// <summary>工位1下料确认遮罩层是否可见（操作员下料通知）</summary>
        public bool Station1UnloadMaskVisible
        {
            get => _station1UnloadMaskVisible;
            set => SetProperty(ref _station1UnloadMaskVisible, value);
        }

        private bool _station2UnloadMaskVisible;
        /// <summary>工位2下料确认遮罩层是否可见（操作员下料通知）</summary>
        public bool Station2UnloadMaskVisible
        {
            get => _station2UnloadMaskVisible;
            set => SetProperty(ref _station2UnloadMaskVisible, value);
        }

        // ── OCR 比对异常遮罩 ─────────────────────────────────────────

        private bool _station1OcrMismatchVisible;
        /// <summary>工位1 OCR比对异常遮罩层是否可见</summary>
        public bool Station1OcrMismatchVisible
        {
            get => _station1OcrMismatchVisible;
            private set => SetProperty(ref _station1OcrMismatchVisible, value);
        }

        private bool _station2OcrMismatchVisible;
        /// <summary>工位2 OCR比对异常遮罩层是否可见</summary>
        public bool Station2OcrMismatchVisible
        {
            get => _station2OcrMismatchVisible;
            private set => SetProperty(ref _station2OcrMismatchVisible, value);
        }

        private string _ocrMismatchOcrText = string.Empty;
        /// <summary>当前比对失败的 OCR 文本（遮罩展示用）</summary>
        public string OcrMismatchOcrText
        {
            get => _ocrMismatchOcrText;
            private set => SetProperty(ref _ocrMismatchOcrText, value);
        }

        private string _ocrMismatchInternalBatchId = string.Empty;
        /// <summary>当前比对失败所属的内部批次号</summary>
        public string OcrMismatchInternalBatchId
        {
            get => _ocrMismatchInternalBatchId;
            private set => SetProperty(ref _ocrMismatchInternalBatchId, value);
        }

        private string _manualOcrText = string.Empty;
        /// <summary>操作员手动输入的 OCR 值（双向绑定）</summary>
        public string ManualOcrText
        {
            get => _manualOcrText;
            set
            {
                SetProperty(ref _manualOcrText, value);
                OcrConfirmManualCommand?.RaiseCanExecuteChanged();
            }
        }

        private OcrMismatchOverlayState _ocrMismatchState = OcrMismatchOverlayState.Idle;
        /// <summary>遮罩显示初始选择面板（重试/手动输入）</summary>
        public bool IsOcrMismatchIdle          => _ocrMismatchState == OcrMismatchOverlayState.Idle;
        /// <summary>遮罩显示权限不足提示</summary>
        public bool IsOcrMismatchManualDenied  => _ocrMismatchState == OcrMismatchOverlayState.ManualDenied;
        /// <summary>遮罩显示手动输入面板</summary>
        public bool IsOcrMismatchManualAllowed => _ocrMismatchState == OcrMismatchOverlayState.ManualAllowed;

        private OcrMismatchPayload? _ocrMismatchPayload;

        // ── 安全门三态指示 ──────────────────────────────────────────

        private static readonly Brush _doorActiveBrush  = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50)); // 绿
        private static readonly Brush _doorStoppedBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xC1, 0x07)); // 黄
        private static readonly Brush _doorMutedBrush   = new SolidColorBrush(Color.FromRgb(0x9E, 0x9E, 0x9E)); // 灰

        private SafetyDoorMonitorState _door1State = SafetyDoorMonitorState.Stopped;
        private SafetyDoorMonitorState _door2State = SafetyDoorMonitorState.Stopped;

        /// <summary>工位1安全门指示灯颜色（绿=检测中 黄=已停止 灰=屏蔽）</summary>
        public Brush Door1StatusBrush => StateToBrush(_door1State);
        /// <summary>工位1安全门状态文字</summary>
        public string Door1StatusText => StateToText(_door1State);

        /// <summary>工位2安全门指示灯颜色（绿=检测中 黄=已停止 灰=屏蔽）</summary>
        public Brush Door2StatusBrush => StateToBrush(_door2State);
        /// <summary>工位2安全门状态文字</summary>
        public string Door2StatusText => StateToText(_door2State);

        private static Brush StateToBrush(SafetyDoorMonitorState s) => s switch
        {
            SafetyDoorMonitorState.Active  => _doorActiveBrush,
            SafetyDoorMonitorState.Stopped => _doorStoppedBrush,
            _                              => _doorMutedBrush
        };

        private static string StateToText(SafetyDoorMonitorState s) => s switch
        {
            SafetyDoorMonitorState.Active  => "安全门: 检测中",
            SafetyDoorMonitorState.Stopped => "安全门: 已停止",
            _                              => "安全门: 屏蔽"
        };

        private static SafetyDoorMonitorState ComputeDoorState(SafetyDoorState door, bool threadRunning)
        {
            if (door.IsMuted) return SafetyDoorMonitorState.Muted;
            if (!threadRunning || !door.IsEnabled) return SafetyDoorMonitorState.Stopped;
            return SafetyDoorMonitorState.Active;
        }

        private void SetDoor1State(SafetyDoorMonitorState s)
        {
            if (_door1State == s) return;
            _door1State = s;
            RaisePropertyChanged(nameof(Door1StatusBrush));
            RaisePropertyChanged(nameof(Door1StatusText));
        }

        private void SetDoor2State(SafetyDoorMonitorState s)
        {
            if (_door2State == s) return;
            _door2State = s;
            RaisePropertyChanged(nameof(Door2StatusBrush));
            RaisePropertyChanged(nameof(Door2StatusText));
        }

        private string _station1RecipeName = "NONE";
        /// <summary>获取或设置工位1程式名称</summary>
        public string Station1RecipeName
        {
            get => _station1RecipeName;
            set => SetProperty(ref _station1RecipeName, value);
        }

        private string _station2RecipeName = "NONE";
        /// <summary>获取或设置工位2程式名称</summary>
        public string Station2RecipeName
        {
            get => _station2RecipeName;
            set => SetProperty(ref _station2RecipeName, value);
        }

        #endregion

        #region 清除机台记忆

        /// <summary>工位复合清除记忆选择项（工位一 / 工位二）</summary>
        public ObservableCollection<WorkstationMemoryGroup> StationMemoryItems { get; }

        private bool _suppressSelectAllUpdate;
        private bool? _isSelectAllMemoryChecked = false;
        /// <summary>全选/全不选控制（null 表示部分选中）</summary>
        public bool? IsSelectAllMemoryChecked
        {
            get => _isSelectAllMemoryChecked;
            set
            {
                if (value == null) { SetProperty(ref _isSelectAllMemoryChecked, null); return; }
                _suppressSelectAllUpdate = true;
                SetProperty(ref _isSelectAllMemoryChecked, value);
                foreach (var item in StationMemoryItems)
                    item.IsChecked = value == true;
                _suppressSelectAllUpdate = false;
            }
        }

        /// <summary>有清除记忆权限</summary>
        public bool HasClearMemoryPermission => _userService.IsAuthorized(UserLevel.Administrator);

        private void OnStationMemoryItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_suppressSelectAllUpdate || e.PropertyName != nameof(WorkstationMemoryGroup.IsChecked)) return;
            bool allChecked  = StationMemoryItems.All(x => x.IsChecked);
            bool noneChecked = StationMemoryItems.All(x => !x.IsChecked);
            _isSelectAllMemoryChecked = allChecked ? true : noneChecked ? false : (bool?)null;
            RaisePropertyChanged(nameof(IsSelectAllMemoryChecked));
        }

        #endregion

        #region 数据集合

        private ObservableCollection<WaferSlotInfo> _station1SlotStatesDisplay = new();
        /// <summary>工位1晶圆盒槽位状态（倒序：索引12在顶，索引0在底）</summary>
        public ObservableCollection<WaferSlotInfo> Station1SlotStatesDisplay
        {
            get => _station1SlotStatesDisplay;
            private set => SetProperty(ref _station1SlotStatesDisplay, value);
        }

        private ObservableCollection<WaferSlotInfo> _station2SlotStatesDisplay = new();
        /// <summary>工位2晶圆盒槽位状态（倒序：索引12在顶，索引0在底）</summary>
        public ObservableCollection<WaferSlotInfo> Station2SlotStatesDisplay
        {
            get => _station2SlotStatesDisplay;
            private set => SetProperty(ref _station2SlotStatesDisplay, value);
        }

        private bool _station1IsMuted;
        /// <summary>工位1是否处于屏蔽模式</summary>
        public bool Station1IsMuted
        {
            get => _station1IsMuted;
            private set { if (SetProperty(ref _station1IsMuted, value)) RaisePropertyChanged(nameof(Station1IsEnabled)); }
        }
        /// <summary>工位1 UI 卡片是否可操作（屏蔽时禁用）</summary>
        public bool Station1IsEnabled => !_station1IsMuted;

        private bool _station2IsMuted;
        /// <summary>工位2是否处于屏蔽模式</summary>
        public bool Station2IsMuted
        {
            get => _station2IsMuted;
            private set { if (SetProperty(ref _station2IsMuted, value)) RaisePropertyChanged(nameof(Station2IsEnabled)); }
        }
        /// <summary>工位2 UI 卡片是否可操作（屏蔽时禁用）</summary>
        public bool Station2IsEnabled => !_station2IsMuted;

        private ObservableCollection<MachineDetectionData> _station1MachineDetection = [];
        /// <summary>获取或设置工位1检测数据集合</summary>
        public ObservableCollection<MachineDetectionData> Station1MachineDetection
        {
            get => _station1MachineDetection;
            set => SetProperty(ref _station1MachineDetection, value);
        }

        private ObservableCollection<MachineDetectionData> _station2MachineDetection = [];
        /// <summary>获取或设置工位2检测数据集合</summary>
        public ObservableCollection<MachineDetectionData> Station2MachineDetection
        {
            get => _station2MachineDetection;
            set => SetProperty(ref _station2MachineDetection, value);
        }

        private MachineDetectionData _station1CurrentMachineDetection;
        /// <summary>获取或设置工位1最新检测数据</summary>
        public MachineDetectionData Station1CurrentMachineDetection
        {
            get => _station1CurrentMachineDetection;
            set => SetProperty(ref _station1CurrentMachineDetection, value);
        }

        private MachineDetectionData _station2CurrentMachineDetection;
        /// <summary>获取或设置工位2最新检测数据</summary>
        public MachineDetectionData Station2CurrentMachineDetection
        {
            get => _station2CurrentMachineDetection;
            set => SetProperty(ref _station2CurrentMachineDetection, value);
        }

        #endregion

        #region Command

        /// <summary>触发全线初始化的命令</summary>
        public DelegateCommand InitializeCommand { get; }

        /// <summary>智能启动命令：Idle 时启动，Paused 时恢复</summary>
        public DelegateCommand SmartStartCommand { get; }

        /// <summary>触发全线暂停的命令</summary>
        public DelegateCommand PauseCommand { get; }

        /// <summary>触发全线停止的命令</summary>
        public DelegateCommand StopCommand { get; }

        /// <summary>触发全线复位的命令</summary>
        public DelegateCommand ResetCommand { get; }

        /// <summary>工位1切换批次命令</summary>
        public DelegateCommand Station1ChangeLotCommand { get; }

        /// <summary>工位2切换批次命令</summary>
        public DelegateCommand Station2ChangeLotCommand { get; }

        /// <summary>工位1下料确认命令（遮罩层确认按钮）</summary>
        public DelegateCommand Station1UnloadConfirmCommand { get; }
        /// <summary>工位2下料确认命令（遮罩层确认按钮）</summary>
        public DelegateCommand Station2UnloadConfirmCommand { get; }

        /// <summary>工位1槽位详情查看命令（检测完毕后可点击）</summary>
        public DelegateCommand<WaferSlotInfo> Station1ViewSlotDetailCommand { get; }
        /// <summary>工位2槽位详情查看命令（检测完毕后可点击）</summary>
        public DelegateCommand<WaferSlotInfo> Station2ViewSlotDetailCommand { get; }

        /// <summary>OCR比对异常遮罩：重试（相机回检测位重拍）</summary>
        public DelegateCommand OcrRetryCommand { get; }
        /// <summary>OCR比对异常遮罩：进入手动输入流程</summary>
        public DelegateCommand OcrStartManualCommand { get; }
        /// <summary>OCR比对异常遮罩：确认手动输入（ManualOcrText 非空时可执行）</summary>
        public DelegateCommand OcrConfirmManualCommand { get; }
        /// <summary>OCR比对异常遮罩：返回初始选择面板</summary>
        public DelegateCommand OcrBackCommand { get; }

        #endregion

        /// <summary>初始化 HomeViewModel</summary>
        public HomeViewModel(IContainerProvider containerProvider, IUserService userService, IMasterController controller,IParamService paramService )
        {
            _paramService = paramService;
            _controller = controller;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WSDataModule)) as WSDataModule;
            _userService = userService;
            _recipeService = containerProvider.Resolve<IRecipeService<OCRRecipeParam>>();
            _eventAggregator = containerProvider.Resolve<IEventAggregator>();
            _sync = containerProvider.Resolve<IStationSyncService>();
            _hardwareInputMonitor = containerProvider.Resolve<IHardwareInputMonitor>();

            // 订阅操作员下料请求事件：显示下料确认遮罩，操作员确认后释放同步信号
            _eventAggregator.GetEvent<OperatorUnloadRequestedEvent>()
                .Subscribe(OnOperatorUnloadRequested, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);

            // 订阅 OCR 比对异常事件：显示比对异常遮罩，等待操作员决策后通过 TCS 回传结果给站线程
            _eventAggregator.GetEvent<OcrMismatchRequestedEvent>()
                .Subscribe(OnOcrMismatchRequested, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);

            // 订阅屏蔽参数变更通知：显示横幅提示用户需重新初始化
            _eventAggregator.GetEvent<ReinitializeRequiredEvent>()
                .Subscribe(() => NeedsReinitialize = true, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);

            // 同步控制器当前重初始化标记（应用启动时可能已有残留状态）
            NeedsReinitialize = _controller.IsReinitializationRequired;

            StationMemoryItems = new ObservableCollection<WorkstationMemoryGroup>
            {
                new WorkstationMemoryGroup("工位一", new[]
                {
                    E_WorkStation.工位1上下料工站.ToString(),
                    E_WorkStation.工位1拉料工站.ToString()
                }),
                new WorkstationMemoryGroup("工位二", new[]
                {
                    E_WorkStation.工位2上下料工站.ToString(),
                    E_WorkStation.工位2拉料工站.ToString()
                }),
            };
            foreach (var item in StationMemoryItems)
                item.PropertyChanged += OnStationMemoryItemChanged;

            // 订阅用户变更事件以刷新权限
            _userService.CurrentUserChanged += (s, e) => RaisePropertyChanged(nameof(HasClearMemoryPermission));

            // 设备总控命令
            InitializeCommand = new DelegateCommand(
                async () =>
                {
                    try
                    {
                        var selectedItems = StationMemoryItems.Where(x => x.IsChecked).ToList();
                        if (selectedItems.Count > 0)
                        {
                            try
                            {
                                foreach (var group in selectedItems)
                                {
                                    foreach (var stationName in group.StationNames)
                                        _controller.ClearStationMemory(stationName);
                                    group.IsChecked = false;
                                }
                            }
                            catch (Exception ex)
                            {
                                MessageService.ShowMessage($"清除机台记忆失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        await _controller.InitializeAllAsync();
                        if (_controller.CurrentState == MachineState.Idle)
                            NeedsReinitialize = false;
                    }
                    catch { }
                },
                () => _controller.CurrentState == MachineState.Uninitialized
                   || _controller.CurrentState == MachineState.Idle);

            SmartStartCommand = new DelegateCommand(
                async () => { try { await SmartStartAsync(); } catch { } },
                () => _controller.CurrentState == MachineState.Idle
                   || _controller.CurrentState == MachineState.Paused);

            PauseCommand = new DelegateCommand(
                () => { try { _controller.PauseAll(); } catch { } },
                () => _controller.CurrentState == MachineState.Running);

            StopCommand = new DelegateCommand(
                async () => { try { await _controller.StopAllAsync(); } catch { } },
                () => _controller.CurrentState == MachineState.Idle
                   || _controller.CurrentState == MachineState.Running
                   || _controller.CurrentState == MachineState.Paused
                   || _controller.CurrentState == MachineState.Initializing);

            ResetCommand = new DelegateCommand(
                async () => { try { await _controller.ResetAllAsync(); } catch { } },
                () => _controller.CurrentState == MachineState.InitAlarm
                   || _controller.CurrentState == MachineState.RunAlarm);

            // 工位操作命令
            Station1ChangeLotCommand = new DelegateCommand(Station1ShowChangeLotView);
            Station2ChangeLotCommand = new DelegateCommand(Station2ShowChangeLotView);
            Station1UnloadConfirmCommand = new DelegateCommand(OnStation1UnloadConfirm);
            Station2UnloadConfirmCommand = new DelegateCommand(OnStation2UnloadConfirm);

            Station1ViewSlotDetailCommand = new DelegateCommand<WaferSlotInfo>(ShowSlotDetail);
            Station2ViewSlotDetailCommand = new DelegateCommand<WaferSlotInfo>(ShowSlotDetail);

            OcrRetryCommand         = new DelegateCommand(OnOcrRetry);
            OcrStartManualCommand   = new DelegateCommand(OnOcrStartManual);
            OcrConfirmManualCommand = new DelegateCommand(OnOcrConfirmManual, () => !string.IsNullOrWhiteSpace(_manualOcrText));
            OcrBackCommand          = new DelegateCommand(() => SetOcrMismatchState(OcrMismatchOverlayState.Idle));

            // 订阅数据模块事件
            if (_dataModule != null)
            {
                _dataModule.DataChanged += async (s, e) =>
                {
                    try { await RefreshAllAsync(); }
                    catch { }
                };
            }
            RefreshAllAsync();

            // 状态轮询定时器
            _pollTimer = new DispatcherTimer(DispatcherPriority.DataBind)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _pollTimer.Tick += OnPollTick;
            _pollTimer.Start();
        }

        /// <summary>
        /// 智能启动：根据当前状态自动选择启动或恢复
        /// </summary>
        private async Task SmartStartAsync()
        {
            if (_controller.CurrentState == MachineState.Paused)
                await _controller.ResumeAllAsync();
            else if (_controller.CurrentState == MachineState.Idle)
                await _controller.StartAllAsync();
        }

        private void OnPollTick(object? sender, EventArgs e)
        {
            CurrentState = _controller.CurrentState;
            InitializeCommand.RaiseCanExecuteChanged();
            SmartStartCommand.RaiseCanExecuteChanged();
            PauseCommand.RaiseCanExecuteChanged();
            StopCommand.RaiseCanExecuteChanged();
            ResetCommand.RaiseCanExecuteChanged();

            if (_hardwareInputMonitor != null)
            {
                bool threadRunning = _hardwareInputMonitor.IsSafetyMonitoringRunning;
                var doors = _hardwareInputMonitor.GetSafetyDoorSnapshot();
                foreach (var door in doors)
                {
                    if (door.Name == nameof(E_InPutName.工位1门锁))
                        SetDoor1State(ComputeDoorState(door, threadRunning));
                    else if (door.Name == nameof(E_InPutName.工位2门锁))
                        SetDoor2State(ComputeDoorState(door, threadRunning));
                }
            }
        }

        /// <summary>从 WSDataModule 更新 UI 派生字段</summary>
        private async Task RefreshAllAsync()
        {
            if (_dataModule == null)
                return;

            Station1SlotStatesDisplay = new ObservableCollection<WaferSlotInfo>(
                _dataModule.Station1SlotStates.OrderByDescending(s => s.SlotIndex));
            Station2SlotStatesDisplay = new ObservableCollection<WaferSlotInfo>(
                _dataModule.Station2SlotStates.OrderByDescending(s => s.SlotIndex));

            Station1MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation1MachineDetectionData);
            Station2MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation2MachineDetectionData);

            Station2InternalBatches = _dataModule.Station2MesDetectionData?.InternalBatchId ?? string.Empty;
            Station1InternalBatches = _dataModule.Station1MesDetectionData?.InternalBatchId ?? string.Empty;
            Station1RecipeName = _dataModule.Station1MesDetectionData.RecipeName;
            Station2RecipeName = _dataModule.Station2MesDetectionData.RecipeName;
            Station1DetStatus = _dataModule.Station1MesDetectionData.DetectionStatus;
            Station2DetStatus = _dataModule.Station2MesDetectionData.DetectionStatus;

            if (Station1MachineDetection.Count != 0)
            {
                var latest = Station1MachineDetection.OrderByDescending(d => d.Time).FirstOrDefault();
                if (latest != null) Station1CurrentMachineDetection = latest;
            }

            if (Station2MachineDetection.Count != 0)
            {
                var latest = Station2MachineDetection.OrderByDescending(d => d.Time).FirstOrDefault();
                if (latest != null) Station2CurrentMachineDetection = latest;
            }

            Station1IsMuted = await _paramService.GetParamAsync<bool>(E_Params.WorkStation1_Muted.ToString(), false);
            Station2IsMuted = await _paramService.GetParamAsync<bool>(E_Params.WorkStation2_Muted.ToString(), false);
        }

        #region 切换批次

        private void Station1ShowChangeLotView()
        {
            var initParams = new DialogParameters
            {
                { "InitialLayerMode",       _dataModule?.GetLayerMode(E_WorkSpace.工位1) ?? E_LayerProcessMode.全做 },
                { "InitialSpecifiedLayers", _dataModule?.GetSpecifiedLayers(E_WorkSpace.工位1) ?? new System.Collections.Generic.List<int>() }
            };
            DialogService.ShowDialog(nameof(ChangeLotView), initParams, OnDialogCallbackStation1);
        }

        private void Station2ShowChangeLotView()
        {
            var initParams = new DialogParameters
            {
                { "InitialLayerMode",       _dataModule?.GetLayerMode(E_WorkSpace.工位2) ?? E_LayerProcessMode.全做 },
                { "InitialSpecifiedLayers", _dataModule?.GetSpecifiedLayers(E_WorkSpace.工位2) ?? new System.Collections.Generic.List<int>() }
            };
            DialogService.ShowDialog(nameof(ChangeLotView), initParams, OnDialogCallbackStation2);
        }

        private async void OnDialogCallbackStation1(IDialogResult result)
        {
            if (result.Result == ButtonResult.OK)
            {
                if (result.Parameters is DialogParameters param && param.ContainsKey("Lotid") && param.ContainsKey("Userid"))
                {
                    string Userid = param.GetValue<string>("Userid");
                    string lotid = param.GetValue<string>("Lotid");
                    if ((await _userService.GetUserListAsync()).ToList().FindIndex(x => x.UserName == Userid) == -1)
                    {
                        MessageService.ShowMessage($"{Userid}用户不存在 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var mesResult = await _dataModule?.QueryMesAsync(lotid, Userid);
                    if (mesResult == null || !mesResult.IsSuccess)
                    {
                        MessageService.ShowMessage($"{lotid}获取检测数据错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var info = mesResult.Data;

                    if (!(await _dataModule.UpdateStationMesInfoAsync(E_WorkSpace.工位1, info)).IsSuccess)
                    {
                        MessageService.ShowMessage($"工位1切换批次失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var layerMode1 = param.ContainsKey("LayerMode")
                        ? param.GetValue<E_LayerProcessMode>("LayerMode") : E_LayerProcessMode.全做;
                    var specifiedLayers1 = param.ContainsKey("SpecifiedLayers")
                        ? param.GetValue<System.Collections.Generic.List<int>>("SpecifiedLayers") : new System.Collections.Generic.List<int>();
                    _dataModule?.SetLayerMode(E_WorkSpace.工位1, layerMode1, specifiedLayers1);

                    if (_userService.IsAuthorized(UserLevel.SuperUser)
                        && param.ContainsKey("Recipe"))
                    {
                        string recipeName = param.GetValue<string>("Recipe");
                        if (!string.IsNullOrEmpty(recipeName))
                        {
                            var kk = await _recipeService.RecipeParam(recipeName);
                            if (kk == null)
                            {
                                MessageService.ShowMessage($"获取配方参数失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            if (!_dataModule.UpdateStationRecipeParam(E_WorkSpace.工位1, kk).IsSuccess)
                            {
                                MessageService.ShowMessage($"配方切换失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            Station1RecipeName = recipeName;
                        }
                    }

                    MessageService.ShowMessage($"工位1切换批次成功 ", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageService.ShowMessage($"参数传递错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void OnDialogCallbackStation2(IDialogResult result)
        {
            if (result.Result == ButtonResult.OK)
            {
                if (result.Parameters is DialogParameters param && param.ContainsKey("Lotid") && param.ContainsKey("Userid"))
                {
                    string Userid = param.GetValue<string>("Userid");
                    string lotid = param.GetValue<string>("Lotid");
                    if ((await _userService.GetUserListAsync()).ToList().FindIndex(x => x.UserName == Userid) == -1)
                    {
                        MessageService.ShowMessage($"{Userid}用户不存在 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var mesResult = await _dataModule?.QueryMesAsync(lotid, Userid);
                    if (mesResult == null || !mesResult.IsSuccess)
                    {
                        MessageService.ShowMessage($"{lotid}获取检测数据错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var info = mesResult.Data;



                    if (!(await _dataModule.UpdateStationMesInfoAsync(E_WorkSpace.工位2, info)).IsSuccess)
                    {
                        MessageService.ShowMessage($"工位2切换批次失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var layerMode2 = param.ContainsKey("LayerMode")
                        ? param.GetValue<E_LayerProcessMode>("LayerMode") : E_LayerProcessMode.全做;
                    var specifiedLayers2 = param.ContainsKey("SpecifiedLayers")
                        ? param.GetValue<System.Collections.Generic.List<int>>("SpecifiedLayers") : new System.Collections.Generic.List<int>();
                    _dataModule?.SetLayerMode(E_WorkSpace.工位2, layerMode2, specifiedLayers2);

                    if (_userService.IsAuthorized(UserLevel.SuperUser)
                        && param.ContainsKey("Recipe"))
                    {
                        string recipeName = param.GetValue<string>("Recipe");
                        if (!string.IsNullOrEmpty(recipeName))
                        {
                            var kk = await _recipeService.RecipeParam(recipeName);
                            if (kk == null)
                            {
                                MessageService.ShowMessage($"获取配方参数失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            if (!_dataModule.UpdateStationRecipeParam(E_WorkSpace.工位2, kk).IsSuccess)
                            {
                                MessageService.ShowMessage($"配方切换失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                            Station2RecipeName = recipeName;
                        }
                    }


                    MessageService.ShowMessage($"工位2切换批次成功 ", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                }
                else
                {
                    MessageService.ShowMessage($"参数传递错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #endregion

        private void ShowSlotDetail(WaferSlotInfo slot)
        {
            if (slot?.DetectionData == null) return;
            var param = new DialogParameters { { "SlotInfo", slot } };
            DialogService.ShowDialog(nameof(WaferSlotDetailView), param, _ => { });
        }

        /// <summary>
        /// 操作员下料请求处理：显示工位1遮罩层，操作员点击确认后释放同步信号
        /// </summary>
        private void OnOperatorUnloadRequested(string workspace)
        {
            if (workspace == "工位1")
                Station1UnloadMaskVisible = true;
            else if (workspace == "工位2")
                Station2UnloadMaskVisible = true;
        }

        /// <summary>
        /// 工位1下料确认：隐藏遮罩层，释放工位1人工下料完成信号
        /// </summary>
        private void OnStation1UnloadConfirm()
        {
            Station1UnloadMaskVisible = false;
            _sync.Release(nameof(WorkstationSignals.工位1人工下料完成), scope: E_WorkStation.工位1上下料工站.ToString());
        }

        /// <summary>
        /// 工位2下料确认：隐藏遮罩层，释放工位2人工下料完成信号
        /// </summary>
        private void OnStation2UnloadConfirm()
        {
            Station2UnloadMaskVisible = false;
            _sync.Release(nameof(WorkstationSignals.工位2人工下料完成), scope: E_WorkStation.工位2上下料工站.ToString());
        }

        #region OCR 比对异常遮罩处理

        private void OnOcrMismatchRequested(OcrMismatchPayload payload)
        {
            _ocrMismatchPayload        = payload;
            OcrMismatchOcrText         = payload.OcrText;
            OcrMismatchInternalBatchId = payload.InternalBatchId;
            ManualOcrText              = payload.OcrText;   // 预填当前失败文本，方便操作员修正
            SetOcrMismatchState(OcrMismatchOverlayState.Idle);

            if (payload.WorkSpaceName.Contains("工位1"))
                Station1OcrMismatchVisible = true;
            else
                Station2OcrMismatchVisible = true;

            // 站线程取消（停止/急停）时自动收起遮罩，防止孤立 UI
            payload.StationToken.Register(() =>
                Application.Current.Dispatcher.InvokeAsync(HideOcrMismatchOverlay));
        }

        private void OnOcrRetry()
        {
            _ocrMismatchPayload?.Tcs.TrySetResult(new OcrMismatchResult { Action = OcrMismatchAction.Retry });
            HideOcrMismatchOverlay();
        }

        private void OnOcrStartManual()
        {
            SetOcrMismatchState(_userService.IsAuthorized(UserLevel.Engineer)
                ? OcrMismatchOverlayState.ManualAllowed
                : OcrMismatchOverlayState.ManualDenied);
        }

        private void OnOcrConfirmManual()
        {
            _ocrMismatchPayload?.Tcs.TrySetResult(new OcrMismatchResult
            {
                Action        = OcrMismatchAction.ManualInput,
                ManualOcrText = ManualOcrText.Trim()
            });
            HideOcrMismatchOverlay();
        }

        private void HideOcrMismatchOverlay()
        {
            Station1OcrMismatchVisible = false;
            Station2OcrMismatchVisible = false;
            _ocrMismatchPayload        = null;
            SetOcrMismatchState(OcrMismatchOverlayState.Idle);
        }

        private void SetOcrMismatchState(OcrMismatchOverlayState state)
        {
            _ocrMismatchState = state;
            RaisePropertyChanged(nameof(IsOcrMismatchIdle));
            RaisePropertyChanged(nameof(IsOcrMismatchManualDenied));
            RaisePropertyChanged(nameof(IsOcrMismatchManualAllowed));
        }

        private enum OcrMismatchOverlayState { Idle, ManualDenied, ManualAllowed }

        #endregion

        /// <summary>
        /// 重写基类方法，在 ViewModel 销毁时停止定时器
        /// </summary>
        public override void Destroy()
        {
            _pollTimer.Stop();
        }
    }

    /// <summary>安全门运行时监控状态（三态）</summary>
    public enum SafetyDoorMonitorState
    {
        /// <summary>检测中：Safety 线程运行 且 IsEnabled = true 且 IsMuted = false</summary>
        Active,
        /// <summary>已停止：Safety 线程未运行 或 IsEnabled = false（临时屏蔽）且 IsMuted = false</summary>
        Stopped,
        /// <summary>屏蔽：IsMuted = true（数据库持久化屏蔽参数）</summary>
        Muted
    }

    /// <summary>工位复合清除记忆选项：选中时同步清除该工位下所有子工站的记忆。</summary>
    public class WorkstationMemoryGroup : INotifyPropertyChanged
    {
        private bool _isChecked;

        /// <summary>界面显示名称（如"工位一"）</summary>
        public string DisplayName { get; }

        /// <summary>需要清除记忆的工站名称列表（对应 StationBase.StationName）</summary>
        public string[] StationNames { get; }

        /// <summary>是否勾选清除</summary>
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public WorkstationMemoryGroup(string displayName, string[] stationNames)
        {
            DisplayName = displayName;
            StationNames = stationNames;
        }
    }
}
