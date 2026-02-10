using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        All = -1,      // 新增：显示所有级别
        Debug = 0,
        Info = 1,
        Success = 2,
        Warn = 3,
        Error = 4,
        Fatal = 5
    }
}
