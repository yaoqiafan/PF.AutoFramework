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
    /// <inheritdoc/>
    public int TimeoutMs { get; set; } = 1000;

    /// <summary>
    /// 执行一次请求/响应事务，返回响应 PDU（已剥离地址域/CRC/MBAP 头）。
    /// expectedResponsePduLength 是"正常（非异常）响应"的期望 PDU 字节数：
    /// RTU 实现靠它判断收满一帧；TCP 实现有 MBAP 长度前缀天然定界，可忽略此参数。
    /// 无响应/超时应抛 <see cref="TimeoutException"/>，帧校验失败（CRC/MBAP 头不匹配）应抛 <see cref="IOException"/>。
    /// </summary>
    protected abstract Task<byte[]> ExecuteAsync(byte unitId, byte[] requestPdu, int expectedResponsePduLength, CancellationToken token);

    private async Task<byte[]> ExecuteAndUnwrapAsync(byte unitId, byte[] requestPdu, int expectedResponsePduLength, CancellationToken token)
    {
        var responsePdu = await ExecuteAsync(unitId, requestPdu, expectedResponsePduLength, token).ConfigureAwait(false);
        if (ModbusPduCodec.IsExceptionResponse(responsePdu, out _, out var exceptionCode))
            throw new ModbusException(unitId, requestPdu[0], exceptionCode);
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
