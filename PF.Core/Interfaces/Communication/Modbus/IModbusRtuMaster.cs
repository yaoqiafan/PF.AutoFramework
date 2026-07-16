using PF.Core.Enums;
using PF.Core.Events;

namespace PF.Core.Interfaces.Communication.Modbus;

/// <summary>
/// Modbus RTU 主站接口。串口是点对点物理连接，没有"连接服务器"的语义，用"打开/关闭串口"，
/// 结构参照 <see cref="PF.Core.Interfaces.Communication.Serial.ISerialCommunication"/>。
/// </summary>
public interface IModbusRtuMaster : IModbusMaster, IDisposable
{
    /// <summary>串口名称（如 "COM3"）</summary>
    string PortName { get; }

    /// <summary>波特率</summary>
    int BaudRate { get; }

    /// <summary>打开状态</summary>
    ClientStatus Status { get; }

    /// <summary>串口打开成功事件</summary>
    event EventHandler? Opened;

    /// <summary>串口关闭事件，携带关闭原因</summary>
    event EventHandler<string>? Closed;

    /// <summary>错误发生事件（含请求超时、CRC 校验失败等协议层错误）</summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>每次请求/响应事务完成（无论成功/失败）时触发，携带完整原始报文，供调试查看</summary>
    event EventHandler<ModbusFrameExchangedEventArgs>? FrameExchanged;

    /// <summary>异步打开串口</summary>
    Task<bool> OpenAsync();

    /// <summary>异步关闭串口</summary>
    Task CloseAsync();
}
