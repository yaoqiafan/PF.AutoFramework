using PF.Core.Constants;
using PF.Core.Entities.Hardware;
using PF.Core.Entities.Hardware.Vision;
using PF.Core.Enums.Hardware.Vision;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Hardware.Camera.NetworkCamera;
using PF.Infrastructure.Hardware;
using PF.Modules.Debug.Dialogs;
using PF.Modules.Debug.Models;
using PF.UI.Infrastructure.PrismBase;
using System.Diagnostics;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>
    /// 网络摄像机「单台实例」调试页 ViewModel：只读参数展示 + 预览 + 抓图。
    ///
    /// <para><b>参数只读</b>：本页不提供编辑入口。要改 IP/账号/通道等，请在设备树点
    /// 「[网络摄像机]」分组节点，到 <see cref="NetworkCameraGroupViewModel"/> 页统一编辑保存
    /// （那里不自动重载——全量重载会断开所有设备）。</para>
    ///
    /// <para><b>预览实现（P1 定时抓图轮询）</b>：DispatcherTimer 每 300ms 调一次
    /// <see cref="INetworkCamera.CaptureJpegAsync"/> 喂 pf:ImageViewer，约 2~5 fps——
    /// 用于确认视野/对焦/曝光足够，不是真视频流（那需要 RealPlay + PlayCtrl 解码链路）。
    /// 离开页面必须停表（OnNavigatedFrom），否则会持续向相机发抓图请求把相机打满。</para>
    /// </summary>
    public class NetworkCameraDebugViewModel : RegionViewModelBase
    {
        /// <summary>预览轮询间隔（ms）。低于 150ms 对多数 IPC 就是持续打码流接口，帧率收益为零。</summary>
        private const int PreviewIntervalMs = 300;

        /// <summary>
        /// 抓图串行化。手动抓图与预览轮询可能同时打到同一个 _userId 上，
        /// SDK 未保证同一句柄的并发调用安全，故两者共用这一把门。
        /// </summary>
        private readonly SemaphoreSlim _captureGate = new(1, 1);

        private readonly IHardwareManagerService _hardwareManager;

        private DispatcherTimer? _previewTimer;
        private bool _previewBusy;

        /// <summary>以硬件管理服务初始化（只用于回读抓图缓冲区配置值做展示）。</summary>
        public NetworkCameraDebugViewModel(IHardwareManagerService hardwareManager)
        {
            _hardwareManager = hardwareManager;

            ConnectCommand       = new DelegateCommand(() => _ = ExecuteAsync(c => c.ConnectAsync()));
            DisconnectCommand    = new DelegateCommand(() => _ = ExecuteAsync(c => c.DisconnectAsync()));
            ResetCommand         = new DelegateCommand(() => _ = ExecuteAsync(c => c.ResetAsync()));
            SimulateAlarmCommand = new DelegateCommand(() =>
            {
                // SimulateAlarm 定义在 BaseDevice 上（与 IO/轴等调试页同一调用方式），接口不暴露
                if (Camera is BaseDevice bd)
                    bd.SimulateAlarm(
                        AlarmCodes.Hardware.NetworkCameraCaptureFailed, "调试页面手动模拟网络摄像机报警");
            });

            CaptureCommand       = new DelegateCommand(() => _ = CaptureOnceAsync());
            CaptureAndSaveCommand = new DelegateCommand(() => _ = CaptureAndSaveAsync());
            StartPreviewCommand  = new DelegateCommand(StartPreview, () => !IsPreviewing).ObservesProperty(() => IsPreviewing);
            StopPreviewCommand   = new DelegateCommand(StopPreview, () => IsPreviewing).ObservesProperty(() => IsPreviewing);
            ShowParamDialogCommand = new DelegateCommand(ShowParamDialog);
        }

        // ── 设备与参数（只读） ───────────────────────────────────────────────────

        private INetworkCamera? _camera;

        /// <summary>当前摄像机实例。</summary>
        public INetworkCamera? Camera
        {
            get => _camera;
            private set
            {
                if (SetProperty(ref _camera, value))
                {
                    DetachCameraEvents();
                    AttachCameraEvents();
                    RaiseAllReadOnlyChanged();
                }
            }
        }

        /// <summary>头部标题（设备名）。</summary>
        public string DeviceName => Camera?.DeviceName ?? "未选中摄像机";

        /// <summary>头部汇总信息（文案格式与其余硬件调试页一致）。</summary>
        public string HeaderInfo
            => Camera == null
                ? "未选中摄像机"
                : $"设备类别: Camera | 模拟状态: {(Camera.IsSimulated ? "是" : "否")}";

        // ── 顶部设备状态展示 ─────────────────────────────────────────────────────
        // 本页不内嵌参数表单（改参数走顶部「参数设置」弹窗），这里只留状态摘要：
        // 端点、序列号、当前通道、缓冲区大小，以及连接/仿真标记。

        /// <summary>通道号（运行期实际使用值）。</summary>
        public int ReadOnlyChannel => Camera?.ActiveChannel ?? 0;

        /// <summary>抓图缓冲区起始大小（MB）。来自硬件配置，未配置时为默认 8。</summary>
        public string ReadOnlyBufferMb { get; private set; } = "8";

        /// <summary>设备序列号（登录后填充）。</summary>
        public string ReadOnlySerialNumber
            => string.IsNullOrWhiteSpace(Camera?.SerialNumber) ? "—" : Camera!.SerialNumber;

        /// <summary>只读：端点显示。</summary>
        public string Endpoint => Camera == null ? "—" : $"{Camera.IPAddress}:{Camera.Port}";

        /// <summary>列表项连接状态文本。</summary>
        public string ConnectionStateText => Camera?.IsConnected == true ? "● 已登录" : "○ 未登录";

        /// <summary>设备是否已连接（供 XAML DataTrigger 驱动状态胶囊配色）。</summary>
        public bool IsCameraConnected => Camera?.IsConnected == true;

        /// <summary>设备是否处于仿真模式（供 XAML 控制「模拟」标记显隐）。</summary>
        public bool IsCameraSimulated => Camera?.IsSimulated == true;

        /// <summary>可用通道（登录后填充）。</summary>
        public IReadOnlyList<NetworkCameraChannel> Channels
            => Camera?.Channels ?? Array.Empty<NetworkCameraChannel>();

        private NetworkCameraChannel? _selectedChannel;
        /// <summary>抓图通道下拉选择；null = 使用设备当前通道。</summary>
        public NetworkCameraChannel? SelectedChannel
        {
            get => _selectedChannel;
            set => SetProperty(ref _selectedChannel, value);
        }

        /// <summary>传给 CaptureJpegAsync 的通道号：null = 用当前通道。</summary>
        private int? CaptureChannel => SelectedChannel?.ChannelNo;

        private bool _isBusy;
        /// <summary>正在执行阻塞操作（登录/注销/复位，防重复点击）。</summary>
        public bool IsBusy
        {
            get => _isBusy;
            private set => SetProperty(ref _isBusy, value);
        }

        private string _captureStatus = string.Empty;
        /// <summary>单次抓图结果。</summary>
        public string CaptureStatus { get => _captureStatus; private set => SetProperty(ref _captureStatus, value); }

        // ── 预览 ─────────────────────────────────────────────────────────────────

        private BitmapFrame? _previewImage;
        /// <summary>预览图（类型必须是 BitmapFrame：ImageViewer 的 ImageSource 依赖属性就是该类型）。</summary>
        public BitmapFrame? PreviewImage
        {
            get => _previewImage;
            private set
            {
                SetProperty(ref _previewImage, value);
                RaisePropertyChanged(nameof(HasPreview));
            }
        }

        /// <summary>是否有可显示的预览图（控制占位提示的显隐）。</summary>
        public bool HasPreview => PreviewImage != null;

        private string _previewHint = "尚未预览";
        /// <summary>预览占位/中断提示。</summary>
        public string PreviewHint { get => _previewHint; private set => SetProperty(ref _previewHint, value); }

        private string _previewInfoText = string.Empty;
        /// <summary>预览状态条（帧大小 / 抓图耗时 / 有效帧率）。</summary>
        public string PreviewInfoText { get => _previewInfoText; private set => SetProperty(ref _previewInfoText, value); }

        private bool _isPreviewing;
        /// <summary>预览是否进行中。</summary>
        public bool IsPreviewing
        {
            get => _isPreviewing;
            private set => SetProperty(ref _isPreviewing, value);
        }

        // ── 命令 ─────────────────────────────────────────────────────────────────

        /// <summary>登录选中摄像机。</summary>
        public DelegateCommand ConnectCommand { get; }

        /// <summary>注销选中摄像机。</summary>
        public DelegateCommand DisconnectCommand { get; }

        /// <summary>复位选中摄像机（登出重登）。</summary>
        public DelegateCommand ResetCommand { get; }

        /// <summary>模拟硬件报警（验证报警级联链路）。</summary>
        public DelegateCommand SimulateAlarmCommand { get; }

        /// <summary>单次抓图。</summary>
        public DelegateCommand CaptureCommand { get; }

        /// <summary>抓图并直接落盘（格式/目录取设备配置）。</summary>
        public DelegateCommand CaptureAndSaveCommand { get; }

        /// <summary>开始预览。</summary>
        public DelegateCommand StartPreviewCommand { get; }

        /// <summary>停止预览。</summary>
        public DelegateCommand StopPreviewCommand { get; }

        /// <summary>打开参数设置弹窗（本页不内嵌参数区）。</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        // ── Prism 导航生命周期 ───────────────────────────────────────────────────

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Device")) return;

            var cam = navigationContext.Parameters.GetValue<INetworkCamera>("Device");

            // 抓图缓冲区只在硬件配置里，设备接口不暴露——重新进页面时从配置读一次
            ReadOnlyBufferMb = ReadBufferMb(cam);

            Camera = cam;
            SelectedChannel = null;
            PreviewImage = null;
            PreviewHint = "尚未预览";
            PreviewInfoText = string.Empty;
            CaptureStatus = string.Empty;

            RaiseAllReadOnlyChanged();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            // 必须停表：不停会在离开页面后持续向相机发抓图请求
            StopPreview();

            // 解除设备事件订阅：设备实例由 HardwareManagerService 长期持有，
            // 本页 KeepAlive 恒为 false，离开即丢实例——不解绑会随设备活到进程结束。
            DetachCameraEvents();

            base.OnNavigatedFrom(navigationContext);
        }

        // ── 设备事件 ─────────────────────────────────────────────────────────────

        private void AttachCameraEvents()
        {
            if (Camera is System.ComponentModel.INotifyPropertyChanged notifier)
                notifier.PropertyChanged += OnCameraPropertyChanged;
        }

        private void DetachCameraEvents()
        {
            if (_camera is System.ComponentModel.INotifyPropertyChanged notifier)
                notifier.PropertyChanged -= OnCameraPropertyChanged;
        }

        /// <summary>设备状态变化 → 刷新只读展示（连接态/序列号/通道）。</summary>
        private void OnCameraPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(INetworkCamera.IsConnected) or nameof(INetworkCamera.HasAlarm)
                or nameof(INetworkCamera.SerialNumber) or nameof(INetworkCamera.Channels)
                or nameof(INetworkCamera.ActiveChannel))
            {
                RaiseAllReadOnlyChanged();
            }
        }

        private void RaiseAllReadOnlyChanged()
        {
            RaisePropertyChanged(nameof(DeviceName));
            RaisePropertyChanged(nameof(HeaderInfo));
            RaisePropertyChanged(nameof(ReadOnlyChannel));
            RaisePropertyChanged(nameof(ReadOnlySerialNumber));
            RaisePropertyChanged(nameof(Endpoint));
            RaisePropertyChanged(nameof(ConnectionStateText));
            RaisePropertyChanged(nameof(IsCameraConnected));
            RaisePropertyChanged(nameof(IsCameraSimulated));
            RaisePropertyChanged(nameof(Channels));
        }

        /// <summary>从硬件配置读抓图缓冲区大小（MB）；缺失时按默认 8。</summary>
        private string ReadBufferMb(INetworkCamera cam)
        {
            // 设备层把 CaptureBufferMB 转成了字节，这里回读原始配置值展示，避免显示 "8388608"
            var p = _hardwareManager.GetConfig(cam.DeviceId)?.ConnectionParameters;
            return p != null && p.TryGetValue("CaptureBufferMB", out var v) && !string.IsNullOrWhiteSpace(v)
                ? v
                : "8";
        }

        // ── 参数弹窗 ─────────────────────────────────────────────────────────────

        /// <summary>
        /// 打开参数设置弹窗（本页不内嵌参数区，与串口/通讯调试页同一交互）。
        /// 确认后只写库、不自动重载——ReloadAllAsync 是全量断开所有设备。
        /// </summary>
        private void ShowParamDialog()
        {
            if (Camera is not { } cam) return;

            var config = _hardwareManager.GetConfig(cam.DeviceId);
            if (config == null)
            {
                MessageService.ShowSystemMessage("未找到该设备的配置记录，无法修改参数。", "网络摄像机",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                return;
            }

            var p = config.ConnectionParameters;
            var model = new NetworkCameraParamEditModel
            {
                DeviceName  = config.DeviceName,
                IsEnabled   = config.IsEnabled,
                IsSimulated = config.IsSimulated,
                Ip          = p.GetValueOrDefault("IP", cam.IPAddress),
                Port        = p.GetValueOrDefault("Port", cam.Port.ToString()),
                UserName    = p.GetValueOrDefault("UserName", cam.UserName),
                Password    = p.GetValueOrDefault("Password", string.Empty),
                Channel     = p.GetValueOrDefault("Channel", "0"),
                BufferMb    = p.GetValueOrDefault("CaptureBufferMB", "8"),
                SaveFormat    = p.GetValueOrDefault("SaveFormat", "Jpeg"),
                SaveDirectory = p.GetValueOrDefault("SaveDirectory", string.Empty),
            };

            var dialogParams = new DialogParameters { { "Data", model } };
            DialogService.ShowDialog(nameof(NetworkCameraParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var model = result.Parameters.GetValue<NetworkCameraParamEditModel>("CallBackParamItem");
            if (model == null || Camera is not { } cam) return;

            // 数字字段先校验，避免把 "abc" 写进配置后设备实例化时抛异常
            if (!int.TryParse(model.Port.Trim(), out _))
            {
                SaveParamError("端口必须是数字");
                return;
            }
            if (!int.TryParse(model.Channel.Trim(), out _))
            {
                SaveParamError("通道号必须是数字");
                return;
            }
            if (!int.TryParse(model.BufferMb.Trim(), out _))
            {
                SaveParamError("抓图缓冲区(MB)必须是数字");
                return;
            }
            if (!string.IsNullOrWhiteSpace(model.SaveFormat) &&
                !Enum.TryParse<ImageFileFormat>(model.SaveFormat.Trim(), true, out _))
            {
                SaveParamError("存图格式只能是 Bmp / Jpeg / Tiff / Png");
                return;
            }

            var config = _hardwareManager.GetConfig(cam.DeviceId);
            if (config == null)
            {
                SaveParamError("未找到该设备的配置记录");
                return;
            }

            config.DeviceName  = model.DeviceName ?? string.Empty;
            config.IsEnabled   = model.IsEnabled;
            config.IsSimulated = model.IsSimulated;
            config.ConnectionParameters["IP"]              = (model.Ip ?? string.Empty).Trim();
            config.ConnectionParameters["Port"]            = model.Port.Trim();
            config.ConnectionParameters["UserName"]        = (model.UserName ?? string.Empty).Trim();
            config.ConnectionParameters["Password"]        = model.Password ?? string.Empty;
            config.ConnectionParameters["Channel"]         = model.Channel.Trim();
            config.ConnectionParameters["CaptureBufferMB"] = model.BufferMb.Trim();
            // 留空才存空串：设备侧据此回退到默认目录 {ConfigPath}\NetworkCamera\{DeviceId}
            config.ConnectionParameters["SaveFormat"]      = model.SaveFormat?.Trim() ?? string.Empty;
            config.ConnectionParameters["SaveDirectory"]   = model.SaveDirectory?.Trim() ?? string.Empty;

            try
            {
                await _hardwareManager.SaveConfigAsync(config);
            }
            catch (Exception ex)
            {
                // 写库失败要显式反馈：内存缓存可能已更新、数据库没有，静默会让用户以为保存成功
                SaveParamError($"写库失败：{ex.Message}");
                return;
            }

            // 缓冲区大小是纯展示项，保存后立即刷新（其余字段要重载才生效）
            ReadOnlyBufferMb = ReadBufferMb(cam);
            RaisePropertyChanged(nameof(ReadOnlyBufferMb));

            CaptureStatus = "参数已保存。新参数需重新加载硬件后生效（当前连接仍使用旧参数）。";
        }

        private void SaveParamError(string message)
        {
            CaptureStatus = $"参数保存失败：{message}";
            MessageService.ShowSystemMessage(message, "网络摄像机参数",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }

        /// <summary>
        /// 抓图并直接落盘：格式与目录取设备配置（<see cref="INetworkCamera.SaveFormat"/> /
        /// <see cref="INetworkCamera.SaveDirectory"/>），不经过系统参数。
        /// 与「抓图」共用同一把抓图门，避免和预览轮询抢同一个 SDK 句柄。
        /// </summary>
        private async Task CaptureAndSaveAsync()
        {
            if (Camera is not { } cam) return;

            if (!cam.IsConnected)
            {
                CaptureStatus = "相机未连接，请先登录";
                return;
            }

            await _captureGate.WaitAsync();
            try
            {
                CaptureStatus = "正在抓图并存盘…";
                var watch = Stopwatch.StartNew();
                var path = await cam.CaptureAndSaveAsync(CaptureChannel);
                watch.Stop();

                CaptureStatus = $"已存盘 {path}（耗时 {watch.ElapsedMilliseconds} ms）";
            }
            catch (Exception ex)
            {
                CaptureStatus = $"抓图存盘失败: {ex.Message}";
            }
            finally
            {
                _captureGate.Release();
            }
        }

        // ── 设备操作 ─────────────────────────────────────────────────────────────

        private async Task ExecuteAsync(Func<IHardwareDevice, Task> action)
        {
            if (Camera is not { } cam || IsBusy) return;

            IsBusy = true;
            try
            {
                await action(cam);
                RaiseAllReadOnlyChanged();
            }
            catch (Exception ex)
            {
                MessageService.ShowSystemMessage($"操作失败: {ex.Message}", "网络摄像机",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── 抓图与预览 ───────────────────────────────────────────────────────────

        private async Task CaptureOnceAsync()
        {
            if (Camera is not { } cam) return;

            if (!cam.IsConnected)
            {
                CaptureStatus = "相机未连接，请先登录";
                return;
            }

            try
            {
                var (frame, ms) = await CaptureAsync(cam, CaptureChannel);
                var bmp = NetworkCameraPreview.TryBuild(frame.Data, out var hint);
                PreviewImage = bmp;
                PreviewHint = bmp == null ? hint : string.Empty;
                CaptureStatus = bmp == null
                    ? $"抓图成功但预览失败：{hint}（{frame.SizeBytes / 1024.0:F1} KB，通道 {frame.Channel}）"
                    : $"抓图成功 {frame.SizeBytes / 1024.0:F1} KB，耗时 {ms} ms（通道 {frame.Channel}）";
            }
            catch (Exception ex)
            {
                CaptureStatus = $"抓图失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 抓一帧并返回耗时。
        /// <para>耗时用局部 Stopwatch 而非共享字段：手动抓图与预览轮询会并发走到这里，
        /// 共享一个 Stopwatch 会互相 Restart/Stop 踩出对方的数值。</para>
        /// <para>整个过程串行化在 <see cref="_captureGate"/> 上——两路可能同时拿同一个 SDK 句柄抓图。</para>
        /// </summary>
        private async Task<(NetworkCameraFrame Frame, long ElapsedMs)> CaptureAsync(
            INetworkCamera cam, int? channel)
        {
            await _captureGate.WaitAsync();
            try
            {
                var watch = Stopwatch.StartNew();
                var frame = await cam.CaptureJpegAsync(channel);
                watch.Stop();
                return (frame, watch.ElapsedMilliseconds);
            }
            finally
            {
                _captureGate.Release();
            }
        }

        private void StartPreview()
        {
            if (Camera is not { IsConnected: true })
            {
                PreviewHint = "相机未连接，请先登录再预览";
                return;
            }

            if (_previewTimer == null)
            {
                _previewTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromMilliseconds(PreviewIntervalMs)
                };
                _previewTimer.Tick += OnPreviewTick;
            }

            PreviewHint = HasPreview ? string.Empty : "等待首帧…";
            _previewTimer.Start();
            IsPreviewing = true;
        }

        private void StopPreview()
        {
            _previewTimer?.Stop();
            IsPreviewing = false;
        }

        /// <summary>
        /// 轮询节拍：防重入（上一帧没回来就跳过本轮——相机抓图耗时可能超过轮询间隔），
        /// 抓图在后台线程，解码/绑定回 UI 线程。
        /// </summary>
        private void OnPreviewTick(object? sender, EventArgs e)
        {
            if (_previewBusy || Camera is not { IsConnected: true } cam)
                return;

            _previewBusy = true;
            int? channel = CaptureChannel;
            _ = Task.Run(async () =>
            {
                try
                {
                    // 与手动抓图共用一把门：两路不能同时拿同一个 SDK 句柄
                    await _captureGate.WaitAsync();
                    long ms;
                    NetworkCameraFrame frame;
                    try
                    {
                        var watch = Stopwatch.StartNew();
                        frame = await cam.CaptureJpegAsync(channel);
                        watch.Stop();
                        ms = watch.ElapsedMilliseconds;
                    }
                    finally
                    {
                        _captureGate.Release();
                    }

                    var bmp = NetworkCameraPreview.TryBuild(frame.Data, out var hint);
                    double kb = frame.SizeBytes / 1024.0;
                    // 有效帧率受"轮询间隔"与"单帧耗时"中较大者限制
                    double fps = 1000.0 / Math.Max(PreviewIntervalMs, ms);

                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!ReferenceEquals(Camera, cam))
                            return;   // 抓图期间已切换设备，丢弃过期帧

                        if (bmp != null)
                        {
                            PreviewImage = bmp;
                            PreviewHint = string.Empty;
                            PreviewInfoText = $"预览中 · {fps:F1} fps · 抓图 {kb:F1} KB / {ms} ms";
                        }
                        else
                        {
                            PreviewHint = hint;
                        }
                    });
                }
                catch (Exception ex)
                {
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // 与成功分支同样的过期帧守卫：切换设备后旧任务的异常不该覆盖新一台的提示
                        if (!ReferenceEquals(Camera, cam))
                            return;

                        PreviewHint = $"预览中断: {ex.Message}";
                    });
                }
                finally
                {
                    _previewBusy = false;
                }
            });
        }
    }
}
