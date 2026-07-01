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
using PF.Infrastructure.Communication.FileTransfer.Internal;

namespace PF.Infrastructure.Communication.FileTransfer;

/// <summary>
/// <see cref="IFileTransferChannel"/> 实现。聚合多条 <see cref="TransferLane"/>，
/// 负责协议语义：分片调度、Begin/Chunk/Ack/Nack 帧的收发决策、在途接收登记、状态聚合。
/// 单条 Lane 的连接生命周期（重连/心跳/发送队列）由 TransferLane 自己负责，本类不涉及。
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
    private readonly Dictionary<int, TransferLane> _lanes = new();
    private readonly ConcurrentDictionary<Guid, InFlightTransfer> _inboundTransfers = new();
    private readonly ConcurrentDictionary<Guid, TaskCompletionSource<AckResult>> _pendingAcks = new();
    private readonly SemaphoreSlim _sendGate = new(1, 1);
    private readonly object _statusLock = new();
    private bool _started;
    private bool _disposed;

    private readonly record struct AckResult(bool Success, FileTransferFailureReason Reason);

    /// <summary>按配置创建通道实例。配置非法（Links 为空/LaneId 重复/Client 缺 RemoteIp）时同步抛出 <see cref="ArgumentException"/></summary>
    public FileTransferChannel(FileTransferOptions options, string channelName)
    {
        ValidateOptions(options);
        _options = options;
        ChannelName = channelName;

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
    }

    // ────────────────────────────── 生命周期 ──────────────────────────────

    /// <inheritdoc/>
    public Task<bool> StartAsync(CancellationToken token = default)
    {
        ThrowIfDisposed();
        if (_started) return Task.FromResult(true);

        _started = true;
        foreach (var lane in _lanes.Values) lane.Start();
        RecomputeAggregateStatus();

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        if (!_started) return;
        _started = false;

        foreach (var lane in _lanes.Values) await lane.StopAsync().ConfigureAwait(false);
        SetStatus(FileTransferStatus.Stopped);
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

    // ────────────────────────────── 发送 ──────────────────────────────

    /// <inheritdoc/>
    public async Task<FileTransferResult> SendAsync(byte[] data, FileTransferMetadata metadata, CancellationToken token = default)
    {
        ThrowIfDisposed();
        if (data is null) throw new ArgumentNullException(nameof(data));
        if (data.Length == 0) throw new ArgumentException("data 不能为空数组", nameof(data));
        if (data.LongLength > _options.MaxTransferSizeBytes)
            throw new ArgumentException(
                $"数据长度 {data.LongLength} 超过 MaxTransferSizeBytes 上限 {_options.MaxTransferSizeBytes}", nameof(data));

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
                data.LongLength, connectedLanes.Count, _options.TargetChunksPerLane, _options.MinChunkSizeBytes, _options.MaxChunkSizeBytes);

            var flags = FrameCodec.BeginFlags.None;
            if (_options.VerifyChunkCrc) flags |= FrameCodec.BeginFlags.HasChunkCrc;
            var finalHash = 0UL;
            if (_options.VerifyFinalHash)
            {
                flags |= FrameCodec.BeginFlags.HasFinalHash;
                finalHash = FrameCodec.ComputeXxHash64(data);
            }

            var metadataJson = JsonSerializer.Serialize(metadata);

            // Begin 帧广播到每条 Lane，且必须在该 Lane 的任何 Chunk 帧之前发出——
            // 每条 Lane 是独立 TCP 连接、独立 FIFO 发送队列，天然保证顺序，不会出现 Chunk 先于 Begin 到达。
            foreach (var lane in connectedLanes)
                await lane.EnqueueBeginAsync(metadata.TransferId, data.LongLength, flags, finalHash, metadataJson, token).ConfigureAwait(false);

            await DistributeChunksAsync(data, metadata.TransferId, chunkSize, connectedLanes, _options.VerifyChunkCrc, token).ConfigureAwait(false);

            var ackResult = await WaitForAckAsync(tcs, token).ConfigureAwait(false);
            stopwatch.Stop();

            if (ackResult is null)
            {
                var reason = token.IsCancellationRequested ? FileTransferFailureReason.Cancelled : FileTransferFailureReason.TransferTimeout;
                return BuildFailureResult(metadata.TransferId, stopwatch.Elapsed, reason,
                    reason == FileTransferFailureReason.Cancelled ? "调用方取消" : "等待对端确认超时");
            }

            if (!ackResult.Value.Success)
                return BuildFailureResult(metadata.TransferId, stopwatch.Elapsed, ackResult.Value.Reason, "对端校验未通过");

            var bytesPerLane = connectedLanes.ToDictionary(l => l.LaneId, l => l.GetStatus().BytesSent);
            var result = new FileTransferResult
            {
                Success = true,
                TransferId = metadata.TransferId,
                Elapsed = stopwatch.Elapsed,
                ThroughputMBps = stopwatch.Elapsed.TotalSeconds > 0 ? data.Length / 1024.0 / 1024.0 / stopwatch.Elapsed.TotalSeconds : 0,
                BytesPerLane = bytesPerLane,
                FailureReason = FileTransferFailureReason.None
            };

            RaiseTransferCompleted(metadata, TransferDirection.Sent, null, result);
            return result;
        }
        finally
        {
            _pendingAcks.TryRemove(metadata.TransferId, out _);
            _sendGate.Release();
        }
    }

    private async Task<AckResult?> WaitForAckAsync(TaskCompletionSource<AckResult> tcs, CancellationToken token)
    {
        using var timeoutCts = new CancellationTokenSource(_options.TransferTimeoutMs);
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

    private async Task DistributeChunksAsync(byte[] data, Guid transferId, int chunkSize, List<TransferLane> lanes, bool withCrc, CancellationToken token)
    {
        var totalLength = data.LongLength;
        var laneIndex = 0;
        long offset = 0;

        while (offset < totalLength)
        {
            var length = (int)Math.Min(chunkSize, totalLength - offset);
            var chunk = new ReadOnlyMemory<byte>(data, (int)offset, length);

            var lane = lanes[laneIndex % lanes.Count];
            if (!lane.IsConnected)
            {
                var alive = lanes.FirstOrDefault(l => l.IsConnected);
                if (alive is null) throw new IOException("分发分片时全部 Lane 均已断开");
                lane = alive;
            }

            await lane.EnqueueChunkAsync(transferId, offset, chunk, withCrc, token).ConfigureAwait(false);

            if (EnableChunkLevelDiagnostics)
                ChunkTransferred?.Invoke(this, new ChunkTransferredEventArgs
                {
                    TransferId = transferId, LaneId = lane.LaneId, ChunkOffset = offset, ChunkLength = length, Direction = TransferDirection.Sent
                });

            offset += length;
            laneIndex++;

            TransferProgress?.Invoke(this, new FileTransferProgressEventArgs
            {
                TransferId = transferId, BytesTransferred = offset, TotalBytes = totalLength
            });
        }
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
        TransferFailed?.Invoke(this, new FileTransferFailedEventArgs { TransferId = transferId, Reason = reason, Message = message });
        return result;
    }

    // ────────────────────────────── 接收 / 帧分发 ──────────────────────────────

    private async Task OnFrameReceivedAsync(int laneId, FrameCodec.CommonHeader header, Stream stream, CancellationToken token)
    {
        switch (header.Type)
        {
            case FrameCodec.FrameType.Begin:
                await HandleBeginFrameAsync(header.TransferId, stream, token).ConfigureAwait(false);
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

    private async Task HandleBeginFrameAsync(Guid transferId, Stream stream, CancellationToken token)
    {
        var fields = await FrameCodec.ReadBeginFieldsAsync(stream, token).ConfigureAwait(false);

        if (_inboundTransfers.ContainsKey(transferId)) return; // 已由其他 Lane 广播的 Begin 帧登记过，幂等跳过
        if (fields.TotalLength < 0 || fields.TotalLength > _options.MaxTransferSizeBytes) return; // 畸形/超限长度，不登记
        if (_inboundTransfers.Count >= _options.MaxConcurrentInboundTransfers) return; // 在途接收数量达上限，拒绝新传输

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

        var transfer = new InFlightTransfer(transferId, metadata, fields.TotalLength, fields.Flags, fields.FinalHash);
        _inboundTransfers.TryAdd(transferId, transfer);
    }

    private async Task HandleChunkFrameAsync(int laneId, Guid transferId, Stream stream, CancellationToken token)
    {
        var chunkFields = await FrameCodec.ReadChunkFieldsAsync(stream, token).ConfigureAwait(false);

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
            AbortTransfer(transfer, laneId, FileTransferFailureReason.MalformedFrame, "分片偏移越界");
            return;
        }

        var destination = transfer.GetWriteSlice(chunkFields.ChunkOffset, chunkFields.ChunkLength);
        if (!await FrameCodec.ReadExactAsync(stream, destination, token).ConfigureAwait(false))
        {
            AbortTransfer(transfer, laneId, FileTransferFailureReason.ConnectionLost, "读取分片 payload 时连接中断");
            return;
        }

        if (transfer.Flags.HasFlag(FrameCodec.BeginFlags.HasChunkCrc))
        {
            var actualCrc = FrameCodec.ComputeCrc32(destination.Span);
            if (actualCrc != chunkFields.ChunkCrc32)
            {
                AbortTransfer(transfer, laneId, FileTransferFailureReason.ChecksumMismatch, "分片 CRC 校验不通过");
                return;
            }
        }

        transfer.ReportBytesWritten(chunkFields.ChunkLength);
        if (_lanes.TryGetValue(laneId, out var lane)) lane.ReportBytesReceived(chunkFields.ChunkLength);

        if (EnableChunkLevelDiagnostics)
            ChunkTransferred?.Invoke(this, new ChunkTransferredEventArgs
            {
                TransferId = transferId, LaneId = laneId, ChunkOffset = chunkFields.ChunkOffset,
                ChunkLength = chunkFields.ChunkLength, Direction = TransferDirection.Received
            });

        TransferProgress?.Invoke(this, new FileTransferProgressEventArgs
        {
            TransferId = transferId, BytesTransferred = transfer.BytesReceived, TotalBytes = transfer.TotalLength
        });

        if (transfer.IsComplete)
            await FinalizeInboundTransferAsync(transfer, laneId, token).ConfigureAwait(false);
    }

    private async Task FinalizeInboundTransferAsync(InFlightTransfer transfer, int laneId, CancellationToken token)
    {
        _inboundTransfers.TryRemove(transfer.TransferId, out _);

        if (transfer.Flags.HasFlag(FrameCodec.BeginFlags.HasFinalHash))
        {
            var actualHash = FrameCodec.ComputeXxHash64(transfer.Buffer);
            if (actualHash != transfer.ExpectedFinalHash)
            {
                const FileTransferFailureReason reason = FileTransferFailureReason.ChecksumMismatch;
                TransferFailed?.Invoke(this, new FileTransferFailedEventArgs
                {
                    TransferId = transfer.TransferId, Reason = reason, Message = "整体哈希校验不通过"
                });
                if (_lanes.TryGetValue(laneId, out var nackLane))
                    await nackLane.EnqueueNackAsync(transfer.TransferId, (byte)reason, token).ConfigureAwait(false);
                return;
            }
        }

        var elapsed = DateTime.Now - transfer.StartedAt;
        var result = new FileTransferResult
        {
            Success = true,
            TransferId = transfer.TransferId,
            Elapsed = elapsed,
            ThroughputMBps = elapsed.TotalSeconds > 0 ? transfer.TotalLength / 1024.0 / 1024.0 / elapsed.TotalSeconds : 0,
            FailureReason = FileTransferFailureReason.None
        };

        RaiseTransferCompleted(transfer.Metadata, TransferDirection.Received, transfer.Buffer, result);

        if (_lanes.TryGetValue(laneId, out var ackLane))
            await ackLane.EnqueueAckAsync(transfer.TransferId, token).ConfigureAwait(false);
    }

    private void AbortTransfer(InFlightTransfer transfer, int laneId, FileTransferFailureReason reason, string message)
    {
        transfer.Abort();
        _inboundTransfers.TryRemove(transfer.TransferId, out _);
        TransferFailed?.Invoke(this, new FileTransferFailedEventArgs { TransferId = transfer.TransferId, Reason = reason, Message = message });

        if (_lanes.TryGetValue(laneId, out var lane))
            _ = lane.EnqueueNackAsync(transfer.TransferId, (byte)reason, CancellationToken.None);
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

    private void RaiseTransferCompleted(FileTransferMetadata metadata, TransferDirection direction, byte[]? data, FileTransferResult result)
    {
        TransferCompleted?.Invoke(this, new FileTransferCompletedEventArgs
        {
            Metadata = metadata, Direction = direction, Data = data, Result = result
        });
    }

    // ────────────────────────────── Lane 状态聚合 ──────────────────────────────

    private void OnLaneStatusChangedInternal(LaneStatus status, bool isReconnect)
    {
        LaneStatusChanged?.Invoke(this, new LaneStatusChangedEventArgs { Status = status });
        if (isReconnect)
            LaneReconnected?.Invoke(this, new LaneStatusChangedEventArgs { Status = status });

        if (!status.IsConnected && _options.OnLaneFailure == LaneFailurePolicy.RerouteToSurvivingLanes)
            RerouteLanePendingJobs(status.LaneId);

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
        if (survivors.Count == 0) return;

        for (var i = 0; i < pending.Count; i++)
        {
            var target = survivors[i % survivors.Count];
            _ = target.RequeueJobAsync(pending[i], CancellationToken.None);
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
        StateChanged?.Invoke(this, args);
    }
}
