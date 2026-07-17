using PF.Core.Exceptions;
using PF.Core.Interfaces.Communication.Modbus;

namespace PF.Infrastructure.Communication.Modbus.Internal;

/// <summary>
/// <see cref="IModbusMaster"/> 的公共实现：8 个功能码的请求构建/响应解析全部走 <see cref="ModbusPduCodec"/>，
/// 子类（RTU/TCP）只需实现 <see cref="ExecuteAsync"/> 这一个"发请求、收一帧完整响应 PDU"的传输原语。
/// 公开（public）仅因为 C# 不允许 public 的 ModbusRtuMaster/ModbusTcpMaster 继承可访问性更低的基类；
/// 本类不对外暴露任何独立于 <see cref="IModbusMaster"/> 契约之外的成员，不作为公共 API 使用。
/// </summary>
public abstract class ModbusMasterBase : IModbusMaster
{
    private int _timeoutMs = 1000;

    /// <inheritdoc/>
    public int TimeoutMs
    {
        get => _timeoutMs;
        // 0 会让每次请求立即超时，负数（-1 除外）会让 CancellationTokenSource 构造直接抛异常，都不是合法配置
        set => _timeoutMs = value > 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "TimeoutMs 必须为正数");
    }

    /// <summary>
    /// <see cref="SendRawAsync"/> 传给 <see cref="ExecuteAsync"/> 的哨兵值：期望响应长度未知，
    /// RTU 实现改用报文间静默判帧，TCP 实现本就按 MBAP 头定界、不受影响。
    /// </summary>
    protected const int UnknownResponseLength = -1;

    /// <summary>
    /// 执行一次请求/响应事务，返回响应 PDU（已剥离地址域/CRC/MBAP 头）。
    /// expectedResponsePduLength 是"正常（非异常）响应"的期望 PDU 字节数：
    /// RTU 实现靠它判断收满一帧；TCP 实现有 MBAP 长度前缀天然定界，可忽略此参数；
    /// 传 <see cref="UnknownResponseLength"/> 表示长度未知（原始透传），RTU 需改用静默判帧。
    /// 无响应/超时应抛 <see cref="TimeoutException"/>，帧校验失败（CRC/MBAP 头不匹配）应抛 <see cref="IOException"/>。
    /// </summary>
    protected abstract Task<byte[]> ExecuteAsync(byte unitId, byte[] requestPdu, int expectedResponsePduLength, CancellationToken token);

    /// <inheritdoc/>
    public abstract byte[] BuildFrame(byte unitId, byte[] requestPdu);

    /// <inheritdoc/>
    public Task<byte[]> SendRawAsync(byte unitId, byte[] requestPdu, CancellationToken token = default)
    {
        if (requestPdu == null || requestPdu.Length is 0 or > 253)
            throw new ArgumentOutOfRangeException(nameof(requestPdu), "原始 PDU 长度必须在 1~253 字节之间");
        // 不经 ExecuteAndUnwrapAsync：透传模式不校验功能码回显、不解包异常响应，响应 PDU 原样返回
        return ExecuteAsync(unitId, requestPdu, UnknownResponseLength, token);
    }

    private async Task<byte[]> ExecuteAndUnwrapAsync(byte unitId, byte[] requestPdu, int expectedResponsePduLength, CancellationToken token)
    {
        var responsePdu = await ExecuteAsync(unitId, requestPdu, expectedResponsePduLength, token).ConfigureAwait(false);
        if (ModbusPduCodec.IsExceptionResponse(responsePdu, out var originalFunctionCode, out var exceptionCode))
        {
            // 异常响应携带的原功能码也必须与请求一致——不一致说明是迟到/串包的错帧，按帧错误处理而非从站异常
            if (originalFunctionCode != requestPdu[0])
                throw new IOException($"Modbus 异常响应功能码 0x{originalFunctionCode:X2} 与请求 0x{requestPdu[0]:X2} 不一致，响应可能已损坏或串包。");
            throw new ModbusException(unitId, requestPdu[0], exceptionCode);
        }

        // 功能码回显统一在此校验（读/写一起兜住）：写响应的回显校验里查过功能码，但读响应的
        // ParseCoilsResponse/ParseRegistersResponse 只查 ByteCount——长度恰好相同的错帧会被当正常数据接受
        if (responsePdu.Length == 0 || responsePdu[0] != requestPdu[0])
            throw new IOException($"Modbus 响应功能码与请求 0x{requestPdu[0]:X2} 不一致，响应可能已损坏或串包。");

        return responsePdu;
    }

    /// <inheritdoc/>
    public async Task<bool[]> ReadCoilsAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildReadRequest(ModbusFunctionCode.ReadCoils, startAddress, quantity);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.ReadCoils, quantity);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        return ModbusPduCodec.ParseCoilsResponse(response, quantity);
    }

    /// <inheritdoc/>
    public async Task<bool[]> ReadDiscreteInputsAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildReadRequest(ModbusFunctionCode.ReadDiscreteInputs, startAddress, quantity);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.ReadDiscreteInputs, quantity);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        return ModbusPduCodec.ParseCoilsResponse(response, quantity);
    }

    /// <inheritdoc/>
    public async Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildReadRequest(ModbusFunctionCode.ReadHoldingRegisters, startAddress, quantity);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.ReadHoldingRegisters, quantity);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        return ModbusPduCodec.ParseRegistersResponse(response, quantity);
    }

    /// <inheritdoc/>
    public async Task<ushort[]> ReadInputRegistersAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildReadRequest(ModbusFunctionCode.ReadInputRegisters, startAddress, quantity);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.ReadInputRegisters, quantity);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        return ModbusPduCodec.ParseRegistersResponse(response, quantity);
    }

    /// <inheritdoc/>
    public async Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildWriteSingleCoil(address, value);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.WriteSingleCoil, 0);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        ModbusPduCodec.ValidateWriteSingleEcho(response, ModbusFunctionCode.WriteSingleCoil, address, value ? (ushort)0xFF00 : (ushort)0x0000);
    }

    /// <inheritdoc/>
    public async Task WriteSingleRegisterAsync(byte unitId, ushort address, ushort value, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildWriteSingleRegister(address, value);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.WriteSingleRegister, 0);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        ModbusPduCodec.ValidateWriteSingleEcho(response, ModbusFunctionCode.WriteSingleRegister, address, value);
    }

    /// <inheritdoc/>
    public async Task WriteMultipleCoilsAsync(byte unitId, ushort startAddress, bool[] values, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildWriteMultipleCoils(startAddress, values);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.WriteMultipleCoils, 0);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        ModbusPduCodec.ValidateWriteMultipleEcho(response, ModbusFunctionCode.WriteMultipleCoils, startAddress, (ushort)values.Length);
    }

    /// <inheritdoc/>
    public async Task WriteMultipleRegistersAsync(byte unitId, ushort startAddress, ushort[] values, CancellationToken token = default)
    {
        var pdu = ModbusPduCodec.BuildWriteMultipleRegisters(startAddress, values);
        var expected = ModbusPduCodec.GetExpectedNormalResponsePduLength(ModbusFunctionCode.WriteMultipleRegisters, 0);
        var response = await ExecuteAndUnwrapAsync(unitId, pdu, expected, token).ConfigureAwait(false);
        ModbusPduCodec.ValidateWriteMultipleEcho(response, ModbusFunctionCode.WriteMultipleRegisters, startAddress, (ushort)values.Length);
    }
}
