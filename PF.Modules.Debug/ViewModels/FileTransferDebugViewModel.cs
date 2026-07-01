using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums.FileTransfer;
using PF.Core.Events.FileTransfer;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.FileTransfer;
using PF.Modules.Debug.Dialogs;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
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

        private double _progressPercent;
        /// <summary>当前传输进度百分比</summary>
        public double ProgressPercent { get => _progressPercent; set => SetProperty(ref _progressPercent, value); }

        private bool _isSending;
        /// <summary>是否正在发送测试数据（防止重复点击）</summary>
        public bool IsSending { get => _isSending; set => SetProperty(ref _isSending, value); }

        /// <summary>生成随机数据并发送的测试命令</summary>
        public DelegateCommand SendTestDataCommand { get; }

        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化 FileTransfer 调试 ViewModel</summary>
        public FileTransferDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            SendTestDataCommand = new DelegateCommand(async () => await ExecuteSendTestDataAsync(), () => !IsSending && _channel != null);
            ShowParamDialogCommand = new DelegateCommand(ExecuteShowParamDialog);

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _pollTimer.Tick += (_, _) => RefreshLanes();
        }

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Instance")) return;

            _channel = navigationContext.Parameters.GetValue<IFileTransferChannel>("Instance");
            if (_channel == null) return;

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
            RefreshStatus();
            RefreshLanes();
            SubscribeEvents();

            _pollTimer.Start();
            SendTestDataCommand.RaiseCanExecuteChanged();
        }

        private void SubscribeEvents()
        {
            if (_channel == null) return;
            _channel.StateChanged += OnStateChanged;
            _channel.TransferProgress += OnTransferProgress;
            _channel.TransferCompleted += OnTransferCompleted;
            _channel.TransferFailed += OnTransferFailed;
            _channel.LaneStatusChanged += OnLaneStatusChanged;
        }

        private void UnsubscribeEvents()
        {
            if (_channel == null) return;
            _channel.StateChanged -= OnStateChanged;
            _channel.TransferProgress -= OnTransferProgress;
            _channel.TransferCompleted -= OnTransferCompleted;
            _channel.TransferFailed -= OnTransferFailed;
            _channel.LaneStatusChanged -= OnLaneStatusChanged;
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
                    row = new LaneStatusRowModel { LaneId = lane.LaneId };
                    LaneRows.Add(row);
                }
                row.Update(lane);
            }
        }

        private async Task ExecuteSendTestDataAsync()
        {
            if (_channel == null) return;

            IsSending = true;
            SendTestDataCommand.RaiseCanExecuteChanged();
            try
            {
                var data = new byte[TestSizeMb * 1024 * 1024];
                Random.Shared.NextBytes(data);

                var metadata = new FileTransferMetadata { Tag = $"DebugTest_{DateTime.Now:HHmmss}" };
                var result = await _channel.SendAsync(data, metadata);

                AppendLog(result.Success
                    ? $"[发送完成] {TestSizeMb}MB，耗时 {result.Elapsed.TotalSeconds:F2}s，吞吐 {result.ThroughputMBps:F1}MB/s"
                    : $"[发送失败] {result.FailureReason}: {result.ErrorMessage}");
            }
            finally
            {
                IsSending = false;
                SendTestDataCommand.RaiseCanExecuteChanged();
            }
        }

        // ── 参数修改：弹窗 → 保存到数据库 → 重新加载全部通讯实例 → 重新绑定刷新后的实例 ──────────
        // 简化为单 Lane 编辑：取 LinksJson 里的第一条 Lane 展示，保存时重建为仅含一条 Lane 的列表。

        private void ExecuteShowParamDialog()
        {
            if (string.IsNullOrEmpty(_instanceId)) return;
            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var linksJson = config.ConnectionParameters.GetValueOrDefault("LinksJson", "[]");
            var links = JsonSerializer.Deserialize<List<FileTransferLinkEndpoint>>(linksJson) ?? new List<FileTransferLinkEndpoint>();
            var firstLane = links.FirstOrDefault();

            var paramVm = new FileTransferParamViewModel
            {
                RoleText = config.ConnectionParameters.GetValueOrDefault("Role", nameof(FileTransferRole.Server)),
                LaneId = firstLane?.LaneId ?? 0,
                LocalIp = firstLane?.LocalIp ?? "0.0.0.0",
                Port = firstLane?.Port ?? 0,
                RemoteIp = firstLane?.RemoteIp ?? string.Empty
            };

            var dialogParams = new DialogParameters { { "Data", paramVm } };
            DialogService.ShowDialog(nameof(FileTransferParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var paramItem = result.Parameters.GetValue<FileTransferParamViewModel>("CallBackParamItem");
            if (paramItem == null) return;

            if (!Enum.TryParse<FileTransferRole>(paramItem.RoleText, ignoreCase: true, out var role))
            {
                AppendLog($"[参数] Role 填写不合法（需为 Server 或 Client），已取消保存: {paramItem.RoleText}");
                return;
            }

            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var newLink = new FileTransferLinkEndpoint
            {
                LaneId = paramItem.LaneId,
                LocalIp = paramItem.LocalIp,
                Port = paramItem.Port,
                RemoteIp = string.IsNullOrWhiteSpace(paramItem.RemoteIp) ? null : paramItem.RemoteIp
            };

            config.ConnectionParameters["Role"] = role.ToString();
            config.ConnectionParameters["LinksJson"] = JsonSerializer.Serialize(new List<FileTransferLinkEndpoint> { newLink });
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
            ProgressPercent = e.PercentComplete);

        private void OnTransferCompleted(object? sender, FileTransferCompletedEventArgs e) => RunOnUi(() =>
            AppendLog($"[{e.Direction}完成] {e.Metadata.Tag}，{e.Result.ThroughputMBps:F1}MB/s"));

        private void OnTransferFailed(object? sender, FileTransferFailedEventArgs e) => RunOnUi(() =>
            AppendLog($"[传输失败] {e.Reason}: {e.Message}"));

        private void OnLaneStatusChanged(object? sender, LaneStatusChangedEventArgs e) => RunOnUi(() =>
            AppendLog($"[Lane{e.Status.LaneId}] {(e.Status.IsConnected ? "已连接" : "已断开")} 重连次数={e.Status.ReconnectAttempts}"));

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

    /// <summary>单条 Lane 状态展示行，支持原地更新以避免列表闪烁</summary>
    public class LaneStatusRowModel : BindableBase
    {
        /// <summary>Lane 编号</summary>
        public int LaneId { get; init; }

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
