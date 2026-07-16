namespace PF.Core.Events;

/// <summary>
/// Modbus 单次请求/响应事务的原始报文事件参数（完整 ADU 字节：RTU 含地址+PDU+CRC，TCP 含 MBAP 头+PDU），
/// 供调试面板等场景直接展示/复制十六进制报文，与物理链路抓包比对。
/// </summary>
public sealed class ModbusFrameExchangedEventArgs : EventArgs
{
    /// <summary>从站地址</summary>
    public byte UnitId { get; }

    /// <summary>本次发出的完整请求报文</summary>
    public byte[] RequestFrame { get; }

    /// <summary>收到的完整响应报文；超时未收到任何响应时为 null</summary>
    public byte[]? ResponseFrame { get; }

    /// <summary>本次事务是否成功（收到有效的正常响应）</summary>
    public bool Success { get; }

    /// <summary>失败原因（超时/CRC 或 MBAP 校验失败/从站异常响应等），成功时为 null</summary>
    public string? ErrorMessage { get; }

    /// <summary>初始化</summary>
    public ModbusFrameExchangedEventArgs(byte unitId, byte[] requestFrame, byte[]? responseFrame, bool success, string? errorMessage)
    {
        UnitId = unitId;
        RequestFrame = requestFrame;
        ResponseFrame = responseFrame;
        Success = success;
        ErrorMessage = errorMessage;
    }
}
