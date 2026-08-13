using PF.Core.Constants;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware.Camera.LineScan;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware;
using PF.Infrastructure.Logging;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 线阵（线扫）相机调试 ViewModel，功能对齐海康官方 BasicDemoLineScan 示例
    /// （初始化 / 图像采集 / 图像保存 / 参数四组），并补齐框架自有的编码器、帧控制与高级节点区域。
    ///
    /// <para><b>预览为什么要节流+降采样</b>：线扫单帧可达 16K×上万行、数百 MB，
    /// 每帧都在 UI 线程转 BitmapSource 会直接卡死界面。这里的做法是：SDK 回调只把最新帧
    /// 塞进一个字段（旧的直接丢弃），由 UI 侧的定时器按固定节奏取走并降采样后渲染。</para>
    ///
    /// <para><b>实例按相机复用</b>：基类默认每次导航都新建 ViewModel，调试面板不适用——
    /// 填好的帧长/编码器接线、读回来的参数、枚举结果，一离开页面就全丢了。
    /// 这里重写 IsNavigationTarget/KeepAlive，同一台相机复用同一实例、换相机才新建。
    /// 但仍必须在 OnNavigatedFrom 退订 FrameReceived：页面不可见时没必要跑渲染，
    /// 且换相机后旧实例若还挂着订阅就是纯泄漏。</para>
    /// </summary>
    public class LineScanCameraDebugViewModel : RegionViewModelBase
    {
        /// <summary>预览刷新间隔（毫秒）。约 10fps，足够看清扫描效果又不至于抢占 UI 线程。</summary>
        private const int RenderIntervalMs = 100;

        private ILineScanCamera _camera;
        private BaseDevice _baseDevice;

        /// <summary>
        /// 硬件分类日志。面板不再自建日志列表——操作结果统一发到 ILogService，
        /// 由主窗体底部的日志栏统一展示，与相机自身写的硬件日志混排在同一条时间线上。
        /// </summary>
        private readonly CategoryLogger _logger;

        private readonly DispatcherTimer _statusTimer;
        private readonly DispatcherTimer _renderTimer;

        /// <summary>待渲染的最新一帧。SDK 回调线程写、UI 线程取，用 Interlocked 交换保证不撕裂。</summary>
        private LineScanFrame _pendingFrame;

        /// <summary>帧率统计：窗口起点与窗口内帧数。</summary>
        private DateTime _fpsWindowStart = DateTime.Now;
        private int _fpsWindowCount;

        /// <summary>下拉框回填期间抑制"选中项变化即写节点"，避免程序性回填被当成用户操作。</summary>
        private bool _suppressNodeWrite;

        /// <summary>初始化线阵相机调试 ViewModel</summary>
        public LineScanCameraDebugViewModel(ILogService logService)
        {
            _logger = CategoryLoggerFactory.Hardware(logService);

            InitializeCommands();

            _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _statusTimer.Tick += OnStatusTimerTick;

            _renderTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(RenderIntervalMs) };
            _renderTimer.Tick += OnRenderTimerTick;

            SaveDirectory = Path.Combine(ConstGlobalParam.ConfigPath, "Vision", "DebugImages");
        }

        #region 【Prism 导航生命周期】

        /// <summary>
        /// 同一台相机复用已有实例，换相机才新建。
        /// <para>基类默认恒为 false（每次导航都新建实例），对调试面板不合适：
        /// 填好的帧长、编码器接线、读取回来的参数、枚举结果，一离开页面就全没了。
        /// 复用实例后这些状态跨导航保留，代价是每台相机常驻一个 ViewModel——数量有界，可接受。</para>
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext)
            => navigationContext.Parameters.ContainsKey("Device")
               && ReferenceEquals(navigationContext.Parameters.GetValue<object>("Device"), _camera);

        /// <summary>配合 <see cref="IsNavigationTarget"/>：实例要留在 Region 中才谈得上复用。</summary>
        public override bool KeepAlive => true;

        /// <summary>导航进入时绑定相机、订阅帧事件并启动轮询</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Device")) return;

            var camera = navigationContext.Parameters.GetValue<ILineScanCamera>("Device");

            // 复用实例时是同一台相机，不重置任何已填参数，只把事件与轮询重新挂上
            if (!ReferenceEquals(camera, _camera))
            {
                _camera = camera;
                _baseDevice = _camera as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知线阵相机设备";
                    DeviceDescription = "无法获取底层设备信息";
                }
            }

            if (_camera != null)
            {
                // 先退再订：OnNavigatedFrom 已退过一次，这里保证无论走哪条路径都只有一份订阅
                _camera.FrameReceived -= OnFrameReceived;
                _camera.FrameReceived += OnFrameReceived;

                // 进页面就把设备真实状态读回来。
                // 不依赖"ViewModel 实例能活到下次导航"——DebugViewRegion 嵌在硬件调试页里，
                // 外层页面一旦被回收，内层实例连同填写的值一起没了；而且显示设备的实际值
                // 本来就比显示"上次谁填了什么"更可信。
                RunAsync("加载相机状态", LoadFromCameraAsync);
            }

            _statusTimer.Start();
            _renderTimer.Start();
        }

        /// <summary>导航离开时退订帧事件并停止轮询（不退订会泄漏历史 ViewModel）</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);

            if (_camera != null) _camera.FrameReceived -= OnFrameReceived;

            _statusTimer.Stop();
            _renderTimer.Stop();
        }

        #endregion

        #region 【设备信息与状态】

        private string _deviceName = "未选中相机";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _isAlarm;
        /// <summary>获取或设置是否报警</summary>
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        private bool _isGrabbing;
        /// <summary>获取或设置是否正在取流</summary>
        public bool IsGrabbing { get => _isGrabbing; set => SetProperty(ref _isGrabbing, value); }

        private string _cameraInfo = "-";
        /// <summary>获取或设置相机型号/序列号/链路信息</summary>
        public string CameraInfo { get => _cameraInfo; set => SetProperty(ref _cameraInfo, value); }

        private string _frameControlTarget = "-";
        /// <summary>获取或设置帧控制落点说明（采集卡 / 相机自身）</summary>
        public string FrameControlTarget { get => _frameControlTarget; set => SetProperty(ref _frameControlTarget, value); }

        #endregion

        #region 【相机本体参数】

        private string _scanMode = "LineScan";
        /// <summary>获取或设置扫描模式</summary>
        public string ScanMode { get => _scanMode; set => SetProperty(ref _scanMode, value); }

        private string _exposureTimeUs = "50";
        /// <summary>获取或设置曝光时间（微秒）</summary>
        public string ExposureTimeUs { get => _exposureTimeUs; set => SetProperty(ref _exposureTimeUs, value); }

        private string _digitalShift = "0";
        /// <summary>获取或设置数字增益</summary>
        public string DigitalShift { get => _digitalShift; set => SetProperty(ref _digitalShift, value); }

        /// <summary>像素格式可选项</summary>
        public ObservableCollection<string> PixelFormats { get; } = new();

        private string _selectedPixelFormat;
        /// <summary>
        /// 获取或设置当前像素格式。选中即立即下发；
        /// 下发成功后须重读无损压缩模式——两者互相约束，改完像素格式后原压缩档可能已失效。
        /// </summary>
        public string SelectedPixelFormat
        {
            get => _selectedPixelFormat;
            set
            {
                if (!SetProperty(ref _selectedPixelFormat, value) || _suppressNodeWrite) return;
                RunAsync("设置像素格式", async () =>
                {
                    if (await TrySetEnumNodeAsync("PixelFormat", value, v => SelectedPixelFormat = v))
                        await ReloadEnumNodeAsync("ImageCompressionMode", ImageCompressionModes,
                            v => SetSuppressed(() => SelectedImageCompressionMode = v));
                });
            }
        }

        /// <summary>无损压缩模式可选项</summary>
        public ObservableCollection<string> ImageCompressionModes { get; } = new();

        private string _selectedImageCompressionMode;
        /// <summary>获取或设置无损压缩模式（选中即下发）</summary>
        public string SelectedImageCompressionMode
        {
            get => _selectedImageCompressionMode;
            set
            {
                if (!SetProperty(ref _selectedImageCompressionMode, value) || _suppressNodeWrite) return;
                RunAsync("设置压缩模式", () =>
                    TrySetEnumNodeAsync("ImageCompressionMode", value, v => SelectedImageCompressionMode = v));
            }
        }

        /// <summary>模拟增益档位可选项</summary>
        public ObservableCollection<string> PreampGains { get; } = new();

        private string _selectedPreampGain;
        /// <summary>获取或设置模拟增益档位（选中即下发）</summary>
        public string SelectedPreampGain
        {
            get => _selectedPreampGain;
            set
            {
                if (!SetProperty(ref _selectedPreampGain, value) || _suppressNodeWrite) return;
                RunAsync("设置模拟增益", () =>
                    TrySetEnumNodeAsync("PreampGain", value, v => SelectedPreampGain = v));
            }
        }

        #endregion

        #region 【行触发与编码器】

        /// <summary>行触发方式可选项</summary>
        public IReadOnlyList<LineTriggerMode> LineTriggerModes { get; } =
            (LineTriggerMode[])Enum.GetValues(typeof(LineTriggerMode));

        private LineTriggerMode _selectedLineTriggerMode = LineTriggerMode.Encoder;
        /// <summary>获取或设置行触发方式</summary>
        public LineTriggerMode SelectedLineTriggerMode
        {
            get => _selectedLineTriggerMode;
            set
            {
                if (SetProperty(ref _selectedLineTriggerMode, value)) RaisePropertyChanged(nameof(IsEncoderMode));
            }
        }

        /// <summary>当前是否为编码器行触发（控制编码器区域是否可用）</summary>
        public bool IsEncoderMode => SelectedLineTriggerMode == LineTriggerMode.Encoder;

        private string _lineTriggerSource = string.Empty;
        /// <summary>获取或设置行触发源（留空则按行触发方式取默认值）</summary>
        public string LineTriggerSource { get => _lineTriggerSource; set => SetProperty(ref _lineTriggerSource, value); }

        private bool _lineRateEnable;
        /// <summary>获取或设置内部行频使能（仅内部行频模式有意义）</summary>
        public bool LineRateEnable { get => _lineRateEnable; set => SetProperty(ref _lineRateEnable, value); }

        private string _acquisitionLineRate = "10000";
        /// <summary>获取或设置内部行频（行/秒）</summary>
        public string AcquisitionLineRate { get => _acquisitionLineRate; set => SetProperty(ref _acquisitionLineRate, value); }

        private string _encoderSelector = "Encoder0";
        /// <summary>获取或设置编码器选择器</summary>
        public string EncoderSelector { get => _encoderSelector; set => SetProperty(ref _encoderSelector, value); }

        private string _encoderSourceA = "Line1";
        /// <summary>获取或设置编码器 A 相信号源</summary>
        public string EncoderSourceA { get => _encoderSourceA; set => SetProperty(ref _encoderSourceA, value); }

        private string _encoderSourceB = "Line3";
        /// <summary>获取或设置编码器 B 相信号源</summary>
        public string EncoderSourceB { get => _encoderSourceB; set => SetProperty(ref _encoderSourceB, value); }

        private string _pulseEquivalentUm = "1";
        /// <summary>获取或设置读数头当量（微米/脉冲）</summary>
        public string PulseEquivalentUm { get => _pulseEquivalentUm; set => SetProperty(ref _pulseEquivalentUm, value); }

        private string _dividerRatio = "1";
        /// <summary>获取或设置分频/倍频系数</summary>
        public string DividerRatio { get => _dividerRatio; set => SetProperty(ref _dividerRatio, value); }

        private string _lineSpacingText = "-";
        /// <summary>获取或设置换算出的行间距说明</summary>
        public string LineSpacingText { get => _lineSpacingText; set => SetProperty(ref _lineSpacingText, value); }

        #endregion

        #region 【帧控制】

        private string _imageHeight = "1000";
        /// <summary>获取或设置帧长（一帧累计多少行）</summary>
        public string ImageHeight { get => _imageHeight; set => SetProperty(ref _imageHeight, value); }

        private string _frameTimeoutMs = "3000";
        /// <summary>获取或设置帧超时（毫秒，仅采集卡链路有对应节点）</summary>
        public string FrameTimeoutMs { get => _frameTimeoutMs; set => SetProperty(ref _frameTimeoutMs, value); }

        private bool _frameTriggerEnable;
        /// <summary>获取或设置是否启用帧触发</summary>
        public bool FrameTriggerEnable { get => _frameTriggerEnable; set => SetProperty(ref _frameTriggerEnable, value); }

        private string _frameTriggerSource = "SoftwareSignal0";
        /// <summary>获取或设置帧触发源</summary>
        public string FrameTriggerSource { get => _frameTriggerSource; set => SetProperty(ref _frameTriggerSource, value); }

        private string _frameTriggerActivation = "RisingEdge";
        /// <summary>获取或设置帧触发有效边沿</summary>
        public string FrameTriggerActivation { get => _frameTriggerActivation; set => SetProperty(ref _frameTriggerActivation, value); }

        private string _streamSelector = string.Empty;
        /// <summary>获取或设置流选择器（采集卡链路，留空则不下发）</summary>
        public string StreamSelector { get => _streamSelector; set => SetProperty(ref _streamSelector, value); }

        private string _cameraType = string.Empty;
        /// <summary>获取或设置相机类型匹配（CameraLink 位宽，留空则不下发）</summary>
        public string CameraType { get => _cameraType; set => SetProperty(ref _cameraType, value); }

        #endregion

        #region 【取帧、预览与存盘】

        private string _waitTimeoutMs = "10000";
        /// <summary>获取或设置单次取帧的等待超时（毫秒）</summary>
        public string WaitTimeoutMs { get => _waitTimeoutMs; set => SetProperty(ref _waitTimeoutMs, value); }

        private BitmapFrame _previewImage;
        /// <summary>
        /// 获取或设置预览图像。类型必须是 <see cref="BitmapFrame"/>——
        /// pf:ImageViewer 的 ImageSource 依赖属性即为该类型。
        /// </summary>
        public BitmapFrame PreviewImage { get => _previewImage; set => SetProperty(ref _previewImage, value); }

        /// <summary>是否已有可显示的预览图（用于在无图时让出位置给提示文本）。</summary>
        public bool HasPreview => _previewImage != null;

        private string _previewHint = "尚未收到图像";
        /// <summary>获取或设置预览区提示文本（无图或格式不支持预览时显示）</summary>
        public string PreviewHint { get => _previewHint; set => SetProperty(ref _previewHint, value); }

        private string _frameStats = "-";
        /// <summary>获取或设置帧统计信息（帧号/尺寸/大小/帧率）</summary>
        public string FrameStats { get => _frameStats; set => SetProperty(ref _frameStats, value); }

        private string _saveDirectory;
        /// <summary>获取或设置存盘目录</summary>
        public string SaveDirectory { get => _saveDirectory; set => SetProperty(ref _saveDirectory, value); }

        /// <summary>枚举到的在线相机列表</summary>
        public ObservableCollection<string> DiscoveredCameras { get; } = new();

        #endregion

        #region 【属性树】

        /// <summary>
        /// 设备属性树的全部节点（名称/分类/类型/权限/当前值）。
        /// 从相机的 GenICam XML 枚举而来，等价于 MVS 客户端的属性树，不必再手填节点名。
        /// </summary>
        public ObservableCollection<GenICamNode> Nodes { get; } = new();

        private GenICamNode _selectedNode;
        /// <summary>当前选中的节点。选中即把节点名/当前值/可选项填进编辑区。</summary>
        public GenICamNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (!SetProperty(ref _selectedNode, value) || value == null) return;

                NodeName = value.Name;
                NodeValue = value.Value ?? string.Empty;

                NodeEnumEntries.Clear();
                foreach (var e in value.EnumEntries) NodeEnumEntries.Add(e);
            }
        }

        private string _nodeFilter = string.Empty;
        /// <summary>节点过滤关键字（按名称/显示名/分类匹配）。</summary>
        public string NodeFilter
        {
            get => _nodeFilter;
            set { if (SetProperty(ref _nodeFilter, value)) ApplyNodeFilter(); }
        }

        private bool _writableNodesOnly;
        /// <summary>只看当前可写的节点。排查"为什么改不了"时特别有用。</summary>
        public bool WritableNodesOnly
        {
            get => _writableNodesOnly;
            set { if (SetProperty(ref _writableNodesOnly, value)) ApplyNodeFilter(); }
        }

        private string _nodeSummary = "尚未枚举";
        /// <summary>属性树统计说明。</summary>
        public string NodeSummary { get => _nodeSummary; set => SetProperty(ref _nodeSummary, value); }

        private string _nodeName = string.Empty;
        /// <summary>当前操作的 GenICam 节点名（由属性树选中填入，也可手工修改）</summary>
        public string NodeName { get => _nodeName; set => SetProperty(ref _nodeName, value); }

        private string _nodeValue = string.Empty;
        /// <summary>获取或设置节点值</summary>
        public string NodeValue { get => _nodeValue; set => SetProperty(ref _nodeValue, value); }

        /// <summary>枚举节点的可选项（读取后填充，绑到可编辑下拉框，省掉单独的列表控件）</summary>
        public ObservableCollection<string> NodeEnumEntries { get; } = new();

        #endregion

        #region 【命令】

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>模拟报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; private set; }
        /// <summary>枚举在线相机命令</summary>
        public DelegateCommand DiscoverCommand { get; private set; }
        /// <summary>读取相机当前参数命令</summary>
        public DelegateCommand RefreshParamsCommand { get; private set; }
        /// <summary>下发完整配置命令</summary>
        public DelegateCommand ApplyConfigCommand { get; private set; }
        /// <summary>开流命令</summary>
        public DelegateCommand StartGrabCommand { get; private set; }
        /// <summary>停流命令</summary>
        public DelegateCommand StopGrabCommand { get; private set; }
        /// <summary>帧软触发命令</summary>
        public DelegateCommand SoftwareTriggerCommand { get; private set; }
        /// <summary>取一帧命令（阻塞等待）</summary>
        public DelegateCommand GrabOneCommand { get; private set; }
        /// <summary>存盘命令（参数为 Bmp/Jpeg/Tiff/Png）</summary>
        public DelegateCommand<string> SaveImageCommand { get; private set; }
        /// <summary>读取节点命令</summary>
        public DelegateCommand ReadNodeCommand { get; private set; }
        /// <summary>写入节点命令</summary>
        public DelegateCommand WriteNodeCommand { get; private set; }
        /// <summary>执行命令节点命令</summary>
        public DelegateCommand ExecuteNodeCommand { get; private set; }
        /// <summary>枚举设备属性树命令</summary>
        public DelegateCommand EnumerateNodesCommand { get; private set; }

        private void InitializeCommands()
        {
            ConnectCommand = new DelegateCommand(() => RunAsync("连接", async () =>
            {
                if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None);
                await LoadFromCameraAsync();
            }));

            DisconnectCommand = new DelegateCommand(() => RunAsync("断开连接", async () =>
            {
                if (_baseDevice != null) await _baseDevice.DisconnectAsync();
            }));

            ResetCommand = new DelegateCommand(() => RunAsync("复位", async () =>
            {
                if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None);
                await LoadFromCameraAsync();
            }));

            SimulateAlarmCommand = new DelegateCommand(() =>
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.LineScanFrameTimeout, "调试页面手动模拟线阵相机报警"));

            DiscoverCommand = new DelegateCommand(() => RunAsync("枚举相机", async () =>
            {
                DiscoveredCameras.Clear();
                if (_camera == null) return;

                var list = await _camera.DiscoverAsync();
                foreach (var d in list) DiscoveredCameras.Add(d.DisplayName);

                Log($"发现 {list.Count} 台在线相机。");
            }));

            RefreshParamsCommand = new DelegateCommand(() => RunAsync("读取相机参数", LoadFromCameraAsync));

            EnumerateNodesCommand = new DelegateCommand(() => RunAsync("枚举属性树", async () =>
            {
                if (_camera == null) return;

                NodeSummary = "正在枚举...";
                _allNodes = await _camera.EnumerateNodesAsync();
                ApplyNodeFilter();
            }));

            ApplyConfigCommand = new DelegateCommand(() => RunAsync("下发配置", async () =>
            {
                if (_camera == null) return;

                var config = BuildConfig();
                await _camera.ApplyConfigAsync(config);

                LineSpacingText = _camera.LineSpacingUm > 0
                    ? $"{_camera.LineSpacingUm:F3} μm/行"
                    : "未使用编码器";

                Log("配置已下发（逐节点结果见上条硬件日志）。");
            }));

            StartGrabCommand = new DelegateCommand(() => RunAsync("开流", async () =>
            {
                if (_camera == null) return;
                bool ok = await _camera.ArmAsync();
                if (ok) Log("已开流。"); else LogWarn("开流失败（详见上条硬件日志）。");
            }));

            StopGrabCommand = new DelegateCommand(() => RunAsync("停流", async () =>
            {
                if (_camera == null) return;
                await _camera.StopAsync();
                Log("已停流。");
            }));

            SoftwareTriggerCommand = new DelegateCommand(() => RunAsync("帧软触发", async () =>
            {
                if (_camera == null) return;
                bool ok = await _camera.SoftwareTriggerFrameAsync();
                if (ok) Log("帧软触发已发送。"); else LogWarn("帧软触发失败（节点不存在或不可执行）。");
            }));

            GrabOneCommand = new DelegateCommand(() => RunAsync("取一帧", async () =>
            {
                if (_camera == null) return;

                int timeout = int.TryParse(WaitTimeoutMs, out var t) ? t : 10000;
                var frame = await _camera.WaitFrameAsync(timeout);
                Log($"取到一帧：{frame.Width}×{frame.Height}，{frame.SizeBytes / 1024}KB，帧号 {frame.FrameNumber}。");
            }));

            SaveImageCommand = new DelegateCommand<string>(format => RunAsync("存盘", async () =>
            {
                if (_camera == null) return;

                if (!Enum.TryParse<ImageFileFormat>(format, true, out var fileFormat))
                    fileFormat = ImageFileFormat.Bmp;

                string fileName = $"LineScan_{DateTime.Now:yyyyMMdd_HHmmss_fff}"
                    + $"_w{_camera.LastImageWidth}_h{_camera.LastImageHeight}"
                    + $".{fileFormat.ToString().ToLowerInvariant()}";

                string fullPath = Path.Combine(SaveDirectory ?? string.Empty, fileName);

                bool ok = await _camera.SaveLastImageAsync(fullPath, fileFormat);
                if (ok) Log($"图像已存盘：{fullPath}"); else LogWarn("存盘失败（尚无图像或相机未连接）。");
            }));

            ReadNodeCommand = new DelegateCommand(() => RunAsync("读取节点", async () =>
            {
                if (_camera == null || string.IsNullOrWhiteSpace(NodeName)) return;

                NodeEnumEntries.Clear();

                string value = await _camera.GetNodeAsync(NodeName);
                if (value == null)
                {
                    LogWarn($"节点 '{NodeName}' 读取失败（不存在或不可读）。");
                    return;
                }

                NodeValue = value;
                foreach (var e in await _camera.GetEnumEntriesAsync(NodeName)) NodeEnumEntries.Add(e);

                Log($"节点 '{NodeName}' = '{value}'"
                    + (NodeEnumEntries.Count > 0 ? $"（可选项 {NodeEnumEntries.Count} 个）" : string.Empty));
            }));

            WriteNodeCommand = new DelegateCommand(() => RunAsync("写入节点", async () =>
            {
                if (_camera == null || string.IsNullOrWhiteSpace(NodeName)) return;

                bool ok = await _camera.SetNodeAsync(NodeName, NodeValue ?? string.Empty);
                if (ok) Log($"节点 '{NodeName}' 已写入 '{NodeValue}'。");
                else LogWarn($"节点 '{NodeName}' 写入失败（不可写或值非法）。");
            }));

            ExecuteNodeCommand = new DelegateCommand(() => RunAsync("执行节点", async () =>
            {
                if (_camera == null || string.IsNullOrWhiteSpace(NodeName)) return;

                bool ok = await _camera.ExecuteCommandAsync(NodeName);
                if (ok) Log($"命令节点 '{NodeName}' 已执行。"); else LogWarn($"命令节点 '{NodeName}' 执行失败。");
            }));
        }

        #endregion

        #region 【参数读取】

        /// <summary>读取相机当前参数：三个枚举节点的可选项与当前值，以及曝光/数字增益。</summary>
        private async Task LoadParameterOptionsAsync()
        {
            if (_camera == null) return;

            await ReloadEnumNodeAsync("PixelFormat", PixelFormats,
                v => SetSuppressed(() => SelectedPixelFormat = v));
            await ReloadEnumNodeAsync("ImageCompressionMode", ImageCompressionModes,
                v => SetSuppressed(() => SelectedImageCompressionMode = v));
            await ReloadEnumNodeAsync("PreampGain", PreampGains,
                v => SetSuppressed(() => SelectedPreampGain = v));

            string exposure = await _camera.GetNodeAsync("ExposureTime");
            if (exposure != null) ExposureTimeUs = exposure;

            string shift = await _camera.GetNodeAsync("DigitalShift");
            if (shift != null) DigitalShift = shift;

            Log("已读取相机当前参数。");
        }

        /// <summary>
        /// 把相机与其采集卡的当前状态整体读回界面：型号/链路、相机侧参数、行触发、帧控制。
        /// 未连接时只刷新型号/链路信息，不去读节点。
        /// </summary>
        private async Task LoadFromCameraAsync()
        {
            RefreshCameraInfo();

            if (_camera == null || !_camera.IsConnected) return;

            await LoadParameterOptionsAsync();
            await LoadLineTriggerAsync();
            await LoadFrameControlAsync();
        }

        /// <summary>读回行触发方式与编码器接线。新旧固件节点树不同，两组都试。</summary>
        private async Task LoadLineTriggerAsync()
        {
            string? v;
            if ((v = await _camera.GetNodeAsync("ScanMode")) != null) ScanMode = v;

            // 新节点树：LineTriggerMode(bool) + LineTriggerSource；老节点树退回 TriggerSource
            string? mode = await _camera.GetNodeAsync("LineTriggerMode");
            string? source = await _camera.GetNodeAsync("LineTriggerSource") ?? await _camera.GetNodeAsync("TriggerSource");

            if (source != null) LineTriggerSource = source;

            if (mode != null)
            {
                bool lineTriggerOn = string.Equals(mode, "true", StringComparison.OrdinalIgnoreCase)
                                     || string.Equals(mode, "On", StringComparison.OrdinalIgnoreCase);

                SelectedLineTriggerMode = !lineTriggerOn
                    ? LineTriggerMode.InternalRate
                    : source != null && source.Contains("Encoder", StringComparison.OrdinalIgnoreCase)
                        ? LineTriggerMode.Encoder
                        : LineTriggerMode.ExternalLine;
            }

            if ((v = await _camera.GetNodeAsync("AcquisitionLineRateEnable")) != null)
                LineRateEnable = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            if ((v = await _camera.GetNodeAsync("AcquisitionLineRate")) != null) AcquisitionLineRate = v;

            if ((v = await _camera.GetNodeAsync("EncoderSelector")) != null) EncoderSelector = v;
            if ((v = await _camera.GetNodeAsync("EncoderSourceA")) != null) EncoderSourceA = v;
            if ((v = await _camera.GetNodeAsync("EncoderSourceB")) != null) EncoderSourceB = v;
        }

        /// <summary>
        /// 读回帧控制。挂了采集卡就读卡的节点树（ImageHeight/FrameTimeoutTime/...），
        /// 直连则读相机自身的 Height。
        /// </summary>
        private async Task LoadFrameControlAsync()
        {
            string? v;

            if (_camera.HasFrameGrabber && _camera is Infrastructure.Hardware.Camera.LineScan.BaseLineScanCamera { Parent: { } card })
            {
                if ((v = await card.GetNodeAsync("ImageHeight")) != null) ImageHeight = v;
                if ((v = await card.GetNodeAsync("FrameTimeoutTime")) != null) FrameTimeoutMs = v;
                if ((v = await card.GetNodeAsync("StreamSelector")) != null) StreamSelector = v;
                if ((v = await card.GetNodeAsync("CameraType")) != null) CameraType = v;
                if ((v = await card.GetNodeAsync("StreamTriggerSource")) != null) FrameTriggerSource = v;
                if ((v = await card.GetNodeAsync("StreamTriggerActivation")) != null) FrameTriggerActivation = v;
                if ((v = await card.GetNodeAsync("StreamTriggerEnable")) != null)
                    FrameTriggerEnable = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                if ((v = await _camera.GetNodeAsync("Height")) != null) ImageHeight = v;
                if ((v = await _camera.GetNodeAsync("FrameTriggerSource")) != null) FrameTriggerSource = v;
                if ((v = await _camera.GetNodeAsync("FrameTriggerMode")) != null)
                    FrameTriggerEnable = string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
            }
        }

        /// <summary>重新加载一个枚举节点的可选项与当前值。节点不存在时清空列表并跳过。</summary>
        private async Task ReloadEnumNodeAsync(string node, ObservableCollection<string> entries,
            Action<string> applyCurrent)
        {
            entries.Clear();

            if (_camera == null) return;

            foreach (var e in await _camera.GetEnumEntriesAsync(node)) entries.Add(e);

            string current = await _camera.GetNodeAsync(node);
            if (current != null) applyCurrent(current);
        }

        /// <summary>
        /// 写入枚举节点。失败时把下拉框回滚到相机的实际值——
        /// 否则界面显示的档位与相机实际档位不一致，是最容易误导现场的一类假象。
        /// </summary>
        private async Task<bool> TrySetEnumNodeAsync(string node, string value, Action<string> rollback)
        {
            if (_camera == null || string.IsNullOrEmpty(value)) return false;

            if (await _camera.SetNodeAsync(node, value))
            {
                Log($"{node} 已设为 '{value}'。");
                return true;
            }

            string actual = await _camera.GetNodeAsync(node);
            SetSuppressed(() => rollback(actual));

            LogWarn($"{node} 设为 '{value}' 失败，已回退到相机当前值 '{actual ?? "未知"}'。");
            return false;
        }

        /// <summary>在抑制"选中即写节点"的状态下执行回填动作。</summary>
        private void SetSuppressed(Action action)
        {
            _suppressNodeWrite = true;
            try { action(); }
            finally { _suppressNodeWrite = false; }
        }

        /// <summary>按界面填写值组装完整配置。空字符串保持为 null（语义为不下发）。</summary>
        private LineScanCameraConfig BuildConfig() => new()
        {
            ScanMode = NullIfBlank(ScanMode),
            PixelFormat = NullIfBlank(SelectedPixelFormat),
            ImageCompressionMode = NullIfBlank(SelectedImageCompressionMode),
            PreampGain = NullIfBlank(SelectedPreampGain),
            ExposureTimeUs = double.TryParse(ExposureTimeUs, out var exp) ? exp : null,
            DigitalShift = double.TryParse(DigitalShift, out var ds) ? ds : null,

            LineTrigger = new LineTriggerConfig
            {
                Mode = SelectedLineTriggerMode,
                TriggerSource = NullIfBlank(LineTriggerSource),
                AcquisitionLineRateEnable = LineRateEnable,
                AcquisitionLineRate = int.TryParse(AcquisitionLineRate, out var lr) ? lr : 0,
                Encoder = SelectedLineTriggerMode == LineTriggerMode.Encoder
                    ? new EncoderConfig
                    {
                        Selector = EncoderSelector ?? string.Empty,
                        SourceA = EncoderSourceA ?? string.Empty,
                        SourceB = EncoderSourceB ?? string.Empty,
                        PulseEquivalentUm = double.TryParse(PulseEquivalentUm, out var pe) ? pe : 0,
                        DividerRatio = double.TryParse(DividerRatio, out var dr) ? dr : 1,
                    }
                    : null,
            },

            FrameControl = new FrameControlConfig
            {
                ImageHeight = int.TryParse(ImageHeight, out var h) ? h : 0,
                FrameTimeoutMs = int.TryParse(FrameTimeoutMs, out var ft) ? ft : 0,
                TriggerEnable = FrameTriggerEnable,
                TriggerSource = FrameTriggerSource ?? string.Empty,
                TriggerActivation = NullIfBlank(FrameTriggerActivation),
                StreamSelector = NullIfBlank(StreamSelector),
                CameraType = NullIfBlank(CameraType),
            },
        };

        private static string NullIfBlank(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

        /// <summary>属性树枚举结果的全集，界面上按 <see cref="NodeFilter"/> / <see cref="WritableNodesOnly"/> 过滤后展示。</summary>
        private IReadOnlyList<GenICamNode> _allNodes = Array.Empty<GenICamNode>();

        /// <summary>按关键字与"只看可写"过滤属性树。</summary>
        private void ApplyNodeFilter()
        {
            Nodes.Clear();

            IEnumerable<GenICamNode> query = _allNodes;

            if (WritableNodesOnly)
                query = query.Where(n => n.IsWritable);

            if (!string.IsNullOrWhiteSpace(NodeFilter))
            {
                string key = NodeFilter.Trim();
                query = query.Where(n =>
                    n.Name.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || n.DisplayName.Contains(key, StringComparison.OrdinalIgnoreCase)
                    || n.Category.Contains(key, StringComparison.OrdinalIgnoreCase));
            }

            foreach (var n in query.OrderBy(n => n.Category).ThenBy(n => n.Name))
                Nodes.Add(n);

            NodeSummary = _allNodes.Count == 0
                ? "尚未枚举"
                : $"共 {_allNodes.Count} 个可用节点，其中可写 {_allNodes.Count(n => n.IsWritable)} 个；当前显示 {Nodes.Count} 个";
        }

        private void RefreshCameraInfo()
        {
            if (_camera == null) return;

            string model = string.IsNullOrEmpty(_camera.ModelName) ? "-" : _camera.ModelName;
            string sn = string.IsNullOrEmpty(_camera.SerialNumber) ? "-" : _camera.SerialNumber;
            CameraInfo = $"{model} | SN: {sn} | 传输层: {_camera.TransportLayer}";

            FrameControlTarget = _camera.HasFrameGrabber
                ? "经采集卡：帧长/帧触发下发到采集卡节点树"
                : "相机直连：帧长/帧触发下发到相机自身节点树（无帧超时节点）";
        }

        #endregion

        #region 【帧接收与预览】

        /// <summary>
        /// 帧事件回调。运行在 SDK 取流线程上，这里只做一次引用交换，
        /// 绝不能在此做图像转换或触碰 UI —— 那会直接拖慢取流甚至丢帧。
        /// </summary>
        private void OnFrameReceived(object sender, LineScanFrame frame)
        {
            Interlocked.Exchange(ref _pendingFrame, frame);
            Interlocked.Increment(ref _fpsWindowCount);
        }

        private void OnRenderTimerTick(object sender, EventArgs e)
        {
            var frame = Interlocked.Exchange(ref _pendingFrame, null);
            if (frame == null) return;

            UpdateFrameStats(frame);

            PreviewImage = LineScanPreview.TryBuild(frame, out string hint);
            PreviewHint = hint;
            RaisePropertyChanged(nameof(HasPreview));
        }

        private void UpdateFrameStats(LineScanFrame frame)
        {
            double elapsed = (DateTime.Now - _fpsWindowStart).TotalSeconds;
            double fps = 0;
            if (elapsed >= 1.0)
            {
                fps = _fpsWindowCount / elapsed;
                _fpsWindowStart = DateTime.Now;
                Interlocked.Exchange(ref _fpsWindowCount, 0);
                _lastFps = fps;
            }

            FrameStats = $"帧号 {frame.FrameNumber} | {frame.Width}×{frame.Height} | "
                + $"{frame.SizeBytes / 1024.0 / 1024.0:F2}MB | {frame.PixelFormat} | {_lastFps:F1} fps";
        }

        private double _lastFps;

        #endregion

        #region 【私有辅助】

        /// <summary>统一的异步命令外壳：吞掉异常并落到日志栏，不让 async void 击穿进程。</summary>
        private async void RunAsync(string opName, Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception ex)
            {
                _logger.Error($"[{DeviceName}] {opName}失败：{ex.Message}", ex);
            }
        }

        private void Log(string message) => _logger.Info($"[{DeviceName}] {message}");

        private void LogWarn(string message) => _logger.Warn($"[{DeviceName}] {message}");

        private void OnStatusTimerTick(object sender, EventArgs e)
        {
            if (_baseDevice == null) return;

            IsConnected = _baseDevice.IsConnected;
            IsAlarm = _baseDevice.HasAlarm;
            IsGrabbing = _camera?.IsGrabbing ?? false;
        }

        #endregion
    }
}
