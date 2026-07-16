using Microsoft.Win32.SafeHandles;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Entities.Communication.FileTransfer;
using PF.Core.Enums;
using PF.Core.Enums.FileTransfer;
using PF.Core.Events.FileTransfer;
using PF.Core.Interfaces.Communication;
using PF.Core.Interfaces.Communication.FileTransfer;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Communication.FileTransfer.Internal;
using PF.Infrastructure.Logging;

namespace PF.Infrastructure.Communication.FileTransfer;

/// <summary>
/// <see cref="IFileTransferChannel"/> 实现。聚合多条 <see cref="TransferLane"/>，
/// 负责协议语义：分片调度、Begin/Chunk/Ack/Nack 帧的收发决策、在途接收登记、状态聚合。
/// 单条 Lane 的连接生命周期（重连/心跳/发送队列）由 TransferLane 自己负责，本类不涉及。
///
/// 内存策略：收发两侧的峰值内存都只与「Lane 数 × 分片大小」量级相关，与传输的数据大小无关——
///   · 发送：byte[]/Memory 版零拷贝切片引用调用方缓冲；文件版（SendFileAsync）按分片租借缓冲边读边发，
///     在途租借量受 TransferLane 发送队列深度反压约束；
///   · 接收：不超过 InMemoryReceiveThresholdBytes 的传输在内存重组，超过则直接落盘为临时文件，
///     不再为整个传输分配大数组。两种形态对消费方透明：完成事件提供 OpenReadStream /
///     SaveToFileAsync / GetBytesAsync 统一消费入口，无需分辨 Data/FilePath。
/// </summary>
[CommunicationUI(NavigationConstants.Views.FileTransferDebugView)]
public sealed class FileTransferChannel : IFileTransferChannel, ICommunication
{
    /// <inheritdoc/>
    public string ChannelName { get; }
    /// <inheritdoc/>
    public FileTransferRole Role => _options.Role;

    /// <inheritdoc cref="ICommunication.InstanceId"/>
    string ICommunication.InstanceId => ChannelName;
    /// <inheritdoc cref="ICommunication.Category"/>
    CommunicationCategory ICommunication.Category => CommunicationCategory.FileTransfer;
    /// <inheritdoc cref="ICommunication.Role"/>
    CommunicationRole ICommunication.Role => Role switch
    {
        FileTransferRole.Server => CommunicationRole.Server,
        FileTransferRole.Client => CommunicationRole.Client,
        _ => CommunicationRole.None
    };
    /// <inheritdoc cref="ICommunication.DisplayName"/>
    string ICommunication.DisplayName => $"{ChannelName} ({Role})";
    /// <inheritdoc/>
    public FileTransferStatus Status { get; private set; } = FileTransferStatus.Stopped;
    /// <inheritdoc/>
    public IReadOnlyList<FileTransferLinkEndpoint> Links => _options.Links;
    /// <inheritdoc/>
    public IReadOnlyList<LaneStatus> LaneStatuses => _lanes.Values.Select(l => l.GetStatus()).ToList();
    /// <inheritdoc/>
    public bool EnableChunkLevelDiagnostics { get; set; }

    /// <inheritdoc/>
    public event EventHandler<ChannelStateChangedEventArgs>? StateChanged;
    /// <inheritdoc/>
    public event EventHandler<LaneStatusChangedEventArgs>? LaneStatusChanged;
    /// <inheritdoc/>
    public event EventHandler<LaneStatusChangedEventArgs>? LaneReconnected;
    /// <inheritdoc/>
    public event EventHandler<FileTransferProgressEventArgs>? TransferProgress;
    /// <inheritdoc/>
    public event EventHandler<FileTransferCompletedEventArgs>? TransferCompleted;
    /// <inheritdoc/>
    public event EventHandler<FileTransferFailedEventArgs>? TransferFailed;
    /// <inheritdoc/>
    public event EventHandler<ChunkTransferredEventArgs>? ChunkTransferred;

    private readonly FileTransferOptions _options;
    private readonly CategoryLogger? _logger;
    private readonly Dictionary<int, TransferLane> _lanes = new();
    private readonly ConcurrentDictionary<Guid, InFlightTransfer> _inboundTransfers = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AckResult>> _pendingAcks = new();

    // 已拒收的入站传输（值为拒收时间，供清扫过期）：Begin 帧经全部 Lane 广播，同一传输会到达 N 次，
    // 记下已拒收的 TransferId 避免每条 Lane 都重复回 Nack、重复发 TransferFailed 事件
    private readonly ConcurrentDictionary<Guid, DateTime> _rejectedInbound = new();

    // Begin 登记的"检查-创建-登记"临界区：Begin 帧几乎同时从多条 Lane 到达，
    // 无锁时两条 Lane 都能通过 ContainsKey 检查而重复创建接收缓冲（落盘模式下第二个 Create
    // 因文件独占打开抛异常，触发虚假的 TransferFailed 事件）
    private readonly object _inboundRegistrationLock = new();

    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly object _statusLock = new();
    private bool _started;
    private bool _disposed;
    private CancellationTokenSource? _sweepCts;
    private Task? _sweepTask;

    // 单个分片 payload 的合理上限（远大于 MaxChunkSizeBytes 默认档位）：ChunkLength 由对端帧头声明，
    // 落盘模式要按它租借缓冲，畸形/恶意帧给出巨大值时必须在此拦下，不能只依赖 ValidateBounds
    private const int MaxChunkPayloadBytes = 128 * 1024 * 1024;

    // 孤儿在途接收的清扫检查周期（毫秒）
    private const int InboundSweepIntervalMs = 5000;

    private readonly record struct AckResult(bool Success, FileTransferFailureReason Reason);

    /// <summary>按配置创建通道实例。配置非法（Links 为空/LaneId 重复/Client 缺 RemoteIp）时同步抛出 <see cref="ArgumentException"/></summary>
    /// <param name="options">通道配置</param>
    /// <param name="channelName">通道名称</param>
    /// <param name="logger">日志服务，缺省时不记录日志（保持与既有调用点兼容）</param>
    public FileTransferChannel(FileTransferOptions options, string channelName, ILogService? logger = null)
    {
        ValidateOptions(options);
        _options = options;
        ChannelName = channelName;
        _logger = logger == null ? null : CategoryLoggerFactory.Communication(logger);

        foreach (var link in options.Links)
        {
            var lane = new TransferLane(link.LaneId, link, options.Role, options, OnFrameReceivedAsync, OnLaneStatusChangedInternal);
            _lanes.Add(link.LaneId, lane);
        }
    }

    private static void ValidateOptions(FileTransferOptions options)
    {
        if (options.Links.Count == 0)
            throw new ArgumentException("Links 不能为空，至少配置一条链路", nameof(options));

        var duplicateLaneIds = options.Links.GroupBy(l => l.LaneId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicateLaneIds.Count > 0)
            throw new ArgumentException($"LaneId 重复：{string.Join(",", duplicateLaneIds)}", nameof(options));

        if (options.Role == FileTransferRole.Client && options.Links.Any(l => string.IsNullOrWhiteSpace(l.RemoteIp)))
            throw new ArgumentException("Client 角色下每条 Link 必须指定 RemoteIp", nameof(options));

        // LocalIp 会在 Lane 里 IPAddress.Parse（Server 监听 / Client 绑定出口网口），配置错误必须在构造时暴露，
        // 不能等到运行期在守护循环里反复解析失败（RemoteIp 不做同样校验：ConnectAsync 支持主机名）
        var invalidLocalIps = options.Links
            .Where(l => !System.Net.IPAddress.TryParse(l.LocalIp, out _))
            .Select(l => $"LaneId {l.LaneId}: '{l.LocalIp}'").ToList();
        if (invalidLocalIps.Count > 0)
            throw new ArgumentException($"以下 Link 的 LocalIp 无法解析为 IP 地址：{string.Join("；", invalidLocalIps)}", nameof(options));
    }

    // ────────────────────────────── 生命周期 ──────────────────────────────

    /// <inheritdoc/>
    public Task<bool> StartAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        if (_started) return Task.FromResult(true);

        _started = true;
        CleanupLeftoverReceiveFiles();

        _sweepCts = new CancellationTokenSource();
        _sweepTask = Task.Run(() => SweepOrphanInboundLoopAsync(_sweepCts.Token));

        foreach (var lane in _lanes.Values) lane.Start();
        RecomputeAggregateStatus();

        _logger?.Info($"[FileTransfer:{ChannelName}] 通道已启动（{Role}，{_lanes.Count} 条 Lane）");
        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (!_started) return;
        _started = false;

        _sweepCts?.Cancel();

        foreach (var lane in _lanes.Values) await lane.StopAsync().ConfigureAwait(false);

        // Lane 已全部停止，不会再有新分片到达，中止所有在途接收（关句柄、删未完成的临时文件）
        AbortAllInboundTransfers(FileTransferFailureReason.Cancelled, "通道停止");
        _rejectedInbound.Clear();

        if (_sweepTask != null)
        {
            try { await _sweepTask.ConfigureAwait(false); } catch { /* 清扫任务收尾异常无需上抛 */ }
            _sweepTask = null;
        }
        _sweepCts?.Dispose();
        _sweepCts = null;

        SetStatus(FileTransferStatus.Stopped);
        _logger?.Info($"[FileTransfer:{ChannelName}] 通道已停止");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(FileTransferChannel));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // StopAsync().Wait() 在有 SynchronizationContext（如 UI 线程）时会死锁，
        // 强制在线程池线程上执行，脱离当前上下文（与 TcpServer.Dispose 的既有做法保持一致）。
        Task.Run(() => StopAsync()).GetAwaiter().GetResult();
        _sendGate.Dispose();
    }

    /// <summary>通道启动时清理上次运行残留的临时文件（未完成/未被消费方删除的落盘文件对实时流水线已无意义）</summary>
    private void CleanupLeftoverReceiveFiles()
    {
        try
        {
            var dir = GetReceiveDirectory();
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.EnumerateFiles(dir, "*.pfft"))
            {
                try { File.Delete(file); } catch { /* 单个文件被占用则跳过 */ }
            }
        }
        catch { /* 目录不可访问不阻塞启动，真正需要落盘时 Create 会再次尝试并暴露问题 */ }
    }

    /// <summary>本通道的接收落盘子目录（根目录来自配置，子目录按通道名隔离，通道名中的非法路径字符替换为下划线）</summary>
    private string GetReceiveDirectory()
    {
        var safeName = string.Join("_", ChannelName.Split(Path.GetInvalidFileNameChars()));
        return Path.Combine(_options.ReceiveDirectory, safeName);
    }

    // ────────────────────────────── 发送 ──────────────────────────────

    /// <inheritdoc/>
    public Task<FileTransferResult> SendAsync(byte[] data, FileTransferMetadata metadata, CancellationToken token = default)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));
        return SendAsync(data.AsMemory(), metadata, token);
    }

    /// <inheritdoc/>
    public Task<FileTransferResult> SendAsync(ReadOnlyMemory<byte> data, FileTransferMetadata metadata, CancellationToken token = default)
    {
        ThrowIfDisposed();
        if (data.Length == 0) throw new ArgumentException("data 不能为空", nameof(data));
        if (data.Length > _options.MaxTransferSizeBytes)
            throw new ArgumentException(
                $"数据长度 {data.Length} 超过 MaxTransferSizeBytes 上限 {_options.MaxTransferSizeBytes}", nameof(data));

        return SendCoreAsync(data, fileHandle: null, data.Length, metadata, token);
    }

    /// <inheritdoc/>
    public async Task<FileTransferResult> SendFileAsync(string filePath, FileTransferMetadata metadata, CancellationToken token = default)
    {
        ThrowIfDisposed();
        var totalLength = new FileInfo(filePath).Length; // 文件不存在时此处抛 FileNotFoundException
        if (totalLength == 0) throw new ArgumentException($"文件为空：{filePath}", nameof(filePath));
        if (totalLength > _options.MaxTransferSizeBytes)
            throw new ArgumentException(
                $"文件长度 {totalLength} 超过 MaxTransferSizeBytes 上限 {_options.MaxTransferSizeBytes}", nameof(filePath));

        using var handle = File.OpenHandle(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await SendCoreAsync(ReadOnlyMemory<byte>.Empty, handle, totalLength, metadata, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 发送主流程（内存/文件两种来源共用）：fileHandle 为 null 时从 memoryData 零拷贝切片发送，
    /// 否则从文件句柄按分片边读边发。
    /// </summary>
    private async Task<FileTransferResult> SendCoreAsync(ReadOnlyMemory<byte> memoryData, SafeFileHandle? fileHandle,
        long totalLength, FileTransferMetadata metadata, CancellationToken token)
    {
        if (!await _sendGate.WaitAsync(0, token).ConfigureAwait(false))
        {
            return new FileTransferResult
            {
                Success = false,
                TransferId = metadata.TransferId,
                FailureReason = FileTransferFailureReason.Busy,
                ErrorMessage = "上一次传输尚未完成"
            };
        }

        var stopwatch = Stopwatch.StartNew();
        var tcs = new TaskCompletionSource<AckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingAcks[metadata.TransferId] = tcs;

        try
        {
            var connectedLanes = _lanes.Values.Where(l => l.IsConnected).ToList();
            if (connectedLanes.Count == 0)
                return BuildFailureResult(metadata.TransferId, stopwatch.Elapsed, FileTransferFailureReason.ConnectionLost, "没有任何 Lane 处于连接状态");

            var chunkSize = _options.ChunkSizeBytes ?? ChunkSizeCalculator.Calculate(
                totalLength, connectedLanes.Count, _options.TargetChunksPerLane, _options.MinChunkSizeBytes, _options.MaxChunkSizeBytes);

            var flags = FrameCodec.BeginFlags.None;
            if (_options.VerifyChunkCrc) flags |= FrameCodec.BeginFlags.HasChunkCrc;
            var finalHash = 0UL;
            if (_options.VerifyFinalHash)
            {
                flags |= FrameCodec.BeginFlags.HasFinalHash;
                // 协议要求 Begin 帧先于所有 Chunk 帧携带整体哈希，因此文件来源要先顺序读一遍算哈希、
                // 发送时再读第二遍——用多一次顺序读换取内存占用与文件大小脱钩
                if (fileHandle is null)
                    finalHash = FrameCodec.ComputeXxHash64(memoryData.Span);
                else
                    finalHash = await FrameCodec.ComputeXxHash64Async(fileHandle, totalLength, token).ConfigureAwait(false);
            }

            var metadataJson = JsonSerializer.Serialize(metadata);

            // Begin 帧广播到每条 Lane，且必须在该 Lane 的任何 Chunk 帧之前发出——
            // 每条 Lane 是独立 TCP 连接、独立 FIFO 发送队列，天然保证顺序，不会出现 Chunk 先于 Begin 到达。
            foreach (var lane in connectedLanes)
                await lane.EnqueueBeginAsync(metadata.TransferId, totalLength, flags, finalHash, metadataJson, token).ConfigureAwait(false);

            if (fileHandle is null)
                await DistributeChunksAsync(memoryData, metadata.TransferId, chunkSize, connectedLanes, _options.VerifyChunkCrc, token).ConfigureAwait(false);
            else
                await DistributeChunksFromFileAsync(fileHandle, totalLength, metadata.TransferId, chunkSize, connectedLanes, _options.VerifyChunkCrc, token).ConfigureAwait(false);

            var ackResult = await WaitForAckAsync(tcs, totalLength, token).ConfigureAwait(false);
            stopwatch.Stop();

            if (ackResult is null)
            {
                var reason = token.IsCancellationRequested ? FileTransferFailureReason.Cancelled : FileTransferFailureReason.TransferTimeout;
                return BuildFailureResult(metadata.TransferId, stopwatch.Elapsed, reason,
                    reason == FileTransferFailureReason.Cancelled ? "调用方取消" : "等待对端确认超时");
            }

            if (!ackResult.Value.Success)
                return BuildFailureResult(metadata.TransferId, stopwatch.Elapsed, ackResult.Value.Reason,
                    $"对端拒收或校验未通过：{ackResult.Value.Reason}");

            var bytesPerLane = connectedLanes.ToDictionary(l => l.LaneId, l => l.GetStatus().BytesSent);
            var result = new FileTransferResult
            {
                Success = true,
                TransferId = metadata.TransferId,
                Elapsed = stopwatch.Elapsed,
                ThroughputMBps = stopwatch.Elapsed.TotalSeconds > 0 ? totalLength / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds : 0,
                BytesPerLane = bytesPerLane,
                FailureReason = FileTransferFailureReason.None
            };

            RaiseTransferCompleted(metadata, TransferDirection.Sent, data: null, filePath: null, result);
            return result;
        }
        finally
        {
            _pendingAcks.TryRemove(metadata.TransferId, out _);
            _sendGate.Release();
        }
    }

    /// <summary>
    /// 等待对端 Ack。等待时长在 TransferTimeoutMs 基础上按数据量线性追加——
    /// 分发循环结束只代表分片全部入队，Ack 前对端还要完成最后一批在途分片的落地和
    /// 整体哈希回读（两者耗时都与数据量成正比，落盘模式回读 4GB 本身就可能超过 10s 的默认超时），
    /// 固定超时对大传输必然误判失败。追加额度按保守吞吐 100MB/s 估算。
    /// </summary>
    private async Task<AckResult?> WaitForAckAsync(TaskCompletionSource<AckResult> tcs, long totalLength, CancellationToken token)
    {
        var extraMs = totalLength / (100L * 1024 * 1024) * 1000;
        var timeoutMs = (int)Math.Min(int.MaxValue, _options.TransferTimeoutMs + extraMs);
        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(token, timeoutCts.Token);
        await using var registration = linkedCts.Token.Register(() => tcs.TrySetCanceled());

        try
        {
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
    }

    private async Task DistributeChunksAsync(ReadOnlyMemory<byte> data, Guid transferId, int chunkSize, List<TransferLane> lanes, bool withCrc, CancellationToken token)
    {
        var totalLength = (long)data.Length;
        var laneIndex = 0;
        long offset = 0;

        while (offset < totalLength)
        {
            var length = (int)Math.Min(chunkSize, totalLength - offset);
            var chunk = data.Slice((int)offset, length);

            var lane = PickLane(lanes, laneIndex);
            await lane.EnqueueChunkAsync(transferId, offset, chunk, withCrc, token).ConfigureAwait(false);

            ReportChunkSent(transferId, lane.LaneId, offset, length);

            offset += length;
            laneIndex++;

            RaiseTransferProgress(transferId, offset, totalLength);
        }
    }

    /// <summary>
    /// 从文件按分片边读边发：每个分片租借一块缓冲，所有权随发送任务转移、帧写完后在任务内归还。
    /// 在途租借量受 TransferLane 发送队列深度反压约束（队列满时本循环阻塞在入队处），
    /// 峰值 ≈ Lane 数 × 队列深度 × 分片大小，与文件大小无关。
    /// </summary>
    private async Task DistributeChunksFromFileAsync(SafeFileHandle fileHandle, long totalLength, Guid transferId,
        int chunkSize, List<TransferLane> lanes, bool withCrc, CancellationToken token)
    {
        var laneIndex = 0;
        long offset = 0;

        while (offset < totalLength)
        {
            var length = (int)Math.Min(chunkSize, totalLength - offset);

            var rented = ArrayPool<byte>.Shared.Rent(length);
            var ownershipTransferred = false;
            try
            {
                var payload = rented.AsMemory(0, length);
                var read = 0;
                while (read < length)
                {
                    var n = await RandomAccess.ReadAsync(fileHandle, payload[read..], offset + read, token).ConfigureAwait(false);
                    if (n == 0) throw new EndOfStreamException($"文件在发送过程中被截断（预期 {totalLength} 字节，偏移 {offset + read} 处读到末尾）");
                    read += n;
                }

                var lane = PickLane(lanes, laneIndex);
                await lane.EnqueueChunkOwnedAsync(transferId, offset, rented, length, withCrc, token).ConfigureAwait(false);
                ownershipTransferred = true;

                ReportChunkSent(transferId, lane.LaneId, offset, length);
            }
            finally
            {
                // 入队成功后缓冲所有权归发送任务（帧写完后在任务内归还）；入队前失败则在此归还
                if (!ownershipTransferred) ArrayPool<byte>.Shared.Return(rented);
            }

            offset += length;
            laneIndex++;

            RaiseTransferProgress(transferId, offset, totalLength);
        }
    }

    private static TransferLane PickLane(List<TransferLane> lanes, int laneIndex)
    {
        var lane = lanes[laneIndex % lanes.Count];
        if (lane.IsConnected) return lane;
        return lanes.FirstOrDefault(l => l.IsConnected)
               ?? throw new IOException("分发分片时全部 Lane 均已断开");
    }

    private void ReportChunkSent(Guid transferId, int laneId, long offset, int length)
    {
        if (!EnableChunkLevelDiagnostics) return;
        RaiseChunkTransferred(transferId, laneId, offset, length, TransferDirection.Sent);
    }

    private FileTransferResult BuildFailureResult(Guid transferId, TimeSpan elapsed, FileTransferFailureReason reason, string message)
    {
        var result = new FileTransferResult
        {
            Success = false,
            TransferId = transferId,
            Elapsed = elapsed,
            FailureReason = reason,
            ErrorMessage = message
        };
        RaiseTransferFailed(transferId, reason, message);
        return result;
    }

    // ────────────────────────────── 接收 / 帧分发 ──────────────────────────────

    private async Task OnFrameReceivedAsync(int laneId, FrameCodec.CommonHeader header, Stream stream, CancellationToken token)
    {
        switch (header.Type)
        {
            case FrameCodec.FrameType.Begin:
                await HandleBeginFrameAsync(laneId, header.TransferId, stream, token).ConfigureAwait(false);
                break;

            case FrameCodec.FrameType.Chunk:
                await HandleChunkFrameAsync(laneId, header.TransferId, stream, token).ConfigureAwait(false);
                break;

            case FrameCodec.FrameType.TransferAck:
                CompletePendingAck(header.TransferId, success: true, FileTransferFailureReason.None);
                break;

            case FrameCodec.FrameType.TransferNack:
                var reasonCode = await FrameCodec.ReadTransferNackFieldsAsync(stream, token).ConfigureAwait(false);
                CompletePendingAck(header.TransferId, success: false, (FileTransferFailureReason)reasonCode);
                break;
        }
    }

    private void CompletePendingAck(Guid transferId, bool success, FileTransferFailureReason reason)
    {
        if (_pendingAcks.TryGetValue(transferId, out var tcs))
            tcs.TrySetResult(new AckResult(success, reason));
    }

    private async Task HandleBeginFrameAsync(int laneId, Guid transferId, Stream stream, CancellationToken token)
    {
        var fields = await FrameCodec.ReadBeginFieldsAsync(stream, token).ConfigureAwait(false);

        if (_rejectedInbound.ContainsKey(transferId)) return; // 已由其他 Lane 的 Begin 拒收并回过 Nack，幂等跳过

        if (fields.TotalLength <= 0 || fields.TotalLength > _options.MaxTransferSizeBytes)
        {
            // 拒收必须回 Nack：静默丢弃会让发送端傻等完整个超时周期，且拿到的原因还是误导性的"超时"
            await RejectInboundAsync(laneId, transferId, FileTransferFailureReason.MalformedFrame,
                $"Begin 帧声明的长度非法或超限：{fields.TotalLength}", token).ConfigureAwait(false);
            return;
        }

        FileTransferMetadata metadata;
        try
        {
            metadata = JsonSerializer.Deserialize<FileTransferMetadata>(fields.MetadataJson)
                       ?? new FileTransferMetadata { TransferId = transferId };
        }
        catch
        {
            metadata = new FileTransferMetadata { TransferId = transferId };
        }

        var rejectReason = FileTransferFailureReason.None;
        string? rejectMessage = null;

        lock (_inboundRegistrationLock)
        {
            if (_inboundTransfers.ContainsKey(transferId)) return; // 已由其他 Lane 广播的 Begin 帧登记过，幂等跳过

            if (_inboundTransfers.Count >= _options.MaxConcurrentInboundTransfers)
            {
                rejectReason = FileTransferFailureReason.Busy;
                rejectMessage = $"在途接收数量已达上限 {_options.MaxConcurrentInboundTransfers}";
            }
            else
            {
                try
                {
                    var transfer = InFlightTransfer.Create(transferId, metadata, fields.TotalLength, fields.Flags, fields.FinalHash,
                        _options.InMemoryReceiveThresholdBytes, GetReceiveDirectory());
                    _inboundTransfers.TryAdd(transferId, transfer);
                }
                catch (Exception ex)
                {
                    // 接收缓冲创建失败（落盘预分配时磁盘空间不足/目录不可写等）：不登记，
                    // 该传输的后续分片会被排空丢弃
                    rejectReason = FileTransferFailureReason.Unknown;
                    rejectMessage = $"接收缓冲创建失败：{ex.Message}";
                }
            }
        }

        if (rejectReason == FileTransferFailureReason.None) return;

        await RejectInboundAsync(laneId, transferId, rejectReason, rejectMessage!, token).ConfigureAwait(false);
    }

    /// <summary>
    /// 拒收入站传输：登记拒收记录（供其他 Lane 的同一 Begin 幂等跳过）、发 TransferFailed 事件、
    /// 通过收到 Begin 的 Lane 回 Nack 让发送端立刻失败而非等超时。事件与 Nack 均保证只发一次。
    /// </summary>
    private async Task RejectInboundAsync(int laneId, Guid transferId, FileTransferFailureReason reason, string message, CancellationToken token)
    {
        if (!_rejectedInbound.TryAdd(transferId, DateTime.UtcNow)) return; // 其他 Lane 已在处理拒收

        RaiseTransferFailed(transferId, reason, message);

        if (!_lanes.TryGetValue(laneId, out var lane)) return;
        try
        {
            await lane.EnqueueNackAsync(transferId, (byte)reason, token).ConfigureAwait(false);
        }
        catch
        {
            // Lane 恰好停止/断开时无法回 Nack，发送端退化为按超时失败
        }
    }

    private async Task HandleChunkFrameAsync(int laneId, Guid transferId, Stream stream, CancellationToken token)
    {
        var chunkFields = await FrameCodec.ReadChunkFieldsAsync(stream, token).ConfigureAwait(false);

        // ChunkLength/Offset 是对端帧头声明的裸数据，负值或巨大值意味着字节流已错乱（或恶意帧）——
        // 此时按它去排空/租借缓冲只会放大破坏（如按 2GB 排空空转到心跳超时），直接判协议损坏，
        // 异常沿接收循环传播使本条 Lane 断开重连，字节流从帧边界重新对齐
        if (chunkFields.ChunkLength < 0 || chunkFields.ChunkLength > MaxChunkPayloadBytes || chunkFields.ChunkOffset < 0)
            throw new InvalidDataException(
                $"Chunk 帧字段异常（offset={chunkFields.ChunkOffset}, length={chunkFields.ChunkLength}），协议错乱，断开重连");

        if (!_inboundTransfers.TryGetValue(transferId, out var transfer))
        {
            // 未知 TransferId（Begin 未登记成功，或已被中止清理）：仍需把 Payload 读掉排空字节流，
            // 否则后续帧解析会全部错位。
            await DiscardPayloadAsync(stream, chunkFields.ChunkLength, token).ConfigureAwait(false);
            return;
        }

        if (transfer.IsAborted)
        {
            await DiscardPayloadAsync(stream, chunkFields.ChunkLength, token).ConfigureAwait(false);
            return;
        }

        if (!transfer.ValidateBounds(chunkFields.ChunkOffset, chunkFields.ChunkLength))
        {
            await DiscardPayloadAsync(stream, chunkFields.ChunkLength, token).ConfigureAwait(false);
            AbortTransfer(transfer, laneId, FileTransferFailureReason.MalformedFrame, "分片偏移越界或长度异常");
            return;
        }

        if (transfer.IsFileMode)
        {
            // 落盘模式：经租借缓冲中转后按偏移写入临时文件，缓冲随即归还，内存占用与数据大小无关
            var rented = ArrayPool<byte>.Shared.Rent(chunkFields.ChunkLength);
            try
            {
                var payload = rented.AsMemory(0, chunkFields.ChunkLength);
                if (!await FrameCodec.ReadExactAsync(stream, payload, token).ConfigureAwait(false))
                {
                    AbortTransfer(transfer, laneId, FileTransferFailureReason.ConnectionLost, "读取分片 payload 时连接中断");
                    return;
                }

                if (transfer.Flags.HasFlag(FrameCodec.BeginFlags.HasChunkCrc) &&
                    FrameCodec.ComputeCrc32(payload.Span) != chunkFields.ChunkCrc32)
                {
                    AbortTransfer(transfer, laneId, FileTransferFailureReason.ChecksumMismatch, "分片 CRC 校验不通过");
                    return;
                }

                if (!await transfer.TryWriteToFileAsync(chunkFields.ChunkOffset, payload, token).ConfigureAwait(false))
                    return; // 与清扫/停止的 Abort 竞态：该传输已中止，静默丢弃
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
        else
        {
            // 内存模式：直接读入重组缓冲对应偏移，零额外拷贝
            var destination = transfer.GetWriteSlice(chunkFields.ChunkOffset, chunkFields.ChunkLength);
            if (!await FrameCodec.ReadExactAsync(stream, destination, token).ConfigureAwait(false))
            {
                AbortTransfer(transfer, laneId, FileTransferFailureReason.ConnectionLost, "读取分片 payload 时连接中断");
                return;
            }

            if (transfer.Flags.HasFlag(FrameCodec.BeginFlags.HasChunkCrc) &&
                FrameCodec.ComputeCrc32(destination.Span) != chunkFields.ChunkCrc32)
            {
                AbortTransfer(transfer, laneId, FileTransferFailureReason.ChecksumMismatch, "分片 CRC 校验不通过");
                return;
            }
        }

        transfer.ReportBytesWritten(chunkFields.ChunkOffset, chunkFields.ChunkLength);
        if (_lanes.TryGetValue(laneId, out var lane)) lane.ReportBytesReceived(chunkFields.ChunkLength);

        if (EnableChunkLevelDiagnostics)
            RaiseChunkTransferred(transferId, laneId, chunkFields.ChunkOffset, chunkFields.ChunkLength, TransferDirection.Received);

        RaiseTransferProgress(transferId, transfer.BytesReceived, transfer.TotalLength);

        // TryRemove 兼做完成判定的并发去重：多条 Lane 同时写完最后几个分片时，只有移除成功的那条执行收尾。
        // 收尾（落盘模式的整体哈希回读可能耗时数秒）放到线程池执行，避免阻塞本 Lane 的接收循环——
        // 阻塞期间读不到对端心跳，会被本端心跳看门狗误判断线。
        if (transfer.IsComplete && _inboundTransfers.TryRemove(transfer.TransferId, out _))
            _ = Task.Run(() => FinalizeInboundTransferAsync(transfer, laneId, CancellationToken.None), CancellationToken.None);
    }

    /// <summary>接收收尾：整体哈希校验（落盘模式流式回读）→ 关闭句柄 → 完成事件 → 回 Ack。调用前必须已从在途字典移除</summary>
    private async Task FinalizeInboundTransferAsync(InFlightTransfer transfer, int laneId, CancellationToken token)
    {
        try
        {
            if (transfer.Flags.HasFlag(FrameCodec.BeginFlags.HasFinalHash))
            {
                ulong actualHash;
                if (transfer.IsFileMode)
                    actualHash = await transfer.ComputeFileHashAsync(token).ConfigureAwait(false);
                else
                    actualHash = FrameCodec.ComputeXxHash64(transfer.Buffer!);

                if (actualHash != transfer.ExpectedFinalHash)
                {
                    transfer.Abort(); // 落盘模式下同时删除已写好的坏文件
                    const FileTransferFailureReason reason = FileTransferFailureReason.ChecksumMismatch;
                    RaiseTransferFailed(transfer.TransferId, reason, "整体哈希校验不通过");
                    if (_lanes.TryGetValue(laneId, out var nackLane))
                        await nackLane.EnqueueNackAsync(transfer.TransferId, (byte)reason, token).ConfigureAwait(false);
                    return;
                }
            }

            // 关闭文件句柄后消费方才能打开临时文件；内存模式为空操作
            transfer.CloseFile();

            var elapsed = DateTime.Now - transfer.StartedAt;
            var result = new FileTransferResult
            {
                Success = true,
                TransferId = transfer.TransferId,
                Elapsed = elapsed,
                ThroughputMBps = elapsed.TotalSeconds > 0 ? transfer.TotalLength / 1024.0 / 1024.0 / elapsed.TotalSeconds : 0,
                FailureReason = FileTransferFailureReason.None
            };

            RaiseTransferCompleted(transfer.Metadata, TransferDirection.Received, transfer.Buffer, transfer.FilePath, result);

            if (_lanes.TryGetValue(laneId, out var ackLane))
                await ackLane.EnqueueAckAsync(transfer.TransferId, token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // 收尾阶段的意外异常（如哈希回读时磁盘错误）：清理并通知失败，不让异常淹没在线程池里
            transfer.Abort();
            RaiseTransferFailed(transfer.TransferId, FileTransferFailureReason.Unknown, $"接收收尾失败：{ex.Message}");
        }
    }

    private void AbortTransfer(InFlightTransfer transfer, int laneId, FileTransferFailureReason reason, string message)
    {
        transfer.Abort();
        _inboundTransfers.TryRemove(transfer.TransferId, out _);
        RaiseTransferFailed(transfer.TransferId, reason, message);

        if (_lanes.TryGetValue(laneId, out var lane))
            _ = lane.EnqueueNackAsync(transfer.TransferId, (byte)reason, CancellationToken.None);
    }

    /// <summary>中止全部在途接收（通道停止/全部 Lane 断开时调用），释放重组缓冲/文件句柄并删除未完成的临时文件</summary>
    private void AbortAllInboundTransfers(FileTransferFailureReason reason, string message)
    {
        foreach (var (id, transfer) in _inboundTransfers)
        {
            if (!_inboundTransfers.TryRemove(id, out _)) continue;
            transfer.Abort();
            RaiseTransferFailed(id, reason, message);
        }
    }

    /// <summary>
    /// 孤儿在途接收清扫：发送端中途死亡/断线后，接收端已登记的在途传输不会再有分片到达，
    /// 若不清理会一直占着重组缓冲/文件句柄，且累计到 MaxConcurrentInboundTransfers 后所有新传输都会被拒收。
    /// 超过 TransferTimeoutMs 无新分片的在途接收在此中止。
    /// </summary>
    private async Task SweepOrphanInboundLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try { await Task.Delay(InboundSweepIntervalMs, token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            var now = DateTime.UtcNow;
            foreach (var (id, transfer) in _inboundTransfers)
            {
                if ((now - transfer.LastActivityUtc).TotalMilliseconds <= _options.TransferTimeoutMs) continue;
                if (!_inboundTransfers.TryRemove(id, out _)) continue;

                transfer.Abort();
                RaiseTransferFailed(id, FileTransferFailureReason.TransferTimeout,
                    $"接收超时：{_options.TransferTimeoutMs}ms 内未再收到该传输的任何分片，已中止并清理");
            }

            // 拒收记录只为在 Begin 广播窗口内去重，过期即清，防止字典无限增长
            foreach (var (id, rejectedAt) in _rejectedInbound)
            {
                if ((now - rejectedAt).TotalMilliseconds > _options.TransferTimeoutMs * 2L)
                    _rejectedInbound.TryRemove(id, out _);
            }
        }
    }

    private static async Task DiscardPayloadAsync(Stream stream, int length, CancellationToken token)
    {
        if (length <= 0) return;
        var buffer = new byte[Math.Min(length, 64 * 1024)];
        var remaining = length;
        while (remaining > 0)
        {
            var toRead = Math.Min(remaining, buffer.Length);
            if (!await FrameCodec.ReadExactAsync(stream, buffer.AsMemory(0, toRead), token).ConfigureAwait(false))
                return;
            remaining -= toRead;
        }
    }

    // ────────────────────────────── 事件触发（隔离订阅者异常） ──────────────────────────────
    // 这些事件大多在 Lane 接收循环的调用链上同步触发，订阅者抛出的异常若沿栈传播会掀翻整条连接、
    // 触发无谓的断线重连，因此统一在此吞掉。订阅者内部的阻塞操作（如同步 Dispatcher 调用）仍会拖慢
    // 接收循环，属订阅方责任——进度/分片级事件的处理应尽量轻或自行异步化。

    private void RaiseTransferCompleted(FileTransferMetadata metadata, TransferDirection direction, byte[]? data, string? filePath, FileTransferResult result)
    {
        _logger?.Info($"[FileTransfer:{ChannelName}] 传输完成 {metadata.TransferId}（{direction}，{result.Elapsed.TotalSeconds:F1}s，{result.ThroughputMBps:F1}MB/s）");
        try
        {
            TransferCompleted?.Invoke(this, new FileTransferCompletedEventArgs
            {
                Metadata = metadata, Direction = direction, Data = data, FilePath = filePath, Result = result
            });
        }
        catch { /* 订阅者异常不改变传输结果 */ }
    }

    private void RaiseTransferFailed(Guid transferId, FileTransferFailureReason reason, string message)
    {
        _logger?.Warn($"[FileTransfer:{ChannelName}] 传输失败 {transferId}（{reason}）：{message}");
        try
        {
            TransferFailed?.Invoke(this, new FileTransferFailedEventArgs { TransferId = transferId, Reason = reason, Message = message });
        }
        catch { /* 订阅者异常不能沿接收循环/清扫循环传播 */ }
    }

    private void RaiseTransferProgress(Guid transferId, long bytesTransferred, long totalBytes)
    {
        try
        {
            TransferProgress?.Invoke(this, new FileTransferProgressEventArgs
            {
                TransferId = transferId, BytesTransferred = bytesTransferred, TotalBytes = totalBytes
            });
        }
        catch { /* 同上 */ }
    }

    private void RaiseChunkTransferred(Guid transferId, int laneId, long chunkOffset, int chunkLength, TransferDirection direction)
    {
        try
        {
            ChunkTransferred?.Invoke(this, new ChunkTransferredEventArgs
            {
                TransferId = transferId, LaneId = laneId, ChunkOffset = chunkOffset,
                ChunkLength = chunkLength, Direction = direction
            });
        }
        catch { /* 同上 */ }
    }

    // ────────────────────────────── Lane 状态聚合 ──────────────────────────────

    private void OnLaneStatusChangedInternal(LaneStatus status, bool isReconnect)
    {
        if (isReconnect) _logger?.Info($"[FileTransfer:{ChannelName}] Lane {status.LaneId} 已重连");
        else if (!status.IsConnected) _logger?.Warn($"[FileTransfer:{ChannelName}] Lane {status.LaneId} 已断开");

        // 本回调在 Lane 的连接守护/收尾路径上同步执行，订阅者异常同样不能外传
        try { LaneStatusChanged?.Invoke(this, new LaneStatusChangedEventArgs { Status = status }); } catch { }
        if (isReconnect)
        {
            try { LaneReconnected?.Invoke(this, new LaneStatusChangedEventArgs { Status = status }); } catch { }
        }

        if (!status.IsConnected)
        {
            if (_options.OnLaneFailure == LaneFailurePolicy.RerouteToSurvivingLanes)
                RerouteLanePendingJobs(status.LaneId);

            // 全部 Lane 断开：在途接收不可能再完成，立即清理，不必等清扫任务超时
            if (_started && _lanes.Values.All(l => !l.IsConnected) && !_inboundTransfers.IsEmpty)
                AbortAllInboundTransfers(FileTransferFailureReason.ConnectionLost, "全部 Lane 已断开，在途接收无法完成");
        }

        RecomputeAggregateStatus();
    }

    private void RerouteLanePendingJobs(int failedLaneId)
    {
        if (!_lanes.TryGetValue(failedLaneId, out var failedLane)) return;
        var pending = failedLane.DrainPendingJobs();
        if (pending.Count == 0) return;

        // 已经交给 Socket 发送、尚未确认的数据无法追回，这里只能挽救还排队中、尚未发出的分片，
        // 属于既定的简化取舍：真正丢失的部分依赖整体哈希校验不通过 -> 上层整体重传兜底。
        var survivors = _lanes.Values.Where(l => l.LaneId != failedLaneId && l.IsConnected).ToList();
        if (survivors.Count == 0)
        {
            // 无处改派：任务里可能持有租借缓冲，执行其归还逻辑后丢弃
            _ = TransferLane.ReleaseJobsAsync(pending);
            return;
        }

        for (var i = 0; i < pending.Count; i++)
        {
            var target = survivors[i % survivors.Count];
            _ = RequeueSafelyAsync(target, pending[i]);
        }
    }

    /// <summary>改派任务到幸存 Lane。目标 Lane 恰好也停止（队列已关闭）时不能让异常无人观察，退化为释放任务持有的资源</summary>
    private static async Task RequeueSafelyAsync(TransferLane target, Func<Stream, CancellationToken, Task> job)
    {
        try
        {
            await target.RequeueJobAsync(job, CancellationToken.None).ConfigureAwait(false);
        }
        catch
        {
            await TransferLane.ReleaseJobsAsync([job]).ConfigureAwait(false);
        }
    }

    private void RecomputeAggregateStatus()
    {
        if (!_started)
        {
            SetStatus(FileTransferStatus.Stopped);
            return;
        }

        var connectedCount = _lanes.Values.Count(l => l.IsConnected);
        var newStatus = connectedCount switch
        {
            0 when _options.Role == FileTransferRole.Server => FileTransferStatus.WaitingForPeer,
            0 => FileTransferStatus.Faulted,
            _ when connectedCount == _lanes.Count => FileTransferStatus.Connected,
            _ => FileTransferStatus.Degraded
        };

        SetStatus(newStatus);
    }

    private void SetStatus(FileTransferStatus newStatus)
    {
        ChannelStateChangedEventArgs args;
        lock (_statusLock)
        {
            if (Status == newStatus) return;
            args = new ChannelStateChangedEventArgs { OldStatus = Status, NewStatus = newStatus };
            Status = newStatus;
        }
        try { StateChanged?.Invoke(this, args); } catch { /* 订阅者异常不能沿状态回调链传播 */ }
    }
}
