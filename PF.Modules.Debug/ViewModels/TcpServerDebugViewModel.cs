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
    /// <summary>TCP 服务端调试 ViewModel，接收 NavigationParameter("Instance") 传入的 IServer 实例</summary>
    public class TcpServerDebugViewModel : RegionViewModelBase
    {
        private readonly ICommunicationManagerService _commManager;
        private IServer? _server;
        private string _instanceId = string.Empty;

        private string _serverName = "未选中";
        /// <summary>服务器名称</summary>
        public string ServerName { get => _serverName; set => SetProperty(ref _serverName, value); }

        private string _statusText = "——";
        /// <summary>服务器状态文本</summary>
        public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

        private string _listenEndpoint = "——";
        /// <summary>监听地址</summary>
        public string ListenEndpoint { get => _listenEndpoint; set => SetProperty(ref _listenEndpoint, value); }

        /// <summary>已连接客户端列表</summary>
        public ObservableCollection<ClientRowModel> Clients { get; } = new();

        /// <summary>事件日志（最新在前）</summary>
        public ObservableCollection<string> LogEntries { get; } = new();

        private string _sendText = string.Empty;
        /// <summary>待广播的测试文本</summary>
        public string SendText { get => _sendText; set => SetProperty(ref _sendText, value); }

        /// <summary>广播测试数据命令</summary>
        public DelegateCommand BroadcastCommand { get; }

        /// <summary>打开本实例参数修改对话框命令</summary>
        public DelegateCommand ShowParamDialogCommand { get; }

        /// <summary>初始化 TCP 服务端调试 ViewModel</summary>
        public TcpServerDebugViewModel(ICommunicationManagerService commManager)
        {
            _commManager = commManager;
            BroadcastCommand = new DelegateCommand(async () => await ExecuteBroadcastAsync());
            ShowParamDialogCommand = new DelegateCommand(ExecuteShowParamDialog);
        }

        /// <inheritdoc/>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (!navigationContext.Parameters.ContainsKey("Instance")) return;

            _server = navigationContext.Parameters.GetValue<IServer>("Instance");
            if (_server == null) return;

            _instanceId = (_server as ICommunication)?.InstanceId ?? string.Empty;
            BindToServer();
        }

        /// <inheritdoc/>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            UnsubscribeEvents();
        }

        private void BindToServer()
        {
            if (_server == null) return;
            ServerName = _server.ServerName;
            RefreshStatus();
            RefreshClients();
            SubscribeEvents();
        }

        private void SubscribeEvents()
        {
            if (_server == null) return;
            _server.ServerStarted += OnServerStarted;
            _server.ServerStopped += OnServerStopped;
            _server.ClientConnected += OnClientConnected;
            _server.ClientDisconnected += OnClientDisconnected;
            _server.DataReceived += OnDataReceived;
        }

        private void UnsubscribeEvents()
        {
            if (_server == null) return;
            _server.ServerStarted -= OnServerStarted;
            _server.ServerStopped -= OnServerStopped;
            _server.ClientConnected -= OnClientConnected;
            _server.ClientDisconnected -= OnClientDisconnected;
            _server.DataReceived -= OnDataReceived;
        }

        private void RefreshStatus()
        {
            if (_server == null) return;
            StatusText = _server.Status.ToString();
            ListenEndpoint = $"{_server.IP}:{_server.Port}";
        }

        private void RefreshClients()
        {
            Clients.Clear();
            if (_server == null) return;
            foreach (var c in _server.Clients)
                Clients.Add(new ClientRowModel { ClientId = c.ClientId, RemoteEndPoint = c.RemoteEndPoint, ConnectedTime = c.ConnectedTime });
        }

        private async Task ExecuteBroadcastAsync()
        {
            if (_server == null || string.IsNullOrEmpty(SendText)) return;
            var bytes = Encoding.UTF8.GetBytes(SendText);
            var ok = await _server.BroadcastAsync(bytes);
            AppendLog(ok ? $"[发送] 广播 {bytes.Length} 字节: {SendText}" : "[发送] 广播失败");
        }

        // ── 参数修改：弹窗 → 保存到数据库 → 重新加载全部通讯实例 → 重新绑定刷新后的实例 ──────────

        private void ExecuteShowParamDialog()
        {
            if (string.IsNullOrEmpty(_instanceId)) return;
            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            var paramVm = new TcpServerParamViewModel
            {
                IP = config.ConnectionParameters.GetValueOrDefault("IP", "0.0.0.0"),
                Port = int.TryParse(config.ConnectionParameters.GetValueOrDefault("Port", "0"), out var port) ? port : 0,
                Backlog = int.TryParse(config.ConnectionParameters.GetValueOrDefault("Backlog", "10"), out var backlog) ? backlog : 10
            };

            var dialogParams = new DialogParameters { { "Data", paramVm } };
            DialogService.ShowDialog(nameof(TcpServerParamDialog), dialogParams, OnParamDialogClosed);
        }

        private async void OnParamDialogClosed(IDialogResult result)
        {
            if (result.Result != ButtonResult.Yes) return;

            var paramItem = result.Parameters.GetValue<TcpServerParamViewModel>("CallBackParamItem");
            if (paramItem == null) return;

            var config = _commManager.GetConfig(_instanceId);
            if (config == null) return;

            config.ConnectionParameters["IP"] = paramItem.IP;
            config.ConnectionParameters["Port"] = paramItem.Port.ToString();
            config.ConnectionParameters["Backlog"] = paramItem.Backlog.ToString();
            await _commManager.SaveConfigAsync(config);

            AppendLog("[参数] 已保存，正在重新加载全部通讯实例...");
            await _commManager.ReloadAllAsync();

            // 重载后旧实例已被释放，必须重新从管理服务里取回刷新后的实例并重新订阅事件
            UnsubscribeEvents();
            _server = _commManager.ActiveCommunications.FirstOrDefault(c => c.InstanceId == _instanceId) as IServer;
            BindToServer();
            AppendLog("[参数] 重新加载完成");
        }

        // IServer 的事件在网络 IO 线程上触发，必须转回 UI 线程才能安全更新绑定的集合/属性
        private void OnServerStarted(object? sender, ServerEventArgs e) => RunOnUi(() =>
        {
            RefreshStatus();
            AppendLog($"[服务器] {e.Message}");
        });

        private void OnServerStopped(object? sender, ServerEventArgs e) => RunOnUi(() =>
        {
            RefreshStatus();
            AppendLog($"[服务器] {e.Message}");
        });

        private void OnClientConnected(object? sender, ClientConnectedEventArgs e) => RunOnUi(() =>
        {
            AppendLog($"[连接] 客户端 {e.ClientId} 已连接 ({e.ServerAddress})");
            RefreshClients();
        });

        private void OnClientDisconnected(object? sender, ClientDisconnectedEventArgs e) => RunOnUi(() =>
        {
            AppendLog($"[断开] 客户端 {e.ClientId}: {e.Reason}");
            RefreshClients();
        });

        private void OnDataReceived(object? sender, DataReceivedEventArgs e) => RunOnUi(() =>
            AppendLog($"[收到] 来自 {e.ClientId}: {Encoding.UTF8.GetString(e.Data)}"));

        private void AppendLog(string message)
        {
            LogEntries.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
            while (LogEntries.Count > 200) LogEntries.RemoveAt(LogEntries.Count - 1);
        }

        private static void RunOnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
    }

    /// <summary>已连接客户端展示行</summary>
    public class ClientRowModel
    {
        /// <summary>客户端ID</summary>
        public string ClientId { get; set; } = string.Empty;
        /// <summary>远程端点</summary>
        public string RemoteEndPoint { get; set; } = string.Empty;
        /// <summary>连接时间</summary>
        public DateTime ConnectedTime { get; set; }
    }
}
