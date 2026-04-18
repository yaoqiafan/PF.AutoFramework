using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    /// <summary>
    /// 服务器事件参数
    /// </summary>
    public class ServerEventArgs : EventArgs
    {
        /// <summary>消息内容</summary>
        public string Message { get; }
        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; }

        /// <summary>
        /// 初始化服务器事件参数
        /// </summary>
        public ServerEventArgs(string message)
        {
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
}
