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
        string ClientId { get; }
        string RemoteEndPoint { get; }
        DateTime ConnectedTime { get; }
        bool IsConnected { get; }
    }
}
