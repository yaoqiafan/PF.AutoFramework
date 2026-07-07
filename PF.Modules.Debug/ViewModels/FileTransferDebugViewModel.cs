using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums;
using PF.Core.Enums.FileTransfer;
using PF.Core.Events.FileTransfer;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.FileTransfer;
using PF.Modules.Debug.Dialogs;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>FileTransfer 通道调试 ViewModel，接收 NavigationParameter("Instance") 传入的 IFileTransferChannel 实例</summary>
    public class FileTransferDebugViewModel : RegionViewModelBase, IDisposable
    {
        private readonly ICommunicationManagerService _commManager;
        private IFileTransferChannel? _channel;
        private string _instanceId = string.Empty;
        private readonly DispatcherTimer _pollTimer;

        private string _channelName = "未选中";
        /// <summary>通道名称</summary>
        public string ChannelName { get => _channelName; set => SetProperty(ref _channelName, value); }

        private string _roleText = "——";
        /// <summary>角色</summary>
        public string RoleText { get => _roleText; set => SetProperty(ref _roleText, value); }

        private string _statusText = "——";
        /// <summary>通道整体状态</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        /// <summary>各 Lane 状态行（增量更新，避免整表重建导致闪烁）</summary>
        public ObservableCollection<LaneStatusRowModel> LaneRows { get; } = new();

        /// <summary>事件日志（最新在前）</summary>
        public ObservableCollection<string> LogEntries { get; } = new();

        private int _testSizeMb = 10;
        /// <summary>测试数据大小（MB）</summary>
        public int TestSizeMb { get => _testSizeMb; set => SetProperty(ref _testSizeMb, value); }

        private string _testTag = "DebugTest";
        /// <summary>随传输携带的业务标签（FileTransferMetadata.Tag），内存/文件两种发送共用</summary>
        public string TestTag { get => _testTag; set => SetProperty(ref _testTag, value); }

        private string _sendFilePath = string.Empty;
        /// <summary>待发送文件的完整路径（SendFileAsync 测试入口）</summary>
        public string SendFilePath
        {
            get => _sendFilePath;
            set
            {
                if (SetProperty(ref _sendFilePath, value))
                    SendFileCommand.RaiseCanExecuteChanged();
            }
        }

        private double _progressPercent;
        /// <summary>当前传输进度百分比</summary>
        public double ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }

        private string _progressText = "——";
        /// <summary>当前传输进度文本（已传/总量）</summary>
        public string ProgressText { get => _progressText; set => SetProperty(ref _progressText, value); }

        private bool _isSending;
        /// <summary>是否正在发送（防止重复点击，内存/文件两种发送共用）</summary>
        public bool IsSending { get => _isSending; set => SetProperty(ref _isSending, value); }

        private bool _chunkDiagnosticsEnabled;
        /// <summary>分片级诊断事件开关，直通 IFileTransferChannel.EnableChunkLevelDiagnostics</summary>
        public bool ChunkDiagnosticsEnabled
        {
            get => _chunkDiagnosticsEnabled;
            set
            {
                if (SetProperty(ref _chunkDiagnosticsEnabled, value) && _channel != null)
                    _channel.EnableChunkLevelDiagnostics = value;
            }
        }

        // ── 接收数据消费方式（演示 FileTransferCompletedEventArgs 的统一消费入口） ──────────
        // 三个入口对内存/落盘两种交付形态一视同仁，消费方无需分辨 Data/FilePath。
        // 四个 bool 属性共享一个枚举字段，是 RadioButton 组绑定的标准做法（只响应置 true）。

        private ReceiveConsumeMode _consumeMode = ReceiveConsumeMode.LogOnly;

        /// <summary>仅记录完成日志，不消费数据</summary>
        public bool ConsumeModeLogOnly
        {
            get => _consumeMode == ReceiveConsumeMode.LogOnly;
            set { if (value) SetConsumeMode(ReceiveConsumeMode.LogOnly); }
        }

        /// <summary>OpenReadStream 流式读取示例</summary>
        public bool ConsumeModeOpenStream
        {
            get => _consumeMode == ReceiveConsumeMode.OpenReadStream;
            set { if (value) SetConsumeMode(ReceiveConsumeMode.OpenReadStream); }
        }

        /// <summary>SaveToFileAsync 保存为文件示例</summary>
        public bool ConsumeModeSaveToFile
        {
            get => _consumeMode == ReceiveConsumeMode.SaveToFile;
            set { if (value) SetConsumeMode(ReceiveConsumeMode.SaveToFile); }
        }

        /// <summary>GetBytesAsync 读回内存示例</summary>
        public bool ConsumeModeGetBytes
        {
            get => _consumeMode == ReceiveConsumeMode.GetBytes;
            set { if (value) SetConsumeMode(ReceiveConsumeMode.GetBytes); }
        }

        /// <summary>是否选中了保存为文件模式（控制保存目录输入的可用性）</summary>
        public bool IsSaveModeSelected => _consumeMode == ReceiveConsumeMode.SaveToFile;

        private void SetConsumeMode(ReceiveConsumeMode mode)
        {
            if (_consumeMode == mode) return;
            _consumeMode = mode;
            RaisePropertyChanged(nameof(ConsumeModeLogOnly));
            RaisePropertyChanged(nameof(ConsumeModeOpenStream));
            RaisePropertyChanged(nameof(ConsumeModeSaveToFile));
            RaisePropertyChanged(nameof(ConsumeModeGetBytes));
            RaisePropertyChanged(nameof(IsSaveModeSelected));
            AppendLog($"[消费方式] 已切换为 {mode}");
        }

        private string _saveDirectory = Path.Combine(Path.GetTempPath(), "PFFileTransfer", "Saved");
        /// <summary>SaveToFileAsync 模式的保存目录</summary>
        public string SaveDirectory { get => _saveDirectory; set => SetProperty(ref _saveDirectory, value); }

        /// <summary>启动通道命令</summary>
        public DelegateCommand StartCommand { get; }
        /// <summary>停止通道命令</summary>
        public DelegateCommand StopCommand { get; }
        /// <summary>生成随机数据并发送的测试命令（SendAsync）</summary>
        public DelegateCommand SendTestDataCommand { get; }
        /// <summary>选择待发送文件命令</summary>
        public DelegateCommand BrowseFileCommand { get; }
        /// <summary>选择接收保存目录命令（SaveToFileAsync 消费模式使用）</summary>
        public DelegateCommand BrowseSaveDirCommand { get; }
        /// <summary>发送磁盘文件命令（SendFileAsync）</summary>
        public DelegateCommand SendFileCommand { get; }
        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化 FileTransfer 调试 ViewModel</summary>
        public FileTransferDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            StartCommand = new DelegateCommand(async () => await ExecuteStartAsync());
            StopCommand = new DelegateCommand(async () => await ExecuteStopAsync());
            SendTestDataCommand = new DelegateCommand(async () => await ExecuteSendTestDataAsync(), () => !IsSending && _channel != null);
            BrowseFileCommand = new DelegateCommand(ExecuteBrowseFile);
            BrowseSaveDirCommand = new DelegateCommand(ExecuteBrowseSaveDir);
            SendFileCommand = new DelegateCommand(async () => await ExecuteSendFileAsync(),
                () => !IsSending && _channel != null && !string.IsNullOrWhiteSpace(SendFilePath));
            ShowParamDialogCommand = new DelegateCommand(ExecuteShowParamDialog);

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _pollTimer.Tick += (_, _) => RefreshLanes();
        }

        /// <summary>
        /// 只有导航目标和当前已绑定的是同一个 InstanceId 才允许复用本实例，
        /// 这样同一个实例的调试页在本次程序运行期间反复进出时，LogEntries 等状态不会被清空重建。
        /// </summary>
        public override bool IsNavigationTarget(NavigationContext navigationContext)
        {
            if (!navigationContext.Parameters.ContainsKey("Instance")) return false;
            var target = navigationContext.Parameters.GetValue<IFileTransferChannel>("Instance");
            return target != null && (target as ICommunication)?.InstanceId == _instanceId;
        }

        /// <summary>本实例依赖 IsNavigationTarget 复用，必须保留在 Region 中才可能被匹配到。</summary>
        public override bool KeepAlive => true;

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Instance")) return;

            var channel = navigationContext.Parameters.GetValue<IFileTransferChannel>("Instance");
            if (channel == null) return;

            // 无论首次绑定还是复用旧实例重新导航进来，都先退订旧引用的事件，理由同 TcpServerDebugViewModel
            UnsubscribeEvents();
            _channel = channel;
            _instanceId = (_channel as ICommunication)?.InstanceId ?? string.Empty;
            BindToChannel();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollTimer.Stop();
            UnsubscribeEvents();
        }

        private void BindToChannel()
        {
            if (_channel == null) return;
            ChannelName = _channel.ChannelName;
            RoleText = _channel.Role.ToString();
            // 重载后拿到的是全新实例（EnableChunkLevelDiagnostics 回到默认 false），把界面上的开关状态重新应用上去
            _channel.EnableChunkLevelDiagnostics = ChunkDiagnosticsEnabled;
            RefreshStatus();
            RefreshLanes();
            SubscribeEvents();

            _pollTimer.Start();
            SendTestDataCommand.RaiseCanExecuteChanged();
            SendFileCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeEvents()
        {
            if (_channel == null) return;
            _channel.StateChanged += OnStateChanged;
            _channel.TransferProgress += OnTransferProgress;
            _channel.TransferCompleted += OnTransferCompleted;
            _channel.TransferFailed += OnTransferFailed;
            _channel.LaneStatusChanged += OnLaneStatusChanged;
            _channel.LaneReconnected += OnLaneReconnected;
            _channel.ChunkTransferred += OnChunkTransferred;
        }

        private void UnsubscribeEvents()
        {
            if (_channel == null) return;
            _channel.StateChanged -= OnStateChanged;
            _channel.TransferProgress -= OnTransferProgress;
            _channel.TransferCompleted -= OnTransferCompleted;
            _channel.TransferFailed -= OnTransferFailed;
            _channel.LaneStatusChanged -= OnLaneStatusChanged;
            _channel.LaneReconnected -= OnLaneReconnected;
            _channel.ChunkTransferred -= OnChunkTransferred;
        }

        private void RefreshStatus()
        {
            if (_channel == null) return;
            StatusText = _channel.Status.ToString();
        }

        private void RefreshLanes()
        {
            if (_channel == null) return;

            foreach (var lane in _channel.LaneStatuses)
            {
                var row = LaneRows.FirstOrDefault(r => r.LaneId == lane.LaneId);
                if (row == null)
                {
                    var link = _channel.Links.FirstOrDefault(k => k.LaneId == lane.LaneId);
                    var configured = link == null
                        ? "——"
                        : _channel.Role == FileTransferRole.Client
                            ? $"{link.LocalIp} → {link.RemoteIp}:{link.Port}"
                            : $"监听 {link.LocalIp}:{link.Port}";
                    row = new LaneStatusRowModel { LaneId = lane.LaneId, ConfiguredEndpoint = configured };
                    LaneRows.Add(row);
                }
                row.Update(lane);
            }
        }

        // ── 通道启停 ──────────────────────────────────────────────────────────

        private async Task ExecuteStartAsync()
        {
            if (_channel == null) return;
            var ok = await _channel.StartAsync();
            RefreshStatus();
            AppendLog(ok ? "[通道] 启动成功" : "[通道] 启动失败");
        }

        private async Task ExecuteStopAsync()
        {
            if (_channel == null) return;
            await _channel.StopAsync();
            RefreshStatus();
            AppendLog("[通道] 已停止");
        }

        // ── 发送测试 ──────────────────────────────────────────────────────────

        private void RaiseSendCommandsCanExecute()
        {
            SendTestDataCommand.RaiseCanExecuteChanged();
            SendFileCommand.RaiseCanExecuteChanged();
        }

        private FileTransferMetadata BuildMetadata(FileContentKind kind, string? fileExtension = null) => new()
        {
            Tag = string.IsNullOrWhiteSpace(TestTag) ? $"DebugTest_{DateTime.Now:HHmmss}" : TestTag,
            ContentKind = kind,
            FileExtension = fileExtension
        };

        private async Task ExecuteSendTestDataAsync()
        {
            if (_channel == null) return;

            if (TestSizeMb <= 0 || TestSizeMb > 512)
            {
                AppendLog("[发送] 测试数据大小需在 1~512 MB 之间（更大数据请走文件发送入口）");
                return;
            }

            IsSending = true;
            RaiseSendCommandsCanExecute();
            try
            {
                var data = new byte[TestSizeMb * 1024 * 1024];
                Random.Shared.NextBytes(data);

                var result = await _channel.SendAsync(data, BuildMetadata(FileContentKind.RawFile));

                AppendLog(result.Success
                    ? $"[发送完成] {TestSizeMb}MB，耗时 {result.Elapsed.TotalSeconds:F2}s，吞吐 {result.ThroughputMBps:F1}MB/s"
                    : $"[发送失败] {result.FailureReason}: {result.ErrorMessage}");
            }
            catch (Exception ex)
            {
                AppendLog($"[发送失败] {ex.Message}");
            }
            finally
            {
                IsSending = false;
                RaiseSendCommandsCanExecute();
            }
        }

        private void ExecuteBrowseFile()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择要发送的文件",
                Filter = "所有文件 (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
                SendFilePath = dialog.FileName;
        }

        private void ExecuteBrowseSaveDir()
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "选择接收数据的保存目录" };
            if (dialog.ShowDialog() == true)
                SaveDirectory = dialog.FolderName;
        }

        private async Task ExecuteSendFileAsync()
        {
            if (_channel == null || string.IsNullOrWhiteSpace(SendFilePath)) return;

            IsSending = true;
            RaiseSendCommandsCanExecute();
            try
            {
                AppendLog($"[发送] 开始发送文件：{SendFilePath}");
                var result = await _channel.SendFileAsync(SendFilePath,
                    BuildMetadata(FileContentKind.RawFile, Path.GetExtension(SendFilePath)));

                AppendLog(result.Success
                    ? $"[发送完成] 文件 {Path.GetFileName(SendFilePath)}，耗时 {result.Elapsed.TotalSeconds:F2}s，吞吐 {result.ThroughputMBps:F1}MB/s"
                    : $"[发送失败] {result.FailureReason}: {result.ErrorMessage}");
            }
            catch (Exception ex)
            {
                // 文件不存在/为空/超限等参数校验直接以异常抛出，转成日志而不是让 async void 崩掉进程
                AppendLog($"[发送失败] {ex.Message}");
            }
            finally
            {
                IsSending = false;
                RaiseSendCommandsCanExecute();
            }
        }

        // ── 参数修改：弹窗 → 保存到数据库 → 重新加载全部通讯实例 → 重新绑定刷新后的实例 ──────────
        // 通道级参数（角色/接收目录/内存阈值）与 Lane 级参数分层传入弹窗，弹窗内可新增/删除 Lane，
        // 保存时把完整 Lane 列表写回 LinksJson，不再只保留第一条导致其余 Lane 配置被覆盖丢失。

        private void ExecuteShowParamDialog()
        {
            if (string.IsNullOrEmpty(_instanceId)) return;
            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var linksJson = config.ConnectionParameters.GetValueOrDefault("LinksJson", "[]");
            var links = JsonSerializer.Deserialize<List<FileTransferLinkEndpoint>>(linksJson) ?? new List<FileTransferLinkEndpoint>();

            var channelParam = new FileTransferChannelParamViewModel
            {
                RoleText = config.ConnectionParameters.GetValueOrDefault("Role", nameof(FileTransferRole.Server)),
                // 未配置时展示 FileTransferOptions 的默认值，保存后才真正写入配置键
                ReceiveDirectory = config.ConnectionParameters.GetValueOrDefault(
                    "ReceiveDirectory", Path.Combine(Path.GetTempPath(), "PFFileTransfer")),
                InMemoryReceiveThresholdMb = int.TryParse(
                    config.ConnectionParameters.GetValueOrDefault("InMemoryReceiveThresholdMb", "16"), out var thresholdMb) && thresholdMb > 0
                    ? thresholdMb : 16
            };

            var laneParams = links.Select(link => new FileTransferLaneParamViewModel
            {
                LaneId = link.LaneId,
                LocalIp = link.LocalIp,
                Port = link.Port,
                RemoteIp = link.RemoteIp ?? string.Empty
            }).ToList();
            if (laneParams.Count == 0)
                laneParams.Add(new FileTransferLaneParamViewModel { LaneId = 0, LocalIp = "0.0.0.0" });

            var dialogParams = new DialogParameters
            {
                { "ChannelData", channelParam },
                { "LanesData", laneParams }
            };
            DialogService.ShowDialog(nameof(FileTransferParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var channelParam = result.Parameters.GetValue<FileTransferChannelParamViewModel>("CallBackChannelParam");
            var lanes = result.Parameters.GetValue<List<FileTransferLaneParamViewModel>>("CallBackLanes");
            if (channelParam == null || lanes == null) return;

            if (!Enum.TryParse<FileTransferRole>(channelParam.RoleText, ignoreCase: true, out var role))
            {
                AppendLog($"[参数] Role 填写不合法（需为 Server 或 Client），已取消保存: {channelParam.RoleText}");
                return;
            }

            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var newLinks = lanes.Select(lane => new FileTransferLinkEndpoint
            {
                LaneId = lane.LaneId,
                LocalIp = lane.LocalIp,
                Port = lane.Port,
                RemoteIp = string.IsNullOrWhiteSpace(lane.RemoteIp) ? null : lane.RemoteIp
            }).ToList();

            config.ConnectionParameters["Role"] = role.ToString();
            config.ConnectionParameters["LinksJson"] = JsonSerializer.Serialize(newLinks);
            config.ConnectionParameters["ReceiveDirectory"] = channelParam.ReceiveDirectory;
            config.ConnectionParameters["InMemoryReceiveThresholdMb"] = channelParam.InMemoryReceiveThresholdMb.ToString();
            await _commManager.SaveConfigAsync(config);

            AppendLog("[参数] 已保存，正在重新加载全部通讯实例...");
            await _commManager.ReloadAllAsync();

            // 重载后旧实例已被释放，必须重新从管理服务里取回刷新后的实例并重新订阅事件
            _pollTimer.Stop();
            UnsubscribeEvents();
            LaneRows.Clear();
            _channel = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == _instanceId) as IFileTransferChannel;
            BindToChannel();
            AppendLog("[参数] 重新加载完成");
        }

        // IFileTransferChannel 的事件在 Lane 的收发循环线程上触发，必须转回 UI 线程才能安全更新绑定属性
        private void OnStateChanged(object? sender, ChannelStateChangedEventArgs e) => RunOnUi(() =>
        {
            StatusText = e.NewStatus.ToString();
            AppendLog($"[通道状态] {e.OldStatus} → {e.NewStatus}");
        });

        private void OnTransferProgress(object? sender, FileTransferProgressEventArgs e) => RunOnUi(() =>
        {
            ProgressPercent = e.PercentComplete;
            ProgressText = $"{e.BytesTransferred / 1048576.0:F1} / {e.TotalBytes / 1048576.0:F1} MB";
        });

        private void OnTransferCompleted(object? sender, FileTransferCompletedEventArgs e)
        {
            RunOnUi(() =>
            {
                // 直接读 Data/FilePath 仅为调试展示通道内部的交付形态；实际消费走下面的统一入口示例
                var delivery = !e.HasPayload ? "" : e.Data != null ? "，内存交付" : $"，已落盘：{e.FilePath}";
                AppendLog($"[{e.Direction}完成] {e.Metadata.Tag}，{e.Result.ThroughputMBps:F1}MB/s{delivery}");
            });

            // 按界面选择的方式演示统一消费入口。消费涉及磁盘 I/O，不能阻塞通道的收尾路径，异步脱开执行
            if (e.Direction == TransferDirection.Received && e.HasPayload && _consumeMode != ReceiveConsumeMode.LogOnly)
                _ = ConsumeReceivedAsync(e, _consumeMode);
        }

        /// <summary>
        /// 接收数据的三种统一消费方式示例：不论通道内部是内存重组还是落盘交付，代码完全一致，
        /// 无需判断 e.Data / e.FilePath 谁有值。
        /// </summary>
        private async Task ConsumeReceivedAsync(FileTransferCompletedEventArgs e, ReceiveConsumeMode mode)
        {
            try
            {
                switch (mode)
                {
                    case ReceiveConsumeMode.OpenReadStream:
                    {
                        // 入口一 OpenReadStream()：拿到只读流按业务协议解析即可，
                        // 内存模式下是 MemoryStream、落盘模式下是异步 FileStream。这里以流式读完统计长度为例
                        await using var stream = e.OpenReadStream();
                        var buffer = new byte[81920];
                        long total = 0;
                        int read;
                        while ((read = await stream.ReadAsync(buffer)) > 0) total += read;
                        AppendLogSafe($"[消费·OpenReadStream] 流式读取完成，共 {total / 1048576.0:F2} MB（大文件全程不整块占用内存）");
                        break;
                    }
                    case ReceiveConsumeMode.SaveToFile:
                    {
                        // 入口二 SaveToFileAsync()：以文件形态交付到目标路径。
                        // 落盘模式同卷为零拷贝改名且所有权转移（无需再删临时文件），内存模式写盘
                        var tag = string.IsNullOrWhiteSpace(e.Metadata.Tag) ? "Received" : e.Metadata.Tag;
                        var safeTag = string.Join("_", tag.Split(Path.GetInvalidFileNameChars()));
                        var extension = string.IsNullOrWhiteSpace(e.Metadata.FileExtension) ? ".bin" : e.Metadata.FileExtension;
                        var target = Path.Combine(SaveDirectory, $"{safeTag}_{DateTime.Now:HHmmssfff}{extension}");
                        await e.SaveToFileAsync(target);
                        AppendLogSafe($"[消费·SaveToFileAsync] 已保存：{target}");
                        break;
                    }
                    case ReceiveConsumeMode.GetBytes:
                    {
                        // 入口三 GetBytesAsync()：以字节数组形态取回（落盘模式超过 512MB 会拒绝，防止误把大文件搬回内存）
                        var bytes = await e.GetBytesAsync();
                        var preview = Convert.ToHexString(bytes.AsSpan(0, Math.Min(4, bytes.Length)));
                        AppendLogSafe($"[消费·GetBytesAsync] 已取回 byte[{bytes.Length}]，前 4 字节：{preview}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // GetBytesAsync 超上限、目标目录不可写等异常在此转日志，不让 fire-and-forget 的任务异常无人观察
                AppendLogSafe($"[消费示例失败] {mode}: {ex.Message}");
            }
        }

        private void AppendLogSafe(string message) => RunOnUi(() => AppendLog(message));

        private void OnTransferFailed(object? sender, FileTransferFailedEventArgs e) => RunOnUi(() =>
            AppendLog($"[传输失败] {e.Reason}: {e.Message}"));

        private void OnLaneStatusChanged(object? sender, LaneStatusChangedEventArgs e) => RunOnUi(() =>
            AppendLog($"[Lane{e.Status.LaneId}] {(e.Status.IsConnected ? "已连接" : "已断开")} 重连次数={e.Status.ReconnectAttempts}"));

        private void OnLaneReconnected(object? sender, LaneStatusChangedEventArgs e) => RunOnUi(() =>
            AppendLog($"[Lane{e.Status.LaneId}] 重连成功，对端 {e.Status.RemoteEndPoint}"));

        private void OnChunkTransferred(object? sender, ChunkTransferredEventArgs e) => RunOnUi(() =>
            AppendLog($"[分片{(e.Direction == TransferDirection.Sent ? "发" : "收")}] Lane{e.LaneId} 偏移={e.ChunkOffset} 长度={e.ChunkLength}"));

        private void AppendLog(string message)
        {
            LogEntries.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
            while (LogEntries.Count > 200) LogEntries.RemoveAt(LogEntries.Count - 1);
        }

        private static void RunOnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);

        /// <summary>停止轮询定时器，防止内存泄漏</summary>
        public void Dispose() => _pollTimer.Stop();

        /// <inheritdoc/>
        public override void Destroy() => Dispose();
    }

    /// <summary>调试面板演示用：接收数据的消费方式（对应 FileTransferCompletedEventArgs 的统一消费入口）</summary>
    public enum ReceiveConsumeMode
    {
        /// <summary>仅记录完成日志，不消费数据</summary>
        LogOnly,

        /// <summary>OpenReadStream()：统一只读流，按业务协议流式解析</summary>
        OpenReadStream,

        /// <summary>SaveToFileAsync()：以文件形态交付到指定目录（落盘模式零拷贝移动并转移所有权）</summary>
        SaveToFile,

        /// <summary>GetBytesAsync()：以字节数组形态读回内存（超 512MB 拒绝）</summary>
        GetBytes
    }

    /// <summary>单条 Lane 状态展示行，支持原地更新以避免列表闪烁</summary>
    public class LaneStatusRowModel : BindableBase
    {
        /// <summary>Lane 编号</summary>
        public int LaneId { get; init; }

        /// <summary>配置的链路端点（Server 显示监听地址，Client 显示本端 → 对端）</summary>
        public string ConfiguredEndpoint { get; init; } = "——";

        private bool _isConnected;
        /// <summary>是否已连接</summary>
        public bool IsConnected { get => _isConnected; private set => SetProperty(ref _isConnected, value); }

        private string _remoteEndPoint = "——";
        /// <summary>对端地址</summary>
        public string RemoteEndPoint { get => _remoteEndPoint; private set => SetProperty(ref _remoteEndPoint, value); }

        private long _bytesSent;
        /// <summary>累计发送字节数</summary>
        public long BytesSent { get => _bytesSent; private set => SetProperty(ref _bytesSent, value); }

        private long _bytesReceived;
        /// <summary>累计接收字节数</summary>
        public long BytesReceived { get => _bytesReceived; private set => SetProperty(ref _bytesReceived, value); }

        private int _reconnectAttempts;
        /// <summary>累计重连次数</summary>
        public int ReconnectAttempts { get => _reconnectAttempts; private set => SetProperty(ref _reconnectAttempts, value); }

        /// <summary>用最新的 LaneStatus 快照刷新本行绑定属性</summary>
        public void Update(LaneStatus status)
        {
            IsConnected = status.IsConnected;
            RemoteEndPoint = status.RemoteEndPoint ?? "——";
            BytesSent = status.BytesSent;
            BytesReceived = status.BytesReceived;
            ReconnectAttempts = status.ReconnectAttempts;
        }
    }
}
