using PF.Core.Enums;

namespace PF.Core.Exceptions;

/// <summary>
/// 从站针对某次请求返回 Modbus 异常响应（功能码最高位置 1）时抛出。
/// </summary>
public sealed class ModbusException : Exception
{
    /// <summary>从站地址</summary>
    public byte UnitId { get; }

    /// <summary>原始请求的功能码（已去除异常标志位）</summary>
    public byte FunctionCode { get; }

    /// <summary>从站返回的异常码</summary>
    public ModbusExceptionCode ExceptionCode { get; }

    /// <summary>初始化 Modbus 异常</summary>
    public ModbusException(byte unitId, byte functionCode, ModbusExceptionCode exceptionCode)
        : base($"Modbus 从站 {unitId} 对功能码 0x{functionCode:X2} 返回异常：{exceptionCode} (0x{(byte)exceptionCode:X2})")
    {
        UnitId = unitId;
        FunctionCode = functionCode;
        ExceptionCode = exceptionCode;
    }
}
