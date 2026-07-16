namespace PF.Core.Interfaces.Communication.Modbus;

/// <summary>
/// Modbus 主站（Master）操作契约。RTU 与 TCP 在功能码/数据模型（PDU）上完全一致，只有成帧方式不同，
/// 因此共享这一份操作接口；<see cref="IModbusRtuMaster"/>/<see cref="IModbusTcpMaster"/> 分别在此基础上
/// 补充各自的传输专属连接语义，对照
/// <see cref="PF.Core.Interfaces.Communication.TCP.IClient"/>/<see cref="PF.Core.Interfaces.Communication.Serial.ISerialCommunication"/>
/// 按传输拆分接口、不强行合并的既定方式。
///
/// 仅实现 Master（客户端）角色，且只覆盖 8 个最常用功能码：01/02/03/04/05/06/0F/10。
/// 从站返回异常响应（功能码最高位置 1）时抛出 <see cref="PF.Core.Exceptions.ModbusException"/>；
/// 请求超时或响应校验失败（CRC/MBAP 头不匹配等）抛出 <see cref="TimeoutException"/> 或 <see cref="IOException"/>。
/// </summary>
public interface IModbusMaster
{
    /// <summary>单次请求的响应超时（毫秒）</summary>
    int TimeoutMs { get; set; }

    /// <summary>读线圈（功能码 01）</summary>
    Task<bool[]> ReadCoilsAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default);

    /// <summary>读离散量输入（功能码 02）</summary>
    Task<bool[]> ReadDiscreteInputsAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default);

    /// <summary>读保持寄存器（功能码 03）</summary>
    Task<ushort[]> ReadHoldingRegistersAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default);

    /// <summary>读输入寄存器（功能码 04）</summary>
    Task<ushort[]> ReadInputRegistersAsync(byte unitId, ushort startAddress, ushort quantity, CancellationToken token = default);

    /// <summary>写单个线圈（功能码 05）</summary>
    Task WriteSingleCoilAsync(byte unitId, ushort address, bool value, CancellationToken token = default);

    /// <summary>写单个寄存器（功能码 06）</summary>
    Task WriteSingleRegisterAsync(byte unitId, ushort address, ushort value, CancellationToken token = default);

    /// <summary>写多个线圈（功能码 0F）</summary>
    Task WriteMultipleCoilsAsync(byte unitId, ushort startAddress, bool[] values, CancellationToken token = default);

    /// <summary>写多个寄存器（功能码 10）</summary>
    Task WriteMultipleRegistersAsync(byte unitId, ushort startAddress, ushort[] values, CancellationToken token = default);
}
