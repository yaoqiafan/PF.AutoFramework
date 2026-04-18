using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication.TCP;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Infrastructure.Communication.TCP
{
    /// <summary>
    /// TCP客户端
    /// </summary>
    public class TCPClient : IClient
    {
        private System.Net.Sockets.TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _receiveCancellationTokenSource;
        private CancellationTokenSource _connectCancellationTokenSource;
        private CancellationTokenSource _reconnectCts;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        private ClientStatus _status = ClientStatus.None;
        private DateTime _connectTime;
        private string _clientId;
        private string _serverIp;
        private int _serverPort;
        private string _localEndPoint;
        private string _remoteEndPoint;

        // 新增：用于记录当前是否启用了后台异步接收
        private bool _isAsyncMode;

        /// <summary>
        /// 客户端标识
        /// </summary>
        public string ClientId => _clientId;

        /// <summary>
        /// 客户端状态
        /// </summary>
        public ClientStatus Status
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
        public string ServerIp => _serverIp;
        /// <summary>
        /// 服务器端口
        /// </summary>
        public int ServerPort => _serverPort;
        /// <summary>
        /// 本地端点
        /// </summary>
        public string LocalEndPoint => _localEndPoint;
        /// <summary>
        /// 远程端点
        /// </summary>
        public string RemoteEndPoint => _remoteEndPoint;
        /// <summary>
        /// 连接时间
        /// </summary>
        public DateTime ConnectTime => _connectTime;

        /// <summary>
        /// 编码方式
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.ASCII;
        /// <summary>
        /// 接收缓冲区大小
        /// </summary>
        public int ReceiveBufferSize { get; set; } = 8192;
        /// <summary>
        /// 发送超时时间
        /// </summary>
        public int SendTimeout { get; set; } = 3000;
        /// <summary>
        /// 接收超时时间
        /// </summary>
        public int ReceiveTimeout { get; set; } = 3000;
        /// <summary>
        /// 连接超时时间
        /// </summary>
        public int ConnectTimeout { get; set; } = 5000;
        /// <summary>
        /// 是否自动重连
        /// </summary>
        public bool AutoReconnect { get; set; } = false;
        /// <summary>
        /// 重连间隔（毫秒）
        /// </summary>
        public int ReconnectInterval { get; set; } = 5000;

        /// <summary>
        /// 连接成功事件
        /// </summary>
        public event EventHandler<ClientConnectedEventArgs> Connected;
        /// <summary>
        /// 断开连接事件
        /// </summary>
        public event EventHandler<ClientDisconnectedEventArgs> Disconnected;
        /// <summary>
        /// 数据接收事件
        /// </summary>
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        /// <summary>
        /// 错误发生事件
        /// </summary>
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        /// <summary>
        /// 构造TCP客户端
        /// </summary>
        public TCPClient(string clientId = null)
        {
            _clientId = clientId ?? Guid.NewGuid().ToString();
        }

        /// <summary>
        /// 异步连接到服务器
        /// </summary>
        public async Task<bool> ConnectAsync(string serverIp, int serverPort, bool IsAsync = true)
        {
            await _connectLock.WaitAsync();
            try
            {
                if (Status == ClientStatus.Connected || Status == ClientStatus.Connecting)
                {
                    throw new InvalidOperationException($"Client is already {(Status == ClientStatus.Connected ? "connected" : "connecting")}");
                }

                Status = ClientStatus.Connecting;
                _serverIp = serverIp;
                _serverPort = serverPort;
                _isAsyncMode = IsAsync; // 记录当前模式

                _connectCancellationTokenSource?.Cancel();
                _connectCancellationTokenSource?.Dispose();
                _connectCancellationTokenSource = new CancellationTokenSource();

                try
                {
                    _tcpClient = new System.Net.Sockets.TcpClient();
                    _tcpClient.SendTimeout = SendTimeout;
                    _tcpClient.ReceiveTimeout = ReceiveTimeout;
                    _tcpClient.SendBufferSize = ReceiveBufferSize;
                    _tcpClient.ReceiveBufferSize = ReceiveBufferSize;

                    var connectTask = _tcpClient.ConnectAsync(serverIp, serverPort);
                    var timeoutTask = Task.Delay(ConnectTimeout, _connectCancellationTokenSource.Token);

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);

                    if (completedTask == timeoutTask)
                    {
                        throw new TimeoutException($"连接超时 ({ConnectTimeout}ms)");
                    }

                    await connectTask; // 确保连接完成

                    _stream = _tcpClient.GetStream();
                    _localEndPoint = _tcpClient.Client.LocalEndPoint?.ToString() ?? "Unknown";
                    _remoteEndPoint = _tcpClient.Client.RemoteEndPoint?.ToString() ?? "Unknown";
                    _connectTime = DateTime.Now;

                    _receiveCancellationTokenSource?.Cancel();
                    _receiveCancellationTokenSource?.Dispose();
                    _receiveCancellationTokenSource = new CancellationTokenSource();

                    if (IsAsync)
                    {
                        // 仅当启用异步时，启动后台接收循环
                        _ = Task.Run(() => ReceiveLoopAsync(_receiveCancellationTokenSource.Token), _receiveCancellationTokenSource.Token);
                    }

                    Status = ClientStatus.Connected;
                    OnConnected($"已连接到服务器 {serverIp}:{serverPort}");

                    return true;
                }
                catch (Exception ex)
                {
                    Status = ClientStatus.Error;
                    OnErrorOccurred($"连接失败: {ex.Message}", ex);
                    await CleanupConnection();
                    return false;
                }
            }
            finally
            {
                _connectLock.Release();
            }
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[ReceiveBufferSize];

            while (!cancellationToken.IsCancellationRequested && Status == ClientStatus.Connected)
            {
                try
                {
                    if (_stream == null || !_stream.CanRead)
                    {
                        await Task.Delay(100, cancellationToken);
                        continue;
                    }

                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        OnDisconnected("连接已关闭", false);
                        await CleanupConnection();
                        break;
                    }

                    var receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);
                    OnDataReceived(receivedData);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
                {
                    OnDisconnected($"IO异常: {socketEx.Message}", false);
                    await CleanupConnection();
                    break;
                }
                catch (SocketException socketEx)
                {
                    OnDisconnected($"Socket异常: {socketEx.Message}", false);
                    await CleanupConnection();
                    break;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"接收数据时发生错误: {ex.Message}", ex);
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }

        /// <summary>
        /// 异步发送字节数据
        /// </summary>
        public async Task<bool> SendAsync(byte[] data)
        {
            if (Status != ClientStatus.Connected || _stream == null)
            {
                throw new InvalidOperationException("客户端未连接");
            }

            if (data == null || data.Length == 0) return true;

            await _sendLock.WaitAsync();
            try
            {
                await _stream.WriteAsync(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"发送数据失败: {ex.Message}", ex);
                await DisconnectAsync();
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// 异步发送字符串数据
        /// </summary>
        public async Task<bool> SendStringAsync(string data)
        {
            if (string.IsNullOrEmpty(data)) return true;
            var bytes = Encoding.GetBytes(data);
            return await SendAsync(bytes);
        }

        // ================= 新增方法区 ================= //

        /// <summary>
        /// 发送数据并等待返回结果（仅在 IsAsync=false 时可用）
        /// </summary>
        public async Task<byte[]> WaitSentReceiveDataAsync(byte[] data, int timeoutMs)
        {
            if (_isAsyncMode)
                throw new InvalidOperationException("当前处于异步接收模式，无法使用同步阻塞读取，请在连接时将 IsAsync 设为 false。");

            if (!await SendAsync(data))
                throw new Exception("数据发送失败，无法等待响应。");

            using var cts = new CancellationTokenSource(timeoutMs);
            var buffer = new byte[ReceiveBufferSize];

            try
            {
                // 等待读取，超时会抛出 OperationCanceledException
                int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

                if (bytesRead == 0)
                {
                    OnDisconnected("服务器断开连接", false);
                    await CleanupConnection();
                    return Array.Empty<byte>();
                }

                var result = new byte[bytesRead];
                Array.Copy(buffer, result, bytesRead);
                return result;
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException($"接收响应超时 ({timeoutMs}ms)。");
            }
        }

        /// <summary>
        /// 在固定的时间窗口内，持续接收所有到达的数据（仅在 IsAsync=false 时可用）
        /// </summary>
        public async Task<byte[]> ReceiveAllDataInTimeWindowAsync(int timeWindowMs)
        {
            if (_isAsyncMode)
                throw new InvalidOperationException("当前处于异步接收模式，无法使用此方法读取流，请在连接时将 IsAsync 设为 false。");

            if (Status != ClientStatus.Connected || _stream == null)
                throw new InvalidOperationException("客户端未连接");

            using var cts = new CancellationTokenSource(timeWindowMs);
            var buffer = new byte[ReceiveBufferSize];
            using var ms = new MemoryStream();

            try
            {
                // 只要时间窗口没到，就一直挂起读取
                while (!cts.IsCancellationRequested)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cts.Token);

                    if (bytesRead == 0)
                    {
                        OnDisconnected("服务器断开连接", false);
                        await CleanupConnection();
                        break;
                    }

                    ms.Write(buffer, 0, bytesRead);
                }
            }
            catch (OperationCanceledException)
            {
                // 预期行为：时间到了，正常跳出循环
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"读取时间窗口数据时发生错误: {ex.Message}", ex);
            }

            return ms.ToArray();
        }

        // ============================================== //

        /// <summary>
        /// 异步断开连接
        /// </summary>
        public async Task DisconnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (Status != ClientStatus.Connected) return;

                Status = ClientStatus.Disconnected;
                OnDisconnected("客户端主动断开", true);
                await CleanupConnection();
            }
            finally
            {
                _connectLock.Release();
            }
        }

        /// <summary>
        /// 异步重连
        /// </summary>
        public async Task ReconnectAsync()
        {
            if (string.IsNullOrEmpty(_serverIp) || _serverPort == 0)
            {
                throw new InvalidOperationException("无法重连：未保存服务器地址");
            }

            await DisconnectAsync();

            if (AutoReconnect)
            {
                _reconnectCts?.Cancel();
                _reconnectCts?.Dispose();
                _reconnectCts = new CancellationTokenSource();
                var cts = _reconnectCts;

                _ = Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested && AutoReconnect && Status != ClientStatus.Connected)
                    {
                        try
                        {
                            await Task.Delay(ReconnectInterval, cts.Token);
                            // 保持上一次连接的 IsAsync 模式
                            await ConnectAsync(_serverIp, _serverPort, _isAsyncMode);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                        catch
                        {
                            // 重连失败，继续尝试
                        }
                    }
                }, cts.Token);
            }
        }

        private async Task CleanupConnection()
        {
            try
            {
                _receiveCancellationTokenSource?.Cancel();
                _receiveCancellationTokenSource?.Dispose();
                _receiveCancellationTokenSource = null;

                _stream?.Close();
                _stream = null;

                _tcpClient?.Close();
                _tcpClient = null;
            }
            catch
            {
            }
        }

        /// <summary>
        /// 触发连接成功事件
        /// </summary>
        protected virtual void OnConnected(string message)
        {
            Connected?.Invoke(this, new ClientConnectedEventArgs(_clientId, $"{_serverIp}:{_serverPort}"));
        }

        /// <summary>
        /// 触发断开连接事件
        /// </summary>
        protected virtual void OnDisconnected(string reason, bool isManual)
        {
            if (Status != ClientStatus.Disconnected)
            {
                Status = ClientStatus.Disconnected;
            }
            Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_clientId, isManual ? $"手动断开: {reason}" : reason));
        }

        /// <summary>
        /// 触发数据接收事件
        /// </summary>
        protected virtual void OnDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs(_clientId, data));
        }

        /// <summary>
        /// 触发错误发生事件
        /// </summary>
        protected virtual void OnErrorOccurred(string errorMessage, Exception exception)
        {
            ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(_clientId, errorMessage, exception));
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
                    AutoReconnect = false;
                    _reconnectCts?.Cancel();
                    _reconnectCts?.Dispose();
                    _reconnectCts = null;

                    _connectCancellationTokenSource?.Cancel();
                    _connectCancellationTokenSource?.Dispose();

                    _receiveCancellationTokenSource?.Cancel();
                    _receiveCancellationTokenSource?.Dispose();

                    _connectLock?.Dispose();
                    _sendLock?.Dispose();

                    try { _stream?.Close(); _stream = null; } catch { }
                    try { _tcpClient?.Close(); _tcpClient = null; } catch { }
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
    }
}