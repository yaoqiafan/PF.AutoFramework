using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    /// <summary>
    /// 客户端连接状态
    /// </summary>
    public enum ClientStatus
    {
        /// <summary>无状态</summary>
        None,
        /// <summary>连接中</summary>
        Connecting,
        /// <summary>已连接</summary>
        Connected,
        /// <summary>已断开</summary>
        Disconnected,
        /// <summary>错误</summary>
        Error
    }
}
