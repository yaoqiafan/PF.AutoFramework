using PF.Core.Enums;

namespace PF.Infrastructure.Communication.Modbus.Internal;

/// <summary>
/// Modbus PDU（Protocol Data Unit）构建/解析，RTU 与 TCP 共用——两者的功能码/数据模型完全一致，
/// 只有外层成帧方式（RTU：地址+PDU+CRC16；TCP：MBAP 头+PDU）不同，协议本体只写一份，避免跑偏。
/// </summary>
internal static class ModbusPduCodec
{
    private const int MaxReadBitQuantity = 2000;
    private const int MaxReadRegisterQuantity = 125;
    private const int MaxWriteMultipleCoilsQuantity = 1968;
    private const int MaxWriteMultipleRegistersQuantity = 123;

    // ── 请求构建 ──────────────────────────────────────────────────────────

    /// <summary>构建读请求 PDU（功能码 01/02/03/04 通用形状：FC + 起始地址(2) + 数量(2)）</summary>
    public static byte[] BuildReadRequest(byte functionCode, ushort startAddress, ushort quantity)
    {
        var isBitRead = functionCode is ModbusFunctionCode.ReadCoils or ModbusFunctionCode.ReadDiscreteInputs;
        var max = isBitRead ? MaxReadBitQuantity : MaxReadRegisterQuantity;
        if (quantity == 0 || quantity > max)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, $"数量必须在 1~{max} 之间");

        var pdu = new byte[5];
        pdu[0] = functionCode;
        WriteUInt16BigEndian(pdu, 1, startAddress);
        WriteUInt16BigEndian(pdu, 3, quantity);
        return pdu;
    }

    /// <summary>构建写单个线圈请求 PDU（功能码 05）：线圈值以 0xFF00/0x0000 表示 ON/OFF</summary>
    public static byte[] BuildWriteSingleCoil(ushort address, bool value)
    {
        var pdu = new byte[5];
        pdu[0] = ModbusFunctionCode.WriteSingleCoil;
        WriteUInt16BigEndian(pdu, 1, address);
        WriteUInt16BigEndian(pdu, 3, value ? (ushort)0xFF00 : (ushort)0x0000);
        return pdu;
    }

    /// <summary>构建写单个寄存器请求 PDU（功能码 06）</summary>
    public static byte[] BuildWriteSingleRegister(ushort address, ushort value)
    {
        var pdu = new byte[5];
        pdu[0] = ModbusFunctionCode.WriteSingleRegister;
        WriteUInt16BigEndian(pdu, 1, address);
        WriteUInt16BigEndian(pdu, 3, value);
        return pdu;
    }

    /// <summary>构建写多个线圈请求 PDU（功能码 0F）</summary>
    public static byte[] BuildWriteMultipleCoils(ushort startAddress, bool[] values)
    {
        if (values == null || values.Length == 0 || values.Length > MaxWriteMultipleCoilsQuantity)
            throw new ArgumentOutOfRangeException(nameof(values), "数量必须在 1~1968 之间");

        var byteCount = (values.Length + 7) / 8;
        var pdu = new byte[6 + byteCount];
        pdu[0] = ModbusFunctionCode.WriteMultipleCoils;
        WriteUInt16BigEndian(pdu, 1, startAddress);
        WriteUInt16BigEndian(pdu, 3, (ushort)values.Length);
        pdu[5] = (byte)byteCount;
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i]) pdu[6 + i / 8] |= (byte)(1 << (i % 8));
        }
        return pdu;
    }

    /// <summary>构建写多个寄存器请求 PDU（功能码 10）</summary>
    public static byte[] BuildWriteMultipleRegisters(ushort startAddress, ushort[] values)
    {
        if (values == null || values.Length == 0 || values.Length > MaxWriteMultipleRegistersQuantity)
            throw new ArgumentOutOfRangeException(nameof(values), "数量必须在 1~123 之间");

        var pdu = new byte[6 + values.Length * 2];
        pdu[0] = ModbusFunctionCode.WriteMultipleRegisters;
        WriteUInt16BigEndian(pdu, 1, startAddress);
        WriteUInt16BigEndian(pdu, 3, (ushort)values.Length);
        pdu[5] = (byte)(values.Length * 2);
        for (var i = 0; i < values.Length; i++)
            WriteUInt16BigEndian(pdu, 6 + i * 2, values[i]);
        return pdu;
    }

    // ── 响应解析 ──────────────────────────────────────────────────────────

    /// <summary>解析读线圈/离散量输入响应 PDU：[FC][ByteCount][Data...]</summary>
    public static bool[] ParseCoilsResponse(byte[] pdu, ushort quantity)
    {
        if (pdu.Length < 2) throw new IOException("Modbus 响应 PDU 长度不足");
        int byteCount = pdu[1];
        if (byteCount != (quantity + 7) / 8 || pdu.Length < 2 + byteCount)
            throw new IOException("Modbus 响应 ByteCount 与请求数量不一致，响应可能已损坏");

        var result = new bool[quantity];
        for (var i = 0; i < quantity; i++)
            result[i] = (pdu[2 + i / 8] & (1 << (i % 8))) != 0;
        return result;
    }

    /// <summary>解析读保持/输入寄存器响应 PDU：[FC][ByteCount][Data...]</summary>
    public static ushort[] ParseRegistersResponse(byte[] pdu, ushort quantity)
    {
        if (pdu.Length < 2) throw new IOException("Modbus 响应 PDU 长度不足");
        int byteCount = pdu[1];
        if (byteCount != quantity * 2 || pdu.Length < 2 + byteCount)
            throw new IOException("Modbus 响应 ByteCount 与请求数量不一致，响应可能已损坏");

        var result = new ushort[quantity];
        for (var i = 0; i < quantity; i++)
            result[i] = ReadUInt16BigEndian(pdu, 2 + i * 2);
        return result;
    }

    /// <summary>校验写单个线圈/寄存器响应是否为请求的原样回显（功能码 05/06 的从站应答语义）</summary>
    public static void ValidateWriteSingleEcho(byte[] pdu, byte functionCode, ushort address, ushort value)
    {
        if (pdu.Length < 5 || pdu[0] != functionCode
            || ReadUInt16BigEndian(pdu, 1) != address
            || ReadUInt16BigEndian(pdu, 3) != value)
            throw new IOException("Modbus 写入响应回显与请求不一致，响应可能已损坏或串包");
    }

    /// <summary>校验写多个线圈/寄存器响应是否为 [FC][起始地址][数量] 回显（功能码 0F/10 的从站应答语义）</summary>
    public static void ValidateWriteMultipleEcho(byte[] pdu, byte functionCode, ushort startAddress, ushort quantity)
    {
        if (pdu.Length < 5 || pdu[0] != functionCode
            || ReadUInt16BigEndian(pdu, 1) != startAddress
            || ReadUInt16BigEndian(pdu, 3) != quantity)
            throw new IOException("Modbus 写入响应回显与请求不一致，响应可能已损坏或串包");
    }

    /// <summary>判断响应 PDU 是否为异常响应（功能码最高位置 1），是则取出异常码</summary>
    public static bool IsExceptionResponse(byte[] pdu, out byte originalFunctionCode, out ModbusExceptionCode exceptionCode)
    {
        originalFunctionCode = 0;
        exceptionCode = default;
        if (pdu.Length == 0 || (pdu[0] & ModbusFunctionCode.ExceptionFlag) == 0) return false;

        originalFunctionCode = (byte)(pdu[0] & ~ModbusFunctionCode.ExceptionFlag);
        if (pdu.Length < 2) throw new IOException("Modbus 异常响应 PDU 缺少异常码字节");
        exceptionCode = (ModbusExceptionCode)pdu[1];
        return true;
    }

    // ── 帧长度推算（供 RTU 判帧） ─────────────────────────────────────────────

    /// <summary>
    /// 推算"正常响应"（非异常）的 PDU 字节数。数量类参数（读操作的 quantity）由调用方在发起请求时已知，
    /// 故可提前算出期望长度；写操作的正常响应固定为 5 字节，不依赖 quantity。
    /// </summary>
    public static int GetExpectedNormalResponsePduLength(byte functionCode, ushort quantity) => functionCode switch
    {
        ModbusFunctionCode.ReadCoils or ModbusFunctionCode.ReadDiscreteInputs => 2 + (quantity + 7) / 8,
        ModbusFunctionCode.ReadHoldingRegisters or ModbusFunctionCode.ReadInputRegisters => 2 + quantity * 2,
        ModbusFunctionCode.WriteSingleCoil or ModbusFunctionCode.WriteSingleRegister => 5,
        ModbusFunctionCode.WriteMultipleCoils or ModbusFunctionCode.WriteMultipleRegisters => 5,
        _ => throw new ArgumentOutOfRangeException(nameof(functionCode), functionCode, "不支持的功能码")
    };

    /// <summary>异常响应固定 PDU 长度：[FC|0x80][ExceptionCode]</summary>
    public const int ExceptionResponsePduLength = 2;

    // ── 字节序工具（Modbus PDU 内部字段均为大端序） ───────────────────────────

    private static void WriteUInt16BigEndian(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)(value >> 8);
        buffer[offset + 1] = (byte)value;
    }

    private static ushort ReadUInt16BigEndian(byte[] buffer, int offset)
        => (ushort)((buffer[offset] << 8) | buffer[offset + 1]);
}
