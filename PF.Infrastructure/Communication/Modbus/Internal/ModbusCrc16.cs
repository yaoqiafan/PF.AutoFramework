namespace PF.Infrastructure.Communication.Modbus.Internal;

/// <summary>
/// CRC16/MODBUS 校验（多项式 0xA001，初值 0xFFFF）。仅供 Modbus RTU 使用——TCP 用 MBAP 头的长度前缀定界，不需要 CRC。
/// </summary>
internal static class ModbusCrc16
{
    /// <summary>计算 CRC16，返回值低字节在前（Modbus RTU 附加 CRC 时先传低字节、再传高字节）</summary>
    public static ushort Compute(ReadOnlySpan<byte> data)
    {
        ushort crc = 0xFFFF;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
            {
                if ((crc & 0x0001) != 0)
                {
                    crc >>= 1;
                    crc ^= 0xA001;
                }
                else
                {
                    crc >>= 1;
                }
            }
        }
        return crc;
    }
}
