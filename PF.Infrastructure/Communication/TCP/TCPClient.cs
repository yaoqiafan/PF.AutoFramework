using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication.TCP;
using System;
using System.Collections.Generic;

using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Communication.TCP
{
    public class TCPClient : IClient
    {
        private System.Net.Sockets.TcpClient _tcpClient;
        private NetworkStream _stream;
        private CancellationTokenSource _receiveCancellationTokenSource;
        private CancellationTokenSource _connectCancellationTokenSource;
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim _connectLock = new SemaphoreSlim(1, 1);

        private ClientStatus _status = ClientStatus.None;
        private DateTime _connectTime;
        private string _clientId;
        private string _serverIp;
        private int _serverPort;
        private string _localEndPoint;
        private string _remoteEndPoint;

        public string ClientId => _clientId;

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

        public string ServerIp => _serverIp;
        public int ServerPort => _serverPort;
        public string LocalEndPoint => _localEndPoint;
        public string RemoteEndPoint => _remoteEndPoint;
        public DateTime ConnectTime => _connectTime;

        public Encoding Encoding { get; set; } = Encoding.ASCII;
        public int ReceiveBufferSize { get; set; } = 8192;
        public int SendTimeout { get; set; } = 3000;
        public int ReceiveTimeout { get; set; } = 3000;
        public int ConnectTimeout { get; set; } = 5000;
        public bool AutoReconnect { get; set; } = false;
        public int ReconnectInterval { get; set; } = 5000;

        public event EventHandler<ClientConnectedEventArgs> Connected;
        public event EventHandler<ClientDisconnectedEventArgs> Disconnected;
        public event EventHandler<DataReceivedEventArgs> DataReceived;
        public event EventHandler<ErrorOccurredEventArgs> ErrorOccurred;

        public TCPClient(string clientId = null)
        {
            _clientId = clientId ?? Guid.NewGuid().ToString();
        }

        public async Task<bool> ConnectAsync(string serverIp, int serverPort)
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

                // 取消之前的连接尝试（如果有）
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

                    // 带超时的连接
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

                    // 启动接收循环
                    _receiveCancellationTokenSource?.Cancel();
                    _receiveCancellationTokenSource?.Dispose();
                    _receiveCancellationTokenSource = new CancellationTokenSource();
                    _ = Task.Run(() => ReceiveLoopAsync(_receiveCancellationTokenSource.Token), _receiveCancellationTokenSource.Token);

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

                    // 使用异步读取
                    var bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                    if (bytesRead == 0)
                    {
                        // 读取0字节表示连接已断开
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
                    // 正常取消，退出循环
                    break;
                }
                catch (IOException ioEx) when (ioEx.InnerException is SocketException socketEx)
                {
                    // Socket异常，连接可能已断开
                    OnDisconnected($"IO异常: {socketEx.Message}", false);
                    await CleanupConnection();
                    break;
                }
                catch (SocketException socketEx)
                {
                    // Socket异常，连接已断开
                    OnDisconnected($"Socket异常: {socketEx.Message}", false);
                    await CleanupConnection();
                    break;
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"接收数据时发生错误: {ex.Message}", ex);
                    await Task.Delay(1000, cancellationToken); // 等待后继续尝试
                }
            }
        }

        public async Task<bool> SendAsync(byte[] data)
        {
            if (Status != ClientStatus.Connected || _stream == null)
            {
                throw new InvalidOperationException("客户端未连接");
            }

            if (data == null || data.Length == 0)
            {
                return true;
            }

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

        public async Task<bool> SendStringAsync(string data)
        {
            if (string.IsNullOrEmpty(data))
            {
                return true;
            }

            var bytes = Encoding.GetBytes(data);
            return await SendAsync(bytes);
        }

        public async Task DisconnectAsync()
        {
            await _connectLock.WaitAsync();
            try
            {
                if (Status != ClientStatus.Connected)
                {
                    return;
                }

                Status = ClientStatus.Disconnected;
                OnDisconnected("客户端主动断开", true);
                await CleanupConnection();
            }
            finally
            {
                _connectLock.Release();
            }
        }

        public async Task ReconnectAsync()
        {
            if (string.IsNullOrEmpty(_serverIp) || _serverPort == 0)
            {
                throw new InvalidOperationException("无法重连：未保存服务器地址");
            }

            await DisconnectAsync();

            if (AutoReconnect)
            {
                // 在后台尝试重连
                _ = Task.Run(async () =>
                {
                    while (AutoReconnect && Status != ClientStatus.Connected)
                    {
                        try
                        {
                            await Task.Delay(ReconnectInterval);
                            await ConnectAsync(_serverIp, _serverPort);
                        }
                        catch
                        {
                            // 重连失败，继续尝试
                        }
                    }
                });
            }
        }

        private async Task CleanupConnection()
        {
            try
            {
                // 停止接收循环
                _receiveCancellationTokenSource?.Cancel();
                _receiveCancellationTokenSource?.Dispose();
                _receiveCancellationTokenSource = null;

                // 关闭流和客户端
                _stream?.Close();
                _stream = null;

                _tcpClient?.Close();
                _tcpClient = null;
            }
            catch
            {
                // 忽略清理异常
            }
        }

        protected virtual void OnConnected(string message)
        {
            Connected?.Invoke(this, new ClientConnectedEventArgs(_clientId, $"{_serverIp}:{_serverPort}"));
        }

        protected virtual void OnDisconnected(string reason, bool isManual)
        {
            if (Status != ClientStatus.Disconnected)
            {
                Status = ClientStatus.Disconnected;
            }

            Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_clientId,
                isManual ? $"手动断开: {reason}" : reason));
        }

        protected virtual void OnDataReceived(byte[] data)
        {
            DataReceived?.Invoke(this, new DataReceivedEventArgs(_clientId, data));
        }

        protected virtual void OnErrorOccurred(string errorMessage, Exception exception)
        {
            ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(_clientId, errorMessage, exception));
        }

        #region IDisposable Support
        private bool _disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                if (disposing)
                {
                    AutoReconnect = false; // 停止自动重连

                    _connectCancellationTokenSource?.Cancel();
                    _connectCancellationTokenSource?.Dispose();

                    _receiveCancellationTokenSource?.Cancel();
                    _receiveCancellationTokenSource?.Dispose();

                    _connectLock?.Dispose();
                    _sendLock?.Dispose();

                    CleanupConnection().Wait();
                }

                _disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
