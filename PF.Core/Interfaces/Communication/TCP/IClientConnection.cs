using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Communication.TCP
{
    /// <summary>
    /// 客户端连接接口
    /// </summary>
    public interface IClientConnection
    {
        /// <summary>客户端ID</summary>
        string ClientId { get; }
        /// <summary>远程端点</summary>
        string RemoteEndPoint { get; }
        /// <summary>连接时间</summary>
        DateTime ConnectedTime { get; }
        /// <summary>是否已连接</summary>
        bool IsConnected { get; }
    }
}
