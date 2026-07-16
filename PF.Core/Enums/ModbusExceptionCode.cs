namespace PF.Core.Enums;

/// <summary>
/// Modbus 标准异常码（Modbus Application Protocol V1.1b3 附录）。
/// 从站响应的功能码最高位置 1 时，紧跟的一个字节即为此码。
/// </summary>
public enum ModbusExceptionCode : byte
{
    /// <summary>非法功能码：从站不支持该功能码</summary>
    IllegalFunction = 0x01,

    /// <summary>非法数据地址：请求的起始地址/数量超出从站支持的范围</summary>
    IllegalDataAddress = 0x02,

    /// <summary>非法数据值：请求中包含的值不被从站接受</summary>
    IllegalDataValue = 0x03,

    /// <summary>从站设备故障：从站在尝试执行请求时发生不可恢复的错误</summary>
    SlaveDeviceFailure = 0x04,

    /// <summary>确认：从站已接受请求，但处理耗时较长，需后续再查询结果</summary>
    Acknowledge = 0x05,

    /// <summary>从站设备忙：从站正在处理长耗时命令，请求方应稍后重试</summary>
    SlaveDeviceBusy = 0x06,

    /// <summary>内存奇偶校验错误：从站在读取扩展文件区时检测到奇偶校验错误</summary>
    MemoryParityError = 0x08,

    /// <summary>网关路径不可用：网关配置错误或网关内部拥塞</summary>
    GatewayPathUnavailable = 0x0A,

    /// <summary>网关目标设备未响应：网关未能从目标设备收到响应</summary>
    GatewayTargetDeviceFailedToRespond = 0x0B
}
