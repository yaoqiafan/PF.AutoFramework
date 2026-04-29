using PF.Core.Enums;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Recipe;
using PF.Core.Interfaces.Station;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.CostParam;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.UI.UserControls;
using System;
using System.Collections.ObjectModel;
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

        private bool _isClearMemoryChecked;
        /// <summary>
        /// 是否清除机台记忆
        /// </summary>
        public bool IsClearMemoryChecked
        {
            get => _isClearMemoryChecked;
            set => SetProperty(ref _isClearMemoryChecked, value);
        }
        /// <summary>
        /// 有清除记忆权限
        /// </summary>
        public bool HasClearMemoryPermission => _userService.IsAuthorized(UserLevel.Administrator);

        #endregion

        #region 数据集合

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

        #endregion

        /// <summary>初始化 HomeViewModel</summary>
        public HomeViewModel(IContainerProvider containerProvider, IUserService userService, IMasterController controller)
        {
            _controller = controller;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WSDataModule)) as WSDataModule;
            _userService = userService;
            _recipeService = containerProvider.Resolve<IRecipeService<OCRRecipeParam>>();

            // 订阅用户变更事件以刷新权限
            _userService.CurrentUserChanged += (s, e) => RaisePropertyChanged(nameof(HasClearMemoryPermission));

            // 设备总控命令
            InitializeCommand = new DelegateCommand(
                async () =>
                {
                    try
                    {
                        if (IsClearMemoryChecked)
                        {
                            try
                            {
                                _controller.ClearAllStationMemory();
                                IsClearMemoryChecked = false;
                            }
                            catch (Exception ex)
                            {
                                MessageService.ShowMessage($"清除机台记忆失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                                return;
                            }
                        }
                        await _controller.InitializeAllAsync();
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
        }

        /// <summary>从 WSDataModule 更新 UI 派生字段</summary>
        private Task RefreshAllAsync()
        {
            if (_dataModule == null)
                return Task.CompletedTask;

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

            return Task.CompletedTask;
        }

        #region 切换批次

        private void Station1ShowChangeLotView()
        {
            DialogService.ShowDialog(nameof(ChangeLotView), new DialogParameters(), OnDialogCallbackStation1);
        }

        private void Station2ShowChangeLotView()
        {
            DialogService.ShowDialog(nameof(ChangeLotView), new DialogParameters(), OnDialogCallbackStation2);
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

                    var kk = await _recipeService.RecipeParam("New_Recipe_100349");
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

                    var kk = await _recipeService.RecipeParam("New_Recipe_141457");
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


                    MessageService.ShowMessage($"工位2切换批次成功 ", "提示", MessageBoxButton.OK, MessageBoxImage.Information);

                }
                else
                {
                    MessageService.ShowMessage($"参数传递错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        #endregion

        /// <summary>
        /// 重写基类方法，在 ViewModel 销毁时停止定时器
        /// </summary>
        public override void Destroy()
        {
            _pollTimer.Stop();
        }
    }
}
