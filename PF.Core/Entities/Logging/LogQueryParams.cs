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
        /// <summary>查询起始时间</summary>
        public DateTime StartTime { get; set; }
        /// <summary>查询结束时间</summary>
        public DateTime EndTime { get; set; }
        /// <summary>日志级别过滤</summary>
        public LogLevel[]? LogLevels { get; set; }
        /// <summary>分类过滤</summary>
        public string[]? Categories { get; set; }
        /// <summary>关键词过滤</summary>
        public string? Keyword { get; set; }
        /// <summary>最大返回数量</summary>
        public int MaxResults { get; set; } = 10000;
        /// <summary>是否按降序排列</summary>
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
