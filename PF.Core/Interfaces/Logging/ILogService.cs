using PF.Core.Entities.Configuration;
using PF.Core.Entities.Logging;
using PF.Core.Enums;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Logging
{
    /// <summary>
    /// 统一日志服务接口
    /// </summary>
    public interface ILogService
    {
        // 基础日志方法
        void Log(LogLevel level, string message, string category = null, Exception exception = null);
        void Debug(string message, string category = null);
        void Info(string message, string category = null);
        void Success(string message, string category = null);
        void Warn(string message, string category = null, Exception exception = null);
        void Error(string message, string category = null, Exception exception = null);
        void Fatal(string message, string category = null, Exception exception = null);

        // UI相关便捷方法
        void ShowUiMessage(string message, LogLevel level = LogLevel.Info);
        void ShowChatMessage(ChatInfoModel chatInfoModel);

        // 配置
        void Configure(LogConfiguration configuration);
        LogConfiguration GetConfiguration();

        // 内存日志查询功能
        List<LogEntry> QueryLogs(DateTime start, DateTime end, LogLevel? level = null, string category = null);
        List<LogEntry> QueryLogsToday(LogLevel? level = null, string category = null);

        // 历史日志文件查询功能
        List<LogEntry> QueryHistoricalLogs(LogQueryParams queryParams);
        List<LogEntry> QueryAllHistoricalLogs(DateTime start, DateTime end);
        List<LogEntry> QueryAllHistoricalLogs();
        List<LogEntry> QueryInfoHistoricalLogs(DateTime start, DateTime end);
        List<LogEntry> QueryInfoHistoricalLogs();
        List<LogEntry> QueryErrorHistoricalLogs(DateTime start, DateTime end);
        List<LogEntry> QueryErrorHistoricalLogs();
        List<LogEntry> QueryWarnHistoricalLogs(DateTime start, DateTime end);
        List<LogEntry> QueryWarnHistoricalLogs();
        List<LogEntry> QuerySystemHistoricalLogs(DateTime start, DateTime end);
        List<LogEntry> QuerySystemHistoricalLogs();

        // 分类管理
        void AddCategory(string category, LogLevel minLevel = LogLevel.Info, string fileNamePrefix = null);
        void RemoveCategory(string category);
        List<string> GetAllCategories();

        // 事件
        event Action<LogEntry> OnLogAdded;
        // 清空
        void Clear();
        void ClearCategory(string category);
    }
}
