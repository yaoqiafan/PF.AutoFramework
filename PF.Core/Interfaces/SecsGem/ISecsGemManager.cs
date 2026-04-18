using PF.Core.Interfaces.SecsGem.Command;
using PF.Core.Interfaces.SecsGem.Communication;
using PF.Core.Interfaces.SecsGem.Params;
using System;
using System.Threading.Tasks;
using PF.Core.Entities.SecsGem.Message;

namespace PF.Core.Interfaces.SecsGem
{
    /// <summary>
    /// SECS/GEM 主管理器接口。
    /// 负责统筹和管理 SECS/GEM 协议的通信生命周期、配置参数、命令集合以及底层客户端。
    /// </summary>
    public interface ISecsGemManager : IDisposable
    {
        /// <summary>
        /// 异步初始化 SECS/GEM 管理器及其关联的子模块（如参数、命令等）。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果初始化成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> InitializeAsync();

        /// <summary>
        /// 异步建立与 Host 或 Equipment 的 SECS/GEM 连接。
        /// </summary>
        /// <returns>返回一个表示异步操作的任务。如果连接成功，结果为 <c>true</c>；否则为 <c>false</c>。</returns>
        Task<bool> ConnectAsync();

        /// <summary>
        /// 异步断开当前的 SECS/GEM 连接，并释放相关网络资源。
        /// </summary>
        /// <returns>返回一个表示异步断开操作的任务。</returns>
        Task DisconnectAsync();

        /// <summary>
        /// 异步发送一条 SECS/GEM 消息，不等待响应。
        /// </summary>
        /// <param name="message">要发送的 SECS/GEM 消息实例。</param>
        /// <returns>返回一个表示异步发送操作的任务。</returns>
        Task SendMessageAsync(SecsGemMessage message);

        /// <summary>
        /// 异步发送一条 SECS/GEM 消息，并阻塞等待对应的响应消息。
        /// </summary>
        /// <param name="message">要发送的 SECS/GEM 消息实例。</param>
        /// <param name="systemBytesHex">系统字节（SystemBytes）的十六进制字符串，用于匹配发送消息与接收到的响应消息。</param>
        /// <returns>返回一个表示异步操作的任务。如果成功收到对应响应，结果为 <c>true</c>；若超时或失败，结果为 <c>false</c>。</returns>
        Task<bool> WaitSendMessageAsync(SecsGemMessage message, string systemBytesHex);

        /// <summary>
        /// 获取一个值，指示当前是否已成功建立 SECS/GEM 连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 获取 SECS/GEM 参数管理器实例，用于管理设备变量（VID）、状态变量（SVID）等。
        /// </summary>
        IParams ParamsManager { get; }

        /// <summary>
        /// 获取 SECS/GEM 命令管理器实例，用于管理主动命令（Primary）和应答命令（Secondary）。
        /// </summary>
        ICommandManager CommandManager { get; }

        /// <summary>
        /// 获取内部的 SECS/GEM 客户端实例，负责处理底层的 TCP/IP 和 HSMS 报文交互。
        /// </summary>
        IinternalClient SecsGemClient { get; }

        /// <summary>
        /// 获取消息更新器实例，主要用于界面 UI 数据绑定或日志流的消息同步。
        /// </summary>
        ISecsGemMessageUpdater MessageUpdater { get; }

        /// <summary>
        /// 当接收到来自远端的 SECS/GEM 消息时触发的事件。
        /// </summary>
        event EventHandler<SecsMessageReceivedEventArgs> MessageReceived;
    }

    /// <summary>
    /// 连接状态改变事件参数。
    /// 包含连接状态切换前后的布尔值以及发生的时间戳。
    /// </summary>
    public class ConnectionStateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// 获取或设置改变前的连接状态（true 表示已连接，false 表示已断开）。
        /// </summary>
        public bool OldState { get; set; }

        /// <summary>
        /// 获取或设置改变后的新连接状态。
        /// </summary>
        public bool NewState { get; set; }

        /// <summary>
        /// 获取或设置状态发生改变时的时间戳。
        /// </summary>
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// 接收到 SECS/GEM 消息事件参数。
    /// 封装了接收到的完整消息对象和接收时间。
    /// </summary>
    public class SecsMessageReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// 获取或设置接收到的 SECS/GEM 消息。
        /// </summary>
        public SecsGemMessage Message { get; set; }

        /// <summary>
        /// 获取或设置接收到该消息时的时间戳。
        /// </summary>
        public DateTime Timestamp { get; set; }
    }
}