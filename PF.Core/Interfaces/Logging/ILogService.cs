using PF.Core.Entities.Configuration;
using PF.Core.Entities.Logging;
using PF.Core.Enums;
using System;
using System.Collections.Generic;

namespace PF.Core.Interfaces.Logging
{
    /// <summary>
    /// 统一日志服务接口
    /// </summary>
    public interface ILogService
    {
        /// <summary>记录日志</summary>
        void Log(LogLevel level, string message, string category = null, Exception exception = null);
        /// <summary>记录调试日志</summary>
        void Debug(string message, string category = null, Exception exception = null);
        /// <summary>记录信息日志</summary>
        void Info(string message, string category = null);
        /// <summary>记录成功日志</summary>
        void Success(string message, string category = null);
        /// <summary>记录警告日志</summary>
        void Warn(string message, string category = null, Exception exception = null);
        /// <summary>记录错误日志</summary>
        void Error(string message, string category = null, Exception exception = null);
        /// <summary>记录致命错误日志</summary>
        void Fatal(string message, string category = null, Exception exception = null);

        /// <summary>显示UI消息</summary>
        void ShowUiMessage(string message, LogLevel level = LogLevel.Info);
        /// <summary>显示聊天消息</summary>
        void ShowChatMessage(ChatInfoModel chatInfoModel);

        /// <summary>配置日志服务</summary>
        void Configure(LogConfiguration configuration);
        /// <summary>获取日志配置</summary>
        LogConfiguration GetConfiguration();

        /// <summary>内存日志条目</summary>
        IEnumerable<LogEntry> LogEntries { get; }
        /// <summary>聊天消息条目</summary>
        IEnumerable<ChatInfoModel> ChatEntries { get; }

        /// <summary>按时间范围查询内存日志</summary>
        List<LogEntry> QueryLogs(DateTime start, DateTime end, LogLevel? level = null, string category = null);
        /// <summary>查询今天的内存日志</summary>
        List<LogEntry> QueryLogsToday(LogLevel? level = null, string category = null);

        /// <summary>按查询参数查询历史日志</summary>
        List<LogEntry> QueryHistoricalLogs(LogQueryParams queryParams);
        /// <summary>按时间范围查询所有级别历史日志</summary>
        List<LogEntry> QueryAllHistoricalLogs(DateTime start, DateTime end);
        /// <summary>查询所有级别历史日志</summary>
        List<LogEntry> QueryAllHistoricalLogs();
        /// <summary>按时间范围查询Info级别历史日志</summary>
        List<LogEntry> QueryInfoHistoricalLogs(DateTime start, DateTime end);
        /// <summary>查询Info级别历史日志</summary>
        List<LogEntry> QueryInfoHistoricalLogs();
        /// <summary>按时间范围查询Error级别历史日志</summary>
        List<LogEntry> QueryErrorHistoricalLogs(DateTime start, DateTime end);
        /// <summary>查询Error级别历史日志</summary>
        List<LogEntry> QueryErrorHistoricalLogs();
        /// <summary>按时间范围查询Warn级别历史日志</summary>
        List<LogEntry> QueryWarnHistoricalLogs(DateTime start, DateTime end);
        /// <summary>查询Warn级别历史日志</summary>
        List<LogEntry> QueryWarnHistoricalLogs();
        /// <summary>按时间范围查询System级别历史日志</summary>
        List<LogEntry> QuerySystemHistoricalLogs(DateTime start, DateTime end);
        /// <summary>查询System级别历史日志</summary>
        List<LogEntry> QuerySystemHistoricalLogs();

        /// <summary>添加日志分类</summary>
        void AddCategory(string category, LogLevel minLevel = LogLevel.Info, string fileNamePrefix = null);
        /// <summary>移除日志分类</summary>
        void RemoveCategory(string category);
        /// <summary>获取所有分类</summary>
        List<string> GetAllCategories();

        /// <summary>日志添加事件</summary>
        event Action<LogEntry> OnLogAdded;
        /// <summary>清空所有内存日志</summary>
        void Clear();
        /// <summary>清空指定分类的内存日志</summary>
        void ClearCategory(string category);
    }
}
