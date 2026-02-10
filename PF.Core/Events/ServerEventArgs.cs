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
        public string Message { get; }
        public DateTime Timestamp { get; }

        public ServerEventArgs(string message)
        {
            Message = message;
            Timestamp = DateTime.Now;
        }
    }
}
