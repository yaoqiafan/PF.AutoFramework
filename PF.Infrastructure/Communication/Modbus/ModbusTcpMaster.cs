using System.Net.Sockets;
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
/// Modbus TCP 主站实现。直接持有 <see cref="TcpClient"/>/<see cref="NetworkStream"/>（不复用已注册的
/// TCPClient 实例），对齐 TransferLane"协议层自己拥有底层传输原语"的既定做法。
///
/// 帧边界靠 MBAP 头的 Length 字段天然定界，不需要长度预测；每次响应都会校验 MBAP 头的 TransactionId
/// 与 UnitId 是否与本次请求一致，不匹配的帧（网络抖动/上一次超时请求的迟到响应等场景下的串包）会被丢弃、
/// 继续等待下一帧，直至匹配或整体超时。
///
/// 对"从站在同一连接上夹带非 Modbus 数据"（典型如不符合协议的心跳帧）的容忍分两道：事务开始前
/// <see cref="DrainStaleBytes"/> 排干并丢弃两次事务之间积压的旁路字节；事务窗口内到达的少量旁路字节
/// 由 <see cref="ReadResyncedHeaderAsync"/> 滑动同步跳过。两者都是丢弃语义——本类不向外暴露这些字节，
/// 若上层需要消费心跳内容，需另行设计事件而非依赖此处。
///
/// 连接意外断开（IO 异常、帧失步、半帧超时废弃连接）或连接失败后自动重连（<see cref="AutoReconnect"/>，
/// 默认开启）：按 <see cref="ReconnectIntervalMs"/> 间隔后台重试，直至成功或主动
/// <see cref="DisconnectAsync"/>/<see cref="Dispose"/>。
/// 帧读取若在半途被超时/取消打断，流内会残留半帧、后续事务无法重新对齐帧边界，此时连接会被主动废弃，
/// 交给重连恢复，避免陷入连环超时。
/// </summary>
[CommunicationUI(NavigationConstants.Views.ModbusTcpDebugView)]
public sealed class ModbusTcpMaster : ModbusMasterBase, IModbusTcpMaster, ICommunication
{
    private readonly string _instanceId;
    private readonly CategoryLogger? _logger;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly SemaphoreSlim _connectLock = new(1, 1);
    private readonly SemaphoreSlim _requestLock = new(1, 1);
    private volatile ClientStatus _status = ClientStatus.None;

    /// <summary>当前帧已从流中消费的字节数（请求经 _requestLock 串行，无并发访问）。
    /// 超时/取消发生时若 &gt; 0，说明帧读到一半被打断、流内残留半帧，连接必须废弃。</summary>
    private int _frameBytesConsumed;

    /// <summary>
    /// MBAP 帧头重新同步的最大滑动字节数；超过仍找不到合法头才判定连接严重失步并断开重连。
    /// 取值放宽到 4096 而非贴着心跳帧长度：真正的边界是 TimeoutMs（滑动全程受 linkedCts 约束，
    /// 垃圾流上不会死循环），本上限只是兜底，卡太紧反而会把"从站发了个偏大的非协议帧"误判成失步而断连。
    /// </summary>
    private const int MaxHeaderResyncBytes = 4096;

    /// <summary>单次事务前排干接收缓冲的字节上限，取典型 socket 接收缓冲大小；用于保证 <see cref="DrainStaleBytes"/> 必然终止。</summary>
    private const int MaxDrainBytes = 64 * 1024;

    private CancellationTokenSource? _reconnectCts;
    private int _reconnectLoopRunning;
    private volatile bool _disposed;
    private int _reconnectIntervalMs = 5000;

    /// <summary>MBAP 事务号计数器。每实例独立（而非全局静态），否则 ResetTransactionId 会波及其他连接实例。</summary>
    private int _transactionIdCounter;

    /// <summary>固定模式下使用的事务号。用 volatile int（C# 的 volatile 不能修饰 ushort），读时强转取低 16 位——UI 线程设置与请求线程读取间保证可见性。</summary>
    private volatile int _fixedTransactionId;

    /// <inheritdoc/>
    public string ServerIp { get; private set; }
    /// <inheritdoc/>
    public int ServerPort { get; private set; }
    /// <inheritdoc/>
    public ClientStatus Status { get => _status; private set => _status = value; }
    /// <inheritdoc/>
    public DateTime ConnectTime { get; private set; }

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
    public bool TransactionIdAutoIncrement { get; set; } = true;

    /// <inheritdoc/>
    public ushort FixedTransactionId
    {
        get => (ushort)_fixedTransactionId;
        set => _fixedTransactionId = value;
    }

    /// <inheritdoc/>
    public void ResetTransactionId() => Interlocked.Exchange(ref _transactionIdCounter, 0);

    /// <summary>
    /// 取本次请求使用的事务号：递增模式原子自增（并发调用不撞号，避免共享 Random 的老坑，
    /// 见 SecsGemMessageTools.GenerateSystemBytes）；固定模式取 <see cref="FixedTransactionId"/>。
    /// </summary>
    private ushort NextTransactionId() => TransactionIdAutoIncrement
        ? (ushort)Interlocked.Increment(ref _transactionIdCounter)
        : (ushort)_fixedTransactionId;

    /// <inheritdoc/>
    public event EventHandler<ClientConnectedEventArgs>? Connected;
    /// <inheritdoc/>
    public event EventHandler<ClientDisconnectedEventArgs>? Disconnected;
    /// <inheritdoc/>
    public event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;
    /// <inheritdoc/>
    public event EventHandler<ModbusFrameExchangedEventArgs>? FrameExchanged;

    // ── ICommunication ──────────────────────────────────────────────────

    string ICommunication.InstanceId => _instanceId;
    CommunicationCategory ICommunication.Category => CommunicationCategory.Modbus;
    CommunicationRole ICommunication.Role => CommunicationRole.Client;
    string ICommunication.DisplayName => $"Modbus TCP [{ServerIp}:{ServerPort}]";
    Task<bool> ICommunication.StartAsync(CancellationToken token) => ConnectAsync(ServerIp, ServerPort, token);
    Task ICommunication.StopAsync() => DisconnectAsync();

    /// <summary>构造 Modbus TCP 主站实例</summary>
    /// <param name="serverIp">目标从站服务端 IP</param>
    /// <param name="serverPort">目标从站服务端端口（Modbus TCP 标准端口 502）</param>
    /// <param name="instanceId">通讯实例唯一标识</param>
    /// <param name="logger">日志服务，缺省时不记录日志</param>
    public ModbusTcpMaster(string serverIp, int serverPort, string instanceId, ILogService? logger = null)
    {
        ServerIp = serverIp;
        ServerPort = serverPort;
        _instanceId = instanceId;
        _logger = logger == null ? null : CategoryLoggerFactory.Communication(logger);
    }

    /// <inheritdoc/>
    public async Task<bool> ConnectAsync(string serverIp, int serverPort, CancellationToken token = default)
    {
        await _connectLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (Status == ClientStatus.Connected) return true;

            Status = ClientStatus.Connecting;
            ServerIp = serverIp;
            ServerPort = serverPort;

            _tcpClient = new TcpClient();
            // 必须透传 token：无 token 的连接尝试在目标不可达时按 OS 默认超时阻塞（Windows 约 21s），
            // 启动编排和重连循环的取消都约束不了它
            await _tcpClient.ConnectAsync(serverIp, serverPort, token).ConfigureAwait(false);
            _stream = _tcpClient.GetStream();
            ConnectTime = DateTime.Now;
            Status = ClientStatus.Connected;
            _logger?.Info($"[ModbusTcp:{_instanceId}] 已连接到 {serverIp}:{serverPort}");
            Connected?.Invoke(this, new ClientConnectedEventArgs(_instanceId, $"{serverIp}:{serverPort}"));
            return true;
        }
        catch (OperationCanceledException)
        {
            Status = ClientStatus.Disconnected;
            CleanupConnection();
            return false;
        }
        catch (Exception ex)
        {
            Status = ClientStatus.Error;
            CleanupConnection();
            RaiseError($"连接失败: {ex.Message}", ex);
            TryStartReconnectLoop();
            return false;
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc/>
    public async Task DisconnectAsync()
    {
        StopReconnectLoop(); // 主动断开优先于自动重连，避免刚断开又被后台重连回去
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status is ClientStatus.None or ClientStatus.Disconnected) return;

            // 先置状态再清理：让在途请求的失败处理（HandleConnectionFailure 只认 Connected）识别为主动断开
            Status = ClientStatus.Disconnected;
            CleanupConnection();
            StopReconnectLoop(); // 兜底清掉状态置位前窗口内刚启动的重连循环
            _logger?.Info($"[ModbusTcp:{_instanceId}] 已主动断开");
            Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_instanceId, "主动断开"));
        }
        finally
        {
            _connectLock.Release();
        }
    }

    /// <inheritdoc/>
    /// <remarks>TransactionId 以 0x0000 占位（实际发送时才分配），其余字节与实发帧完全一致。</remarks>
    public override byte[] BuildFrame(byte unitId, byte[] requestPdu)
        => ModbusTcpFrameCodec.BuildAdu(0, unitId, requestPdu);

    private void CleanupConnection()
    {
        try { _stream?.Dispose(); } catch { /* 忽略关闭异常 */ }
        try { _tcpClient?.Close(); } catch { /* 忽略关闭异常 */ }
        _stream = null;
        _tcpClient = null;
    }

    /// <inheritdoc/>
    protected override async Task<byte[]> ExecuteAsync(byte unitId, byte[] requestPdu, int expectedResponsePduLength, CancellationToken token)
    {
        await _requestLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            // 流必须在拿到 _requestLock 之后取：等锁期间可能发生断线重连换流，锁外提前捕获会拿到已 Dispose 的旧流
            var stream = _stream;
            if (Status != ClientStatus.Connected || stream == null)
                throw new InvalidOperationException("Modbus TCP 未连接");

            using var timeoutCts = new CancellationTokenSource(TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            var transactionId = NextTransactionId();
            var adu = ModbusTcpFrameCodec.BuildAdu(transactionId, unitId, requestPdu);

            try
            {
                _frameBytesConsumed = 0;
                DrainStaleBytes(stream);
                await stream.WriteAsync(adu, linkedCts.Token).ConfigureAwait(false);

                // 收到的帧可能不是本次事务的响应（网络抖动/上一次超时请求的迟到响应等）：
                // 循环读取直到 TransactionId 与 UnitId 均匹配，或整体超时
                while (true)
                {
                    _frameBytesConsumed = 0;

                    // 先重新同步出合法 MBAP 头：从站多发字节/连接混入非 Modbus 数据会让字节流错位，
                    // 直接按错位头解析会得到荒诞的 Length（如几万字节）并误判失步断开；这里就地滑动恢复
                    var header = await ReadResyncedHeaderAsync(stream, linkedCts.Token).ConfigureAwait(false);
                    var (respTransactionId, respUnitId, pduLength) = ModbusTcpFrameCodec.ParseHeader(header);

                    var pdu = new byte[pduLength];
                    await ReadExactAsync(stream, pdu, linkedCts.Token).ConfigureAwait(false);

                    var responseFrame = new byte[header.Length + pdu.Length];
                    Array.Copy(header, responseFrame, header.Length);
                    Array.Copy(pdu, 0, responseFrame, header.Length, pdu.Length);

                    if (respTransactionId == transactionId && respUnitId == unitId)
                    {
                        // 从站异常响应按失败事务记录（Success 语义 = 收到有效的"正常"响应），调试面板/日志能直接
                        // 看出这笔是从站拒绝而不是一笔成功报文；PDU 照常返回，由基类解析并抛 ModbusException
                        var isException = pdu.Length > 0 && (pdu[0] & ModbusFunctionCode.ExceptionFlag) != 0;
                        RaiseFrameExchanged(unitId, adu, responseFrame, !isException,
                            isException ? $"从站返回异常响应（异常码 0x{(pdu.Length > 1 ? pdu[1] : 0):X2}）" : null);
                        return pdu;
                    }

                    // 不匹配：丢弃，继续等下一帧，仍记一笔方便调试排查串包
                    RaiseFrameExchanged(unitId, adu, responseFrame, false,
                        $"事务号/从站地址不匹配（期望 TransId={transactionId} UnitId={unitId}，收到 TransId={respTransactionId} UnitId={respUnitId}），已丢弃继续等待");
                }
            }
            catch (OperationCanceledException)
            {
                // 帧读到一半被打断：流内残留半帧，后续事务会把残帧字节错位解析成 MBAP 头，
                // 永远无法重新对齐——连接必须废弃，交给自动重连恢复
                if (_frameBytesConsumed > 0)
                    HandleConnectionFailure($"帧读取中断（已读 {_frameBytesConsumed} 字节），流内残留半帧，废弃连接", null);

                if (timeoutCts.IsCancellationRequested && !token.IsCancellationRequested)
                {
                    RaiseFrameExchanged(unitId, adu, null, false, "等待响应超时");
                    throw new TimeoutException($"Modbus TCP 等待从站 {unitId} 响应超时 ({TimeoutMs}ms)。");
                }
                throw; // 外部 token 主动取消：让 OperationCanceledException 原样传播
            }
            catch (IOException ex)
            {
                RaiseFrameExchanged(unitId, adu, null, false, $"连接异常: {ex.Message}");
                HandleConnectionFailure($"连接异常: {ex.Message}", ex);
                throw;
            }
            catch (ObjectDisposedException ex)
            {
                // 主动断开/重连换流会把在途请求正在使用的流 Dispose 掉——清理和事件已由触发方完成，
                // 这里只翻译成 IOException 维持接口契约，不再重复清理或触发重连
                RaiseFrameExchanged(unitId, adu, null, false, "连接已被关闭");
                throw new IOException("Modbus TCP 连接已被关闭。", ex);
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    /// <summary>
    /// 事务开始前排干接收缓冲里的陈旧字节并丢弃。两次事务之间到达的字节只可能是两类：从站发出的
    /// 非 Modbus 旁路数据（如不符合协议的心跳帧），或上一笔超时事务的迟到响应。两者都无人认领——
    /// 迟到响应本就会被 TransactionId 匹配丢弃，旁路数据更不属于任何事务——留着只会成为下一帧的
    /// 错位前缀，逼 <see cref="ReadResyncedHeaderAsync"/> 逐字节滑过去。NetworkStream 不带缓冲，
    /// 滑动时每字节一次 recv 系统调用，积压越多越贵；在此一次性读掉，滑窗便只需应付事务窗口内
    /// 到达的少量字节，<see cref="MaxHeaderResyncBytes"/> 也不会被心跳积压撑爆。
    ///
    /// 只排干进入时已到达的数据：<see cref="NetworkStream.DataAvailable"/> 为真保证 Read 不阻塞，
    /// <see cref="MaxDrainBytes"/> 兜住"数据持续涌入"的极端情况以保证本方法必然返回。
    /// 调用点在 _requestLock 内，无并发事务；socket 已失效时 Read 抛出的 IOException 由调用方
    /// 既有的 catch 走 HandleConnectionFailure，不在此吞掉。
    /// </summary>
    private void DrainStaleBytes(NetworkStream stream)
    {
        var buffer = new byte[4096];
        var total = 0;
        while (stream.DataAvailable && total < MaxDrainBytes)
        {
            var read = stream.Read(buffer, 0, Math.Min(buffer.Length, MaxDrainBytes - total));
            if (read <= 0) break;
            total += read;
        }

        // 心跳环境下这是每笔事务都会发生的常态，只记 Debug，避免把 Warn 刷屏
        if (total > 0)
            _logger?.Debug($"[ModbusTcp:{_instanceId}] 事务前排干接收缓冲 {total} 字节陈旧数据（非 Modbus 旁路数据/迟到响应）");
    }

    private async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token).ConfigureAwait(false);
            if (read == 0) throw new IOException("Modbus TCP 连接已被对端关闭。");
            offset += read;
            _frameBytesConsumed += read;
        }
    }

    /// <summary>
    /// 读取并校验 MBAP 帧头；若首读的 7 字节不构成合法 MBAP 头（ProtocolId 非 0 或 Length 越界），
    /// 用滑动窗口向前扫描重新对齐。从站多发字节、连接上混入非 Modbus 数据、上一笔迟到响应残片等
    /// 都会让字节流错位，直接断开重连代价过大，这里尽量就地恢复。Modbus 的 ProtocolId 恒为 0 是
    /// 强同步标记，配合 Length 合法范围 [2,254]（PDU 1~253）误判率很低；即便误同步读出的"帧"也会
    /// 被后续 TransactionId/UnitId 匹配兜住、不会错当成本次响应。连续滑动超过上限仍无合法头才判定
    /// 连接严重失步并抛 IOException（走断开重连兜底）。
    /// </summary>
    private async Task<byte[]> ReadResyncedHeaderAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[ModbusTcpFrameCodec.MbapHeaderLength];
        await ReadExactAsync(stream, header, token).ConfigureAwait(false);

        for (var attempt = 0; attempt < MaxHeaderResyncBytes; attempt++)
        {
            var protocolId = (ushort)((header[2] << 8) | header[3]);
            var length = (ushort)((header[4] << 8) | header[5]);
            if (protocolId == 0 && length is >= 2 and <= ModbusTcpFrameCodec.MaxPduLength + 1)
            {
                if (attempt > 0)
                    _logger?.Warn($"[ModbusTcp:{_instanceId}] MBAP 帧头重新同步：丢弃前 {attempt} 字节脏数据后对齐（从站可能多发字节或连接混入非 Modbus 数据）");
                return header;
            }

            // 丢弃首字节、窗口前移、补读 1 字节，继续在流里寻找合法 MBAP 头
            Array.Copy(header, 1, header, 0, ModbusTcpFrameCodec.MbapHeaderLength - 1);
            var oneByte = new byte[1];
            await ReadExactAsync(stream, oneByte, token).ConfigureAwait(false);
            header[ModbusTcpFrameCodec.MbapHeaderLength - 1] = oneByte[0];
        }

        throw new IOException("无法重新同步 Modbus TCP MBAP 帧头（连续滑动未找到合法头），帧已严重失步");
    }

    // ── 意外断开与自动重连 ─────────────────────────────────────────────────

    /// <summary>
    /// 连接意外失效（IO 异常、帧失步、半帧超时）的统一处理：置 Error、清理连接、
    /// 触发 Disconnected 事件并启动自动重连。仅在 Connected 状态下动作——主动断开已提前置状态，
    /// 其在途请求的失败会走 ObjectDisposedException 分支，不会重复清理或把连接重连回去。
    /// </summary>
    private void HandleConnectionFailure(string reason, Exception? ex)
    {
        if (Status != ClientStatus.Connected) return;
        Status = ClientStatus.Error;
        CleanupConnection();
        _logger?.Warn($"[ModbusTcp:{_instanceId}] 连接异常断开: {reason}", ex);
        Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_instanceId, reason));
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
            _logger?.Info($"[ModbusTcp:{_instanceId}] 启动自动重连（间隔 {ReconnectIntervalMs}ms）");
            try
            {
                while (!cts.Token.IsCancellationRequested && AutoReconnect)
                {
                    await Task.Delay(ReconnectIntervalMs, cts.Token).ConfigureAwait(false);
                    if (Status == ClientStatus.Connected) break;
                    if (await ConnectAsync(ServerIp, ServerPort, cts.Token).ConfigureAwait(false))
                    {
                        _logger?.Info($"[ModbusTcp:{_instanceId}] 自动重连成功");
                        break;
                    }
                }
            }
            catch (OperationCanceledException) { /* 主动断开/释放时取消，正常退出 */ }
            catch (Exception ex)
            {
                _logger?.Error($"[ModbusTcp:{_instanceId}] 自动重连循环异常退出: {ex.Message}", ex);
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

    private void RaiseError(string message, Exception? ex)
    {
        _logger?.Error($"[ModbusTcp:{_instanceId}] {message}", ex);
        ErrorOccurred?.Invoke(this, new ErrorOccurredEventArgs(_instanceId, message, ex));
    }

    private void RaiseFrameExchanged(byte unitId, byte[] request, byte[]? response, bool success, string? errorMessage)
    {
        // 只记失败事务，避免正常轮询在日志里刷屏；成功报文的实时查看走调试面板订阅本事件
        if (!success) _logger?.Warn($"[ModbusTcp:{_instanceId}] 从站{unitId} 事务失败：{errorMessage}");
        FrameExchanged?.Invoke(this, new ModbusFrameExchangedEventArgs(unitId, request, response, success, errorMessage));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _disposed = true;
        StopReconnectLoop();
        CleanupConnection();
        _connectLock.Dispose();
        _requestLock.Dispose();
        _reconnectCts?.Dispose();
    }
}
