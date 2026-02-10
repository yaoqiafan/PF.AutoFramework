using log4net;
using log4net.Appender;
using log4net.Core;
using log4net.Layout;
using log4net.Repository.Hierarchy;

using PF.Core.Entities.Configuration;
using PF.Core.Entities.Logging;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PF.Infrastructure.Logging
{
    /// <summary>
    /// 高性能统一日志服务 (无UI依赖版)
    /// </summary>
    public class LogService : ILogService, IDisposable
    {
        #region 常量
        private const int MAX_MEMORY_LOG_ENTRIES = 1000; // 内存中保留的最大日志数
        private const int MAX_MEMORY_CHAT_ENTRIES = 200;
        private const string DEFAULT_CATEGORY = "Default";
        private const string UI_CATEGORY = "UI";
        private const string CHAT_CATEGORY = "Chat";
        private const string SYSTEM_CATEGORY = "System";

        // 历史日志查询正则
        private const string HISTORICAL_LOG_PATTERN = @"(\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2},\d{3})\s+(ERROR|WARN|INFO|DEBUG|FATAL)\s+-\s+(.+)";
        private static readonly Regex LogLineRegex = new Regex(HISTORICAL_LOG_PATTERN,
            RegexOptions.Compiled | RegexOptions.Singleline);
        #endregion

        #region 单例
        private static readonly Lazy<LogService> _instance = new Lazy<LogService>(() => new LogService());
        public static LogService Instance => _instance.Value;
        #endregion

        #region 私有字段
        private readonly ILog _log4netLogger;
        private LogConfiguration _configuration;
        private bool _disposed;
        private bool _log4netConfigured = false;
        private readonly object _log4netLock = new object();
        private readonly Dictionary<string, ILog> _categoryLoggers = new Dictionary<string, ILog>();
        private readonly object _loggersLock = new object();

        // 内存缓存 (替代原来的 ObservableCollection)
        private readonly List<LogEntry> _memoryLogEntries = new List<LogEntry>();
        private readonly List<ChatInfoModel> _memoryChatEntries = new List<ChatInfoModel>();
        private readonly object _memoryLock = new object();

        // 生产者-消费者队列 (用于文件写入)
        private readonly BlockingCollection<LogEntry> _logQueue = new(new ConcurrentQueue<LogEntry>());
        private readonly CancellationTokenSource _processingCts = new();
        private Task _processingTask;

        // 定时器
        private readonly Timer _cleanupTimer;
        private readonly Timer _flushTimer;
        #endregion

        #region 公共属性
        // 注意：接口 ILogService 需要同步修改，将此处类型改为 IEnumerable 或 IReadOnlyList
        // 这里返回副本以保证线程安全
        public IEnumerable<LogEntry> LogEntries
        {
            get
            {
                lock (_memoryLock)
                {
                    return _memoryLogEntries.ToList();
                }
            }
        }

        public IEnumerable<ChatInfoModel> ChatEntries
        {
            get
            {
                lock (_memoryLock)
                {
                    return _memoryChatEntries.ToList();
                }
            }
        }

        public event Action<LogEntry> OnLogAdded;
        #endregion

        #region 构造函数
        public LogService() : this(new LogConfiguration().ConfigureDefaultCategories()) { }

        public LogService(LogConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

            // 确保基础目录存在
            EnsureBaseDirectory();

            // 初始化log4net
            InitializeLog4Net();
            _log4netLogger = _categoryLoggers.ContainsKey("System") ? _categoryLoggers["System"] : LogManager.GetLogger("System");

            // 启动处理任务
            StartProcessingTask();

            // 初始化定时器
            _cleanupTimer = new Timer(
                CleanupOldLogs,
                null,
                TimeSpan.FromHours(1),
                TimeSpan.FromDays(1));

            _flushTimer = new Timer(
                FlushLogs,
                null,
                TimeSpan.FromSeconds(30),
                TimeSpan.FromSeconds(30));
        }

        private void EnsureBaseDirectory()
        {
            try
            {
                var basePath = GetAbsolutePath(_configuration.BasePath);
                if (!Directory.Exists(basePath))
                {
                    Directory.CreateDirectory(basePath);
                }

                // 为每个分类创建子目录
                foreach (var category in _configuration.GetFileLogCategories())
                {
                    var categoryPath = Path.Combine(basePath, category);
                    if (!Directory.Exists(categoryPath))
                    {
                        Directory.CreateDirectory(categoryPath);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建日志目录失败: {ex.Message}");
            }
        }

        private void InitializeLog4Net()
        {
            lock (_log4netLock)
            {
                if (_log4netConfigured)
                {
                    return;
                }

                try
                {
                    var hierarchy = (Hierarchy)LogManager.GetRepository();

                    // 清空现有配置
                    hierarchy.ResetConfiguration();

                    // 配置根记录器
                    hierarchy.Root.Level = Level.All;

                    // 添加控制台Appender
                    if (_configuration.EnableConsoleLogging)
                    {
                        var consoleAppender = CreateConsoleAppender();
                        hierarchy.Root.AddAppender(consoleAppender);
                    }

                    // 清空之前的记录器
                    lock (_loggersLock)
                    {
                        _categoryLoggers.Clear();
                    }

                    // 为每个分类创建独立的记录器和Appender
                    if (_configuration.EnableFileLogging)
                    {
                        foreach (var category in _configuration.GetFileLogCategories())
                        {
                            CreateLoggerForCategory(category);
                        }
                    }

                    // 应用配置
                    hierarchy.Configured = true;
                    _log4netConfigured = true;

                    // 记录初始化日志
                    // 直接调用内部方法，避免触发 LogAdded 事件循环
                    RecordToLog4Net(new LogEntry
                    {
                        Level = LogLevel.Info,
                        Message = "Log4Net初始化完成",
                        Category = SYSTEM_CATEGORY,
                        Timestamp = DateTime.Now
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Log4Net初始化失败: {ex.Message}");
                    ConfigureFallbackLogging();
                }
            }
        }

        private void CreateLoggerForCategory(string category)
        {
            try
            {
                var config = _configuration.GetCategoryConfig(category);
                if (!config.EnableFileLog)
                {
                    return;
                }

                var basePath = GetAbsolutePath(_configuration.BasePath);
                var categoryPath = Path.Combine(basePath, category);

                if (!Directory.Exists(categoryPath))
                {
                    Directory.CreateDirectory(categoryPath);
                }

                var currentDate = DateTime.Now;
                string logFileName;
                if (_configuration.SplitByHour)
                {
                    logFileName = $"{config.FileNamePrefix}_{currentDate:yyyy-MM-dd-HH}.log";
                }
                else
                {
                    logFileName = $"{config.FileNamePrefix}_{currentDate:yyyy-MM-dd}.log";
                }

                var logFilePath = Path.Combine(categoryPath, logFileName);
                var logger = LogManager.GetLogger(category);

                var fileAppender = new FileAppender
                {
                    File = logFilePath,
                    AppendToFile = true,
                    Layout = new PatternLayout("%date [%thread] %-5level - %message%newline"),
                    Threshold = ConvertToLog4NetLevel(config.MinLevel),
                    Encoding = Encoding.UTF8
                };

                fileAppender.ActivateOptions();

                var loggerImpl = (Logger)logger.Logger;
                loggerImpl.AddAppender(fileAppender);
                loggerImpl.Level = ConvertToLog4NetLevel(config.MinLevel);

                lock (_loggersLock)
                {
                    _categoryLoggers[category] = logger;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"创建分类{category}的记录器失败: {ex.Message}");
            }
        }


        private IAppender CreateConsoleAppender()
        {
            var consoleAppender = new ConsoleAppender
            {
                Layout = new PatternLayout("%date [%thread] %-5level [%property{Category}] - %message%newline"),
                Threshold = ConvertToLog4NetLevel(_configuration.MinimumLevel)
            };
            consoleAppender.ActivateOptions();
            return consoleAppender;
        }

        private void ConfigureFallbackLogging()
        {
            try
            {
                var hierarchy = (Hierarchy)LogManager.GetRepository();
                hierarchy.ResetConfiguration();

                var consoleAppender = new ConsoleAppender
                {
                    Layout = new PatternLayout("%date [%thread] %-5level - %message%newline")
                };
                consoleAppender.ActivateOptions();

                hierarchy.Root.AddAppender(consoleAppender);
                hierarchy.Root.Level = Level.All;
                hierarchy.Configured = true;
                _log4netConfigured = true;
            }
            catch
            {
            }
        }

        private string GetAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
        }

        private void StartProcessingTask()
        {
            _processingTask = Task.Factory.StartNew(
                ProcessLogQueue,
                _processingCts.Token,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);
        }
        #endregion

        #region ILogService 实现 - 基础日志方法
        public void Configure(LogConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            EnsureBaseDirectory();
            lock (_log4netLock)
            {
                _log4netConfigured = false;
                InitializeLog4Net();
            }
        }

        public LogConfiguration GetConfiguration()
        {
            return _configuration;
        }

        public void Log(LogLevel level, string message, string category = null, Exception exception = null)
        {
            if (!ShouldLog(level, category))
                return;

            var entry = new LogEntry
            {
                Level = level,
                Message = message,
                Category = category ?? DEFAULT_CATEGORY,
                Exception = exception,
                Timestamp = DateTime.Now
            };

            try
            {
                if (!_logQueue.IsAddingCompleted)
                {
                    _logQueue.Add(entry);
                }
                else
                {
                    ProcessLogEntrySync(entry);
                }
            }
            catch (InvalidOperationException)
            {
                ProcessLogEntrySync(entry);
            }
        }

        public void ShowUiMessage(string message, LogLevel level = LogLevel.Info)
        {
            Log(level, message, UI_CATEGORY);
        }

        public void ShowChatMessage(ChatInfoModel chatInfoModel)
        {
            if (chatInfoModel == null)
                return;

            // 1. 记录到常规日志 (可选，作为Info)
            Info($"Chat from {chatInfoModel.SenderId}: {chatInfoModel.Message}", CHAT_CATEGORY);

            // 2. 添加到聊天内存缓存
            lock (_memoryLock)
            {
                _memoryChatEntries.Add(chatInfoModel);
                while (_memoryChatEntries.Count > MAX_MEMORY_CHAT_ENTRIES)
                {
                    _memoryChatEntries.RemoveAt(0);
                }
            }
        }

        public void Debug(string message, string category = null) => Log(LogLevel.Debug, message, category);
        public void Info(string message, string category = null) => Log(LogLevel.Info, message, category);
        public void Success(string message, string category = null) => Log(LogLevel.Success, message, category);
        public void Warn(string message, string category = null, Exception exception = null) => Log(LogLevel.Warn, message, category, exception);
        public void Error(string message, string category = null, Exception exception = null) => Log(LogLevel.Error, message, category, exception);
        public void Fatal(string message, string category = null, Exception exception = null) => Log(LogLevel.Fatal, message, category, exception);
        #endregion

        #region 分类管理
        public void AddCategory(string category, LogLevel minLevel = LogLevel.Info, string fileNamePrefix = null)
        {
            _configuration.AddCategory(category, minLevel, fileNamePrefix);
            lock (_log4netLock)
            {
                _log4netConfigured = false;
                InitializeLog4Net();
            }
        }

        public void RemoveCategory(string category)
        {
            _configuration.RemoveCategory(category);
            lock (_log4netLock)
            {
                _log4netConfigured = false;
                InitializeLog4Net();
            }
        }

        public List<string> GetAllCategories()
        {
            return _configuration.Categories.Keys.ToList();
        }

        public void ClearCategory(string category)
        {
            lock (_memoryLock)
            {
                _memoryLogEntries.RemoveAll(x => x.Category == category);
            }
        }
        #endregion

        #region 查询功能
        public List<LogEntry> QueryLogs(DateTime start, DateTime end, LogLevel? level = null, string category = null)
        {
            lock (_memoryLock)
            {
                var query = _memoryLogEntries.AsEnumerable()
                    .Where(x => x.Timestamp >= start && x.Timestamp <= end);

                if (level.HasValue)
                    query = query.Where(x => x.Level == level.Value);

                if (!string.IsNullOrEmpty(category))
                    query = query.Where(x => x.Category == category);

                return query.OrderByDescending(x => x.Timestamp).ToList();
            }
        }

        public List<LogEntry> QueryLogsToday(LogLevel? level = null, string category = null)
        {
            var today = DateTime.Today;
            return QueryLogs(today, today.AddDays(1), level, category);
        }

        public List<LogEntry> QueryHistoricalLogs(LogQueryParams queryParams)
        {
            var results = new List<LogEntry>();
            try
            {
                var logBasePath = GetAbsolutePath(_configuration.HistoricalLogPath ?? _configuration.BasePath);
                if (!Directory.Exists(logBasePath))
                    return results;

                var directories = GetHistoricalLogDirectories(logBasePath, queryParams.StartTime, queryParams.EndTime);
                var filesToRead = CollectLogFiles(directories, queryParams.Categories);

                var allLogs = new ConcurrentBag<LogEntry>();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
                };

                Parallel.ForEach(filesToRead, parallelOptions, file =>
                {
                    foreach (var logEntry in ReadLogFile(file, queryParams))
                    {
                        allLogs.Add(logEntry);
                    }
                });

                results = allLogs.ToList();
                if (queryParams.OrderByDescending)
                    results = results.OrderByDescending(log => log.Timestamp).ToList();
                else
                    results = results.OrderBy(log => log.Timestamp).ToList();

                if (results.Count > queryParams.MaxResults)
                    results = results.Take(queryParams.MaxResults).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"查询历史日志异常: {ex.Message}");
            }
            return results;
        }

        public List<LogEntry> QueryAllHistoricalLogs(DateTime start, DateTime end)
        {
            return QueryHistoricalLogs(new LogQueryParams { StartTime = start, EndTime = end });
        }

        public List<LogEntry> QueryAllHistoricalLogs()
        {
            return QueryHistoricalLogs(LogQueryParams.ForToday());
        }

        public List<LogEntry> QueryInfoHistoricalLogs(DateTime start, DateTime end)
        {
            return QueryHistoricalLogs(new LogQueryParams
            {
                StartTime = start,
                EndTime = end,
                LogLevels = new[] { LogLevel.Info, LogLevel.Success }
            });
        }

        public List<LogEntry> QueryInfoHistoricalLogs()
        {
            return QueryInfoHistoricalLogs(DateTime.Today, DateTime.Today.AddDays(1));
        }

        public List<LogEntry> QueryErrorHistoricalLogs(DateTime start, DateTime end)
        {
            return QueryHistoricalLogs(new LogQueryParams
            {
                StartTime = start,
                EndTime = end,
                LogLevels = new[] { LogLevel.Error, LogLevel.Fatal }
            });
        }

        public List<LogEntry> QueryErrorHistoricalLogs()
        {
            return QueryErrorHistoricalLogs(DateTime.Today, DateTime.Today.AddDays(1));
        }

        public List<LogEntry> QueryWarnHistoricalLogs(DateTime start, DateTime end)
        {
            return QueryHistoricalLogs(new LogQueryParams
            {
                StartTime = start,
                EndTime = end,
                LogLevels = new[] { LogLevel.Warn }
            });
        }

        public List<LogEntry> QueryWarnHistoricalLogs()
        {
            return QueryWarnHistoricalLogs(DateTime.Today, DateTime.Today.AddDays(1));
        }

        public List<LogEntry> QuerySystemHistoricalLogs(DateTime start, DateTime end)
        {
            return QueryHistoricalLogs(new LogQueryParams
            {
                StartTime = start,
                EndTime = end,
                LogLevels = new[] { LogLevel.Debug },
                Categories = new[] { "System" }
            });
        }

        public List<LogEntry> QuerySystemHistoricalLogs()
        {
            return QuerySystemHistoricalLogs(DateTime.Today, DateTime.Today.AddDays(1));
        }
        #endregion

        #region 历史日志查询辅助方法
        private List<DirectoryInfo> GetHistoricalLogDirectories(string basePath, DateTime start, DateTime end)
        {
            var directories = new List<DirectoryInfo>();
            try
            {
                var rootDir = new DirectoryInfo(basePath);
                if (rootDir.Exists)
                {
                    var dateDirs = rootDir.GetDirectories("*", SearchOption.TopDirectoryOnly);
                    foreach (var dir in dateDirs)
                    {
                        // 假设目录格式为 yyyyMMdd 或其他可解析的格式
                        // 这里尝试宽松解析，或者您需要根据实际目录命名策略修改
                        // 示例代码中原始逻辑是 yyyyMMdd
                        if (DateTime.TryParseExact(dir.Name, "yyyyMMdd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dirDate))
                        {
                            if (dirDate.Date >= start.Date && dirDate.Date <= end.Date)
                            {
                                directories.Add(dir);
                            }
                        }
                    }
                }
                return directories.OrderBy(d => d.Name).ToList();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"获取日志目录失败: {ex.Message}");
                return directories;
            }
        }

        private List<FileInfo> CollectLogFiles(List<DirectoryInfo> directories, string[] categories)
        {
            var filesToRead = new List<FileInfo>();
            foreach (var directory in directories)
            {
                var allFiles = directory.GetFiles("*.log", SearchOption.AllDirectories);
                if (categories == null || categories.Length == 0)
                {
                    filesToRead.AddRange(allFiles);
                }
                else
                {
                    foreach (var category in categories)
                    {
                        var config = _configuration.GetCategoryConfig(category);
                        var prefix = config.FileNamePrefix;
                        if (!string.IsNullOrEmpty(prefix))
                        {
                            var pattern = $".{prefix}.";
                            filesToRead.AddRange(allFiles.Where(f => f.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                }
            }
            return filesToRead.DistinctBy(f => f.FullName).ToList();
        }

        private IEnumerable<LogEntry> ReadLogFile(FileInfo file, LogQueryParams queryParams)
        {
            var logEntries = new List<LogEntry>();
            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                StringBuilder currentMessage = null;
                DateTime? currentTime = null;
                LogLevel? currentLevel = null;
                var categoryFromFile = ExtractCategoryFromFileName(file.Name);

                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = LogLineRegex.Match(line);
                    if (match.Success)
                    {
                        if (currentMessage != null && currentTime.HasValue && currentLevel.HasValue)
                        {
                            var entry = CreateLogEntry(currentMessage, currentTime.Value, currentLevel.Value, categoryFromFile);
                            if (ShouldIncludeEntry(entry, queryParams))
                            {
                                logEntries.Add(entry);
                            }
                        }
                        currentTime = DateTime.ParseExact(match.Groups[1].Value, "yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture);
                        currentLevel = ParseLogLevel(match.Groups[2].Value);
                        currentMessage = new StringBuilder(match.Groups[3].Value.Trim());
                    }
                    else if (currentMessage != null && !string.IsNullOrWhiteSpace(line))
                    {
                        currentMessage.AppendLine().Append(line.Trim());
                    }
                }

                if (currentMessage != null && currentTime.HasValue && currentLevel.HasValue)
                {
                    var entry = CreateLogEntry(currentMessage, currentTime.Value, currentLevel.Value, categoryFromFile);
                    if (ShouldIncludeEntry(entry, queryParams))
                    {
                        logEntries.Add(entry);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取日志文件失败 {file.Name}: {ex.Message}");
            }
            return logEntries;
        }

        private string ExtractCategoryFromFileName(string fileName)
        {
            var parts = fileName.Split('.');
            if (parts.Length >= 2)
            {
                if (parts[1] == "current") return parts[0];
                return parts[1];
            }
            return DEFAULT_CATEGORY;
        }

        private LogEntry CreateLogEntry(StringBuilder message, DateTime time, LogLevel level, string category)
        {
            return new LogEntry
            {
                Message = message.ToString(),
                Timestamp = time,
                Level = level,
                Category = category
            };
        }

        private bool ShouldIncludeEntry(LogEntry entry, LogQueryParams queryParams)
        {
            if (entry.Timestamp < queryParams.StartTime || entry.Timestamp > queryParams.EndTime) return false;
            if (queryParams.LogLevels != null && queryParams.LogLevels.Length > 0 && !queryParams.LogLevels.Contains(entry.Level)) return false;
            if (queryParams.Categories != null && queryParams.Categories.Length > 0 && !queryParams.Categories.Contains(entry.Category)) return false;
            if (!string.IsNullOrEmpty(queryParams.Keyword) && !entry.Message.Contains(queryParams.Keyword, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        private LogLevel ParseLogLevel(string levelStr)
        {
            return levelStr.ToUpper() switch
            {
                "DEBUG" => LogLevel.Debug,
                "INFO" => LogLevel.Info,
                "WARN" => LogLevel.Warn,
                "ERROR" => LogLevel.Error,
                "FATAL" => LogLevel.Fatal,
                _ => LogLevel.Info
            };
        }
        #endregion

        #region 核心处理逻辑
        private void ProcessLogQueue()
        {
            try
            {
                foreach (var entry in _logQueue.GetConsumingEnumerable(_processingCts.Token))
                {
                    ProcessLogEntry(entry);
                }
            }
            catch (OperationCanceledException) { }
        }

        private void ProcessLogEntry(LogEntry entry)
        {
            // 1. 记录到文件 (log4net)
            RecordToLog4Net(entry);

            // 2. 添加到内存缓存 (仅最近N条)
            lock (_memoryLock)
            {
                _memoryLogEntries.Insert(0, entry);
                while (_memoryLogEntries.Count > MAX_MEMORY_LOG_ENTRIES)
                {
                    _memoryLogEntries.RemoveAt(_memoryLogEntries.Count - 1);
                }
            }

            // 3. 触发事件 (通知 ViewModel)
            OnLogAdded?.Invoke(entry);
        }

        private void ProcessLogEntrySync(LogEntry entry)
        {
            ProcessLogEntry(entry);
        }

        private bool ShouldLog(LogLevel level, string category)
        {
            if (level < _configuration.MinimumLevel) return false;
            category = category ?? DEFAULT_CATEGORY;
            var config = _configuration.GetCategoryConfig(category);
            return level >= config.MinLevel;
        }

        private Level ConvertToLog4NetLevel(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => Level.Debug,
                LogLevel.Info => Level.Info,
                LogLevel.Success => Level.Info,
                LogLevel.Warn => Level.Warn,
                LogLevel.Error => Level.Error,
                LogLevel.Fatal => Level.Fatal,
                _ => Level.Info
            };
        }
        #endregion

        #region Log4Net集成
        private void RecordToLog4Net(LogEntry entry)
        {
            try
            {
                ILog logger = null;
                lock (_loggersLock)
                {
                    if (_categoryLoggers.TryGetValue(entry.Category, out var categoryLogger))
                    {
                        logger = categoryLogger;
                    }
                }
                if (logger == null) logger = _log4netLogger;

                string logMessage = entry.Message;
                if (entry.Exception != null)
                {
                    logMessage = $"{entry.Message} | Exception: {entry.Exception.Message}";
                    if (entry.Exception.StackTrace != null) logMessage += $"\n{entry.Exception.StackTrace}";
                }

                switch (entry.Level)
                {
                    case LogLevel.Debug: logger.Debug(logMessage, entry.Exception); break;
                    case LogLevel.Info:
                    case LogLevel.Success: logger.Info(logMessage, entry.Exception); break;
                    case LogLevel.Warn: logger.Warn(logMessage, entry.Exception); break;
                    case LogLevel.Error: logger.Error(logMessage, entry.Exception); break;
                    case LogLevel.Fatal: logger.Fatal(logMessage, entry.Exception); break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Log4Net记录失败: {ex.Message}");
            }
        }

        private void FlushLogs(object state)
        {
            try
            {
                var repository = LogManager.GetRepository() as Hierarchy;
                if (repository != null)
                {
                    foreach (var appender in repository.GetAppenders())
                    {
                        if (appender is BufferingAppenderSkeleton bufferingAppender)
                        {
                            bufferingAppender.Flush();
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region 清理和释放
        public void Clear()
        {
            lock (_memoryLock)
            {
                _memoryLogEntries.Clear();
                _memoryChatEntries.Clear();
            }
        }

        private void CleanupOldLogs(object state)
        {
            if (!_configuration.AutoDeleteLogs || _configuration.AutoDeleteIntervalDays < 1) return;
            try
            {
                var basePath = GetAbsolutePath(_configuration.BasePath);
                if (Directory.Exists(basePath))
                {
                    var cutoffDate = DateTime.Now.AddDays(-_configuration.AutoDeleteIntervalDays);
                    CleanupDirectory(basePath, cutoffDate);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"清理旧日志失败: {ex.Message}");
            }
        }

        private void CleanupDirectory(string directoryPath, DateTime cutoffDate)
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(directoryPath))
                    CleanupDirectory(dir, cutoffDate);

                foreach (var file in Directory.GetFiles(directoryPath, "*.log"))
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate) fileInfo.Delete();
                    }
                    catch { }
                }

                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
                        Directory.Delete(directoryPath);
                }
                catch { }
            }
            catch { }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _logQueue.CompleteAdding();
            _processingCts.Cancel();
            try { _processingTask?.Wait(TimeSpan.FromSeconds(5)); } catch { }
            _cleanupTimer?.Dispose();
            _flushTimer?.Dispose();
            _processingCts.Dispose();
            _logQueue.Dispose();
            _disposed = true;
            GC.SuppressFinalize(this);
        }
        #endregion

        #region 工厂方法
        public static class Factory
        {
            private static ILogService _service;
            private static readonly object _lock = new();

            public static ILogService GetService(LogConfiguration configuration = null)
            {
                lock (_lock)
                {
                    if (_service == null)
                    {
                        _service = new LogService(configuration ?? new LogConfiguration().ConfigureDefaultCategories());
                    }
                    else if (configuration != null)
                    {
                        _service.Configure(configuration);
                    }
                    return _service;
                }
            }
        }
        #endregion
    }
}