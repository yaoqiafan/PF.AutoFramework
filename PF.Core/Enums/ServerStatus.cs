using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    /// <summary>
    /// 服务器状态枚举
    /// </summary>
    public enum ServerStatus
    {
        Stopped,
        Starting,
        Running,
        Stopping,
        Error
    }
}
