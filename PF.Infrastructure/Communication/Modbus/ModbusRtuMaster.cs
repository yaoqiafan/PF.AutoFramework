using System.IO.Ports;
using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.Modbus;
using PF.Infrastructure.Communication.Modbus.Internal;

namespace PF.Infrastructure.Communication.Modbus;

/// <summary>
/// Modbus RTU 主站实现。直接持有 <see cref="SerialPort"/>（不复用已注册的 SerialPortCommunication 实例），
/// 对齐 SerialPortCommunication/TransferLane"协议层自己拥有底层传输原语"的既定做法。
///
/// 帧边界判定以"按 PDU 推算的期望响应长度"为主：发起请求前已知功能码与数量，可提前算出正常响应的字节数
/// （<see cref="ModbusPduCodec.GetExpectedNormalResponsePduLength"/>），收满即返回；异常响应固定 2 字节 PDU，
/// 靠首个功能码字节的最高位区分。仅靠 T3.5 静默判帧在托管 SerialPort.DataReceived 上误判率高，这里只用超时兜底。
/// </summary>
[CommunicationUI(NavigationConstants.Views.ModbusRtuDebugView)]
public sealed class ModbusRtuMaster : ModbusMasterBase, IModbusRtuMaster, ICommunication
{
    private readonly SerialPort _port;
    private readonly string _instanceId;
    private readonly SemaphoreSlim _openLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    private readonly object _bufferLock = new();
    private readonly List<byte> _buffer = new();
    private int _expectedTotalLength;
    private TaskCompletionSource<byte[]>? _pendingResponse;

    private ClientStatus _status = ClientStatus.None;

    /// <inheritdoc/>
    public string PortName { get; }
    /// <inheritdoc/>
    public int BaudRate { get; }
    /// <inheritdoc/>
    public ClientStatus Status { get => _status; private set => _status = value; }

    /// <inheritdoc/>
    public event EventHandler? Opened;
    /// <inheritdoc/>
    public event EventHandler<string>? Closed;
    /// <inheritdoc/>
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
    /// <inheritdoc/>
    public event EventHandler<ModbusFrameExchangedEventArgs>? FrameExchanged;

    // ── ICommunication ──────────────────────────────────────────────────

    string ICommunication.InstanceId => _instanceId;
    CommunicationCategory ICommunication.Category => CommunicationCategory.Modbus;
    CommunicationRole ICommunication.Role => CommunicationRole.None;
    string ICommunication.DisplayName => $"Modbus RTU [{PortName}] {BaudRate}bps";
    Task<bool> ICommunication.StartAsync(CancellationToken token) => OpenAsync();
    Task ICommunication.StopAsync() => CloseAsync();

    /// <summary>
    /// 构造 Modbus RTU 主站实例
    /// </summary>
    /// <param name="portName">串口名称（如 "COM3"）</param>
    /// <param name="baudRate">波特率</param>
    /// <param name="instanceId">通讯实例唯一标识</param>
    /// <param name="parity">校验位，默认 None</param>
    /// <param name="dataBits">数据位，默认 8</param>
    /// <param name="stopBits">停止位，默认 One</param>
    public ModbusRtuMaster(string portName, int baudRate, string instanceId,
        Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One)
    {
        PortName = portName;
        BaudRate = baudRate;
        _instanceId = instanceId;

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
                RaiseError($"打开串口失败: {ex.Message}", ex);
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
            if (Status is ClientStatus.None or ClientStatus.Disconnected) return;

            if (_port.IsOpen) _port.Close();
            Status = ClientStatus.Disconnected;
            Closed?.Invoke(this, "主动关闭");
        }
        catch (Exception ex)
        {
            RaiseError($"关闭串口失败: {ex.Message}", ex);
        }
        finally
        {
            _openLock.Release();
        }
    }

    /// <inheritdoc/>
    protected override async Task<byte[]> ExecuteAsync(byte unitId, byte[] requestPdu, int expectedResponsePduLength, CancellationToken token)
    {
        if (Status != ClientStatus.Connected || !_port.IsOpen)
            throw new InvalidOperationException("Modbus RTU 串口未打开");

        await _requestLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var adu = new byte[1 + requestPdu.Length + 2];
            adu[0] = unitId;
            Array.Copy(requestPdu, 0, adu, 1, requestPdu.Length);
            var crc = ModbusCrc16.Compute(adu.AsSpan(0, 1 + requestPdu.Length));
            adu[^2] = (byte)crc;           // CRC 低字节先传
            adu[^1] = (byte)(crc >> 8);

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_bufferLock)
            {
                // 上一笔请求超时后，其迟到响应字节可能仍滞留在串口驱动的接收缓冲区里（OnPortDataReceived
                // 尚未被触发去读走它们）。若不在此丢弃，这些残留字节会被当作本次新事务的响应前缀，
                // 导致 _buffer[1]（用于判断"是否异常响应"）读到的其实是上一帧的残片——轻则 CRC 校验失败
                // 报 IOException，重则残片恰好落在"未置异常位"的分支，让本该抛 ModbusException 的真实异常
                // 响应被误判为超时。DiscardInBuffer 必须和下面的 Clear/挂起请求登记在同一个锁内原子完成，
                // 否则 Discard 和 Write 之间的窗口仍可能被并发触发的 OnPortDataReceived 抢先读到脏数据。
                _port.DiscardInBuffer();
                _buffer.Clear();
                // 正常响应总长 = 地址(1) + 正常PDU长度 + CRC(2)；具体走正常还是异常分支要等收到功能码字节后才能判断，
                // 由 OnPortDataReceived 内部处理
                _expectedTotalLength = 1 + expectedResponsePduLength + 2;
                _pendingResponse = tcs;
            }

            _port.Write(adu, 0, adu.Length);

            using var timeoutCts = new CancellationTokenSource(TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            await using var registration = linkedCts.Token.Register(() => tcs.TrySetCanceled());

            byte[] responseAdu;
            try
            {
                responseAdu = await tcs.Task.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                RaiseFrameExchanged(unitId, adu, null, false, "等待响应超时");
                throw new TimeoutException($"Modbus RTU 等待从站 {unitId} 响应超时 ({TimeoutMs}ms)。");
            }
            finally
            {
                lock (_bufferLock) { _pendingResponse = null; }
            }

            // CRC 校验（低字节在前）
            var receivedCrc = (ushort)(responseAdu[^2] | (responseAdu[^1] << 8));
            var computedCrc = ModbusCrc16.Compute(responseAdu.AsSpan(0, responseAdu.Length - 2));
            if (receivedCrc != computedCrc)
            {
                RaiseFrameExchanged(unitId, adu, responseAdu, false, "CRC 校验失败");
                throw new IOException($"Modbus RTU 响应 CRC 校验失败（从站 {unitId}）。");
            }

            if (responseAdu[0] != unitId)
            {
                RaiseFrameExchanged(unitId, adu, responseAdu, false, "响应地址与请求不一致");
                throw new IOException($"Modbus RTU 响应地址与请求不一致（期望 {unitId}，收到 {responseAdu[0]}）。");
            }

            RaiseFrameExchanged(unitId, adu, responseAdu, true, null);
            return responseAdu[1..^2]; // 去掉地址与 CRC，返回 PDU
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private void RaiseFrameExchanged(byte unitId, byte[] request, byte[]? response, bool success, string? errorMessage)
        => FrameExchanged?.Invoke(this, new ModbusFrameExchangedEventArgs(unitId, request, response, success, errorMessage));

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            var bytesToRead = _port.BytesToRead;
            if (bytesToRead <= 0) return;

            var chunk = new byte[bytesToRead];
            var read = _port.Read(chunk, 0, bytesToRead);
            if (read <= 0) return;

            TaskCompletionSource<byte[]>? toComplete = null;
            byte[]? result = null;

            lock (_bufferLock)
            {
                if (_pendingResponse == null) return; // 没有在途请求：噪声字节或上一笔超时后的迟到数据，丢弃

                _buffer.AddRange(read < bytesToRead ? chunk[..read] : chunk);

                if (_buffer.Count >= 2)
                {
                    var functionCode = _buffer[1];
                    var total = (functionCode & ModbusFunctionCode.ExceptionFlag) != 0
                        ? 1 + ModbusPduCodec.ExceptionResponsePduLength + 2 // 地址(1) + 异常PDU(2) + CRC(2)
                        : _expectedTotalLength;

                    if (_buffer.Count >= total)
                    {
                        result = _buffer.Take(total).ToArray();
                        toComplete = _pendingResponse;
                        _pendingResponse = null;
                    }
                }
            }

            toComplete?.TrySetResult(result!);
        }
        catch (Exception ex)
        {
            RaiseError($"接收数据时发生错误: {ex.Message}", ex);
        }
    }

    private void OnPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        => RaiseError($"串口错误: {e.EventType}", null);

    private void RaiseError(string message, Exception? ex)
        => ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(_instanceId, message, ex));

    /// <inheritdoc/>
    public void Dispose()
    {
        _port.DataReceived -= OnPortDataReceived;
        _port.ErrorReceived -= OnPortErrorReceived;
        try { if (_port.IsOpen) _port.Close(); } catch { /* 忽略关闭异常 */ }
        _port.Dispose();
        _openLock.Dispose();
        _requestLock.Dispose();
    }
}
