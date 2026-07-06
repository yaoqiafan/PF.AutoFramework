using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.Serial;
using System.IO.Ports;
using System.Text;

namespace PF.Infrastructure.Communication.Serial
{
    /// <summary>
    /// 串口通讯实现，基于 <see cref="System.IO.Ports.SerialPort"/> 封装。
    /// 串口是点对点物理连接，没有服务端/客户端概念，<see cref="ICommunication.Role"/> 固定为 None——
    /// 在通讯调试树里会直接扁平挂在 Serial 分类节点下，不会像 TCP 那样再分"服务端/客户端"一层。
    /// 暂未接入任何具体硬件，供后续需要串口通讯的设备复用。
    /// </summary>
    public sealed class SerialPortCommunication : ISerialCommunication, ICommunication
    {
        private readonly SerialPort _port;
        private readonly SemaphoreSlim _openLock = new(1, 1);
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private readonly string _instanceId;
        private ClientStatus _status = ClientStatus.None;

        /// <inheritdoc/>
        public string PortName { get; }
        /// <inheritdoc/>
        public int BaudRate { get; }
        /// <inheritdoc/>
        public ClientStatus Status
        {
            get => _status;
            private set => _status = value;
        }
        /// <inheritdoc/>
        public Encoding Encoding { get; set; } = Encoding.ASCII;

        /// <inheritdoc/>
        public event EventHandler? Opened;
        /// <inheritdoc/>
        public event EventHandler<string>? Closed;
        /// <inheritdoc/>
        public event EventHandler<DataReceivedEventArgs>? DataReceived;
        /// <inheritdoc/>
        public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

        // ── ICommunication ──────────────────────────────────────────────────

        /// <inheritdoc cref="ICommunication.InstanceId"/>
        string ICommunication.InstanceId => _instanceId;
        /// <inheritdoc cref="ICommunication.Category"/>
        CommunicationCategory ICommunication.Category => CommunicationCategory.Serial;
        /// <inheritdoc cref="ICommunication.Role"/>
        CommunicationRole ICommunication.Role => CommunicationRole.None;
        /// <inheritdoc cref="ICommunication.DisplayName"/>
        string ICommunication.DisplayName => $"串口 [{PortName}] {BaudRate}bps";

        /// <summary>
        /// 构造串口通讯实例
        /// </summary>
        /// <param name="portName">串口名称（如 "COM3"）</param>
        /// <param name="baudRate">波特率，默认 9600</param>
        /// <param name="parity">校验位，默认 None</param>
        /// <param name="dataBits">数据位，默认 8</param>
        /// <param name="stopBits">停止位，默认 One</param>
        /// <param name="instanceId">通讯实例唯一标识，缺省时使用 portName</param>
        public SerialPortCommunication(string portName, int baudRate = 9600,
            Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One,
            string? instanceId = null)
        {
            PortName = portName;
            BaudRate = baudRate;
            _instanceId = instanceId ?? portName;

            _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
            _port.DataReceived += OnPortDataReceived;
            _port.ErrorReceived += OnPortErrorReceived;
        }

        /// <inheritdoc/>
        public Task<bool> OpenAsync()
        {
            return Task.Run(async () =>
            {
                await _openLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (Status == ClientStatus.Connected) return true;

                    Status = ClientStatus.Connecting;
                    _port.Open();
                    Status = ClientStatus.Connected;
                    Opened?.Invoke(this, EventArgs.Empty);
                    return true;
                }
                catch (Exception ex)
                {
                    Status = ClientStatus.Error;
                    ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(PortName, $"打开串口失败: {ex.Message}", ex));
                    return false;
                }
                finally
                {
                    _openLock.Release();
                }
            });
        }

        /// <inheritdoc/>
        public async Task CloseAsync()
        {
            await _openLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (Status == ClientStatus.None || Status == ClientStatus.Disconnected) return;

                if (_port.IsOpen) _port.Close();
                Status = ClientStatus.Disconnected;
                Closed?.Invoke(this, "主动关闭");
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(PortName, $"关闭串口失败: {ex.Message}", ex));
            }
            finally
            {
                _openLock.Release();
            }
        }

        /// <inheritdoc/>
        public async Task<bool> SendAsync(byte[] data)
        {
            if (Status != ClientStatus.Connected || !_port.IsOpen)
                throw new InvalidOperationException("串口未打开");

            if (data == null || data.Length == 0) return true;

            await _sendLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _port.Write(data, 0, data.Length);
                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(PortName, $"发送数据失败: {ex.Message}", ex));
                return false;
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <inheritdoc/>
        public Task<bool> SendStringAsync(string data)
        {
            if (string.IsNullOrEmpty(data)) return Task.FromResult(true);
            return SendAsync(Encoding.GetBytes(data));
        }

        private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var bytesToRead = _port.BytesToRead;
                if (bytesToRead <= 0) return;

                var buffer = new byte[bytesToRead];
                // Read 不保证读满请求的长度，必须按实际返回值截取，否则事件可能带出尾部全零的脏数据
                var read = _port.Read(buffer, 0, bytesToRead);
                if (read <= 0) return;
                if (read < bytesToRead) Array.Resize(ref buffer, read);
                DataReceived?.Invoke(this, new DataReceivedEventArgs(PortName, buffer));
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(PortName, $"接收数据时发生错误: {ex.Message}", ex));
            }
        }

        private void OnPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
            => ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(PortName, $"串口错误: {e.EventType}", null));

        // ── ICommunication 生命周期 ──────────────────────────────────────────

        /// <summary>供 ICommunicationManagerService 统一调度的启动入口</summary>
        Task<bool> ICommunication.StartAsync(CancellationToken token) => OpenAsync();

        /// <summary>供 ICommunicationManagerService 统一调度的停止入口</summary>
        Task ICommunication.StopAsync() => CloseAsync();

        /// <inheritdoc/>
        public void Dispose()
        {
            _port.DataReceived -= OnPortDataReceived;
            _port.ErrorReceived -= OnPortErrorReceived;
            try { if (_port.IsOpen) _port.Close(); } catch { /* 忽略关闭异常 */ }
            _port.Dispose();
            _openLock.Dispose();
            _sendLock.Dispose();
        }
    }
}
