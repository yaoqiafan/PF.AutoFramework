namespace PF.Infrastructure.Communication.Modbus.Internal;

/// <summary>本实现覆盖的 8 个标准 Modbus 功能码</summary>
internal static class ModbusFunctionCode
{
    public const byte ReadCoils = 0x01;
    public const byte ReadDiscreteInputs = 0x02;
    public const byte ReadHoldingRegisters = 0x03;
    public const byte ReadInputRegisters = 0x04;
    public const byte WriteSingleCoil = 0x05;
    public const byte WriteSingleRegister = 0x06;
    public const byte WriteMultipleCoils = 0x0F;
    public const byte WriteMultipleRegisters = 0x10;

    /// <summary>异常响应标志位：从站在原功能码上置此位表示本次请求返回异常</summary>
    public const byte ExceptionFlag = 0x80;
}
