namespace PF.Core.Constants
{
    /// <summary>
    /// 通讯层常量
    /// </summary>
    public static class CommunicationConstants
    {
        /// <summary>
        /// TCP 单次读取缓冲区大小（64KB）。
        /// TcpServer 与 TCPClient 共用此值，保证两端单次收发能力对等；
        /// 注意：TCP 为字节流，缓冲区对齐只能降低拆包频率，不能消除拆包/粘包，
        /// 超过此大小的消息仍需协议层做拼接分帧处理。
        /// </summary>
        public const int TcpReceiveBufferSize = 64 * 1024;
    }
}
