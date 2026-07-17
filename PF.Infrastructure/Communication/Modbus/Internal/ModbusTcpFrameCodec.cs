namespace PF.Infrastructure.Communication.Modbus.Internal;

/// <summary>
/// Modbus TCP MBAP（Modbus Application Protocol）头构建/解析：
/// [TransactionId(2)][ProtocolId(2)=0][Length(2)][UnitId(1)] + PDU。
/// </summary>
internal static class ModbusTcpFrameCodec
{
    /// <summary>MBAP 头固定长度（不含 PDU）</summary>
    public const int MbapHeaderLength = 7;

    /// <summary>Modbus 规范的 PDU 上限（253 字节），响应声明超过它即视为帧失步/非法</summary>
    public const int MaxPduLength = 253;

    // 事务号计数器不放在这里：它必须每 ModbusTcpMaster 实例独立（支持按实例暂停递增/重置），
    // 见 ModbusTcpMaster.NextTransactionId

    /// <summary>构建完整 ADU（MBAP 头 + PDU）</summary>
    public static byte[] BuildAdu(ushort transactionId, byte unitId, byte[] pdu)
    {
        var adu = new byte[MbapHeaderLength + pdu.Length];
        adu[0] = (byte)(transactionId >> 8);
        adu[1] = (byte)transactionId;
        adu[2] = 0; // ProtocolId 恒为 0（Modbus）
        adu[3] = 0;
        var length = (ushort)(1 + pdu.Length); // Length 字段覆盖 UnitId + 后续 PDU
        adu[4] = (byte)(length >> 8);
        adu[5] = (byte)length;
        adu[6] = unitId;
        Array.Copy(pdu, 0, adu, MbapHeaderLength, pdu.Length);
        return adu;
    }

    /// <summary>解析 7 字节 MBAP 头，返回事务号/从站地址/紧随其后的 PDU 字节数</summary>
    public static (ushort TransactionId, byte UnitId, int PduLength) ParseHeader(byte[] header)
    {
        if (header.Length != MbapHeaderLength)
            throw new ArgumentException($"MBAP 头长度必须为 {MbapHeaderLength} 字节", nameof(header));

        var transactionId = (ushort)((header[0] << 8) | header[1]);
        // header[2..3] = ProtocolId，Modbus 恒为 0，不同网关实现宽严不一，此处不做强校验
        var length = (ushort)((header[4] << 8) | header[5]);
        var unitId = header[6];
        var pduLength = length - 1; // Length 覆盖 UnitId(1) + PDU
        // 上限校验防帧失步雪崩：若前一笔事务超时在半帧处，残留字节会被错位解析成 MBAP 头，
        // Length 字段此时可能是任意值（最大 65534）——不设上限就会按错误长度等着收满，连环超时且永远无法重新对齐
        if (pduLength is < 0 or > MaxPduLength)
            throw new IOException($"Modbus TCP 响应 MBAP Length 字段非法（PDU 长度 {pduLength}），帧可能已失步");

        return (transactionId, unitId, pduLength);
    }
}
