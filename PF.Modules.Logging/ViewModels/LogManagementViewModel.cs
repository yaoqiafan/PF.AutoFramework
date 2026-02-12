using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using PF.Common.Core.PrismBase;
using PF.Core.Entities.Logging;
using PF.Core.Interfaces.Logging;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;

namespace PF.Modules.Logging.ViewModels
{
    public class LogManagementViewModel : ViewModelBase
    {
        private readonly ILogService _logService;

        // 默认日志文件夹路径 (根据你的实际项目结构调整)
        private readonly string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");

        public LogManagementViewModel()
        {
            _logService = ServiceProvider.GetRequiredService<ILogService>();

            // 初始化命令
            ClearLogsCommand = new DelegateCommand(ClearLogs);
            ExportLogsCommand = new DelegateCommand(ExportLogs);
            RefreshCommand = new DelegateCommand(Refresh);
            QueryHistoryCommand = new DelegateCommand(async () => await QueryHistory());

            // 初始化集合
            HistoricalLogs = new ObservableCollection<LogEntry>();
            QueryDate = DateTime.Today; // 默认查询今天
        }

        #region 属性

        private DateTime _queryDate;
        public DateTime QueryDate
        {
            get => _queryDate;
            set => SetProperty(ref _queryDate, value);
        }

        private string _queryKeyword;
        public string QueryKeyword
        {
            get => _queryKeyword;
            set => SetProperty(ref _queryKeyword, value);
        }

        private bool _isQuerying;
        public bool IsQuerying
        {
            get => _isQuerying;
            set => SetProperty(ref _isQuerying, value);
        }

        private string _queryStatusMessage;
        public string QueryStatusMessage
        {
            get => _queryStatusMessage;
            set => SetProperty(ref _queryStatusMessage, value);
        }

        // 历史日志结果集
        public ObservableCollection<LogEntry> HistoricalLogs { get; }

        #endregion

        #region 命令

        public DelegateCommand ClearLogsCommand { get; }
        public DelegateCommand ExportLogsCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand QueryHistoryCommand { get; }

        #endregion

        #region 方法

        private async Task QueryHistory()
        {
            if (IsQuerying) return;

            IsQuerying = true;
            HistoricalLogs.Clear();
            QueryStatusMessage = "正在检索文件...";

            try
            {
                await Task.Run(() =>
                {
                    // 1. 确定文件名格式 (这里假设是 NLog 或常见的按天生成格式，如: 2026-02-12.log)
                    // 你需要根据实际的 LogConfig 修改这里的文件名匹配模式
                    string fileName = $"{QueryDate:yyyy-MM-dd}.log";
                    string filePath = Path.Combine(_logDirectory, fileName);

                    // 如果找不到 .log，尝试找 .txt 或其他格式
                    if (!File.Exists(filePath))
                    {
                        filePath = Path.Combine(_logDirectory, $"{QueryDate:yyyy-MM-dd}.txt");
                    }

                    if (!File.Exists(filePath))
                    {
                        // 尝试递归搜索
                        if (Directory.Exists(_logDirectory))
                        {
                            var files = Directory.GetFiles(_logDirectory, $"*{QueryDate:yyyy-MM-dd}*.*", SearchOption.AllDirectories);
                            filePath = files.FirstOrDefault();
                        }
                    }

                    if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                            QueryStatusMessage = $"未找到 {QueryDate:yyyy-MM-dd} 的日志文件");
                        return;
                    }

                    // 2. 读取并解析文件
                    var lines = File.ReadAllLines(filePath, Encoding.UTF8);
                    var keyword = QueryKeyword?.ToLower();

                    foreach (var line in lines)
                    {
                        // 简单的关键词过滤
                        if (!string.IsNullOrWhiteSpace(keyword) && !line.ToLower().Contains(keyword))
                            continue;

                        // 3. 解析日志行 (这里需要根据你的日志文件实际格式编写解析逻辑)
                        // 假设格式为: [时间] [级别] 消息
                        var entry = ParseLogLine(line);
                        if (entry != null)
                        {
                            Application.Current.Dispatcher.Invoke(() => HistoricalLogs.Add(entry));
                        }
                    }

                    Application.Current.Dispatcher.Invoke(() =>
                        QueryStatusMessage = $"查询完成，共找到 {HistoricalLogs.Count} 条记录");
                });
            }
            catch (Exception ex)
            {
                QueryStatusMessage = $"查询出错: {ex.Message}";
            }
            finally
            {
                IsQuerying = false;
            }
        }

        private LogEntry ParseLogLine(string line)
        {
            // 正则表达式匹配：日期 时间 [级别] [分类] 消息
            // 根据实际日志格式调整
            var regex = new Regex(@"^(\d{4}-\d{2}-\d{2}\s\d{2}:\d{2}:\d{2}\.\d{3})\s+\[(\w+)\]\s+(?:\[(.*?)\]\s+)?(.*)$");
            var match = regex.Match(line);

            if (match.Success)
            {
                Enum.TryParse(match.Groups[2].Value, true, out Core.Enums.LogLevel level);

                return new LogEntry
                {
                    Timestamp = DateTime.Parse(match.Groups[1].Value),
                    Level = level,
                    Category = match.Groups[3].Success ? match.Groups[3].Value : null,
                    Message = match.Groups[4].Value
                };
            }

            // 无法解析或者是堆栈跟踪的后续行，直接返回文本
            return new LogEntry { Message = line, Timestamp = QueryDate, Level = Core.Enums.LogLevel.All };
        }

        private void Refresh()
        {
            // 刷新逻辑，如果需要通知 LogListViewModel 刷新，
            // 可以通过 EventAggregator 或者让 LogListViewModel 监听 Service 事件
            // 这里简单记录一条日志来触发更新
            _logService.Info("用户在管理界面手动请求刷新", "系统");
        }

        private void ClearLogs()
        {
            var result = MessageBox.Show(
                "确定要清空所有内存中的日志吗？此操作不可撤销。",
                "确认清空",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                _logService.Clear();
                _logService.Info("日志已通过管理界面清空", "系统");
            }
        }

        private void ExportLogs()
        {
            try
            {
                var saveDialog = new SaveFileDialog
                {
                    Filter = "CSV文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                    FileName = $"FullLogs_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".csv"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    // 获取所有日志（不只是筛选后的）
                    var logs = _logService.LogEntries.ToList();

                    var sb = new StringBuilder();
                    // 这里简化CSV生成逻辑，实际建议复用 Helper 类
                    sb.AppendLine("时间,级别,分类,消息");
                    foreach (var log in logs)
                    {
                        sb.AppendLine($"{log.Timestamp},{log.Level},{log.Category},\"{log.Message?.Replace("\"", "\"\"")}\"");
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                    _logService.Info($"日志已导出到: {saveDialog.FileName}", "系统");
                    MessageBox.Show("导出成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion
    }
}