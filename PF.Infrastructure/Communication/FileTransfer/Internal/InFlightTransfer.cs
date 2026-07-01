using PF.Core.Entities.Communication.FileTransfer;

namespace PF.Infrastructure.Communication.FileTransfer.Internal;

/// <summary>
/// 接收端在途传输状态。收到 Begin 帧时创建并预分配目标缓冲区，
/// 各 Lane 收到 Chunk 帧后校验边界通过后直接读入缓冲区对应偏移，互不加锁（偏移区间不重叠）。
/// </summary>
internal sealed class InFlightTransfer
{
    public Guid TransferId { get; }
    public FileTransferMetadata Metadata { get; }
    public long TotalLength { get; }
    public FrameCodec.BeginFlags Flags { get; }
    public ulong ExpectedFinalHash { get; }
    public byte[] Buffer { get; }
    public DateTime StartedAt { get; } = DateTime.Now;

    private long _bytesReceived;
    private int _aborted;

    public long BytesReceived => Interlocked.Read(ref _bytesReceived);
    public bool IsAborted => Volatile.Read(ref _aborted) != 0;
    public bool IsComplete => BytesReceived >= TotalLength;

    public InFlightTransfer(Guid transferId, FileTransferMetadata metadata, long totalLength,
        FrameCodec.BeginFlags flags, ulong expectedFinalHash)
    {
        TransferId = transferId;
        Metadata = metadata;
        TotalLength = totalLength;
        Flags = flags;
        ExpectedFinalHash = expectedFinalHash;
        // TotalLength 已在创建前由调用方校验不超过 MaxTransferSizeBytes（且不超过数组长度上限），此处可放心分配
        Buffer = new byte[totalLength];
    }

    /// <summary>
    /// 校验分片边界是否落在缓冲区范围内。必须在读取 Payload 之前调用——不能假设帧头数据总是可信，
    /// 畸形帧的 Offset/Length 组合可能超出缓冲区范围，越界读写会抛异常甚至写坏内存。
    /// </summary>
    public bool ValidateBounds(long offset, int length) =>
        !IsAborted && offset >= 0 && length >= 0 && offset + length <= TotalLength;

    /// <summary>校验通过后，取得缓冲区对应偏移的可写切片，供调用方直接把网络数据读入，不做额外拷贝</summary>
    public Memory<byte> GetWriteSlice(long offset, int length) => Buffer.AsMemory((int)offset, length);

    /// <summary>数据写入切片后上报字节数，供 <see cref="IsComplete"/> 判定和进度事件使用</summary>
    public void ReportBytesWritten(int length) => Interlocked.Add(ref _bytesReceived, length);

    /// <summary>标记该传输已中止（校验失败/越界/超时），其余 Lane 收到属于该 TransferId 的后续分片时直接丢弃</summary>
    public void Abort() => Interlocked.Exchange(ref _aborted, 1);
}
