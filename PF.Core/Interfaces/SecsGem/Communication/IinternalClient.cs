using PF.Core.Entities.SecsGem.Message;
using PF.Core.Interfaces.Communication.TCP;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.SecsGem.Communication
{
    public interface IinternalClient : IDisposable
    {
        bool SecsGemStatus { get; }

        /// <summary>
        /// 初始化客户端
        /// </summary>
        Task<bool> InitializationClient();

        /// <summary>
        /// 启动客户端
        /// </summary>
        Task<bool> StartClient();

        /// <summary>
        /// 关闭客户端
        /// </summary>
        Task Close();

        /// <summary>
        /// 发送消息
        /// </summary>
        Task SendMessage(SecsGemMessage msg);

        /// <summary>
        /// 等待回复消息
        /// </summary>
        /// <param name="systemBytesHex">系统字节十六进制字符串</param>
        /// <param name="timeoutMs">超时时间（毫秒）</param>
        /// <returns>回复消息</returns>
        Task<SecsGemMessage> WaitForReplyAsync(string systemBytesHex, int timeoutMs = 5000);

        /// <summary>
        /// 记录SECS/GEM交互日志
        /// </summary>
        /// <param name="strData">日志内容</param>
        void WriteSecsGemLog(string strData);

        event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;
    }
}
