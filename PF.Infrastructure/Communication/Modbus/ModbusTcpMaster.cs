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
/// v1 不做自动重连守护循环（对齐 TCPClient 自身的模型——它也只暴露 ConnectAsync/ReconnectAsync/Disconnected
/// 事件，不带后台守护循环）：断线只更新 Status 并触发事件，重连由调用方（调试面板手动触发，或未来上层按需
/// 包一层重试策略）决定。
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
    private ClientStatus _status = ClientStatus.None;

    /// <inheritdoc/>
    public string ServerIp { get; private set; }
    /// <inheritdoc/>
    public int ServerPort { get; private set; }
    /// <inheritdoc/>
    public ClientStatus Status { get => _status; private set => _status = value; }
    /// <inheritdoc/>
    public DateTime ConnectTime { get; private set; }

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
    Task<bool> ICommunication.StartAsync(CancellationToken token) => ConnectAsync(ServerIp, ServerPort);
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
    public async Task<bool> ConnectAsync(string serverIp, int serverPort)
    {
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status == ClientStatus.Connected) return true;

            Status = ClientStatus.Connecting;
            ServerIp = serverIp;
            ServerPort = serverPort;

            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(serverIp, serverPort).ConfigureAwait(false);
            _stream = _tcpClient.GetStream();
            ConnectTime = DateTime.Now;
            Status = ClientStatus.Connected;
            _logger?.Info($"[ModbusTcp:{_instanceId}] 已连接到 {serverIp}:{serverPort}");
            Connected?.Invoke(this, new ClientConnectedEventArgs(_instanceId, $"{serverIp}:{serverPort}"));
            return true;
        }
        catch (Exception ex)
        {
            Status = ClientStatus.Error;
            CleanupConnection();
            RaiseError($"连接失败: {ex.Message}", ex);
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
        await _connectLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (Status is ClientStatus.None or ClientStatus.Disconnected) return;

            CleanupConnection();
            Status = ClientStatus.Disconnected;
            _logger?.Info($"[ModbusTcp:{_instanceId}] 已主动断开");
            Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_instanceId, "主动断开"));
        }
        finally
        {
            _connectLock.Release();
        }
    }

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
        var stream = _stream;
        if (Status != ClientStatus.Connected || stream == null)
            throw new InvalidOperationException("Modbus TCP 未连接");

        await _requestLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeoutMs);
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);

            var transactionId = ModbusTcpFrameCodec.NextTransactionId();
            var adu = ModbusTcpFrameCodec.BuildAdu(transactionId, unitId, requestPdu);

            try
            {
                await stream.WriteAsync(adu, linkedCts.Token).ConfigureAwait(false);

                // 收到的帧可能不是本次事务的响应（网络抖动/上一次超时请求的迟到响应等）：
                // 循环读取直到 TransactionId 与 UnitId 均匹配，或整体超时
                while (true)
                {
                    var header = new byte[ModbusTcpFrameCodec.MbapHeaderLength];
                    await ReadExactAsync(stream, header, linkedCts.Token).ConfigureAwait(false);
                    var (respTransactionId, respUnitId, pduLength) = ModbusTcpFrameCodec.ParseHeader(header);

                    var pdu = new byte[pduLength];
                    await ReadExactAsync(stream, pdu, linkedCts.Token).ConfigureAwait(false);

                    var responseFrame = new byte[header.Length + pdu.Length];
                    Array.Copy(header, responseFrame, header.Length);
                    Array.Copy(pdu, 0, responseFrame, header.Length, pdu.Length);

                    if (respTransactionId == transactionId && respUnitId == unitId)
                    {
                        RaiseFrameExchanged(unitId, adu, responseFrame, true, null);
                        return pdu;
                    }

                    // 不匹配：丢弃，继续等下一帧，仍记一笔方便调试排查串包
                    RaiseFrameExchanged(unitId, adu, responseFrame, false,
                        $"事务号/从站地址不匹配（期望 TransId={transactionId} UnitId={unitId}，收到 TransId={respTransactionId} UnitId={respUnitId}），已丢弃继续等待");
                }
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                RaiseFrameExchanged(unitId, adu, null, false, "等待响应超时");
                throw new TimeoutException($"Modbus TCP 等待从站 {unitId} 响应超时 ({TimeoutMs}ms)。");
            }
            catch (IOException ex)
            {
                RaiseFrameExchanged(unitId, adu, null, false, $"连接异常: {ex.Message}");
                Status = ClientStatus.Error;
                CleanupConnection();
                _logger?.Warn($"[ModbusTcp:{_instanceId}] 连接异常断开: {ex.Message}", ex);
                Disconnected?.Invoke(this, new ClientDisconnectedEventArgs(_instanceId, $"连接异常: {ex.Message}"));
                throw;
            }
        }
        finally
        {
            _requestLock.Release();
        }
    }

    private static async Task ReadExactAsync(Stream stream, byte[] buffer, CancellationToken token)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), token).ConfigureAwait(false);
            if (read == 0) throw new IOException("Modbus TCP 连接已被对端关闭。");
            offset += read;
        }
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
        CleanupConnection();
        _connectLock.Dispose();
        _requestLock.Dispose();
    }
}
