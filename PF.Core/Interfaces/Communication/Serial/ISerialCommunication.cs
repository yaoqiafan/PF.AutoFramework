using PF.Core.Enums;
using PF.Core.Events;
using System.Text;

namespace PF.Core.Interfaces.Communication.Serial;

/// <summary>
/// 串口通讯接口。结构参考 <see cref="PF.Core.Interfaces.Communication.TCP.IClient"/>，
/// 但串口是点对点物理连接，没有"连接服务器"的语义，换成"打开/关闭串口"。
/// </summary>
public interface ISerialCommunication : IDisposable
{
    /// <summary>串口名称（如 "COM3"）</summary>
    string PortName { get; }

    /// <summary>波特率</summary>
    int BaudRate { get; }

    /// <summary>打开状态</summary>
    ClientStatus Status { get; }

    /// <summary>编码方式，SendStringAsync 按此编码转换字符串，默认 Encoding.ASCII</summary>
    Encoding Encoding { get; set; }

    /// <summary>串口打开成功事件</summary>
    event EventHandler? Opened;

    /// <summary>串口关闭事件，携带关闭原因</summary>
    event EventHandler<string>? Closed;

    /// <summary>数据接收事件</summary>
    event EventHandler<DataReceivedEventArgs>? DataReceived;

    /// <summary>错误发生事件</summary>
    event EventHandler<ErrorOccurredEventArgs>? ErrorOccurred;

    /// <summary>异步打开串口</summary>
    Task<bool> OpenAsync();

    /// <summary>异步关闭串口</summary>
    Task CloseAsync();

    /// <summary>异步发送字节数据</summary>
    Task<bool> SendAsync(byte[] data);

    /// <summary>异步发送字符串数据（按 Encoding 属性转换）</summary>
    Task<bool> SendStringAsync(string data);
}
