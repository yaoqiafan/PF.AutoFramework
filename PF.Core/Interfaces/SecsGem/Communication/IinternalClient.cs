using PF.Core.Entities.SecsGem.Message;
using PF.Core.Interfaces.Communication.TCP;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.Communication
{
    /// <summary>
    /// 内部 SECS/GEM 客户端接口。
    /// 负责处理底层的 TCP/IP 连接、HSMS 协议的报文收发、以及同步/异步消息链路的管理。
    /// </summary>
    public interface IinternalClient : IDisposable
    {
        /// <summary>
        /// 获取当前 SECS/GEM 客户端的网络连接状态。
        /// </summary>
        /// <value><c>true</c> 表示已连接并准备好通信；否则为 <c>false</c>。</value>
        bool SecsGemStatus { get; }

        /// <summary>
        /// 异步初始化客户端，通常包括加载网络配置（如 IP、端口、Device ID 等）和准备底层 Socket 资源。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果初始化成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> InitializationClient();

        /// <summary>
        /// 异步启动客户端并尝试建立与远端（Host 或 Equipment）的连接。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果连接成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> StartClient();

        /// <summary>
        /// 异步关闭客户端，安全断开现有的 TCP 连接并释放底层网络资源。
        /// </summary>
        /// <returns>返回一个表示异步关闭操作的任务。</returns>
        Task Close();

        /// <summary>
        /// 异步发送一条 SECS/GEM 消息（Primary Message 或不需要回复的 Secondary Message）。
        /// 此方法仅负责发送，不会阻塞等待对方的应答。
        /// </summary>
        /// <param name="msg">要发送的 SECS/GEM 消息实例。</param>
        /// <returns>返回一个表示异步发送操作的任务。</returns>
        Task SendMessage(SecsGemMessage msg);

        /// <summary>
        /// 异步等待与指定 SystemBytes 匹配的回复消息（Secondary Message）。
        /// 常用于发送主动指令后阻塞当前业务流，直到收到设备或上位机的确认。
        /// </summary>
        /// <param name="systemBytesHex">系统字节（SystemBytes）的十六进制字符串，用于唯一关联请求和回复报文。</param>
        /// <param name="timeoutMs">超时时间（毫秒），默认 5000 毫秒（对应 SECS 标准的 T3 超时时间）。</param>
        /// <returns>返回接收到的回复消息 <see cref="SecsGemMessage"/>，如果在指定时间内未收到回复则返回 null 或抛出超时异常。</returns>
        Task<SecsGemMessage> WaitForReplyAsync(string systemBytesHex, int timeoutMs = 5000);

        /// <summary>
        /// 记录 SECS/GEM 交互的底层调试日志（通常用于记录 HSMS 原始字节流或关键链路状态变迁）。
        /// </summary>
        /// <param name="strData">要记录的日志字符串内容。</param>
        void WriteSecsGemLog(string strData);

        /// <summary>
        /// 当底层成功接收并解析出一条完整的 SECS/GEM 消息时触发的事件。
        /// 上层服务（如 ISecsGemManager）可通过订阅此事件来分发和处理进来的指令。
        /// </summary>
        event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;
    }
}