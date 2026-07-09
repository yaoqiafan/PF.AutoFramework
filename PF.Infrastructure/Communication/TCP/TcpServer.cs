using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.TCP;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Communication.TCP
{
    /// <summary>
    /// TCP服务器实现
    /// </summary>
    [CommunicationUI(NavigationConstants.Views.TcpServerDebugView)]
    public class TcpServer : IServer, ICommunication
    {
        private TcpListener _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ConcurrentDictionary<string, ClientConnection> _clients;
        private readonly object _lockObject = new object();
        private ServerStatus _status = ServerStatus.Stopped;
        private ClientStatus _clientstatus = ClientStatus.None;
        private string _displayName;
        /// <summary>
        /// 服务器名称
        /// </summary>
        public string ServerName { get; }
        /// <summary>
        /// 服务器状态
        /// </summary>
        public ServerStatus Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                }
            }
        }
        /// <summary>
        /// 服务器IP地址
        /// </summary>
        public string IP { get; private set; }
        /// <summary>
        /// 服务器端口
        /// </summary>
        public int Port { get; private set; }
        /// <summary>
        /// 已连接的客户端列表
        /// </summary>
        public IReadOnlyList<IClientConnection> Clients => _clients.Values.ToList();

        /// <summary>由 ICommunicationManagerService 按配置驱动启动时使用的监听 IP，需在 StartAsync(CancellationToken) 调用前设置</summary>
        public string BindIp { get; set; } = "0.0.0.0";
        /// <summary>由 ICommunicationManagerService 按配置驱动启动时使用的监听端口</summary>
        public int BindPort { get; set; }
        /// <summary>由 ICommunicationManagerService 按配置驱动启动时使用的挂起连接队列长度</summary>
        public int Backlog { get; set; } = 10;

        /// <inheritdoc cref="ICommunication.InstanceId"/>
        string ICommunication.InstanceId => ServerName;
        /// <inheritdoc cref="ICommunication.Category"/>
        CommunicationCategory ICommunication.Category => CommunicationCategory.Tcp;
        /// <inheritdoc cref="ICommunication.Role"/>
        CommunicationRole ICommunication.Role => CommunicationRole.Server;
        /// <inheritdoc cref="ICommunication.DisplayName"/>
        string ICommunication.DisplayName => $"[{_displayName}]";

        /// <summary>供 ICommunicationManagerService 统一调度的启动入口，内部使用 BindIp/BindPort/Backlog</summary>
        public Task<bool> StartAsync(CancellationToken token = default) => StartAsync(BindIp, BindPort, Backlog);

        /// <summary>
        /// 编码方式
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.ASCII;

        /// <summary>
        /// 客户端连接状态
        /// </summary>
        public ClientStatus ClientStatue
        {
            get => _clientstatus;
            private set
            {
                if (_clientstatus != value)
                {
                    _clientstatus = value;
                }
            }
        }

        /// <summary>
        /// 服务器启动事件
        /// </summary>
        public event EventHandler<ServerEventArgs> ServerStarted;
        /// <summary>
        /// 服务器停止事件
        /// </summary>
        public event EventHandler<ServerEventArgs> ServerStopped;
        /// <summary>
        /// 客户端连接事件
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> ClientConnected;
        /// <summary>
        /// 客户端断开事件
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;
        /// <summary>
        /// 数据接收事件
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        /// <summary>
        /// 错误发生事件（如接受连接失败）。单次 accept 出错不代表服务器停止，
        /// 与 <see cref="ServerStopped"/> 严格区分
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 构造TCP服务器
        /// </summary>
        public TcpServer(string displayName = null, string serverName = "Default TcpServer")
        {
            ServerName = serverName;
            _clients = new ConcurrentDictionary<string, ClientConnection>();
            _displayName = displayName ?? $"{ServerName}";
        }

        /// <summary>
        /// 异步启动TCP服务器
        /// </summary>
        public async Task<bool> StartAsync(string IPipString, int port, int backlog = 10)
        {
            if (Status != ServerStatus.Stopped)
            {
                throw new InvalidOperationException("Server is already running or starting");
            }
            try
            {
                if (IPAddress.TryParse(IPipString, out IPAddress ipAddress))
                {
                    Status = ServerStatus.Starting;
                    IP = IPipString;
                    Port = port;

                    _listener = new TcpListener(ipAddress, port);
                    _listener.Start(backlog);

                    _cancellationTokenSource = new CancellationTokenSource();

                    // 启动监听循环
                    _ = Task.Run(() => ListenForClientsAsync(_cancellationTokenSource.Token));

                    Status = ServerStatus.Running;

                    OnServerStarted($"TCP服务器已启动，监听端口: {port}");

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception ex)
            {
                Status = ServerStatus.Error;
                OnServerStopped($"服务器启动失败: {ex.Message}");
                await StopAsync();
                return false;
            }
        }

        private async Task ListenForClientsAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var client = await _listener.AcceptTcpClientAsync();

                    if (client != null)
                    {
                        _ = Task.Run(() => HandleClientAsync(client, cancellationToken));
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 监听器已关闭，正常退出
                    break;
                }
                catch (Exception ex)
                {
                    // 单次 accept 失败服务器仍在运行，此前误报 ServerStopped 会让订阅方以为服务器已停
                    OnErrorOccurred($"接受客户端连接时发生错误: {ex.Message}", ex);
                }
            }
        }

        private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken cancellationToken)
        {
            var clientConnection = new ClientConnection(tcpClient);
            var clientId = clientConnection.ClientId;
            var clientEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";

            if (!_clients.TryAdd(clientId, clientConnection))
            {
                tcpClient.Close();
                return;
            }

            NetworkStream stream = null;
            string disconnectReason = "未知原因";

            try
            {
                OnClientConnected(clientConnection);

                stream = tcpClient.GetStream();
                // 修复：原始 8MB+100 的固定缓冲区在每个客户端连接时分配，
                // 100 个并发客户端即占用 ~800MB 堆内存，极易引发 OOM。
                // 工业设备协议单帧通常远小于 64KB，调整为合理大小。
                // 若确实需要大包，应在应用层做流式分帧，而非一次性大缓冲。
                var buffer = new byte[CommunicationConstants.TcpReceiveBufferSize]; // 64KB，与 TCPClient 共用常量保持对等

                // 修复：彻底移除 Poll/DataAvailable 同步阻塞轮询。
                // 每个客户端的 Poll 调用最多阻塞 1 s（加上 IsSocketConnected 内的 3 次 Poll
                // 共可达 5 s），100 个并发连接即耗尽线程池。
                // 直接用 await ReadAsync 进行真正的异步等待：操作系统在数据到达时唤醒，
                // 期间不占用线程。返回 0 字节 = 对端优雅关闭；IOException/SocketException = 异常断开。
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                        if (bytesRead == 0)
                        {
                            disconnectReason = "客户端正常关闭连接";
                            break;
                        }

                        var receivedData = new byte[bytesRead];
                        Array.Copy(buffer, receivedData, bytesRead);
                        OnDataReceived(clientEndPoint, receivedData);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (IOException ioEx)
                    {
                        disconnectReason = $"IO异常: {ioEx.Message}";
                        break;
                    }
                    catch (SocketException socketEx)
                    {
                        disconnectReason = $"Socket异常: {socketEx.Message}";
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                disconnectReason = $"连接异常: {ex.Message}";
            }
            finally
            {
                // 确保只触发一次断开事件
                if (_clients.TryRemove(clientId, out var removedClient))
                {
                    OnClientDisconnected(clientEndPoint, disconnectReason);
                    removedClient.Dispose();
                }

                try
                {
                    stream?.Close();
                    tcpClient?.Close();
                }
                catch
                {
                    // 忽略关闭异常
                }
            }
        }

        /// <summary>
        /// 检测Socket是否仍然连接
        /// </summary>
        private bool IsSocketConnected(Socket socket)
        {
            try
            {
                // 使用Poll方法检查连接状态
                // 检查读状态，如果不可读且没有数据，则连接可能已断开
                bool part1 = socket.Poll(1000, SelectMode.SelectRead);
                bool part2 = (socket.Available == 0);
                if (part1 && part2)
                {
                    return false;
                }

                // 检查连接状态
                return !(socket.Poll(1000, SelectMode.SelectError) &&
                        socket.Poll(1000, SelectMode.SelectWrite));
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 异步停止服务器
        /// </summary>
        public async Task StopAsync()
        {
            Status = ServerStatus.Stopping;
            try
            {
                _cancellationTokenSource?.Cancel();

                // 断开所有客户端连接
                var disconnectTasks = _clients.Values.Select(client =>
                    DisconnectClientAsync(client.ClientId));
                await Task.WhenAll(disconnectTasks);

                _listener?.Stop();
                _cancellationTokenSource?.Dispose();

                Status = ServerStatus.Stopped;
                OnServerStopped("服务器已停止");
            }
            catch (Exception ex)
            {
                Status = ServerStatus.Error;
                OnServerStopped($"停止服务器时发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 向指定客户端发送数据。
        /// 通过每客户端发送锁串行化写入：并发 WriteAsync 同一 NetworkStream 会交错字节流、破坏帧边界。
        /// </summary>
        public async Task<bool> SendAsync(string clientId, byte[] data)
        {
            if (!_clients.TryGetValue(clientId, out var client))
                return false;

            try
            {
                await client.SendLock.WaitAsync();
                try
                {
                    var stream = client.TcpClient.GetStream();
                    await stream.WriteAsync(data, 0, data.Length);
                    return true;
                }
                finally
                {
                    client.SendLock.Release();
                }
            }
            catch (ObjectDisposedException)
            {
                // 客户端已断开并释放，发送失败即可，无需再触发断开流程
                return false;
            }
            catch
            {
                await DisconnectClientAsync(clientId);
                return false;
            }
        }

        /// <summary>
        /// 向所有客户端广播数据
        /// </summary>
        public async Task<bool> BroadcastAsync(byte[] data)
        {
            var sendTasks = _clients.Values.Select(client =>
                SendAsync(client.ClientId, data)).ToList();

            var results = await Task.WhenAll(sendTasks);
            return results.All(r => r);
        }

        /// <summary>
        /// 异步断开指定客户端
        /// </summary>
        public async Task<bool> DisconnectClientAsync(string clientId)
        {
            // 先原子移除再关闭：与 HandleClientAsync 的 finally（读循环感知到断开后同样会 TryRemove）竞争时，
            // 只有移除成功的一方触发断开事件，避免同一客户端发出两次 ClientDisconnected
            if (!_clients.TryRemove(clientId, out var client))
                return false;

            try { client.TcpClient.Close(); } catch { /* 已断开等关闭异常忽略 */ }
            client.Dispose();

            OnClientDisconnected(clientId, "服务器主动断开连接");
            return true;
        }

        /// <summary>
        /// 触发服务器启动事件
        /// </summary>
        protected virtual void OnServerStarted(string message)
        {
            ServerStarted?.Invoke(this, new ServerEventArgs(message));
        }

        /// <summary>
        /// 触发服务器停止事件
        /// </summary>
        protected virtual void OnServerStopped(string message)
        {
            ServerStopped?.Invoke(this, new ServerEventArgs(message));
        }

        /// <summary>
        /// 触发客户端连接事件
        /// </summary>
        protected virtual void OnClientConnected(IClientConnection client)
        {
            ClientStatue = ClientStatus.Connected;
            ClientConnected?.Invoke(this, new ClientConnectedEventArgs(client.ClientId, this.IP));
        }

        /// <summary>
        /// 触发客户端断开事件
        /// </summary>
        protected virtual void OnClientDisconnected(string clientId, string reason)
        {
            ClientStatue = ClientStatus.Disconnected;
            ClientDisconnected?.Invoke(this, new ClientDisconnectedEventArgs(clientId, reason));
        }

        /// <summary>
        /// 触发数据接收事件
        /// </summary>
        protected virtual void OnDataReceived(string clientId, byte[] data)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs(clientId, data));
        }

        /// <summary>
        /// 触发错误发生事件
        /// </summary>
        protected virtual void OnErrorOccurred(string errorMessage, Exception exception)
        {
            ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(ServerName, errorMessage, exception));
        }

        #region IDisposable Support
        private bool _disposedValue = false;

        /// <summary>
        /// 释放资源
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    if (Status == ServerStatus.Running)
                    {
                        // 修复：StopAsync().Wait() 在有 SynchronizationContext（如 UI 线程）
                        // 时会死锁。强制在线程池线程上执行，脱离当前上下文。
                        Task.Run(() => StopAsync()).GetAwaiter().GetResult();
                    }

                    _cancellationTokenSource?.Dispose();
                    _listener = null;
                }
                _disposedValue = true;
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion

        /// <summary>
        /// 客户端连接内部实现
        /// </summary>
        private class ClientConnection : IClientConnection, IDisposable
        {
            public string ClientId { get; }
            public string RemoteEndPoint { get; }
            public DateTime ConnectedTime { get; }
            public bool IsConnected => _tcpClient?.Connected ?? false;
            internal TcpClient TcpClient => _tcpClient;

            /// <summary>
            /// 每客户端发送锁。有意不在 Dispose 中释放：
            /// SemaphoreSlim 不持有非托管资源，而带等待者时提前 Dispose 会让并发发送方抛异常。
            /// </summary>
            internal SemaphoreSlim SendLock { get; } = new SemaphoreSlim(1, 1);

            private readonly TcpClient _tcpClient;

            public ClientConnection(TcpClient tcpClient)
            {
                _tcpClient = tcpClient;
                ClientId = Guid.NewGuid().ToString();
                RemoteEndPoint = tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                ConnectedTime = DateTime.Now;
            }

            public void Dispose()
            {
                try
                {
                    _tcpClient?.Close();
                }
                catch
                {
                    // 忽略关闭异常
                }
            }
        }
    }
}
