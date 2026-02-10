using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Entities.Logging
{
    /// <summary>
    /// 历史日志查询参数
    /// </summary>
    public class LogQueryParams
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public LogLevel[]? LogLevels { get; set; }
        public string[]? Categories { get; set; }
        public string? Keyword { get; set; }
        public int MaxResults { get; set; } = 10000;
        public bool OrderByDescending { get; set; } = true;

        /// <summary>
        /// 创建当天查询参数
        /// </summary>
        public static LogQueryParams ForToday(LogLevel[]? levels = null, string[]? categories = null)
        {
            var today = DateTime.Today;
            return new LogQueryParams
            {
                StartTime = today,
                EndTime = today.AddDays(1),
                LogLevels = levels,
                Categories = categories
            };
        }

        /// <summary>
        /// 创建最近N天查询参数
        /// </summary>
        public static LogQueryParams ForLastDays(int days, LogLevel[]? levels = null, string[]? categories = null)
        {
            return new LogQueryParams
            {
                StartTime = DateTime.Today.AddDays(-days),
                EndTime = DateTime.Today.AddDays(1),
                LogLevels = levels,
                Categories = categories
            };
        }
    }
}
