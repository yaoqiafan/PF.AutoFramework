using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Logging
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }
        /// <summary>日志级别</summary>
        public LogLevel Level { get; set; }
        /// <summary>日志消息</summary>
        public string Message { get; set; }
        /// <summary>日志分类</summary>
        public string Category { get; set; }
        /// <summary>异常信息</summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// 初始化日志条目
        /// </summary>
        public LogEntry()
        {
            Timestamp = DateTime.Now;
        }
    }
}
