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
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace PF.Infrastructure.Logging
{
    /// <summary>
    /// 高性能统一日志服务
    /// </summary>
    public class LogService : ILogService, IDisposable
    {
        #region 常量
        private const int DEFAULT_BATCH_SIZE = 50;
        private const int DEFAULT_BATCH_INTERVAL_MS = 100;
        private const int MAX_UI_LOG_ENTRIES = 1000;
        private const int MAX_CHAT_ENTRIES = 200;
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

        // UI集合
        private readonly ObservableCollection<LogEntry> _logEntries = new();
        private readonly ObservableCollection<ChatInfoModel> _chatEntries = new();
        private readonly object _uiLock = new object();

        // 生产者-消费者队列
        private readonly BlockingCollection<LogEntry> _logQueue = new(new ConcurrentQueue<LogEntry>());
        private readonly CancellationTokenSource _processingCts = new();
        private Task _processingTask;

        // UI批量队列和定时器
        private readonly ConcurrentQueue<LogEntry> _uiLogQueue = new();
        private readonly ConcurrentQueue<ChatInfoModel> _uiChatQueue = new();
        private readonly Timer _uiBatchTimer;
        private readonly Timer _cleanupTimer;
        private readonly Timer _flushTimer;
        #endregion

        #region 公共属性
        public ObservableCollection<LogEntry> LogEntries => _logEntries;
        public ObservableCollection<ChatInfoModel> ChatEntries => _chatEntries;
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
            _log4netLogger = _categoryLoggers["System"];

            // 启用WPF集合同步
            EnableWpfCollectionSynchronization();

            // 启动处理任务
            StartProcessingTask();

            // 初始化定时器
            _uiBatchTimer = new Timer(
                ProcessUiBatchUpdates,
                null,
                TimeSpan.FromMilliseconds(DEFAULT_BATCH_INTERVAL_MS),
                TimeSpan.FromMilliseconds(DEFAULT_BATCH_INTERVAL_MS));

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

                // 确保目录存在
                if (!Directory.Exists(categoryPath))
                {
                    Directory.CreateDirectory(categoryPath);
                }

                // 构建当天日志文件名
                var currentDate = DateTime.Now;
                string logFileName;
                if (_configuration.SplitByHour)
                {
                    // 按小时：System_2026-01-21-14.log
                    logFileName = $"{config.FileNamePrefix}_{currentDate:yyyy-MM-dd-HH}.log";
                }
                else
                {
                    // 按天：System_2026-01-21.log
                    logFileName = $"{config.FileNamePrefix}_{currentDate:yyyy-MM-dd}.log";
                }

                var logFilePath = Path.Combine(categoryPath, logFileName);

                // 获取该分类的记录器
                var logger = LogManager.GetLogger(category);

                // 创建Appender
                var fileAppender = new FileAppender
                {
                    File = logFilePath,
                    AppendToFile = true,
                    Layout = new PatternLayout("%date [%thread] %-5level - %message%newline"),
                    Threshold = ConvertToLog4NetLevel(config.MinLevel)
                };

                fileAppender.ActivateOptions();

                // 将Appender添加到该记录器
                var loggerImpl = (Logger)logger.Logger;
                loggerImpl.AddAppender(fileAppender);
                loggerImpl.Level = ConvertToLog4NetLevel(config.MinLevel);

                // 存储记录器
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
                // 如果连回退配置都失败，则记录到调试输出
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

        private void EnableWpfCollectionSynchronization()
        {
            try
            {
                BindingOperations.EnableCollectionSynchronization(_logEntries, _uiLock);
                BindingOperations.EnableCollectionSynchronization(_chatEntries, _uiLock);
            }
            catch (InvalidOperationException)
            {
                // 非WPF环境忽略此错误
            }
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

            // 确保目录存在
            EnsureBaseDirectory();

            // 重新配置log4net
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

            Info($"Chat from {chatInfoModel.SenderId}: {chatInfoModel.Message}", CHAT_CATEGORY);
            _uiChatQueue.Enqueue(chatInfoModel);
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

            // 重新配置log4net以包含新分类
            lock (_log4netLock)
            {
                _log4netConfigured = false;
                InitializeLog4Net();
            }
        }

        public void RemoveCategory(string category)
        {
            _configuration.RemoveCategory(category);

            // 重新配置log4net
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
            try
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    lock (_uiLock)
                    {
                        var entriesToRemove = _logEntries
                            .Where(x => x.Category == category)
                            .ToList();

                        foreach (var entry in entriesToRemove)
                        {
                            _logEntries.Remove(entry);
                        }
                    }
                });
            }
            catch
            {
                // 非WPF环境忽略此错误
            }
        }
        #endregion

        #region 查询功能
        public List<LogEntry> QueryLogs(DateTime start, DateTime end, LogLevel? level = null, string category = null)
        {
            lock (_uiLock)
            {
                var query = _logEntries.AsEnumerable()
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
                // 获取日志根目录
                var logBasePath = GetAbsolutePath(_configuration.HistoricalLogPath ?? _configuration.BasePath);

                if (!Directory.Exists(logBasePath))
                    return results;

                // 获取符合条件的目录
                var directories = GetHistoricalLogDirectories(logBasePath, queryParams.StartTime, queryParams.EndTime);

                // 收集需要查询的文件
                var filesToRead = CollectLogFiles(directories, queryParams.Categories);

                // 读取和解析文件
                var allLogs = new ConcurrentBag<LogEntry>();
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2)
                };

                Parallel.ForEach(filesToRead, parallelOptions, file =>
                {
                    // ReadLogFile内部已经处理了异常，直接遍历即可
                    foreach (var logEntry in ReadLogFile(file, queryParams))
                    {
                        allLogs.Add(logEntry);
                    }
                });

                // 排序和限制结果数量
                results = allLogs.ToList();

                if (queryParams.OrderByDescending)
                {
                    results = results.OrderByDescending(log => log.Timestamp).ToList();
                }
                else
                {
                    results = results.OrderBy(log => log.Timestamp).ToList();
                }

                if (results.Count > queryParams.MaxResults)
                {
                    results = results.Take(queryParams.MaxResults).ToList();
                }
            }
            catch (Exception ex)
            {
                Error($"查询历史日志异常: {ex.Message}", SYSTEM_CATEGORY, ex);
            }

            return results;
        }

        public List<LogEntry> QueryAllHistoricalLogs(DateTime start, DateTime end)
        {
            return QueryHistoricalLogs(new LogQueryParams
            {
                StartTime = start,
                EndTime = end
            });
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
                var dateDirs = rootDir.GetDirectories("*", SearchOption.TopDirectoryOnly);

                foreach (var dir in dateDirs)
                {
                    if (DateTime.TryParseExact(dir.Name, "yyyyMMdd",
                        CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dirDate))
                    {
                        if (dirDate.Date >= start.Date && dirDate.Date <= end.Date)
                        {
                            directories.Add(dir);
                        }
                    }
                }

                return directories.OrderBy(d => d.Name).ToList();
            }
            catch (Exception ex)
            {
                Debug($"获取日志目录失败: {ex.Message}", SYSTEM_CATEGORY);
                return directories;
            }
        }

        private List<FileInfo> CollectLogFiles(List<DirectoryInfo> directories, string[]? categories)
        {
            var filesToRead = new List<FileInfo>();

            foreach (var directory in directories)
            {
                var allFiles = directory.GetFiles("*.log", SearchOption.AllDirectories);

                if (categories == null || categories.Length == 0)
                {
                    // 查询所有类型
                    filesToRead.AddRange(allFiles);
                }
                else
                {
                    // 按分类筛选文件
                    foreach (var category in categories)
                    {
                        var config = _configuration.GetCategoryConfig(category);
                        var prefix = config.FileNamePrefix;

                        if (!string.IsNullOrEmpty(prefix))
                        {
                            // 查找符合格式的文件
                            var pattern = $"*.{prefix}.log";
                            filesToRead.AddRange(allFiles.Where(f =>
                                f.Name.Contains($".{prefix}.", StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                }
            }

            // 去重
            return filesToRead.DistinctBy(f => f.FullName).ToList();
        }

        private IEnumerable<LogEntry> ReadLogFile(FileInfo file, LogQueryParams queryParams)
        {
            var logEntries = new List<LogEntry>();

            try
            {
                using var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream, Encoding.UTF8);

                StringBuilder? currentMessage = null;
                DateTime? currentTime = null;
                LogLevel? currentLevel = null;
                var categoryFromFile = ExtractCategoryFromFileName(file.Name);

                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var match = LogLineRegex.Match(line);

                    if (match.Success)
                    {
                        // 添加上一个日志条目
                        if (currentMessage != null && currentTime.HasValue && currentLevel.HasValue)
                        {
                            var entry = CreateLogEntry(currentMessage, currentTime.Value, currentLevel.Value, categoryFromFile);
                            if (ShouldIncludeEntry(entry, queryParams))
                            {
                                logEntries.Add(entry);
                            }
                        }

                        // 开始新的日志条目
                        currentTime = DateTime.ParseExact(match.Groups[1].Value,
                            "yyyy-MM-dd HH:mm:ss,fff", CultureInfo.InvariantCulture);
                        currentLevel = ParseLogLevel(match.Groups[2].Value);
                        currentMessage = new StringBuilder(match.Groups[3].Value.Trim());
                    }
                    else if (currentMessage != null && !string.IsNullOrWhiteSpace(line))
                    {
                        // 多行日志消息
                        currentMessage.AppendLine().Append(line.Trim());
                    }
                }

                // 添加最后一个条目
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
                // 记录文件读取错误，但继续处理其他文件
                System.Diagnostics.Debug.WriteLine($"读取日志文件失败 {file.Name}: {ex.Message}");
                // 如果需要，可以在这里记录到日志服务
                // Debug($"读取日志文件失败 {file.Name}: {ex.Message}", SYSTEM_CATEGORY);
            }

            return logEntries;
        }

        private string ExtractCategoryFromFileName(string fileName)
        {
            // 从文件名中提取分类，如：log2026-01-21.System.log -> System
            // 或 System.current.log -> System
            var parts = fileName.Split('.');
            if (parts.Length >= 2)
            {
                // 如果是滚动文件，如：System.current.log
                if (parts[1] == "current")
                {
                    return parts[0]; // 返回 System
                }
                else
                {
                    return parts[1]; // 返回 System
                }
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
            // 时间过滤
            if (entry.Timestamp < queryParams.StartTime || entry.Timestamp > queryParams.EndTime)
                return false;

            // 日志级别过滤
            if (queryParams.LogLevels != null && queryParams.LogLevels.Length > 0)
            {
                if (!queryParams.LogLevels.Contains(entry.Level))
                    return false;
            }

            // 分类过滤
            if (queryParams.Categories != null && queryParams.Categories.Length > 0)
            {
                if (!queryParams.Categories.Contains(entry.Category))
                    return false;
            }

            // 关键词过滤
            if (!string.IsNullOrEmpty(queryParams.Keyword))
            {
                if (!entry.Message.Contains(queryParams.Keyword, StringComparison.OrdinalIgnoreCase))
                    return false;
            }

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
                    try
                    {
                        ProcessLogEntry(entry);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"日志处理错误: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常退出
            }
        }

        private void ProcessLogEntry(LogEntry entry)
        {
            // 记录到log4net
            RecordToLog4Net(entry);

            // 如果需要UI日志，放入UI队列
            if (_configuration.EnableUiLogging)
            {
                _uiLogQueue.Enqueue(entry);
            }

            // 触发事件
            OnLogAdded?.Invoke(entry);
        }

        private void ProcessLogEntrySync(LogEntry entry)
        {
            RecordToLog4Net(entry);
            OnLogAdded?.Invoke(entry);
        }

        private bool ShouldLog(LogLevel level, string category)
        {
            if (level < _configuration.MinimumLevel)
                return false;

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

                // 尝试获取对应分类的记录器
                lock (_loggersLock)
                {
                    if (_categoryLoggers.TryGetValue(entry.Category, out var categoryLogger))
                    {
                        logger = categoryLogger;
                    }
                }

                // 如果没有找到对应分类的记录器，使用默认记录器
                if (logger == null)
                {
                    logger = _log4netLogger;
                }

                string logMessage = entry.Message;
                if (entry.Exception != null)
                {
                    logMessage = $"{entry.Message} | Exception: {entry.Exception.Message}";
                    if (entry.Exception.StackTrace != null)
                    {
                        logMessage += $"\n{entry.Exception.StackTrace}";
                    }
                }

                switch (entry.Level)
                {
                    case LogLevel.Debug:
                        logger.Debug(logMessage, entry.Exception);
                        break;
                    case LogLevel.Info:
                    case LogLevel.Success:
                        logger.Info(logMessage, entry.Exception);
                        break;
                    case LogLevel.Warn:
                        logger.Warn(logMessage, entry.Exception);
                        break;
                    case LogLevel.Error:
                        logger.Error(logMessage, entry.Exception);
                        break;
                    case LogLevel.Fatal:
                        logger.Fatal(logMessage, entry.Exception);
                        break;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"刷新日志失败: {ex.Message}");
            }
        }
        #endregion

        #region UI批量更新
        private void ProcessUiBatchUpdates(object state)
        {
            ProcessLogUiBatch();
            ProcessChatUiBatch();
        }

        private void ProcessLogUiBatch()
        {
            if (_uiLogQueue.IsEmpty)
                return;

            var batch = new List<LogEntry>();
            while (_uiLogQueue.TryDequeue(out var entry) && batch.Count < DEFAULT_BATCH_SIZE)
            {
                batch.Add(entry);
            }

            if (batch.Count > 0)
            {
                UpdateUiLogs(batch);
            }
        }

        private void ProcessChatUiBatch()
        {
            if (_uiChatQueue.IsEmpty)
                return;

            var batch = new List<ChatInfoModel>();
            while (_uiChatQueue.TryDequeue(out var chat) && batch.Count < DEFAULT_BATCH_SIZE)
            {
                batch.Add(chat);
            }

            if (batch.Count > 0)
            {
                UpdateUiChats(batch);
            }
        }

        private void UpdateUiLogs(List<LogEntry> batch)
        {
            try
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    lock (_uiLock)
                    {
                        foreach (var entry in batch.OrderByDescending(x => x.Timestamp))
                        {
                            _logEntries.Insert(0, entry);
                        }

                        var maxEntries = Math.Max(_configuration.MaxUiEntries, MAX_UI_LOG_ENTRIES);
                        while (_logEntries.Count > maxEntries)
                        {
                            _logEntries.RemoveAt(_logEntries.Count - 1);
                        }
                    }
                }, DispatcherPriority.Background);
            }
            catch
            {
                // 非WPF环境忽略此错误
            }
        }

        private void UpdateUiChats(List<ChatInfoModel> batch)
        {
            try
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    lock (_uiLock)
                    {
                        foreach (var chat in batch)
                        {
                            _chatEntries.Add(chat);
                        }

                        while (_chatEntries.Count > MAX_CHAT_ENTRIES)
                        {
                            _chatEntries.RemoveAt(0);
                        }
                    }
                }, DispatcherPriority.Background);
            }
            catch
            {
                // 非WPF环境忽略此错误
            }
        }

        public void Clear()
        {
            try
            {
                Application.Current?.Dispatcher.InvokeAsync(() =>
                {
                    lock (_uiLock)
                    {
                        _logEntries.Clear();
                        _chatEntries.Clear();
                    }
                });
            }
            catch
            {
                // 非WPF环境忽略此错误
            }
        }
        #endregion

        #region 清理和释放
        private void CleanupOldLogs(object state)
        {
            if (!_configuration.AutoDeleteLogs || _configuration.AutoDeleteIntervalDays < 1)
                return;

            try
            {
                var basePath = GetAbsolutePath(_configuration.BasePath);

                if (!Directory.Exists(basePath))
                    return;

                var cutoffDate = DateTime.Now.AddDays(-_configuration.AutoDeleteIntervalDays);
                CleanupDirectory(basePath, cutoffDate);
            }
            catch (Exception ex)
            {
                Error($"清理旧日志失败: {ex.Message}", SYSTEM_CATEGORY, ex);
            }
        }

        private void CleanupDirectory(string directoryPath, DateTime cutoffDate)
        {
            try
            {
                // 清理子目录
                var directories = Directory.GetDirectories(directoryPath);
                foreach (var dir in directories)
                {
                    CleanupDirectory(dir, cutoffDate);
                }

                // 清理日志文件
                var files = Directory.GetFiles(directoryPath, "*.log");
                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate)
                        {
                            fileInfo.Delete();
                        }
                    }
                    catch
                    {
                        // 忽略无法删除的文件
                    }
                }

                // 删除空目录
                try
                {
                    if (!Directory.EnumerateFileSystemEntries(directoryPath).Any())
                    {
                        Directory.Delete(directoryPath);
                    }
                }
                catch
                {
                    // 忽略无法删除的目录
                }
            }
            catch (Exception ex)
            {
                Debug($"清理目录失败 {directoryPath}: {ex.Message}", SYSTEM_CATEGORY);
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _logQueue.CompleteAdding();
            _processingCts.Cancel();

            try
            {
                _processingTask?.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException) { }

            _uiBatchTimer?.Dispose();
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
