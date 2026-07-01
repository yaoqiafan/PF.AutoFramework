using System.Buffers.Binary;
using System.IO.Hashing;
using System.Text;

namespace PF.Infrastructure.Communication.FileTransfer.Internal;

/// <summary>
/// 线路帧协议的编解码。帧结构详见设计文档：
/// 公共头(22B: Magic 4 + Version 1 + FrameType 1 + TransferId 16) + 按 FrameType 变化的字段。
/// Begin 帧在发送方调用 SendAsync 时数据已全部在手，TotalLength/FinalHash 提前算好随 Begin 帧一起发出，
/// 不需要单独的"结束帧"。
/// </summary>
internal static class FrameCodec
{
    /// <summary>协议魔数，ASCII "PFTC"，用于识别合法帧、拒绝垃圾数据/协议不兼容的连接</summary>
    public const uint MagicValue = 0x50465443;

    public const byte CurrentVersion = 1;

    public const int CommonHeaderSize = 4 + 1 + 1 + 16; // Magic + Version + FrameType + TransferId
    public const int BeginFixedFieldsSize = 8 + 1 + 8 + 2; // TotalLength + Flags + FinalHash + MetadataJsonLength
    public const int ChunkFixedFieldsSize = 8 + 4 + 4; // ChunkOffset + ChunkLength + ChunkCrc32

    public enum FrameType : byte
    {
        Begin = 0,
        Chunk = 1,
        Heartbeat = 2,
        HeartbeatAck = 3,

        /// <summary>接收端校验通过后回复，SendAsync 等到这个才能判定端到端成功，不只是本地写完 Socket</summary>
        TransferAck = 4,

        /// <summary>接收端校验失败后回复，携带失败原因，供发送端整体重传决策使用</summary>
        TransferNack = 5
    }

    [Flags]
    public enum BeginFlags : byte
    {
        None = 0,
        HasChunkCrc = 1 << 0,
        HasFinalHash = 1 << 1
    }

    public readonly record struct CommonHeader(FrameType Type, Guid TransferId);

    public readonly record struct BeginFields(long TotalLength, BeginFlags Flags, ulong FinalHash, string MetadataJson);

    public readonly record struct ChunkFields(long ChunkOffset, int ChunkLength, uint ChunkCrc32);

    // ────────────────────────────── 校验计算 ──────────────────────────────

    public static uint ComputeCrc32(ReadOnlySpan<byte> data) => Crc32.HashToUInt32(data);

    public static ulong ComputeXxHash64(ReadOnlySpan<byte> data) => XxHash64.HashToUInt64(data);

    // ────────────────────────────── 写帧 ──────────────────────────────

    public static async Task WriteBeginFrameAsync(Stream stream, Guid transferId, long totalLength,
        BeginFlags flags, ulong finalHash, string metadataJson, CancellationToken token)
    {
        // Span<byte> 是 ref struct，C# 12 下不允许在 async 方法内跨 await 使用，
        // 因此把所有 Span 操作放进同步辅助方法，async 方法只处理 byte[] 和 await。
        var buffer = BuildBeginFrameBytes(transferId, totalLength, flags, finalHash, metadataJson);
        await stream.WriteAsync(buffer, token).ConfigureAwait(false);
    }

    private static byte[] BuildBeginFrameBytes(Guid transferId, long totalLength,
        BeginFlags flags, ulong finalHash, string metadataJson)
    {
        var metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
        if (metadataBytes.Length > ushort.MaxValue)
            throw new ArgumentException("MetadataJson 编码后长度超过 65535 字节", nameof(metadataJson));

        var buffer = new byte[CommonHeaderSize + BeginFixedFieldsSize + metadataBytes.Length];
        var span = buffer.AsSpan();
        WriteCommonHeader(span, FrameType.Begin, transferId);

        var offset = CommonHeaderSize;
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset, 8), totalLength); offset += 8;
        span[offset] = (byte)flags; offset += 1;
        BinaryPrimitives.WriteUInt64LittleEndian(span.Slice(offset, 8), finalHash); offset += 8;
        BinaryPrimitives.WriteUInt16LittleEndian(span.Slice(offset, 2), (ushort)metadataBytes.Length); offset += 2;
        metadataBytes.CopyTo(span[offset..]);

        return buffer;
    }

    public static async Task WriteChunkFrameAsync(Stream stream, Guid transferId, long chunkOffset,
        ReadOnlyMemory<byte> payload, uint crc32, CancellationToken token)
    {
        var header = BuildChunkFrameHeaderBytes(transferId, chunkOffset, payload.Length, crc32);
        await stream.WriteAsync(header, token).ConfigureAwait(false);
        await stream.WriteAsync(payload, token).ConfigureAwait(false);
    }

    private static byte[] BuildChunkFrameHeaderBytes(Guid transferId, long chunkOffset, int payloadLength, uint crc32)
    {
        var header = new byte[CommonHeaderSize + ChunkFixedFieldsSize];
        var span = header.AsSpan();
        WriteCommonHeader(span, FrameType.Chunk, transferId);

        var offset = CommonHeaderSize;
        BinaryPrimitives.WriteInt64LittleEndian(span.Slice(offset, 8), chunkOffset); offset += 8;
        BinaryPrimitives.WriteInt32LittleEndian(span.Slice(offset, 4), payloadLength); offset += 4;
        BinaryPrimitives.WriteUInt32LittleEndian(span.Slice(offset, 4), crc32);

        return header;
    }

    public static async Task WriteHeartbeatFrameAsync(Stream stream, bool isAck, CancellationToken token)
    {
        var buffer = new byte[CommonHeaderSize];
        WriteCommonHeader(buffer, isAck ? FrameType.HeartbeatAck : FrameType.Heartbeat, Guid.Empty);
        await stream.WriteAsync(buffer, token).ConfigureAwait(false);
    }

    public static async Task WriteTransferAckFrameAsync(Stream stream, Guid transferId, CancellationToken token)
    {
        var buffer = new byte[CommonHeaderSize];
        WriteCommonHeader(buffer, FrameType.TransferAck, transferId);
        await stream.WriteAsync(buffer, token).ConfigureAwait(false);
    }

    public static async Task WriteTransferNackFrameAsync(Stream stream, Guid transferId, byte failureReasonCode, CancellationToken token)
    {
        var buffer = new byte[CommonHeaderSize + 1];
        WriteCommonHeader(buffer, FrameType.TransferNack, transferId);
        buffer[CommonHeaderSize] = failureReasonCode;
        await stream.WriteAsync(buffer, token).ConfigureAwait(false);
    }

    public static async Task<byte> ReadTransferNackFieldsAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[1];
        if (!await ReadExactAsync(stream, buffer, token).ConfigureAwait(false))
            throw new EndOfStreamException("连接在读取 TransferNack 帧字段时中断");
        return buffer[0];
    }

    private static void WriteCommonHeader(Span<byte> span, FrameType type, Guid transferId)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(span[..4], MagicValue);
        span[4] = CurrentVersion;
        span[5] = (byte)type;
        transferId.TryWriteBytes(span.Slice(6, 16));
    }

    // ────────────────────────────── 读帧 ──────────────────────────────

    /// <summary>读取公共头。对端优雅关闭连接（在帧边界处读到 0 字节）时返回 null</summary>
    public static async Task<CommonHeader?> ReadCommonHeaderAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[CommonHeaderSize];
        var completed = await ReadExactAsync(stream, buffer, token).ConfigureAwait(false);
        if (!completed) return null;

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(0, 4));
        if (magic != MagicValue)
            throw new InvalidDataException($"帧头 Magic 不匹配（收到 0x{magic:X8}），协议错乱或版本不兼容，需断开重连");

        var version = buffer[4];
        if (version != CurrentVersion)
            throw new InvalidDataException($"帧头协议版本不支持：{version}");

        var type = (FrameType)buffer[5];
        var transferId = new Guid(buffer.AsSpan(6, 16));
        return new CommonHeader(type, transferId);
    }

    public static async Task<BeginFields> ReadBeginFieldsAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[BeginFixedFieldsSize];
        if (!await ReadExactAsync(stream, buffer, token).ConfigureAwait(false))
            throw new EndOfStreamException("连接在读取 Begin 帧字段时中断");

        var totalLength = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
        var flags = (BeginFlags)buffer[8];
        var finalHash = BinaryPrimitives.ReadUInt64LittleEndian(buffer.AsSpan(9, 8));
        var metadataLength = BinaryPrimitives.ReadUInt16LittleEndian(buffer.AsSpan(17, 2));

        var metadataJson = string.Empty;
        if (metadataLength > 0)
        {
            var metadataBuffer = new byte[metadataLength];
            if (!await ReadExactAsync(stream, metadataBuffer, token).ConfigureAwait(false))
                throw new EndOfStreamException("连接在读取 Begin 帧 MetadataJson 字段时中断");
            metadataJson = Encoding.UTF8.GetString(metadataBuffer);
        }

        return new BeginFields(totalLength, flags, finalHash, metadataJson);
    }

    /// <summary>只读取 Chunk 帧的固定字段（偏移/长度/CRC），不读取 Payload——Payload 由调用方直接读入目标缓冲区对应偏移处，避免多一次拷贝</summary>
    public static async Task<ChunkFields> ReadChunkFieldsAsync(Stream stream, CancellationToken token)
    {
        var buffer = new byte[ChunkFixedFieldsSize];
        if (!await ReadExactAsync(stream, buffer, token).ConfigureAwait(false))
            throw new EndOfStreamException("连接在读取 Chunk 帧字段时中断");

        var chunkOffset = BinaryPrimitives.ReadInt64LittleEndian(buffer.AsSpan(0, 8));
        var chunkLength = BinaryPrimitives.ReadInt32LittleEndian(buffer.AsSpan(8, 4));
        var crc32 = BinaryPrimitives.ReadUInt32LittleEndian(buffer.AsSpan(12, 4));

        return new ChunkFields(chunkOffset, chunkLength, crc32);
    }

    /// <summary>严格读满指定长度。对端在帧边界处优雅关闭返回 false；帧读到一半被关闭视为异常抛出</summary>
    public static async Task<bool> ReadExactAsync(Stream stream, Memory<byte> buffer, CancellationToken token)
    {
        var totalRead = 0;
        while (totalRead < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[totalRead..], token).ConfigureAwait(false);
            if (read == 0)
            {
                if (totalRead == 0) return false;
                throw new EndOfStreamException("连接在帧中途被对端关闭");
            }
            totalRead += read;
        }
        return true;
    }
}
