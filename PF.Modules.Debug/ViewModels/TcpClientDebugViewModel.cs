using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.TCP;
using PF.Modules.Debug.Dialogs;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>TCP 客户端调试 ViewModel，接收 NavigationParameter("Instance") 传入的 IClient 实例</summary>
    public class TcpClientDebugViewModel : RegionViewModelBase
    {
        private readonly ICommunicationManagerService _commManager;
        private IClient? _client;
        private string _instanceId = string.Empty;

        private string _clientId = "未选中";
        /// <summary>客户端标识</summary>
        public string ClientId { get => _clientId; set => SetProperty(ref _clientId, value); }

        private string _statusText = "——";
        /// <summary>连接状态文本</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _targetEndpoint = "——";
        /// <summary>目标服务端地址</summary>
        public string TargetEndpoint { get => _targetEndpoint; set => SetProperty(ref _targetEndpoint, value); }

        /// <summary>事件日志（最新在前）</summary>
        public ObservableCollection<string> LogEntries { get; } = new();

        private string _sendText = string.Empty;
        /// <summary>待发送的测试文本</summary>
        public string SendText { get => _sendText; set => SetProperty(ref _sendText, value); }

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; }
        /// <summary>断开命令</summary>
        public DelegateCommand DisconnectCommand { get; }
        /// <summary>发送测试数据命令</summary>
        public DelegateCommand SendCommand { get; }
        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化 TCP 客户端调试 ViewModel</summary>
        public TcpClientDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            ConnectCommand = new DelegateCommand(async () => await ExecuteConnectAsync());
            DisconnectCommand = new DelegateCommand(async () => { if (_client != null) await _client.DisconnectAsync(); });
            SendCommand = new DelegateCommand(async () => await ExecuteSendAsync());
            ShowParamDialogCommand = new DelegateCommand(ExecuteShowParamDialog);
        }

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Instance")) return;

            _client = navigationContext.Parameters.GetValue<IClient>("Instance");
            if (_client == null) return;

            _instanceId = (_client as ICommunication)?.InstanceId ?? string.Empty;
            BindToClient();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            UnsubscribeEvents();
        }

        private void BindToClient()
        {
            if (_client == null) return;
            ClientId = _client.ClientId;
            RefreshStatus();
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_client == null) return;
            _client.Connected += OnConnected;
            _client.Disconnected += OnDisconnected;
            _client.DataReceived += OnDataReceived;
            _client.ErrorOccurred += OnErrorOccurred;
        }

        private void UnsubscribeEvents()
        {
            if (_client == null) return;
            _client.Connected -= OnConnected;
            _client.Disconnected -= OnDisconnected;
            _client.DataReceived -= OnDataReceived;
            _client.ErrorOccurred -= OnErrorOccurred;
        }

        private void RefreshStatus()
        {
            if (_client == null) return;
            StatusText = _client.Status.ToString();
            // 连接前 ServerIp/ServerPort 尚未赋值，展示用目标地址改读 TargetServerIp/TargetServerPort
            TargetEndpoint = $"{_client.TargetServerIp}:{_client.TargetServerPort}";
        }

        private async Task ExecuteConnectAsync()
        {
            if (_client == null || string.IsNullOrEmpty(_client.TargetServerIp)) return;
            var ok = await _client.ConnectAsync(_client.TargetServerIp, _client.TargetServerPort);
            RefreshStatus();
            AppendLog(ok ? "[连接] 连接成功" : "[连接] 连接失败");
        }

        private async Task ExecuteSendAsync()
        {
            if (_client == null || string.IsNullOrEmpty(SendText)) return;
            var bytes = Encoding.UTF8.GetBytes(SendText);
            var ok = await _client.SendAsync(bytes);
            AppendLog(ok ? $"[发送] {bytes.Length} 字节: {SendText}" : "[发送] 发送失败");
        }

        // ── 参数修改：弹窗 → 保存到数据库 → 重新加载全部通讯实例 → 重新绑定刷新后的实例 ──────────

        private void ExecuteShowParamDialog()
        {
            if (string.IsNullOrEmpty(_instanceId)) return;
            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var paramVm = new TcpClientParamViewModel
            {
                ServerIp = config.ConnectionParameters.GetValueOrDefault("ServerIp", string.Empty),
                ServerPort = int.TryParse(config.ConnectionParameters.GetValueOrDefault("ServerPort", "0"), out var port) ? port : 0
            };

            var dialogParams = new DialogParameters { { "Data", paramVm } };
            DialogService.ShowDialog(nameof(TcpClientParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var paramItem = result.Parameters.GetValue<TcpClientParamViewModel>("CallBackParamItem");
            if (paramItem == null) return;

            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            config.ConnectionParameters["ServerIp"] = paramItem.ServerIp;
            config.ConnectionParameters["ServerPort"] = paramItem.ServerPort.ToString();
            await _commManager.SaveConfigAsync(config);

            AppendLog("[参数] 已保存，正在重新加载全部通讯实例...");
            await _commManager.ReloadAllAsync();

            UnsubscribeEvents();
            _client = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == _instanceId) as IClient;
            BindToClient();
            AppendLog("[参数] 重新加载完成");
        }

        // IClient 的事件在网络 IO 线程上触发，必须转回 UI 线程才能安全更新绑定的属性
        private void OnConnected(object? sender, ClientConnectedEventArgs e) => RunOnUi(() =>
        {
            RefreshStatus();
            AppendLog($"[连接] 已连接到 {e.ServerAddress}");
        });

        private void OnDisconnected(object? sender, ClientDisconnectedEventArgs e) => RunOnUi(() =>
        {
            RefreshStatus();
            AppendLog($"[断开] {e.Reason}");
        });

        private void OnDataReceived(object? sender, DataReceivedEventArgs e) => RunOnUi(() =>
            AppendLog($"[收到] {Encoding.UTF8.GetString(e.Data)}"));

        private void OnErrorOccurred(object? sender, ErrorOccurredEventArgs e) => RunOnUi(() =>
            AppendLog($"[错误] {e.ErrorMessage}"));

        private void AppendLog(string message)
        {
            LogEntries.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
            while (LogEntries.Count > 200) LogEntries.RemoveAt(LogEntries.Count - 1);
        }

        private static void RunOnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
    }
}
