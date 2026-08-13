using PF.Core.Entities.Hardware.Vision;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Logging;
using PF.Infrastructure.Mechanisms.Vision;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 线扫检测模组调试 ViewModel。
    ///
    /// <para><b>这个面板真正的价值在"边填边算"</b>：线扫的帧长、行频、曝光上限、加减速余量
    /// 是互相牵制的，任何一个填错，症状都只是"图不对"，而原因分别落在轴、相机、光学三处。
    /// 配方每改一次就重算换算结果并跑一次校验，让参数在**按下扫描之前**就能看出是否自洽；
    /// 校验不过时「开始扫描」直接禁用。</para>
    ///
    /// <para>模组实例通过 <see cref="IMechanism"/> 集合按类型筛选获得，而不是按 DryIoc 服务键解析——
    /// 一台设备上可能有多条扫描线，各自是一个 LineScanDetectionModule 实例，
    /// 写死服务键就只能调第一条。</para>
    /// </summary>
    public class LineScanDetectionModuleDebugViewModel : RegionViewModelBase
    {
        private readonly CategoryLogger _logger;
        private readonly DispatcherTimer _statusTimer;

        /// <summary>由 <see cref="Profile"/> 换算出的配方快照，供只读展示属性读取。</summary>
        private ScanProfile _derived = new();

        private CancellationTokenSource? _scanCts;

        /// <summary>初始化线扫检测模组调试 ViewModel</summary>
        public LineScanDetectionModuleDebugViewModel(IEnumerable<IMechanism> mechanisms, ILogService logService)
        {
            _logger = CategoryLoggerFactory.Hardware(logService);

            foreach (var m in mechanisms.OfType<LineScanDetectionModule>())
                Modules.Add(m);

            SelectedModule = Modules.FirstOrDefault();

            // PropertyGrid 改任何一项都要重算派生量与校验，故订阅整个视图对象的属性变更
            Profile.PropertyChanged += OnProfileChanged;

            InitializeCommands();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _statusTimer.Tick += OnStatusTick;
            _statusTimer.Start();

            RefreshDerived();
        }

        #region 【模组选择与状态】

        /// <summary>本机上的全部线扫检测模组。多条扫描线时在此切换。</summary>
        public ObservableCollection<LineScanDetectionModule> Modules { get; } = new();

        private LineScanDetectionModule? _selectedModule;
        /// <summary>当前调试的模组。</summary>
        public LineScanDetectionModule? SelectedModule
        {
            get => _selectedModule;
            set { if (SetProperty(ref _selectedModule, value)) RefreshModuleInfo(); }
        }

        private string _moduleName = "未找到线扫检测模组";
        /// <summary>当前模组名称（顶部状态栏显示）。</summary>
        public string ModuleName { get => _moduleName; set => SetProperty(ref _moduleName, value); }

        private string _moduleInfo = "-";
        /// <summary>模组信息（轴与相机的解析结果）。</summary>
        public string ModuleInfo { get => _moduleInfo; set => SetProperty(ref _moduleInfo, value); }

        private bool _isInitialized;
        /// <summary>模组是否已初始化。</summary>
        public bool IsInitialized { get => _isInitialized; set => SetProperty(ref _isInitialized, value); }

        private bool _hasAlarm;
        /// <summary>模组是否报警。</summary>
        public bool HasAlarm { get => _hasAlarm; set => SetProperty(ref _hasAlarm, value); }

        private string _debugMessage = "就绪";
        /// <summary>最后反馈（顶部状态栏显示，与其他模组调试页一致）。</summary>
        public string DebugMessage { get => _debugMessage; set => SetProperty(ref _debugMessage, value); }

        private bool _isScanning;
        /// <summary>是否正在扫描（扫描期间禁掉重复触发）。</summary>
        public bool IsScanning
        {
            get => _isScanning;
            set { if (SetProperty(ref _isScanning, value)) ScanCommand.RaiseCanExecuteChanged(); }
        }

        private string _axisPosition = "-";
        /// <summary>扫描轴当前位置（实时刷新，用于确认起止点填得对不对）。</summary>
        public string AxisPosition { get => _axisPosition; set => SetProperty(ref _axisPosition, value); }

        #endregion

        #region 【扫描配方】

        /// <summary>
        /// 扫描配方编辑对象，绑给 pf:PropertyGrid。
        /// 带 [Category]/[DisplayName]/[Description]，属性说明直接在网格里可见。
        /// </summary>
        public ScanProfileParamView Profile { get; } = new();

        #endregion

        #region 【换算结果 —— 只读】

        /// <summary>扫描行程（mm）。</summary>
        public string ScanLengthText => $"{_derived.ScanLengthMm:F2} mm";

        /// <summary>帧长（行）= 行程 ÷ 行间距。</summary>
        public string FrameHeightText => $"{_derived.FrameHeightLines} 行";

        /// <summary>实际行频（行/秒）= 速度 ÷ 行间距。</summary>
        public string ActualLineRateText => $"{_derived.ActualLineRate:F0} 行/秒";

        /// <summary>行周期，即单行可用的最长曝光时间。</summary>
        public string MaxExposureText => $"{_derived.MaxExposureTimeUs:F1} μs";

        /// <summary>理论加减速距离。</summary>
        public string AccelDistanceText
            => $"加速 {_derived.TheoreticalAccelDistanceMm:F2} / 减速 {_derived.TheoreticalDecelDistanceMm:F2} mm";

        /// <summary>轴实际要走的起止位置（含加减速余量）。</summary>
        public string MoveRangeText => $"{_derived.MoveStartMm:F2} → {_derived.MoveEndMm:F2} mm";

        /// <summary>理论帧时间与据此推出的帧超时。</summary>
        public string FrameTimeText => $"{_derived.EstimatedFrameTimeMs} ms（帧超时 {_derived.FrameTimeoutMs} ms）";

        private string _validationText = string.Empty;
        /// <summary>配方校验问题清单；为空表示通过。</summary>
        public string ValidationText { get => _validationText; set => SetProperty(ref _validationText, value); }

        private bool _isProfileValid;
        /// <summary>配方是否通过校验。</summary>
        public bool IsProfileValid
        {
            get => _isProfileValid;
            set { if (SetProperty(ref _isProfileValid, value)) ScanCommand.RaiseCanExecuteChanged(); }
        }

        #endregion

        #region 【扫描结果】

        private BitmapFrame? _previewImage;
        /// <summary>扫描结果预览图。类型须为 BitmapFrame（pf:ImageViewer 的依赖属性类型）。</summary>
        public BitmapFrame? PreviewImage
        {
            get => _previewImage;
            set { if (SetProperty(ref _previewImage, value)) RaisePropertyChanged(nameof(HasPreview)); }
        }

        /// <summary>是否已有预览图。</summary>
        public bool HasPreview => _previewImage != null;

        private string _previewHint = "尚未扫描";
        /// <summary>无图时的提示文本。</summary>
        public string PreviewHint { get => _previewHint; set => SetProperty(ref _previewHint, value); }

        private string _resultText = "-";
        /// <summary>扫描结果摘要（尺寸、大小、耗时）。</summary>
        public string ResultText { get => _resultText; set => SetProperty(ref _resultText, value); }

        #endregion

        #region 【命令】

        /// <summary>初始化模组（命名与其他模组调试页保持一致）</summary>
        public DelegateCommand InitializeModuleCommand { get; private set; } = null!;
        /// <summary>报警复位</summary>
        public DelegateCommand ResetModuleCommand { get; private set; } = null!;
        /// <summary>模组停止</summary>
        public DelegateCommand StopCommand { get; private set; } = null!;
        /// <summary>执行一次扫描</summary>
        public DelegateCommand ScanCommand { get; private set; } = null!;
        /// <summary>中止正在进行的扫描</summary>
        public DelegateCommand AbortCommand { get; private set; } = null!;
        /// <summary>把扫描轴移动到扫描起点（不扫描，用于对位）</summary>
        public DelegateCommand GotoStartCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            InitializeModuleCommand = new DelegateCommand(() => RunAsync("初始化模组",
                async () => { if (SelectedModule != null) await SelectedModule.InitializeAsync(); RefreshModuleInfo(); }));

            ResetModuleCommand = new DelegateCommand(() => RunAsync("复位模组",
                async () => { if (SelectedModule != null) await SelectedModule.ResetAsync(); }));

            StopCommand = new DelegateCommand(() => RunAsync("停止模组",
                async () => { if (SelectedModule != null) await SelectedModule.StopAsync(); }));

            GotoStartCommand = new DelegateCommand(() => RunAsync("移动到扫描起点", async () =>
            {
                var axis = SelectedModule?.ScanAxis;
                if (axis == null) { LogWarn("模组未初始化，取不到扫描轴。"); return; }

                // 走到含加速余量的实际起点，与扫描时的起始位置完全一致，便于目视对位
                await axis.MoveAbsoluteAsync(_derived.MoveStartMm, _derived.EffectivePositioningVelocity,
                    _derived.AccelerationMmPerSec2, _derived.EffectiveDeceleration, _derived.SCurveTimeMs);

                Log($"扫描轴移动到起始位 {_derived.MoveStartMm:F2}mm。");
            }));

            ScanCommand = new DelegateCommand(ExecuteScan, () => !IsScanning && IsProfileValid);

            AbortCommand = new DelegateCommand(() =>
            {
                _scanCts?.Cancel();
                LogWarn("已请求中止扫描。");
            });
        }

        /// <summary>执行一次扫描并把结果渲染到预览区。</summary>
        private void ExecuteScan() => RunAsync("扫描", async () =>
        {
            if (SelectedModule == null) { LogWarn("未选择模组。"); return; }

            IsScanning = true;
            PreviewHint = "扫描中...";

            var cts = new CancellationTokenSource();
            _scanCts = cts;

            var startedAt = DateTime.Now;
            try
            {
                var frame = await SelectedModule.ScanAsync(Profile.ToProfile(), null, cts.Token);

                double elapsed = (DateTime.Now - startedAt).TotalMilliseconds;
                ResultText = $"{frame.Width}×{frame.Height}，{frame.SizeBytes / 1024.0 / 1024.0:F2}MB，"
                           + $"帧号 {frame.FrameNumber}，耗时 {elapsed:F0}ms";

                PreviewImage = LineScanPreview.TryBuild(frame, out string hint);
                PreviewHint = hint;

                Log($"扫描完成：{ResultText}");
            }
            finally
            {
                IsScanning = false;
                _scanCts = null;
                cts.Dispose();
            }
        });

        #endregion

        #region 【私有辅助】

        private void OnProfileChanged(object? sender, PropertyChangedEventArgs e) => RefreshDerived();

        /// <summary>
        /// 重算全部派生量并跑一次校验。
        /// <para>每次输入变化都重算，是为了让"这组参数自不自洽"在按下扫描**之前**就可见——
        /// 事后从一张废图倒推是轴、相机还是光学的问题，代价高得多。</para>
        /// </summary>
        private void RefreshDerived()
        {
            _derived = Profile.ToProfile();

            RaisePropertyChanged(nameof(ScanLengthText));
            RaisePropertyChanged(nameof(FrameHeightText));
            RaisePropertyChanged(nameof(ActualLineRateText));
            RaisePropertyChanged(nameof(MaxExposureText));
            RaisePropertyChanged(nameof(AccelDistanceText));
            RaisePropertyChanged(nameof(MoveRangeText));
            RaisePropertyChanged(nameof(FrameTimeText));

            var problems = _derived.Validate();
            IsProfileValid = problems.Count == 0;
            ValidationText = problems.Count == 0
                ? "配方校验通过。"
                : "· " + string.Join("\n· ", problems);
        }

        private void RefreshModuleInfo()
        {
            if (SelectedModule == null)
            {
                ModuleName = "未找到线扫检测模组";
                ModuleInfo = "请确认已在 App.xaml.cs 中注册 LineScanDetectionModule。";
                return;
            }

            ModuleName = SelectedModule.MechanismName;

            string axis = SelectedModule.ScanAxis != null ? "已解析" : "未解析（需先初始化）";
            string cam = SelectedModule.Camera != null
                ? $"已解析（{(SelectedModule.Camera.HasFrameGrabber ? "经采集卡" : "直连")}）"
                : "未解析（需先初始化）";

            ModuleInfo = $"扫描轴：{axis} | 相机：{cam}";
        }

        private void OnStatusTick(object? sender, EventArgs e)
        {
            var module = SelectedModule;
            if (module == null) return;

            IsInitialized = module.IsInitialized;
            HasAlarm = module.HasAlarm;

            double? pos = module.ScanAxis?.CurrentPosition;
            AxisPosition = pos.HasValue ? $"{pos.Value:F3} mm" : "-";
        }

        /// <summary>统一的异步命令外壳：吞掉异常并落到日志栏与顶部反馈，不让 async void 击穿进程。</summary>
        private async void RunAsync(string opName, Func<Task> action)
        {
            try
            {
                DebugMessage = $"{opName}中...";
                await action();
                DebugMessage = $"{opName}完成";
            }
            catch (OperationCanceledException)
            {
                DebugMessage = $"{opName}已取消";
                LogWarn($"{opName}已取消。");
            }
            catch (Exception ex)
            {
                // 配方校验失败会带整段问题清单，原样输出比截断更有用
                DebugMessage = $"{opName}失败：{ex.Message}";
                _logger.Error($"[{ModuleName}] {opName}失败：{ex.Message}");
                PreviewHint = $"{opName}失败：{ex.Message}";
            }
        }

        private void Log(string message)
        {
            DebugMessage = message;
            _logger.Info($"[{ModuleName}] {message}");
        }

        private void LogWarn(string message)
        {
            DebugMessage = message;
            _logger.Warn($"[{ModuleName}] {message}");
        }

        /// <summary>视图销毁时停掉轮询并退订配方变更。</summary>
        public override void Destroy()
        {
            _statusTimer.Stop();
            Profile.PropertyChanged -= OnProfileChanged;
        }

        #endregion
    }
}
