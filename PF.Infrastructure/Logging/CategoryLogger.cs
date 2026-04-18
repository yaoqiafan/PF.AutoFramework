using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Logging
{
    /// <summary>
    /// 分类日志记录器
    /// </summary>
    public class CategoryLogger
    {
        private readonly ILogService _logService;
        private readonly string _category;

        /// <summary>
        /// 构造分类日志记录器
        /// </summary>
        public CategoryLogger(ILogService logService, string category)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _category = category ?? throw new ArgumentNullException(nameof(category));
        }

        /// <summary>
        /// 记录调试级别日志
        /// </summary>
        public void Debug(string message,Exception ex=null ) => _logService.Debug(message, _category,ex );
        /// <summary>
        /// 记录信息级别日志
        /// </summary>
        public void Info(string message) => _logService.Info(message, _category);
        /// <summary>
        /// 记录成功级别日志
        /// </summary>
        public void Success(string message) => _logService.Success(message, _category);
        /// <summary>
        /// 记录警告级别日志
        /// </summary>
        public void Warn(string message, Exception ex = null) => _logService.Warn(message, _category, ex);
        /// <summary>
        /// 记录错误级别日志
        /// </summary>
        public void Error(string message, Exception ex = null) => _logService.Error(message, _category, ex);
        /// <summary>
        /// 记录致命级别日志
        /// </summary>
        public void Fatal(string message, Exception ex = null) => _logService.Fatal(message, _category, ex);

        /// <summary>
        /// 记录指定级别的日志
        /// </summary>
        public void Log(LogLevel level, string message, Exception ex = null) =>
            _logService.Log(level, message, _category, ex);
    }
}
