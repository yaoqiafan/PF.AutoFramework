using System.IO.Ports;
using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.Modbus;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Communication.Modbus.Internal;
using PF.Infrastructure.Logging;

namespace PF.Infrastructure.Communication.Modbus;

/// <summary>
/// Modbus RTU 主站实现。直接持有 <see cref="SerialPort"/>（不复用已注册的 SerialPortCommunication 实例），
/// 对齐 SerialPortCommunication/TransferLane"协议层自己拥有底层传输原语"的既定做法。
///
/// 帧边界判定以"按 PDU 推算的期望响应长度"为主：发起请求前已知功能码与数量，可提前算出正常响应的字节数
/// （<see cref="ModbusPduCodec.GetExpectedNormalResponsePduLength"/>），收满即返回；异常响应固定 2 字节 PDU，
/// 靠首个功能码字节的最高位区分。仅靠 T3.5 静默判帧在托管 SerialPort.DataReceived 上误判率高，这里只用超时兜底。
///
/// 串口意外失效（USB 转串被拔出、驱动报错等）时自动重连（<see cref="AutoReconnect"/>，默认开启）：
/// 检测发生在事务执行/收发出错时，检测到后关闭失效端口、触发 <see cref="Closed"/>，并按
/// <see cref="ReconnectIntervalMs"/> 间隔后台重试打开，直至成功或主动 <see cref="CloseAsync"/>/<see cref="Dispose"/>。
/// </summary>
[CommunicationUI(NavigationConstants.Views.ModbusRtuDebugView)]
public sealed class ModbusRtuMaster : ModbusMasterBase, IModbusRtuMaster, ICommunication
{
    private readonly SerialPort _port;
    private readonly string _instanceId;
    private readonly CategoryLogger? _logger;
    private readonly SemaphoreSlim _openLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);

    private readonly object _bufferLock = new();
    private readonly List<byte> _buffer = new();
    private int _expectedTotalLength;
    private TaskCompletionSource<byte[]>? _pendingResponse;

    /// <summary>
    /// 原始透传（SendRawAsync）无法预知响应长度，改用报文间静默判帧：每收到一段数据就重置本定时器，
    /// 静默窗口内无新字节到达即认为一帧收完。窗口取 10 个字符时间（1 字符按 11 位算）与 30ms 的较大者——
    /// 协议规定的 T3.5 在托管 SerialPort 的事件批量投递下裕量不足，低波特率下按字符时间放大。
    /// </summary>
    private readonly System.Threading.Timer _rawIdleTimer;
    private int RawIdleMs => Math.Max(30, 110_000 / BaudRate);

    private volatile ClientStatus _status = ClientStatus.None;

    private CancellationTokenSource? _reconnectCts;
    private int _reconnectLoopRunning;
    private volatile bool _disposed;
    private int _reconnectIntervalMs = 5000;

    /// <inheritdoc/>
    public string PortName { get; }
    /// <inheritdoc/>
    public int BaudRate { get; }
    /// <inheritdoc/>
    public ClientStatus Status { get => _status; private set => _status = value; }

    /// <inheritdoc/>
    public bool AutoReconnect { get; set; } = true;

    /// <inheritdoc/>
    public int ReconnectIntervalMs
    {
        get => _reconnectIntervalMs;
        set => _reconnectIntervalMs = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "ReconnectIntervalMs 必须为正数");
    }

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
    /// <param name="logger">日志服务，缺省时不记录日志</param>
    public ModbusRtuMaster(string portName, int baudRate, string instanceId,
        Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One, ILogService? logger = null)
    {
        PortName = portName;
        BaudRate = baudRate;
        _instanceId = instanceId;
        _logger = logger == null ? null : CategoryLoggerFactory.Communication(logger);

        _port = new SerialPort(portName, baudRate, parity, dataBits, stopBits);
        _port.DataReceived += OnPortDataReceived;
        _port.ErrorReceived += OnPortErrorReceived;
        _rawIdleTimer = new System.Threading.Timer(OnRawIdleElapsed, null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <inheritdoc/>
    public override byte[] BuildFrame(byte unitId, byte[] requestPdu) => BuildRtuAdu(unitId, requestPdu);

    private static byte[] BuildRtuAdu(byte unitId, byte[] requestPdu)
    {
        var adu = new byte[1 + requestPdu.Length + 2];
        adu[0] = unitId;
        Array.Copy(requestPdu, 0, adu, 1, requestPdu.Length);
        var crc = ModbusCrc16.Compute(adu.AsSpan(0, 1 + requestPdu.Length));
        adu[^2] = (byte)crc;           // CRC 低字节先传
        adu[^1] = (byte)(crc >> 8);
        return adu;
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
                _logger?.Info($"[ModbusRtu:{_instanceId}] 串口已打开 ({PortName} {BaudRate}bps)");
                Opened?.Invoke(this, EventArgs.Empty);
                return true;
            }
            catch (Exception ex)
            {
                Status = ClientStatus.Error;
                RaiseError($"打开串口失败: {ex.Message}", ex);
                TryStartReconnectLoop();
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
        StopReconnectLoop(); // 主动关闭优先于自动重连，避免刚关掉又被后台重连回去
        await _openLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status is ClientStatus.None or ClientStatus.Disconnected) return;

            // 先置状态再关端口：让并发的失效检测（HandleUnexpectedClose 只认 Connected）识别为主动关闭
            Status = ClientStatus.Disconnected;
            if (_port.IsOpen) _port.Close();
            StopReconnectLoop(); // 兜底清掉状态置位前窗口内刚启动的重连循环
            _logger?.Info($"[ModbusRtu:{_instanceId}] 串口已关闭");
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
        {
            // Status 仍是 Connected 但端口已不在打开状态 = 串口被意外拔出/失效
            HandleUnexpectedClose("请求时发现串口已不在打开状态");
            throw new InvalidOperationException("Modbus RTU 串口未打开");
        }

        await _requestLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var adu = BuildRtuAdu(unitId, requestPdu);

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            try
            {
                lock (_bufferLock)
                {
                    // 上一笔请求超时后，其迟到响应字节可能仍滞留在串口驱动的接收缓冲区里（OnPortDataReceived
                    // 尚未被触发去读走它们）。若不在此丢弃，这些残留字节会被当作本次新事务的响应前缀，
                    // 导致 _buffer[1]（用于判断"是否异常响应"）读到的其实是上一帧的残片——轻则 CRC 校验失败
                    // 报 IOException，重则残片恰好落在"未置异常位"的分支，让本该抛 ModbusException 的真实异常
                    // 响应被误判为超时。DiscardInBuffer 必须和 Clear/挂起请求登记在同一个锁内原子完成，
                    // 且 OnPortDataReceived 的 _port.Read 也必须在同一把锁内（见彼处注释），两侧缺一不可。
                    _port.DiscardInBuffer();
                    _buffer.Clear();
                    // 正常响应总长 = 地址(1) + 正常PDU长度 + CRC(2)；具体走正常还是异常分支要等收到功能码字节后才能判断，
                    // 由 OnPortDataReceived 内部处理。期望长度未知（原始透传）时保持哨兵值，改用静默判帧。
                    _expectedTotalLength = expectedResponsePduLength >= 0 ? 1 + expectedResponsePduLength + 2 : UnknownResponseLength;
                    _pendingResponse = tcs;
                    _rawIdleTimer.Change(Timeout.Infinite, Timeout.Infinite); // 清掉上一笔透传残留的静默定时
                }

                // 默认 WriteTimeout 为无限：硬件流控挂死时同步 Write 会永久阻塞且无法被 token 打断，用 TimeoutMs 兜底
                _port.WriteTimeout = TimeoutMs;
                _port.Write(adu, 0, adu.Length);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or TimeoutException)
            {
                lock (_bufferLock) { _pendingResponse = null; }
                RaiseFrameExchanged(unitId, adu, null, false, $"串口访问失败: {ex.Message}");
                HandleUnexpectedClose($"串口访问失败: {ex.Message}");
                if (ex is TimeoutException) throw;
                throw new IOException($"Modbus RTU 串口访问失败: {ex.Message}", ex);
            }

            using var timeoutCts = new CancellationTokenSource(TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
            await using var registration = linkedCts.Token.Register(() => tcs.TrySetCanceled());

            byte[] responseAdu;
            try
            {
                responseAdu = await tcs.Task.ConfigureAwait(false);
            }
            // 只把"确实是响应超时"包装成 TimeoutException；外部 token 主动取消（如工站取消式暂停）
            // 让 OperationCanceledException 原样传播，调用方才能把两者区分开
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
            {
                RaiseFrameExchanged(unitId, adu, null, false, "等待响应超时");
                throw new TimeoutException($"Modbus RTU 等待从站 {unitId} 响应超时 ({TimeoutMs}ms)。");
            }
            finally
            {
                lock (_bufferLock) { _pendingResponse = null; }
            }

            // 最短帧防护：地址(1)+功能码(1)+CRC(2)；长度判帧路径天然满足，静默判帧收到的噪声碎帧在此拦截
            if (responseAdu.Length < 4)
            {
                RaiseFrameExchanged(unitId, adu, responseAdu, false, "响应帧过短");
                throw new IOException($"Modbus RTU 响应帧过短（{responseAdu.Length} 字节）。");
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

            var pdu = responseAdu[1..^2]; // 去掉地址与 CRC
            // 从站异常响应按失败事务记录（Success 语义 = 收到有效的"正常"响应），调试面板/日志能直接
            // 看出这笔是从站拒绝而不是一笔成功报文；PDU 照常返回，由基类解析并抛 ModbusException
            var isException = pdu.Length > 0 && (pdu[0] & ModbusFunctionCode.ExceptionFlag) != 0;
            RaiseFrameExchanged(unitId, adu, responseAdu, !isException,
                isException ? $"从站返回异常响应（异常码 0x{(pdu.Length > 1 ? pdu[1] : 0):X2}）" : null);
            return pdu;
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private void RaiseFrameExchanged(byte unitId, byte[] request, byte[]? response, bool success, string? errorMessage)
    {
        // 只记失败事务，避免正常轮询在日志里刷屏；成功报文的实时查看走调试面板订阅本事件
        if (!success) _logger?.Warn($"[ModbusRtu:{_instanceId}] 从站{unitId} 事务失败：{errorMessage}");
        FrameExchanged?.Invoke(this, new ModbusFrameExchangedEventArgs(unitId, request, response, success, errorMessage));
    }

    private void OnPortDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        try
        {
            TaskCompletionSource<byte[]>? toComplete = null;
            byte[]? result = null;

            // _port.Read 必须在 _bufferLock 内。若在锁外先读，会出现这样的交错：
            // 本线程把上一笔超时事务的迟到响应读进本地 chunk（驱动缓冲随之清空）→ 尚未进锁 →
            // 新事务在锁内 DiscardInBuffer（此时已无字节可丢）并登记 _pendingResponse →
            // 本线程进锁发现有在途请求，把脏字节追加进 _buffer——恰好绕过 ExecuteAsync 丢弃残留的防线。
            // 读取放进锁内后两种加锁顺序都安全：本线程先拿锁，脏字节读出后因 _pendingResponse == null 被丢弃；
            // 请求线程先拿锁，DiscardInBuffer 能真正清掉驱动缓冲里的残留。
            lock (_bufferLock)
            {
                var bytesToRead = _port.BytesToRead;
                if (bytesToRead <= 0) return;

                var chunk = new byte[bytesToRead];
                var read = _port.Read(chunk, 0, bytesToRead);
                if (read <= 0) return;

                if (_pendingResponse == null) return; // 没有在途请求：噪声字节或上一笔超时后的迟到数据，丢弃

                _buffer.AddRange(read < bytesToRead ? chunk[..read] : chunk);

                if (_expectedTotalLength == UnknownResponseLength)
                {
                    // 原始透传：长度未知，静默窗口内没有新字节即认为收完一帧（由 OnRawIdleElapsed 完成挂起请求）
                    _rawIdleTimer.Change(RawIdleMs, Timeout.Infinite);
                }
                else if (_buffer.Count >= 2)
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
            if (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
                HandleUnexpectedClose($"接收数据时串口访问失败: {ex.Message}");
        }
    }

    /// <summary>原始透传的静默判帧回调：窗口期内无新字节，取缓冲区现有内容作为完整一帧</summary>
    private void OnRawIdleElapsed(object? state)
    {
        TaskCompletionSource<byte[]>? toComplete = null;
        byte[]? result = null;
        lock (_bufferLock)
        {
            if (_pendingResponse == null || _expectedTotalLength != UnknownResponseLength || _buffer.Count == 0) return;
            result = _buffer.ToArray();
            toComplete = _pendingResponse;
            _pendingResponse = null;
        }
        toComplete?.TrySetResult(result!);
    }

    private void OnPortErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        => RaiseError($"串口错误: {e.EventType}", null);

    private void RaiseError(string message, Exception? ex)
    {
        _logger?.Error($"[ModbusRtu:{_instanceId}] {message}", ex);
        ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(_instanceId, message, ex));
    }

    // ── 意外断开与自动重连 ─────────────────────────────────────────────────

    /// <summary>
    /// 串口意外失效（USB 转串被拔出、驱动报错等）的统一处理：关闭失效端口、置 Error、
    /// 触发 Closed 事件并启动自动重连。仅在 Connected 状态下动作（主动关闭已提前置状态，不会进来）；
    /// 极端竞态下可能重复触发 Closed 事件，但重连循环本身有 CAS 防重入，不会起两个。
    /// </summary>
    private void HandleUnexpectedClose(string reason)
    {
        if (Status != ClientStatus.Connected) return;
        Status = ClientStatus.Error;
        try { if (_port.IsOpen) _port.Close(); } catch { /* 端口已失效时 Close 本身可能抛异常，忽略 */ }
        _logger?.Warn($"[ModbusRtu:{_instanceId}] 串口意外断开: {reason}");
        Closed?.Invoke(this, $"意外断开: {reason}");
        TryStartReconnectLoop();
    }

    private void TryStartReconnectLoop()
    {
        if (!AutoReconnect || _disposed) return;
        if (Interlocked.CompareExchange(ref _reconnectLoopRunning, 1, 0) != 0) return;

        _reconnectCts?.Dispose();
        var cts = new CancellationTokenSource();
        _reconnectCts = cts;

        _ = Task.Run(async () =>
        {
            _logger?.Info($"[ModbusRtu:{_instanceId}] 启动串口自动重连（间隔 {ReconnectIntervalMs}ms）");
            try
            {
                while (!cts.Token.IsCancellationRequested && AutoReconnect)
                {
                    await Task.Delay(ReconnectIntervalMs, cts.Token).ConfigureAwait(false);
                    if (Status == ClientStatus.Connected) break;
                    if (await OpenAsync().ConfigureAwait(false))
                    {
                        _logger?.Info($"[ModbusRtu:{_instanceId}] 串口自动重连成功");
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { /* 主动关闭/释放时取消，正常退出 */ }
            catch (Exception ex)
            {
                _logger?.Error($"[ModbusRtu:{_instanceId}] 自动重连循环异常退出: {ex.Message}", ex);
            }
            finally
            {
                Interlocked.Exchange(ref _reconnectLoopRunning, 0);
            }
        }, CancellationToken.None);
    }

    private void StopReconnectLoop()
    {
        try { _reconnectCts?.Cancel(); }
        catch (ObjectDisposedException) { /* 与 Dispose 竞态时 CTS 可能已释放，忽略 */ }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        StopReconnectLoop();
        _port.DataReceived -= OnPortDataReceived;
        _port.ErrorReceived -= OnPortErrorReceived;
        try { if (_port.IsOpen) _port.Close(); } catch { /* 忽略关闭异常 */ }
        _port.Dispose();
        _rawIdleTimer.Dispose();
        _openLock.Dispose();
        _requestLock.Dispose();
        _reconnectCts?.Dispose();
    }
}
