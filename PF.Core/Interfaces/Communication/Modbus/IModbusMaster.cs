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

    /// <summary>
    /// 构建与实际发送完全一致的完整请求帧（RTU：从站地址+PDU+CRC16；TCP：MBAP 头+PDU，
    /// 其中 TransactionId 以 0x0000 占位、实际发送时才分配），仅供报文预览/记录，不产生任何 IO。
    /// </summary>
    byte[] BuildFrame(byte unitId, byte[] requestPdu);

    /// <summary>
    /// 发送原始 PDU（功能码+数据，1~253 字节，不含从站地址/CRC/MBAP 头，成帧由传输层自动完成）
    /// 并等待一帧响应，返回原始响应 PDU。与 8 个内置读写方法不同：不校验功能码回显，也不把从站
    /// 异常响应解包成 ModbusException——异常响应原样返回（首字节最高位置 1），适用于框架未覆盖的
    /// 功能码或自定义诊断报文。RTU 侧因无法预知响应长度，改用报文间静默判帧（响应完整性仍由 CRC
    /// 校验兜底）；TCP 侧仍按 MBAP 头定界并校验 TransactionId/UnitId。超时抛 TimeoutException。
    /// </summary>
    Task<byte[]> SendRawAsync(byte unitId, byte[] requestPdu, CancellationToken token = default);
}
