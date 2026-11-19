using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PF.UI.Infrastructure.PrismBase;
using PF.Core.Entities.Logging;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PF.UI.Infrastructure.Dialog.Basic;
using PF.Core.Constants;
using PF.Infrastructure.Logging;

namespace PF.Modules.Logging.ViewModels
{
    /// <summary>日志列表 ViewModel</summary>
    public class LogListViewModel : ViewModelBase
    {
        private readonly ILogService _logService;
        private readonly CategoryLogger _uiLogger;
        private readonly ICollectionView _logEntriesView;
        private string _searchText;
        private LogLevel? _selectedLogLevel;
        private bool _autoScroll = true;
        private int _totalLogCount;
        private int _filteredLogCount;
        private bool _isSubscribed;
        private bool _needsResync;

        /// <summary>初始化日志列表 ViewModel</summary>
        public LogListViewModel()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();
            _uiLogger = CategoryLoggerFactory.UI(_logService);
            // 初始化日志集合 - 从服务中加载现有日志
            // LogService 内部是 List，0 是最新的，这里我们直接加载
            if (_logService.LogEntries != null)
            {
                LogEntries = new ObservableCollection<LogEntry>(_logService.LogEntries);
            }
            else
            {
                LogEntries = new ObservableCollection<LogEntry>();
            }

            // 初始化命令
            ClearLogsCommand = new DelegateCommand(ClearLogs);
            ExportLogsCommand = new DelegateCommand(ExportLogs);
            CopySelectedCommand = new DelegateCommand(CopySelected);
            CopyAllCommand = new DelegateCommand(CopyAll);
            RefreshCommand = new DelegateCommand(Refresh);

            // 设置集合视图以支持筛选和排序
            _logEntriesView = CollectionViewSource.GetDefaultView(LogEntries);
            _logEntriesView.Filter = LogEntriesFilter;
            _logEntriesView.SortDescriptions.Add(
                new SortDescription("Timestamp", ListSortDirection.Descending));

            // 订阅日志添加事件
            Subscribe();

            // 初始化日志级别选项（包括"全部"）
            LogLevels = new List<LogLevel?>();
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                LogLevels.Add(level);
            }

            SelectedLogLevel = LogLevel.All;

            // 更新统计信息
            UpdateStatistics();

        }

        #region 属性

        /// <summary>获取日志集合视图</summary>
        public ICollectionView LogEntriesView => _logEntriesView;

        /// <summary>获取或设置日志条目集合</summary>
        public ObservableCollection<LogEntry> LogEntries { get; set; }

        private LogEntry _selectedLogEntry;

        // 选中项属性
        /// <summary>获取或设置选中的日志条目</summary>
        public LogEntry SelectedLogEntry
        {
            get => _selectedLogEntry;
            set
            {
                if (SetProperty(ref _selectedLogEntry, value))
                {
                    // 选中项改变时触发其他逻辑
                    OnSelectedLogEntryChanged();
                }
            }
        }

        private void OnSelectedLogEntryChanged()
        {
            // 可以在这里处理选中逻辑，但不要再写 Debug 日志，否则可能导致死循环或刷屏
        }

        /// <summary>获取或设置搜索文本</summary>
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    _logEntriesView.Refresh();
                    UpdateStatistics();
                }
            }
        }

        /// <summary>获取或设置选中的日志级别</summary>
        public LogLevel? SelectedLogLevel
        {
            get => _selectedLogLevel;
            set
            {
                if (SetProperty(ref _selectedLogLevel, value))
                {
                    _logEntriesView.Refresh();
                    UpdateStatistics();
                }
            }
        }

        /// <summary>获取或设置是否自动滚动（列表按时间倒序，跟随即停在顶部最新一条）</summary>
        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        /// <summary>新日志已插入列表后触发（UI 线程），供 View 执行自动滚动</summary>
        public event Action LogAppended;

        /// <summary>获取日志总数</summary>
        public int TotalLogCount
        {
            get => _totalLogCount;
            private set => SetProperty(ref _totalLogCount, value);
        }

        /// <summary>获取筛选后的日志数量</summary>
        public int FilteredLogCount
        {
            get => _filteredLogCount;
            private set => SetProperty(ref _filteredLogCount, value);
        }

        /// <summary>获取日志级别选项列表</summary>
        public List<LogLevel?> LogLevels { get; }

        private bool _isLoading;
        /// <summary>获取或设置是否正在加载</summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        #endregion

        #region 命令

        /// <summary>清空日志命令</summary>
        public ICommand ClearLogsCommand { get; }
        /// <summary>导出日志命令</summary>
        public ICommand ExportLogsCommand { get; }
        /// <summary>复制选中日志命令</summary>
        public ICommand CopySelectedCommand { get; }
        /// <summary>复制全部日志命令</summary>
        public ICommand CopyAllCommand { get; }
        /// <summary>刷新命令</summary>
        public ICommand RefreshCommand { get; }


        #endregion

        #region 私有方法

        private bool LogEntriesFilter(object item)
        {
            if (item is not LogEntry logEntry)
                return false;

            // 1. 按级别筛选
            if (SelectedLogLevel != LogLevel.All && logEntry.Level != SelectedLogLevel)
                return false;

            // 2. 按搜索文本筛选
            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchLower = SearchText.ToLower();
                bool containsText =
                    (logEntry.Message?.ToLower()?.Contains(searchLower) ?? false) ||
                    (logEntry.Category?.ToLower()?.Contains(searchLower) ?? false) ||
                    (logEntry.Exception?.Message?.ToLower()?.Contains(searchLower) ?? false);

                if (!containsText)
                    return false;
            }

            return true;
        }

        private const int MaxUiEntries = 2000;

        /// <summary>
        /// 后台线程写入的新日志先进队列，由 UI 定时器每 200ms 批量并入列表。
        /// <para>此前每条日志一次 Dispatcher.InvokeAsync，并在 UI 线程上把整个筛选视图数一遍（2000 条）。
        /// 这个列表常驻主界面，启动加载、通讯调试、报警连发时日志成百上千条地涌入，
        /// UI 线程被逐条插入 + 逐条全表计数占满，整个界面卡顿，急停、暂停按钮也跟着反应迟钝。</para>
        /// </summary>
        private readonly System.Collections.Concurrent.ConcurrentQueue<LogEntry> _pending = new();
        private System.Windows.Threading.DispatcherTimer? _flushTimer;

        private void OnLogAdded(LogEntry logEntry) => _pending.Enqueue(logEntry);

        private void EnsureFlushTimer()
        {
            if (_flushTimer != null) return;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            _flushTimer = new System.Windows.Threading.DispatcherTimer(
                System.Windows.Threading.DispatcherPriority.Background, dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _flushTimer.Tick += (_, _) => FlushPending();
        }

        private void FlushPending()
        {
            if (_pending.IsEmpty) return;

            // 一批里按到达顺序取出，逐条插到头部 → 最新的在最前，与服务端内存缓冲顺序一致
            int added = 0;
            while (added < MaxUiEntries && _pending.TryDequeue(out var entry))
            {
                LogEntries.Insert(0, entry);
                added++;
            }
            // 突发量超过列表上限时，超出部分本就会被立即挤掉，直接丢弃
            while (_pending.TryDequeue(out _)) { }

            while (LogEntries.Count > MaxUiEntries)
                LogEntries.RemoveAt(LogEntries.Count - 1);

            UpdateStatistics();
            LogAppended?.Invoke();
        }

        private void UpdateStatistics()
        {
            TotalLogCount = LogEntries.Count;
            // 注意：_logEntriesView.Cast<object>().Count() 在数据量大时可能较慢，可以考虑优化
            FilteredLogCount = _logEntriesView?.Cast<object>().Count() ?? 0;
        }

        private async void ClearLogs()
        {
            var result = await MessageService.ShowMessageAsync(
                "确定要清空所有日志吗？此操作不可撤销。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == ButtonResult.Yes)
            {
                // 清空 Service 中的内存缓存
                _logService.Clear();
                // 清空 ViewModel 中的集合
                LogEntries.Clear();
                UpdateStatistics();

                // 记录一条新日志
                _uiLogger.Info("日志已清空");
            }
        }

        private void ExportLogs()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|日志文件 (*.log)|*.log|文本文件 (*.txt)|*.txt",
                    FileName = $"Logs_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var logs = _logEntriesView.Cast<LogEntry>().ToList();

                    var extension = Path.GetExtension(saveDialog.FileName).ToLower();

                    if (extension == ".csv")
                    {
                        ExportToCsv(saveDialog.FileName, logs);
                    }
                    else
                    {
                        ExportToText(saveDialog.FileName, logs);
                    }

                    _uiLogger.Info($"日志已导出到: {saveDialog.FileName}");

                    MessageService.ShowMessage(
                        $"日志已成功导出到：\n{saveDialog.FileName}",
                        "导出成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _uiLogger.Error($"导出日志失败: {ex.Message}", ex);
                MessageService.ShowMessage(
                    $"导出失败：{ex.Message}",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void ExportToCsv(string filePath, List<LogEntry> logs)
        {
            var csv = new StringBuilder();

            // CSV头部
            csv.AppendLine("时间戳,级别,分类,消息,异常信息");

            foreach (var log in logs)
            {
                var time = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss.fff");
                var level = log.Level.ToString();
                var category = EscapeCsvField(log.Category ?? "");
                var message = EscapeCsvField(log.Message ?? "");
                var exception = EscapeCsvField(log.Exception?.Message ?? "");

                csv.AppendLine($"{time},{level},{category},{message},{exception}");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }

        private void ExportToText(string filePath, List<LogEntry> logs)
        {
            var lines = logs.Select(log => FormatLogForExport(log));
            File.WriteAllLines(filePath, lines, Encoding.UTF8);
        }

        private string EscapeCsvField(string field)
        {
            if (field.Contains(",") || field.Contains("\"") || field.Contains("\n"))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }

        private string FormatLogForExport(LogEntry entry)
        {
            var sb = new StringBuilder();
            sb.Append($"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss.fff}]");
            sb.Append($"[{entry.Level}]");

            if (!string.IsNullOrEmpty(entry.Category))
                sb.Append($"[{entry.Category}]");

            sb.Append($" {entry.Message}");

            if (entry.Exception != null)
            {
                sb.Append($" | Exception: {entry.Exception.Message}");
                if (entry.Exception.StackTrace != null)
                {
                    var stackTrace = string.Join(" ",
                        entry.Exception.StackTrace.Split(new[] { '\r', '\n' },
                        StringSplitOptions.RemoveEmptyEntries)
                        .Select(line => line.Trim()));
                    sb.Append($" | StackTrace: {stackTrace}");
                }
            }

            return sb.ToString();
        }

        private void CopySelected()
        {
            if (SelectedLogEntry is LogEntry selectedEntry)
            {
                if (TrySetClipboard(FormatLogForCopy(selectedEntry)))
                    _uiLogger.Info("日志内容已复制到剪贴板");
            }
        }

        private void CopyAll()
        {
            var logs = _logEntriesView.Cast<LogEntry>().ToList();
            var text = string.Join(Environment.NewLine, logs.Select(FormatLogForCopy));
            if (TrySetClipboard(text))
                _uiLogger.Info($"已复制 {logs.Count} 条日志到剪贴板");
        }

        /// <summary>剪贴板被其他进程占用时 SetText 抛 COMException，此前直接冒到全局异常处理弹"系统错误"</summary>
        private bool TrySetClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"复制失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private string FormatLogForCopy(LogEntry entry)
        {
            return $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Message}";
        }

        private void Refresh()
        {
            _logEntriesView.Refresh();
            UpdateStatistics();
            _uiLogger.Info("日志列表已刷新");
        }

        #endregion

        #region 订阅管理

        /// <summary>订阅日志事件（幂等，View 重新 Loaded 时调用以恢复实时刷新）</summary>
        public void Subscribe()
        {
            if (_isSubscribed || _logService == null)
                return;

            _logService.OnLogAdded += OnLogAdded;
            _isSubscribed = true;
            EnsureFlushTimer();
            _flushTimer?.Start();

            // 退订期间产生的日志收不到事件，重新订阅时以服务端内存缓冲为准整体同步一次
            if (_needsResync)
            {
                _needsResync = false;
                LogEntries.Clear();
                foreach (var entry in _logService.LogEntries ?? Enumerable.Empty<LogEntry>())
                {
                    LogEntries.Add(entry);
                }
                UpdateStatistics();
            }
        }

        /// <summary>取消订阅日志事件（幂等）</summary>
        public void Unsubscribe()
        {
            if (!_isSubscribed || _logService == null)
                return;

            _logService.OnLogAdded -= OnLogAdded;
            _isSubscribed = false;
            _needsResync = true;
            _flushTimer?.Stop();
            while (_pending.TryDequeue(out _)) { } // 重新订阅时会按服务端缓冲整体同步
        }

        #endregion
    }
}