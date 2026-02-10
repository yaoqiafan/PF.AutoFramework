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

        public CategoryLogger(ILogService logService, string category)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _category = category ?? throw new ArgumentNullException(nameof(category));
        }

        public void Debug(string message) => _logService.Debug(message, _category);
        public void Info(string message) => _logService.Info(message, _category);
        public void Success(string message) => _logService.Success(message, _category);
        public void Warn(string message, Exception ex = null) => _logService.Warn(message, _category, ex);
        public void Error(string message, Exception ex = null) => _logService.Error(message, _category, ex);
        public void Fatal(string message, Exception ex = null) => _logService.Fatal(message, _category, ex);

        public void Log(LogLevel level, string message, Exception ex = null) =>
            _logService.Log(level, message, _category, ex);
    }
}
