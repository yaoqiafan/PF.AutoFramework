using PF.Core.Entities.Hardware;
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
    /// <para><b>这个面板真正的价值在"边选边看"</b>：起点/终点点位一改就重算行程、理论加减速距离、
    /// 理论帧时间，并跑一次校验，让"轴这段能不能走"在**按下扫描之前**就能看出来；
    /// 校验不过时「开始扫描」直接禁用。</para>
    ///
    /// <para><b>运动参数基本不在这个面板编辑</b>：位置/速度/加减速/S曲线时间来自选中的起点/终点
    /// 两个轴点位在点表里的配置。例外是<b>终点速度</b>：它和曝光直接相关（最大曝光 = 像素精度 ÷ 速度），
    /// 这里可以快捷修改并保存回点表，其余运动参数仍去轴调试页改。
    /// <b>帧长/行频这类成像参数也不在这个面板编辑</b>：它们已经配置在相机自己身上
    /// （UserSetDefault），这里只留一个曝光时间——留空表示不下发，沿用相机当前曝光。</para>
    ///
    /// <para>模组实例通过 <see cref="IMechanism"/> 集合按类型筛选获得，而不是按 DryIoc 服务键解析——
    /// 一台设备上可能有多条扫描线，各自是一个 LineScanDetectionModule 实例，
    /// 写死服务键就只能调第一条。</para>
    /// </summary>
    public class LineScanDetectionModuleDebugViewModel : RegionViewModelBase
    {
        private readonly CategoryLogger _logger;
        private readonly DispatcherTimer _statusTimer;

        /// <summary>由选中的起点/终点点位换算出的几何快照，供只读展示属性读取；无法解析点位时为 null。</summary>
        private ScanGeometry? _derived;

        private CancellationTokenSource? _scanCts;

        /// <summary>初始化线扫检测模组调试 ViewModel</summary>
        public LineScanDetectionModuleDebugViewModel(IEnumerable<IMechanism> mechanisms, ILogService logService)
        {
            _logger = CategoryLoggerFactory.Hardware(logService);

            foreach (var m in mechanisms.OfType<LineScanDetectionModule>())
                Modules.Add(m);

            SelectedModule = Modules.FirstOrDefault();

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
            set { if (SetProperty(ref _selectedModule, value)) { RefreshModuleInfo(); RefreshPointNames(); } }
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
            set
            {
                if (!SetProperty(ref _isScanning, value)) return;
                ScanCommand.RaiseCanExecuteChanged();
                SaveEndSpeedCommand?.RaiseCanExecuteChanged();
            }
        }

        private string _axisPosition = "-";
        /// <summary>扫描轴当前位置（实时刷新，用于确认起止点选得对不对）。</summary>
        public string AxisPosition { get => _axisPosition; set => SetProperty(ref _axisPosition, value); }

        #endregion

        #region 【起点/终点点位】

        /// <summary>扫描轴点表里的全部点位名，供两个下拉框选择。</summary>
        public ObservableCollection<string> PointNames { get; } = new();

        private string? _selectedStartPointName;
        /// <summary>扫描起点点位名（图像第一行对应的位置）。</summary>
        public string? SelectedStartPointName
        {
            get => _selectedStartPointName;
            set { if (SetProperty(ref _selectedStartPointName, value)) RefreshDerived(); }
        }

        private string? _selectedEndPointName;
        /// <summary>扫描终点点位名（图像最后一行对应的位置；速度/加减速/S曲线时间均取自这个点位）。</summary>
        public string? SelectedEndPointName
        {
            get => _selectedEndPointName;
            set { if (SetProperty(ref _selectedEndPointName, value)) RefreshDerived(); }
        }

        private string _startPointDetailText = "-";
        /// <summary>起点在轴点表里的详情（位置/速度），核对用，不用切到轴调试页。</summary>
        public string StartPointDetailText { get => _startPointDetailText; set => SetProperty(ref _startPointDetailText, value); }

        private string _endPointDetailText = "-";
        /// <summary>终点在轴点表里的详情（位置/速度/加减速/S曲线时间），核对用。</summary>
        public string EndPointDetailText { get => _endPointDetailText; set => SetProperty(ref _endPointDetailText, value); }

        #endregion

        #region 【曝光】

        private string _exposureTimeUs = string.Empty;
        /// <summary>
        /// 曝光时间（μs），留空表示不下发、沿用相机当前曝光。改动即按当前终点速度校验（不能超过行周期）。
        /// 帧长/行频等其它成像参数不在这里改——它们已经配置在相机自己身上（UserSetDefault）。
        /// </summary>
        public string ExposureTimeUs
        {
            get => _exposureTimeUs;
            set { if (SetProperty(ref _exposureTimeUs, value)) RefreshDerived(); }
        }

        /// <summary>曝光输入框解析出的值；留空/非法时为 null（不下发、不校验）。</summary>
        private double? ParsedExposureUs => double.TryParse(ExposureTimeUs, out var exp) && exp > 0 ? exp : null;

        private ExposureCheck _exposureCheck = ExposureCheck.Unknown;
        /// <summary>曝光与速度的匹配结果：<c>Exceeded</c> 标红并禁止扫描，<c>NearLimit</c>（超过 90%）标黄。</summary>
        public ExposureCheck ExposureCheck
        {
            get => _exposureCheck;
            private set => SetProperty(ref _exposureCheck, value);
        }

        private string _exposureHintText = string.Empty;
        /// <summary>曝光输入框下方的校验提示。</summary>
        public string ExposureHintText { get => _exposureHintText; private set => SetProperty(ref _exposureHintText, value); }

        #endregion

        #region 【终点速度 —— 快捷修改并持久化】

        private string _endSpeedText = string.Empty;
        /// <summary>
        /// 终点点位速度（mm/s）的编辑值。扫描速度取自终点点位，这里改完点「保存速度」写回轴点位表并持久化，
        /// 不用切到轴调试页；保存后立即重算行频/最大曝光。
        /// </summary>
        public string EndSpeedText
        {
            get => _endSpeedText;
            set { if (SetProperty(ref _endSpeedText, value)) SaveEndSpeedCommand?.RaiseCanExecuteChanged(); }
        }

        /// <summary>把 <see cref="EndSpeedText"/> 写回终点点位并保存点位表。</summary>
        public DelegateCommand SaveEndSpeedCommand { get; private set; } = null!;

        /// <summary>按当前曝光反算一个安全速度（最大速度的 90%）填进 <see cref="EndSpeedText"/>（不保存）。</summary>
        public DelegateCommand FillSafeSpeedCommand { get; private set; } = null!;

        #endregion

        #region 【换算结果 —— 只读】

        /// <summary>扫描行程。</summary>
        public string ScanLengthText => _derived != null ? $"{_derived.ScanLengthMm:F2}" : "-";

        /// <summary>
        /// 理论加减速距离，仅供参考——判断起点到终点这段行程够不够轴提上速/减下速。
        /// 不是硬性校验：余量已经交给起点/终点两个点位自己的位置去把握。
        /// </summary>
        public string AccelDistanceText => _derived != null
            ? $"加速 {_derived.TheoreticalAccelDistanceMm:F2} / 减速 {_derived.TheoreticalDecelDistanceMm:F2}（仅供参考，实际行程 {_derived.ScanLengthMm:F2}）"
            : "-";

        /// <summary>理论帧时间与据此推出的等帧超时。</summary>
        public string FrameTimeText => _derived != null
            ? $"{_derived.EstimatedFrameTimeMs} ms（等帧超时 {_derived.FrameTimeoutMs} ms）"
            : "-";

        /// <summary>相机的像素精度（行间距，μm/行）：编码器换算或硬件配置里的值，0 = 未配置。</summary>
        private double LineSpacingUm => SelectedModule?.Camera?.LineSpacingUm ?? 0;

        /// <summary>扫描速度（终点点位速度）。</summary>
        public string ScanSpeedText => _derived != null ? $"{_derived.VelocityMmPerSec:F2} mm/s（终点 '{_derived.EndPointName}'）" : "-";

        /// <summary>像素精度。</summary>
        public string LineSpacingText => LineSpacingUm > 0
            ? $"{LineSpacingUm:F3} μm/行"
            : "未配置——在硬件配置里给线扫相机填 LineSpacingUm，否则算不出行频/最大曝光";

        /// <summary>行频 = 速度 ÷ 像素精度。</summary>
        public string LineRateText => _derived != null && LineSpacingUm > 0
            ? $"{_derived.LineRateHz(LineSpacingUm):F0} Hz（行周期 {_derived.MaxExposureUs(LineSpacingUm):F1} μs）"
            : "-";

        /// <summary>最大曝光 = 行周期 = 像素精度 ÷ 速度。</summary>
        public string MaxExposureText => _derived != null && LineSpacingUm > 0
            ? $"{_derived.MaxExposureUs(LineSpacingUm):F1} μs（超过 {ScanGeometry.ExposureWarnRatio:P0} 即 {_derived.MaxExposureUs(LineSpacingUm) * ScanGeometry.ExposureWarnRatio:F1} μs 提醒）"
            : "-";

        private string _validationText = string.Empty;
        /// <summary>几何校验问题清单；为空表示通过。</summary>
        public string ValidationText { get => _validationText; set => SetProperty(ref _validationText, value); }

        private bool _isProfileValid;
        /// <summary>当前选择是否通过校验。</summary>
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
        /// <summary>重新从轴点表读取点位名列表（点表在别处被改过时用）</summary>
        public DelegateCommand RefreshPointsCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            InitializeModuleCommand = new DelegateCommand(() => RunAsync("初始化模组", async () =>
            {
                if (SelectedModule != null) await SelectedModule.InitializeAsync();
                RefreshModuleInfo();
                RefreshPointNames();
            }));

            ResetModuleCommand = new DelegateCommand(() => RunAsync("复位模组",
                async () => { if (SelectedModule != null) await SelectedModule.ResetAsync(); }));

            StopCommand = new DelegateCommand(() => RunAsync("停止模组",
                async () => { if (SelectedModule != null) await SelectedModule.StopAsync(); }));

            GotoStartCommand = new DelegateCommand(() => RunAsync("移动到扫描起点", async () =>
            {
                var axis = SelectedModule?.ScanAxis;
                if (axis == null) { LogWarn("模组未初始化，取不到扫描轴。"); return; }
                if (string.IsNullOrEmpty(SelectedStartPointName)) { LogWarn("未选择起点点位。"); return; }

                await axis.MoveToPointAsync(SelectedStartPointName);
                Log($"扫描轴移动到起点 '{SelectedStartPointName}'。");
            }));

            RefreshPointsCommand = new DelegateCommand(RefreshPointNames);

            SaveEndSpeedCommand = new DelegateCommand(ExecuteSaveEndSpeed,
                () => !IsScanning && FindEndPoint() != null && double.TryParse(EndSpeedText, out var v) && v > 0);

            FillSafeSpeedCommand = new DelegateCommand(() =>
            {
                double max = ScanGeometry.MaxVelocityFor(ParsedExposureUs ?? 0, LineSpacingUm);
                if (max <= 0) { LogWarn("先填曝光时间，且相机要配置像素精度，才能反算速度。"); return; }
                EndSpeedText = (max * ScanGeometry.ExposureWarnRatio).ToString("F2");
                Log($"按曝光 {ParsedExposureUs:F1}μs 反算：最大速度 {max:F2}mm/s，已填入其 {ScanGeometry.ExposureWarnRatio:P0} = {EndSpeedText}mm/s（点「保存速度」才生效）。");
            });

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
            if (string.IsNullOrEmpty(SelectedStartPointName) || string.IsNullOrEmpty(SelectedEndPointName))
            {
                LogWarn("请先选择起点与终点点位。");
                return;
            }

            IsScanning = true;
            PreviewHint = "扫描中...";

            var cts = new CancellationTokenSource();
            _scanCts = cts;

            // 曝光留空 = 不下发，沿用相机当前曝光；填了才组一份只带曝光的相机配置
            LineScanCameraConfig? config = ParsedExposureUs is { } exp
                ? new LineScanCameraConfig { ExposureTimeUs = exp }
                : null;

            var startedAt = DateTime.Now;
            try
            {
                var frame = await SelectedModule.ScanAsync(
                    SelectedStartPointName, SelectedEndPointName, config, cts.Token);

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

        /// <summary>
        /// 把编辑的终点速度写回轴点位表并持久化（<c>AddOrUpdatePoint</c> + <c>SavePointTable</c>），然后重算。
        /// 写一份副本进去：点表里的对象是轴的内部状态，不在界面线程上就地改它。
        /// </summary>
        private void ExecuteSaveEndSpeed()
        {
            var axis = SelectedModule?.ScanAxis;
            var end = FindEndPoint();
            if (axis == null || end == null) { LogWarn("模组未初始化或未选终点点位。"); return; }
            if (!double.TryParse(EndSpeedText, out var speed) || speed <= 0) { LogWarn("速度须为大于 0 的数。"); return; }

            double old = end.Speed;
            try
            {
                axis.AddOrUpdatePoint(new AxisPoint
                {
                    Name           = end.Name,
                    TargetPosition = end.TargetPosition,
                    Speed          = speed,
                    Acc            = end.Acc,
                    Dec            = end.Dec,
                    STime          = end.STime,
                    Description    = end.Description,
                    SortOrder      = end.SortOrder,
                });
                axis.SavePointTable();
                Log($"终点 '{end.Name}' 速度 {old:F2} → {speed:F2} mm/s，已保存到点位表。");
            }
            catch (Exception ex)
            {
                LogWarn($"保存终点速度失败：{ex.Message}");
            }
            RefreshDerived();
        }

        private AxisPoint? FindEndPoint()
            => SelectedModule?.ScanAxis?.PointTable.FirstOrDefault(p => p.Name == SelectedEndPointName);

        #region 【私有辅助】

        /// <summary>
        /// 重新从扫描轴点表读取点位名列表。选中项若还在新列表里就保留，否则清空——
        /// 不能悄悄留着一个已经不存在的点位名，那样换算结果会用着旧值却不吭声。
        /// </summary>
        private void RefreshPointNames()
        {
            var axis = SelectedModule?.ScanAxis;
            var names = axis?.PointTable.Select(p => p.Name).ToList() ?? new List<string>();

            PointNames.Clear();
            foreach (var n in names) PointNames.Add(n);

            if (SelectedStartPointName != null && !names.Contains(SelectedStartPointName))
                SelectedStartPointName = null;
            if (SelectedEndPointName != null && !names.Contains(SelectedEndPointName))
                SelectedEndPointName = null;

            RefreshDerived();
        }

        /// <summary>
        /// 重算全部派生量并跑一次校验。
        /// <para>每次点位选择变化都重算，是为了让"这组起点/终点走不走得通"在按下扫描**之前**就可见。</para>
        /// </summary>
        private void RefreshDerived()
        {
            var axis = SelectedModule?.ScanAxis;
            var start = axis?.PointTable.FirstOrDefault(p => p.Name == SelectedStartPointName);
            var end = axis?.PointTable.FirstOrDefault(p => p.Name == SelectedEndPointName);

            StartPointDetailText = start != null
                ? $"{start.TargetPosition:F2} @ {start.Speed:F1}/s"
                : "未选择";
            EndPointDetailText = end != null
                ? $"{end.TargetPosition:F2} @ {end.Speed:F1}/s，加速{end.Acc:F0}/减速{end.Dec:F0}，S曲线{end.STime:F3}s"
                : "未选择";

            _derived = (start != null && end != null) ? new ScanGeometry(start, end) : null;

            // 终点换了或保存过速度：编辑框跟上点表里的当前值
            string currentSpeed = end != null ? end.Speed.ToString("F2") : string.Empty;
            if (!string.Equals(_lastSyncedEndSpeed, currentSpeed, StringComparison.Ordinal) || end?.Name != _lastSyncedEndName)
            {
                EndSpeedText = currentSpeed;
                _lastSyncedEndSpeed = currentSpeed;
                _lastSyncedEndName = end?.Name;
            }

            RaisePropertyChanged(nameof(ScanLengthText));
            RaisePropertyChanged(nameof(AccelDistanceText));
            RaisePropertyChanged(nameof(FrameTimeText));
            RaisePropertyChanged(nameof(ScanSpeedText));
            RaisePropertyChanged(nameof(LineSpacingText));
            RaisePropertyChanged(nameof(LineRateText));
            RaisePropertyChanged(nameof(MaxExposureText));
            SaveEndSpeedCommand?.RaiseCanExecuteChanged();

            if (_derived == null)
            {
                ExposureCheck = ExposureCheck.Unknown;
                ExposureHintText = string.Empty;
                IsProfileValid = false;
                ValidationText = "请先选择起点与终点点位。";
                return;
            }

            // 曝光与速度匹配：超限红（禁止扫描，ScanAsync 也会拦），超过 90% 黄
            double spacing = LineSpacingUm;
            double? exposure = ParsedExposureUs;
            ExposureCheck = _derived.CheckExposure(exposure, spacing);
            ExposureHintText = ExposureCheck switch
            {
                ExposureCheck.Exceeded  => "✕ " + _derived.DescribeExposureExceeded(exposure!.Value, spacing),
                ExposureCheck.NearLimit => $"⚠ 曝光已超过最大曝光 {_derived.MaxExposureUs(spacing):F1}μs 的 {ScanGeometry.ExposureWarnRatio:P0}，余量不多。",
                ExposureCheck.Ok        => $"✓ 曝光在最大曝光 {_derived.MaxExposureUs(spacing):F1}μs 以内。",
                _ => exposure == null ? "未填曝光：不下发、不校验（沿用相机当前曝光）。"
                                      : "相机没有配置像素精度，无法校验曝光。",
            };

            var problems = _derived.Validate().ToList();
            if (ExposureCheck == ExposureCheck.Exceeded)
                problems.Add("曝光超过当前速度下的最大曝光（见曝光提示）。");
            IsProfileValid = problems.Count == 0;
            ValidationText = problems.Count == 0
                ? "校验通过。"
                : "· " + string.Join("\n· ", problems);
        }

        /// <summary>上次同步到 <see cref="EndSpeedText"/> 的点表速度/终点名——只在点表值或终点变了时覆盖编辑框，不打断正在输入的值。</summary>
        private string? _lastSyncedEndSpeed;
        private string? _lastSyncedEndName;

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
            AxisPosition = pos.HasValue ? $"{pos.Value:F3}" : "-";
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
                // 校验失败会带整段问题清单，原样输出比截断更有用
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

        /// <summary>视图销毁时停掉轮询。</summary>
        public override void Destroy()
        {
            _statusTimer.Stop();
        }

        #endregion
    }
}
