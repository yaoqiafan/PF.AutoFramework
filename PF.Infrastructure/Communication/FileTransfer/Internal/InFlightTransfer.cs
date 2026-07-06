using Microsoft.Win32.SafeHandles;
using PF.Core.Entities.Communication.FileTransfer;

namespace PF.Infrastructure.Communication.FileTransfer.Internal;

/// <summary>
/// 接收端在途传输状态，收到 Begin 帧时创建，按 TotalLength 与阈值二选一工作模式：
///   · 内存模式（≤ InMemoryReceiveThresholdBytes）：预分配 byte[] 重组缓冲，各 Lane 的分片直接读入对应偏移（零拷贝）；
///   · 落盘模式（&gt; 阈值）：预分配临时文件，各 Lane 的分片经租借缓冲用 RandomAccess 写到对应偏移——
///     RandomAccess 按显式偏移读写、不共享文件指针，多 Lane 并发写互不重叠的区间无需加锁，
///     内存占用与数据大小无关。
/// </summary>
internal sealed class InFlightTransfer
{
    public Guid TransferId { get; }
    public FileTransferMetadata Metadata { get; }
    public long TotalLength { get; }
    public FrameCodec.BeginFlags Flags { get; }
    public ulong ExpectedFinalHash { get; }
    public DateTime StartedAt { get; } = DateTime.Now;

    /// <summary>内存模式的重组缓冲；落盘模式为 null</summary>
    public byte[]? Buffer { get; }

    /// <summary>落盘模式的临时文件完整路径；内存模式为 null</summary>
    public string? FilePath { get; }

    /// <summary>是否为落盘模式</summary>
    public bool IsFileMode => FilePath != null;

    private SafeFileHandle? _fileHandle;
    private long _lastActivityTicksUtc = DateTime.UtcNow.Ticks;
    private int _aborted;

    // 已接收字节的覆盖区间（互不重叠、按起点升序），用区间覆盖而非简单字节累加判定完成——
    // 重复/重叠分片（对端异常行为或协议错乱）在累加方案下会把计数推到 TotalLength 而数据实际留洞，
    // 关闭 VerifyFinalHash 时就没有任何机制能发现。区间数量级 ≈ Lane 数 + 空洞数，线性扫描足够。
    private readonly List<(long Start, long End)> _coverage = new();
    private readonly object _coverageLock = new();
    private long _coveredBytes;

    public long BytesReceived => Interlocked.Read(ref _coveredBytes);
    public bool IsAborted => Volatile.Read(ref _aborted) != 0;

    /// <summary>覆盖区间互不重叠且都落在 [0, TotalLength) 内，覆盖字节数到达 TotalLength 即等价于无空洞收满</summary>
    public bool IsComplete => BytesReceived >= TotalLength;

    /// <summary>最后一次收到本传输分片的时间（UTC），供通道的孤儿清扫任务判定接收超时</summary>
    public DateTime LastActivityUtc => new(Interlocked.Read(ref _lastActivityTicksUtc), DateTimeKind.Utc);

    private InFlightTransfer(Guid transferId, FileTransferMetadata metadata, long totalLength,
        FrameCodec.BeginFlags flags, ulong expectedFinalHash, byte[]? buffer, string? filePath, SafeFileHandle? fileHandle)
    {
        TransferId = transferId;
        Metadata = metadata;
        TotalLength = totalLength;
        Flags = flags;
        ExpectedFinalHash = expectedFinalHash;
        Buffer = buffer;
        FilePath = filePath;
        _fileHandle = fileHandle;
    }

    /// <summary>
    /// 创建在途传输。totalLength 超过 inMemoryThreshold 时落盘：在 receiveDirectory 下以 TransferId 命名
    /// 预分配临时文件——preallocationSize 同时兼做磁盘剩余空间检查，空间不足时此处直接抛 IOException，
    /// 上层不登记该传输，发送端等不到 Ack 按超时失败。
    /// </summary>
    public static InFlightTransfer Create(Guid transferId, FileTransferMetadata metadata, long totalLength,
        FrameCodec.BeginFlags flags, ulong expectedFinalHash, long inMemoryThreshold, string receiveDirectory)
    {
        if (totalLength <= inMemoryThreshold)
            return new InFlightTransfer(transferId, metadata, totalLength, flags, expectedFinalHash,
                new byte[totalLength], filePath: null, fileHandle: null);

        Directory.CreateDirectory(receiveDirectory);
        var filePath = Path.Combine(receiveDirectory, $"{transferId:N}.pfft");
        var handle = File.OpenHandle(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
            FileOptions.Asynchronous, preallocationSize: totalLength);
        return new InFlightTransfer(transferId, metadata, totalLength, flags, expectedFinalHash,
            buffer: null, filePath, handle);
    }

    /// <summary>
    /// 校验分片边界是否落在有效范围内。必须在读取 Payload 之前调用——不能假设帧头数据总是可信，
    /// 畸形帧的 Offset/Length 组合可能超出范围，越界读写会抛异常甚至写坏数据。
    /// </summary>
    public bool ValidateBounds(long offset, int length) =>
        !IsAborted && offset >= 0 && length >= 0 && offset + length <= TotalLength;

    /// <summary>内存模式：取得重组缓冲对应偏移的可写切片，供调用方把网络数据直接读入，不做额外拷贝</summary>
    public Memory<byte> GetWriteSlice(long offset, int length) => Buffer!.AsMemory((int)offset, length);

    /// <summary>
    /// 落盘模式：把一个分片写到临时文件对应偏移。与 <see cref="Abort"/> 存在竞态（清扫任务/通道停止
    /// 可能在写入过程中关闭句柄），此时返回 false 而不抛出，避免异常沿接收循环传播导致整条 Lane 断线重连。
    /// </summary>
    public async ValueTask<bool> TryWriteToFileAsync(long offset, ReadOnlyMemory<byte> payload, CancellationToken token)
    {
        if (IsAborted) return false;
        try
        {
            await RandomAccess.WriteAsync(_fileHandle!, payload, offset, token).ConfigureAwait(false);
            return true;
        }
        catch when (IsAborted)
        {
            return false;
        }
    }

    /// <summary>落盘模式：全部分片到齐后流式回读临时文件计算整体哈希，内存占用与文件大小无关</summary>
    public Task<ulong> ComputeFileHashAsync(CancellationToken token)
        => FrameCodec.ComputeXxHash64Async(_fileHandle!, TotalLength, token);

    /// <summary>
    /// 数据写入后登记覆盖区间并刷新活动时间，供 <see cref="IsComplete"/> 判定、进度事件和孤儿清扫使用。
    /// 与已有区间重叠的部分（重复分片）不重复计数。
    /// </summary>
    public void ReportBytesWritten(long offset, int length)
    {
        var newStart = offset;
        var newEnd = offset + length;
        long added;

        lock (_coverageLock)
        {
            long mergedStart = newStart, mergedEnd = newEnd, overlap = 0;

            var i = 0;
            while (i < _coverage.Count && _coverage[i].End < newStart) i++; // 跳过完全在新区间左侧的
            var insertAt = i;

            // 吸收所有与新区间相交或相邻的区间，累计其中已覆盖的重叠量
            while (i < _coverage.Count && _coverage[i].Start <= newEnd)
            {
                var (start, end) = _coverage[i];
                overlap += Math.Max(0, Math.Min(end, newEnd) - Math.Max(start, newStart));
                mergedStart = Math.Min(mergedStart, start);
                mergedEnd = Math.Max(mergedEnd, end);
                _coverage.RemoveAt(i);
            }

            _coverage.Insert(insertAt, (mergedStart, mergedEnd));
            added = (newEnd - newStart) - overlap;
        }

        if (added > 0) Interlocked.Add(ref _coveredBytes, added);
        Interlocked.Exchange(ref _lastActivityTicksUtc, DateTime.UtcNow.Ticks);
    }

    /// <summary>成功收尾：关闭文件句柄（消费方才能打开该文件），文件保留、交由消费方处理；内存模式为空操作</summary>
    public void CloseFile()
    {
        var handle = Interlocked.Exchange(ref _fileHandle, null);
        handle?.Dispose();
    }

    /// <summary>
    /// 标记该传输已中止（校验失败/越界/接收超时/通道停止）：其余 Lane 收到属于该 TransferId 的
    /// 后续分片时直接丢弃；落盘模式下关闭句柄并删除未完成的临时文件，不留垃圾。
    /// </summary>
    public void Abort()
    {
        if (Interlocked.Exchange(ref _aborted, 1) != 0) return;
        CloseFile();
        if (FilePath != null)
        {
            try { File.Delete(FilePath); } catch { /* 删除失败（如并发写尚未完全释放句柄）留给下次通道启动时的残留清理 */ }
        }
    }
}
