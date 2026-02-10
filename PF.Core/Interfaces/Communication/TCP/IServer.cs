using PF.Core.Enums;
using PF.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Communication.TCP
{
    /// <summary>
    /// 服务器接口定义
    /// </summary>
    public interface IServer
    {

        Encoding Encoding { get; set; }
        /// <summary>
        /// 服务器名称
        /// </summary>
        string ServerName { get; }

        /// <summary>
        /// 服务器状态
        /// </summary>
        ServerStatus Status { get; }

        /// <summary>
        /// 服务器状态
        /// </summary>
        ClientStatus ClientStatue { get; }

        /// <summary>
        /// 监听端口
        /// </summary>
        string IP { get; }

        /// <summary>
        /// 监听端口
        /// </summary>
        int Port { get; }

        /// <summary>
        /// 客户端连接列表
        /// </summary>
        IReadOnlyList<IClientConnection> Clients { get; }

        /// <summary>
        /// 服务器启动事件
        /// </summary>
        event EventHandler<ServerEventArgs> ServerStarted;

        /// <summary>
        /// 服务器停止事件
        /// </summary>
        event EventHandler<ServerEventArgs> ServerStopped;

        /// <summary>
        /// 客户端连接事件
        /// </summary>
        event EventHandler<ClientConnectedEventArgs> ClientConnected;

        /// <summary>
        /// 客户端断开事件
        /// </summary>
        event EventHandler<ClientDisconnectedEventArgs> ClientDisconnected;

        /// <summary>
        /// 接收到数据事件
        /// </summary>
        event EventHandler<DataReceivedEventArgs> DataReceived;

        /// <summary>
        /// 启动服务器
        /// </summary>
        /// <param name="port">监听端口</param>
        /// <param name="backlog">挂起连接队列的最大长度</param>
        /// <returns>启动结果</returns>
        Task<bool> StartAsync(string IP, int port, int backlog = 10);

        /// <summary>
        /// 停止服务器
        /// </summary>
        /// <returns></returns>
        Task StopAsync();

        /// <summary>
        /// 发送数据到指定客户端
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <param name="data">数据</param>
        /// <returns></returns>
        Task<bool> SendAsync(string clientId, byte[] data);

        /// <summary>
        /// 广播数据到所有客户端
        /// </summary>
        /// <param name="data">数据</param>
        /// <returns></returns>
        Task<bool> BroadcastAsync(byte[] data);

        /// <summary>
        /// 断开指定客户端连接
        /// </summary>
        /// <param name="clientId">客户端ID</param>
        /// <returns></returns>
        Task<bool> DisconnectClientAsync(string clientId);
    }
}
