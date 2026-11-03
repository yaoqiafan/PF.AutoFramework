using PF.Core.Entities.Configuration;
using PF.Core.Entities.Logging;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;

namespace HikCameraMinDemo;

/// <summary>
/// <see cref="ILogService"/> 的最小空实现：只往控制台打一行，查询/配置/分类全部空操作。
/// Demo 不引 PF.Services，避免拖进 log4net 与文件日志那套配置。
/// </summary>
internal sealed class ConsoleLogService : ILogService
{
    public void Log(LogLevel level, string message, string category = null, Exception exception = null)
        => Console.WriteLine($"  [{level,-5}] {message}{(exception is null ? "" : $" ({exception.Message})")}");

    public void Debug(string message, string category = null, Exception exception = null) => Log(LogLevel.Debug, message, category, exception);
    public void Info(string message, string category = null) => Log(LogLevel.Info, message, category);
    public void Success(string message, string category = null) => Log(LogLevel.Success, message, category);
    public void Warn(string message, string category = null, Exception exception = null) => Log(LogLevel.Warn, message, category, exception);
    public void Error(string message, string category = null, Exception exception = null) => Log(LogLevel.Error, message, category, exception);
    public void Fatal(string message, string category = null, Exception exception = null) => Log(LogLevel.Fatal, message, category, exception);

    public void Configure(LogConfiguration configuration) { }
    public LogConfiguration GetConfiguration() => new();

    public IEnumerable<LogEntry> LogEntries => Array.Empty<LogEntry>();
    public List<LogEntry> QueryLogs(DateTime start, DateTime end, LogLevel? level = null, string category = null) => new();
    public List<LogEntry> QueryLogsToday(LogLevel? level = null, string category = null) => new();
    public List<LogEntry> QueryHistoricalLogs(LogQueryParams queryParams) => new();
    public List<LogEntry> QueryAllHistoricalLogs(DateTime start, DateTime end) => new();
    public List<LogEntry> QueryAllHistoricalLogs() => new();
    public List<LogEntry> QueryInfoHistoricalLogs(DateTime start, DateTime end) => new();
    public List<LogEntry> QueryInfoHistoricalLogs() => new();
    public List<LogEntry> QueryErrorHistoricalLogs(DateTime start, DateTime end) => new();
    public List<LogEntry> QueryErrorHistoricalLogs() => new();
    public List<LogEntry> QueryWarnHistoricalLogs(DateTime start, DateTime end) => new();
    public List<LogEntry> QueryWarnHistoricalLogs() => new();
    public List<LogEntry> QuerySystemHistoricalLogs(DateTime start, DateTime end) => new();
    public List<LogEntry> QuerySystemHistoricalLogs() => new();

    public void AddCategory(string category, LogLevel minLevel = LogLevel.Info, string fileNamePrefix = null) { }
    public void RemoveCategory(string category) { }
    public List<string> GetAllCategories() => new();

#pragma warning disable CS0067 // 接口要求有这个事件，Demo 永不触发
    public event Action<LogEntry> OnLogAdded;
#pragma warning restore CS0067

    public void Clear() { }
    public void ClearCategory(string category) { }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
