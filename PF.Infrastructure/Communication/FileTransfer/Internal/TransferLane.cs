using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums.FileTransfer;

namespace PF.Infrastructure.Communication.FileTransfer.Internal;

/// <summary>
/// 单条 Lane：对应一个物理网口的独立 TCP 连接。负责连接建立（Server 监听 / Client 主动连接）、
/// 断线后无限重连、应用层心跳、发送队列（同一连接只有一个写入者，避免并发写 Stream 造成帧交织损坏）、
/// 接收循环。协议语义（Begin/Chunk 帧字段解析、InFlightTransfer 登记）不在此类处理，
/// 由 <see cref="FrameCodec.CommonHeader"/> 之后的分发回调交给上层 FileTransferChannel 完成。
/// </summary>
internal sealed class TransferLane : IAsyncDisposable
{
    public int LaneId { get; }
    public FileTransferLinkEndpoint Endpoint { get; }
    public bool IsConnected => _isConnected;

    private readonly FileTransferRole _role;
    private readonly FileTransferOptions _options;
    private readonly Func<int, FrameCodec.CommonHeader, Stream, CancellationToken, Task> _onFrameReceived;
    private readonly Action<LaneStatus, bool> _onStatusChanged;

    // 发送队列深度即发送侧流水线深度：文件发送时每个排队的 Chunk 任务持有一块租借缓冲，
    // 队列深度 × 分片大小上限 × Lane 数 = 发送侧内存峰值上界（如 2 Lane × 4 × 8MB = 64MB）。
    // 深度 4 已足够让 Socket 持续饱和（队列满时分发循环阻塞在入队处形成反压），不要为吞吐盲目调大。
    //
    // 非 readonly：StopAsync 会 TryComplete 这个 Channel（不可逆的永久关闭），Start() 之后必须换一个全新实例，
    // 否则重新启动后 SendLoopAsync 一读取已关闭的 Channel 就抛 ChannelClosedException、连接被判定断线，
    // GuardianLoopAsync 重连后又立刻复现同样的问题，表现为连接成功/断开无限循环。
    private Channel<Func<Stream, CancellationToken, Task>> _sendQueue = CreateSendQueue();

    private static Channel<Func<Stream, CancellationToken, Task>> CreateSendQueue() =>
        Channel.CreateBounded<Func<Stream, CancellationToken, Task>>(
            new BoundedChannelOptions(4) { FullMode = BoundedChannelFullMode.Wait });

    private TcpListener? _listener;
    private CancellationTokenSource? _lifecycleCts;
    private Task? _guardianTask;

    private long _bytesSent;
    private long _bytesReceived;
    private int _reconnectAttempts;
    private DateTime? _connectedTime;
    private DateTime _lastActivityUtc = DateTime.UtcNow;
    private string? _remoteEndPoint;
    private volatile bool _isConnected;
    private volatile bool _stopping;

    public TransferLane(
        int laneId,
        FileTransferLinkEndpoint endpoint,
        FileTransferRole role,
        FileTransferOptions options,
        Func<int, FrameCodec.CommonHeader, Stream, CancellationToken, Task> onFrameReceived,
        Action<LaneStatus, bool> onStatusChanged)
    {
        LaneId = laneId;
        Endpoint = endpoint;
        _role = role;
        _options = options;
        _onFrameReceived = onFrameReceived;
        _onStatusChanged = onStatusChanged;
    }

    public void Start()
    {
        // 上一轮 StopAsync 已把旧 Channel 永久 Complete，这里必须换一个全新实例才能再次写入
        _sendQueue = CreateSendQueue();
        _stopping = false;
        _lifecycleCts = new CancellationTokenSource();
        _guardianTask = Task.Run(() => GuardianLoopAsync(_lifecycleCts.Token));
    }

    public async Task StopAsync()
    {
        _stopping = true;
        _sendQueue.Writer.TryComplete();
        _lifecycleCts?.Cancel();

        if (_guardianTask != null) await SafeAwaitAsync(_guardianTask).ConfigureAwait(false);
        try { _listener?.Stop(); } catch { /* 已停止或未启动，忽略 */ }
        _lifecycleCts?.Dispose();
    }

    public ValueTask EnqueueBeginAsync(Guid transferId, long totalLength, FrameCodec.BeginFlags flags,
        ulong finalHash, string metadataJson, CancellationToken token)
    {
        return _sendQueue.Writer.WriteAsync(
            (stream, ct) => FrameCodec.WriteBeginFrameAsync(stream, transferId, totalLength, flags, finalHash, metadataJson, ct),
            token);
    }

    public ValueTask EnqueueChunkAsync(Guid transferId, long offset, ReadOnlyMemory<byte> payload, bool withCrc, CancellationToken token)
    {
        return _sendQueue.Writer.WriteAsync(async (stream, ct) =>
        {
            var crc = withCrc ? FrameCodec.ComputeCrc32(payload.Span) : 0u;
            await FrameCodec.WriteChunkFrameAsync(stream, transferId, offset, payload, crc, ct).ConfigureAwait(false);
            Interlocked.Add(ref _bytesSent, payload.Length);
        }, token);
    }

    /// <summary>
    /// 入队一个持有租借缓冲所有权的分片发送任务（文件发送路径使用）：缓冲来自 ArrayPool，
    /// 帧写入完成或任务执行失败后都在任务内部归还；断线改派（DrainPendingJobs → RequeueJobAsync）时
    /// 所有权随闭包一起转移到新 Lane，依然有效。
    /// </summary>
    public ValueTask EnqueueChunkOwnedAsync(Guid transferId, long offset, byte[] rentedBuffer, int length, bool withCrc, CancellationToken token)
    {
        return _sendQueue.Writer.WriteAsync(async (stream, ct) =>
        {
            try
            {
                var payload = rentedBuffer.AsMemory(0, length);
                var crc = withCrc ? FrameCodec.ComputeCrc32(payload.Span) : 0u;
                await FrameCodec.WriteChunkFrameAsync(stream, transferId, offset, payload, crc, ct).ConfigureAwait(false);
                Interlocked.Add(ref _bytesSent, length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedBuffer);
            }
        }, token);
    }

    /// <summary>接收端成功把某个分片写入目标缓冲区后，由 FileTransferChannel 回调上报字节数，供 LaneStatus 展示</summary>
    public void ReportBytesReceived(int count) => Interlocked.Add(ref _bytesReceived, count);

    /// <summary>接收端校验通过后，通过本 Lane 回复 TransferAck 给发送端</summary>
    public ValueTask EnqueueAckAsync(Guid transferId, CancellationToken token)
        => _sendQueue.Writer.WriteAsync((stream, ct) => FrameCodec.WriteTransferAckFrameAsync(stream, transferId, ct), token);

    /// <summary>接收端校验失败后，通过本 Lane 回复 TransferNack 给发送端</summary>
    public ValueTask EnqueueNackAsync(Guid transferId, byte failureReasonCode, CancellationToken token)
        => _sendQueue.Writer.WriteAsync((stream, ct) => FrameCodec.WriteTransferNackFrameAsync(stream, transferId, failureReasonCode, ct), token);

    /// <summary>清空尚未发出的排队任务，供 RerouteToSurvivingLanes 策略把它们改派给其他 Lane。
    /// 已经交给 Socket 发送、尚未确认的数据无法追回，这属于既定的简化取舍（详见设计文档）。</summary>
    public List<Func<Stream, CancellationToken, Task>> DrainPendingJobs()
    {
        var drained = new List<Func<Stream, CancellationToken, Task>>();
        while (_sendQueue.Reader.TryRead(out var job)) drained.Add(job);
        return drained;
    }

    public async ValueTask RequeueJobAsync(Func<Stream, CancellationToken, Task> job, CancellationToken token)
        => await _sendQueue.Writer.WriteAsync(job, token).ConfigureAwait(false);

    public LaneStatus GetStatus() => new()
    {
        LaneId = LaneId,
        IsConnected = _isConnected,
        RemoteEndPoint = _remoteEndPoint,
        ConnectedTime = _connectedTime,
        ReconnectAttempts = _reconnectAttempts,
        BytesSent = Interlocked.Read(ref _bytesSent),
        BytesReceived = Interlocked.Read(ref _bytesReceived),
        CurrentThroughputMBps = 0, // 简化：不做滑动窗口速率统计，如需可后续增强
        LastHeartbeatTime = _lastActivityUtc
    };

    // ────────────────────────────── 连接守护 ──────────────────────────────

    private async Task GuardianLoopAsync(CancellationToken token)
    {
        if (_role == FileTransferRole.Server)
        {
            _listener = new TcpListener(IPAddress.Parse(Endpoint.LocalIp), Endpoint.Port);
            _listener.Start();
        }

        while (!token.IsCancellationRequested)
        {
            TcpClient? client = null;
            try
            {
                if (_role == FileTransferRole.Server)
                {
                    client = await _listener!.AcceptTcpClientAsync(token).ConfigureAwait(false);
                }
                else
                {
                    client = new TcpClient();
                    client.Client.Bind(new IPEndPoint(IPAddress.Parse(Endpoint.LocalIp), 0));
                    using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                    connectCts.CancelAfter(_options.ConnectTimeoutMs);
                    await client.ConnectAsync(Endpoint.RemoteIp!, Endpoint.Port, connectCts.Token).ConfigureAwait(false);
                }

                ConfigureSocket(client);
                await RunConnectionAsync(client, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                client?.Dispose();
                break;
            }
            catch
            {
                // 连接/握手异常：不放弃，计入重连次数，等待间隔后重试
                client?.Dispose();
            }

            if (token.IsCancellationRequested) break;

            Interlocked.Increment(ref _reconnectAttempts);
            PublishStatus(isReconnect: false);

            try { await Task.Delay(_options.ReconnectIntervalMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }

    private void ConfigureSocket(TcpClient client)
    {
        client.NoDelay = _options.NoDelay;
        client.SendBufferSize = _options.SocketBufferSizeBytes;
        client.ReceiveBufferSize = _options.SocketBufferSizeBytes;
        client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        try
        {
            client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, _options.TcpKeepAliveTimeSeconds);
            client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, _options.TcpKeepAliveIntervalSeconds);
            client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, 3);
        }
        catch
        {
            // 部分平台/驱动不支持细粒度 Keepalive 参数，退化为系统默认，应用层心跳仍可兜底检测断线
        }
    }

    private async Task RunConnectionAsync(TcpClient client, CancellationToken lifecycleToken)
    {
        using var stream = client.GetStream();
        _remoteEndPoint = client.Client.RemoteEndPoint?.ToString();
        _isConnected = true;
        _connectedTime = DateTime.Now;
        _lastActivityUtc = DateTime.UtcNow;
        Interlocked.Exchange(ref _reconnectAttempts, 0);
        PublishStatus(isReconnect: true);

        using var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(lifecycleToken);

        var sendLoop = SendLoopAsync(stream, connectionCts.Token);
        var receiveLoop = ReceiveLoopAsync(stream, connectionCts.Token);
        var watchdog = HeartbeatWatchdogAsync(connectionCts.Token);

        try
        {
            await Task.WhenAny(sendLoop, receiveLoop, watchdog).ConfigureAwait(false);
        }
        finally
        {
            connectionCts.Cancel();
            try { client.Close(); } catch { /* 忽略关闭异常 */ }
            _isConnected = false;
            _connectedTime = null;
            if (!_stopping) PublishStatus(isReconnect: false);

            await SafeAwaitAsync(sendLoop).ConfigureAwait(false);
            await SafeAwaitAsync(receiveLoop).ConfigureAwait(false);
            await SafeAwaitAsync(watchdog).ConfigureAwait(false);
        }
    }

    // ────────────────────────────── 发送循环（唯一写入者） ──────────────────────────────

    private async Task SendLoopAsync(Stream stream, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            Func<Stream, CancellationToken, Task>? job = null;

            using (var waitCts = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                waitCts.CancelAfter(_options.HeartbeatIntervalMs);
                try
                {
                    job = await _sendQueue.Reader.ReadAsync(waitCts.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!token.IsCancellationRequested)
                {
                    // 队列空闲超时，落到下面发心跳分支，不是真正取消
                }
                catch (ChannelClosedException)
                {
                    break;
                }
            }

            if (job != null)
                await job(stream, token).ConfigureAwait(false);
            else
                await FrameCodec.WriteHeartbeatFrameAsync(stream, isAck: false, token).ConfigureAwait(false);
        }
    }

    // ────────────────────────────── 接收循环 ──────────────────────────────

    private async Task ReceiveLoopAsync(Stream stream, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            FrameCodec.CommonHeader? header;
            try
            {
                header = await FrameCodec.ReadCommonHeaderAsync(stream, token).ConfigureAwait(false);
            }
            catch (IOException) { break; }
            catch (SocketException) { break; }
            catch (OperationCanceledException) { break; }

            if (header is null) break; // 对端优雅关闭

            _lastActivityUtc = DateTime.UtcNow;

            switch (header.Value.Type)
            {
                case FrameCodec.FrameType.Heartbeat:
                    // 心跳回复也必须经由发送队列，不能在接收循环里直接写 Stream——
                    // 否则会和 SendLoopAsync 并发写同一个连接，导致帧交织损坏。
                    await _sendQueue.Writer.WriteAsync(
                        (s, ct) => FrameCodec.WriteHeartbeatFrameAsync(s, isAck: true, ct), token).ConfigureAwait(false);
                    break;

                case FrameCodec.FrameType.HeartbeatAck:
                    break; // _lastActivityUtc 已在上面更新，无需额外处理

                default:
                    await _onFrameReceived(LaneId, header.Value, stream, token).ConfigureAwait(false);
                    break;
            }
        }
    }

    private async Task HeartbeatWatchdogAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(500, token).ConfigureAwait(false);
            var idleMs = (DateTime.UtcNow - _lastActivityUtc).TotalMilliseconds;
            if (idleMs > _options.HeartbeatTimeoutMs)
                throw new IOException($"Lane {LaneId} 心跳超时（{idleMs:F0}ms 无任何数据/心跳往来），判定断线");
        }
    }

    private void PublishStatus(bool isReconnect) => _onStatusChanged(GetStatus(), isReconnect);

    private static async Task SafeAwaitAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch { /* 连接生命周期结束时的收尾异常均已通过状态回调处理，这里只需吞掉避免未观察异常 */ }
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);
}
