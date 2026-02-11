
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PF.Common.Core.PrismBase;
using PF.Core.Entities.Logging;
using PF.Core.Enums;
using PF.Core.Interfaces.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace PF.Modules.Logging.ViewModels
{
    public class LogListViewModel : ViewModelBase
    {
        private readonly ILogService _logService;
        private readonly ICollectionView _logEntriesView;
        private string _searchText;
        private LogLevel? _selectedLogLevel;
        private bool _autoScroll = true;
        private int _totalLogCount;
        private int _filteredLogCount;
        private bool _showOnlyCurrentDay = false;
        private DateTime? _selectedDate = DateTime.Today;

        public LogListViewModel()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();

            // 初始化命令
            ClearLogsCommand = new DelegateCommand(ClearLogs);
            ExportLogsCommand = new DelegateCommand(ExportLogs);
            CopySelectedCommand = new DelegateCommand(CopySelected);
            CopyAllCommand = new DelegateCommand(CopyAll);
            ToggleDateFilterCommand = new DelegateCommand(ToggleDateFilter);
            RefreshCommand = new DelegateCommand(Refresh);

            // 设置集合视图以支持筛选和排序
            _logEntriesView = CollectionViewSource.GetDefaultView(LogEntries);
            _logEntriesView.Filter = LogEntriesFilter;
            _logEntriesView.SortDescriptions.Add(
                new SortDescription("Timestamp", ListSortDirection.Descending));

            // 订阅日志添加事件
            _logService.OnLogAdded += OnLogAdded;

            // 初始化日志级别选项（包括"全部"）
            LogLevels = new List<LogLevel?>();
            foreach (LogLevel level in Enum.GetValues(typeof(LogLevel)))
            {
                LogLevels.Add(level);
            }

            SelectedLogLevel= LogLevel.All;
            // 初始化日期范围
            DateRanges = new List<DateRangeOption>
            {
                new DateRangeOption { Name = "当天", Days = 0 },
                new DateRangeOption { Name = "最近7天", Days = 7 },
                new DateRangeOption { Name = "最近30天", Days = 30 },
                new DateRangeOption { Name = "全部", Days = -1 }
            };
            SelectedDateRange = DateRanges[0];

            // 更新统计信息
            UpdateStatistics();
        }

        #region 属性

        public ICollectionView LogEntriesView => _logEntriesView;

        public ObservableCollection<LogEntry> LogEntries { get; set; } = new ObservableCollection<LogEntry>();

        // 在 LogListViewModel.cs 中
        private LogEntry _selectedLogEntry;

        // 选中项属性
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
            if (SelectedLogEntry != null)
            {
                // 记录选中日志（可选）
                _logService.Debug($"选中日志: {SelectedLogEntry.Message}", "UI");
            }
            else
            {
                SelectedLogEntry = null;
            }
        }

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

        public bool AutoScroll
        {
            get => _autoScroll;
            set => SetProperty(ref _autoScroll, value);
        }

        public bool ShowOnlyCurrentDay
        {
            get => _showOnlyCurrentDay;
            set
            {
                if (SetProperty(ref _showOnlyCurrentDay, value))
                {
                    _logEntriesView.Refresh();
                    UpdateStatistics();
                }
            }
        }

        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (SetProperty(ref _selectedDate, value))
                {
                    _logEntriesView.Refresh();
                    UpdateStatistics();
                }
            }
        }

        public int TotalLogCount
        {
            get => _totalLogCount;
            private set => SetProperty(ref _totalLogCount, value);
        }

        public int FilteredLogCount
        {
            get => _filteredLogCount;
            private set => SetProperty(ref _filteredLogCount, value);
        }

        public List<LogLevel?> LogLevels { get; }

        public List<DateRangeOption> DateRanges { get; }

        private DateRangeOption _selectedDateRange;
        public DateRangeOption SelectedDateRange
        {
            get => _selectedDateRange;
            set
            {
                if (SetProperty(ref _selectedDateRange, value))
                {
                    ApplyDateFilter();
                }
            }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        #endregion

        #region 命令

        public ICommand ClearLogsCommand { get; }
        public ICommand ExportLogsCommand { get; }
        public ICommand CopySelectedCommand { get; }
        public ICommand CopyAllCommand { get; }
        public ICommand ToggleDateFilterCommand { get; }
        public ICommand RefreshCommand { get; }

        #endregion

        #region 私有方法

        private bool LogEntriesFilter(object item)
        {
            if (item is not LogEntry logEntry)
                return false;

            // 1. 按日期筛选
            if (ShowOnlyCurrentDay && SelectedDate.HasValue)
            {
                if (logEntry.Timestamp.Date != SelectedDate.Value.Date)
                    return false;
            }

            // 2. 按级别筛选

            if (SelectedLogLevel != LogLevel.All && logEntry.Level != SelectedLogLevel) 
                return false;
           
            // 3. 按搜索文本筛选
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

        private void OnLogAdded(LogEntry logEntry)
        {
            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                UpdateStatistics();
            });
        }

        private void UpdateStatistics()
        {
            TotalLogCount = LogEntries.Count;
            FilteredLogCount = _logEntriesView.Cast<object>().Count();
        }

        private void ClearLogs()
        {
            var result = MessageBox.Show(
                "确定要清空所有日志吗？此操作不可撤销。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _logService.Clear();
                UpdateStatistics();
                _logService.Info("日志已清空", "UI");
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

                    _logService.Info($"日志已导出到: {saveDialog.FileName}", "UI");

                    MessageBox.Show(
                        $"日志已成功导出到：\n{saveDialog.FileName}",
                        "导出成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logService.Error($"导出日志失败: {ex.Message}", "UI", ex);
                MessageBox.Show(
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
                string logText = FormatLogForCopy(selectedEntry);
                Clipboard.SetText(logText);
                _logService.Info("日志内容已复制到剪贴板", "UI");
            }
        }

        private void CopyAll()
        {
            var logs = _logEntriesView.Cast<LogEntry>().ToList();
            var text = string.Join(Environment.NewLine, logs.Select(FormatLogForCopy));
            Clipboard.SetText(text);
            _logService.Info($"已复制 {logs.Count} 条日志到剪贴板", "UI");
        }

        private string FormatLogForCopy(LogEntry entry)
        {
            return $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Message}";
        }

        private void ToggleDateFilter()
        {
            ShowOnlyCurrentDay = !ShowOnlyCurrentDay;
        }

        private void Refresh()
        {
            _logEntriesView.Refresh();
            UpdateStatistics();
            _logService.Info("日志列表已刷新", "UI");
        }

        private void ApplyDateFilter()
        {
            if (SelectedDateRange.Days < 0) // 全部
            {
                ShowOnlyCurrentDay = false;
                SelectedDate = null;
            }
            else if (SelectedDateRange.Days == 0) // 今天
            {
                ShowOnlyCurrentDay = true;
                SelectedDate = DateTime.Today;
            }
            else // 最近N天
            {
                ShowOnlyCurrentDay = false;
                SelectedDate = null;
                // 这里可以添加逻辑来查询指定日期范围内的日志
                // 需要调用_logService.QueryLogs方法
            }
        }

        #endregion

        #region 清理

        public void Unsubscribe()
        {
            if (_logService != null)
            {
                _logService.OnLogAdded -= OnLogAdded;
            }
        }

        #endregion
    }

    public class DateRangeOption
    {
        public string Name { get; set; }
        public int Days { get; set; }
        public DateTime? StartDate => Days >= 0 ? DateTime.Today.AddDays(-Days) : (DateTime?)null;
    }
}