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
        /// <summary>显示所有级别</summary>
        All = -1,
        /// <summary>调试级别</summary>
        Debug = 0,
        /// <summary>信息级别</summary>
        Info = 1,
        /// <summary>成功级别</summary>
        Success = 2,
        /// <summary>警告级别</summary>
        Warn = 3,
        /// <summary>错误级别</summary>
        Error = 4,
        /// <summary>致命级别</summary>
        Fatal = 5
    }
}
