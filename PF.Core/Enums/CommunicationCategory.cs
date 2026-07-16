namespace PF.Core.Enums;

/// <summary>通讯实例分类，驱动调试面板树的一级分组。以后新增协议（Modbus等）在此加值即可</summary>
public enum CommunicationCategory
{
    /// <summary>TCP Server/Client</summary>
    Tcp,

    /// <summary>大文件字节数组传输通道</summary>
    FileTransfer,

    /// <summary>串口通讯</summary>
    Serial,

    /// <summary>Modbus 主站（RTU/TCP）</summary>
    Modbus
}
