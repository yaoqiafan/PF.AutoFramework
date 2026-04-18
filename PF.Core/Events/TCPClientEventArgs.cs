using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    /// <summary>
    /// 客户端连接事件参数
    /// </summary>
    public class ClientConnectedEventArgs : EventArgs
    {
        /// <summary>客户端ID</summary>
        public string ClientId { get; }
        /// <summary>服务器地址</summary>
        public string ServerAddress { get; }
        /// <summary>连接时间</summary>
        public DateTime ConnectTime { get; }

        /// <summary>
        /// 初始化客户端连接事件参数
        /// </summary>
        public ClientConnectedEventArgs(string clientId, string serverAddress)
        {
            ClientId = clientId;
            ServerAddress = serverAddress;
            ConnectTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 客户端断开事件参数
    /// </summary>
    public class ClientDisconnectedEventArgs : EventArgs
    {
        /// <summary>客户端ID</summary>
        public string ClientId { get; }
        /// <summary>断开原因</summary>
        public string Reason { get; }
        /// <summary>断开时间</summary>
        public DateTime DisconnectTime { get; }

        /// <summary>
        /// 初始化客户端断开事件参数
        /// </summary>
        public ClientDisconnectedEventArgs(string clientId, string reason)
        {
            ClientId = clientId;
            Reason = reason;
            DisconnectTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 数据接收事件参数
    /// </summary>
    public class DataReceivedEventArgs : EventArgs
    {
        /// <summary>客户端ID</summary>
        public string ClientId { get; }
        /// <summary>接收的数据</summary>
        public byte[] Data { get; }
        /// <summary>接收时间</summary>
        public DateTime ReceiveTime { get; }

        /// <summary>
        /// 初始化数据接收事件参数
        /// </summary>
        public DataReceivedEventArgs(string clientId, byte[] data)
        {
            ClientId = clientId;
            Data = data;
            ReceiveTime = DateTime.Now;
        }
    }

    /// <summary>
    /// 错误发生事件参数
    /// </summary>
    public class ErrorOccurredEventArgs : EventArgs
    {
        /// <summary>客户端ID</summary>
        public string ClientId { get; }
        /// <summary>错误消息</summary>
        public string ErrorMessage { get; }
        /// <summary>异常对象</summary>
        public Exception Exception { get; }
        /// <summary>错误发生时间</summary>
        public DateTime ErrorTime { get; }

        /// <summary>
        /// 初始化错误发生事件参数
        /// </summary>
        public ErrorOccurredEventArgs(string clientId, string errorMessage, Exception exception)
        {
            ClientId = clientId;
            ErrorMessage = errorMessage;
            Exception = exception;
            ErrorTime = DateTime.Now;
        }
    }
}
