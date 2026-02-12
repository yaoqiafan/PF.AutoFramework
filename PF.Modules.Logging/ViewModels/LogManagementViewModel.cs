using Microsoft.Win32;
using PF.UI.Infrastructure.PrismBase;
using PF.Core.Entities.Logging;
using PF.Core.Interfaces.Logging;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;

namespace PF.Modules.Logging.ViewModels
{
    public class LogManagementViewModel : ViewModelBase
    {
        private readonly ILogService _logService;
        private readonly ObservableCollection<LogEntry> _rawLogsSource; // 原始数据源

        public LogManagementViewModel(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            ExportLogsCommand = new DelegateCommand(ExportLogs);
            QueryHistoryCommand = new DelegateCommand(async () => await QueryHistory());

            // 初始化集合
            _rawLogsSource = new ObservableCollection<LogEntry>();
            // 使用 CollectionViewSource 包装原始数据，实现过滤而不删除数据
            LogsView = CollectionViewSource.GetDefaultView(_rawLogsSource);
            LogsView.Filter = OnFilterLogs;

            // 初始化筛选列表
            FilterLevels = new ObservableCollection<string> { "全部" };
            foreach (var level in Enum.GetNames(typeof(Core.Enums.LogLevel)))
            {
                FilterLevels.Add(level);
            }
            SelectedFilterLevel = "全部";

            FilterCategories = new ObservableCollection<string> { "全部" };
            SelectedFilterCategory = "全部";

            // 默认日期
            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
        }

        #region 属性

        // 对外暴露的视图，UI绑定这个
        public ICollectionView LogsView { get; }

        private DateTime _startDate;
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private string _queryKeyword;
        public string QueryKeyword
        {
            get => _queryKeyword;
            set
            {
                if (SetProperty(ref _queryKeyword, value))
                    LogsView.Refresh(); // 关键词改变时刷新视图
            }
        }

        // --- 筛选属性 ---

        public ObservableCollection<string> FilterLevels { get; }

        private string _selectedFilterLevel;
        public string SelectedFilterLevel
        {
            get => _selectedFilterLevel;
            set
            {
                if (SetProperty(ref _selectedFilterLevel, value))
                    LogsView.Refresh();
            }
        }

        public ObservableCollection<string> FilterCategories { get; }

        private string _selectedFilterCategory;
        public string SelectedFilterCategory
        {
            get => _selectedFilterCategory;
            set
            {
                if (SetProperty(ref _selectedFilterCategory, value))
                    LogsView.Refresh();
            }
        }

        // --- 状态属性 ---

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

        #endregion

        #region 命令

        public DelegateCommand ClearLogsCommand { get; }
        public DelegateCommand ExportLogsCommand { get; }
        public DelegateCommand RefreshCommand { get; }
        public DelegateCommand QueryHistoryCommand { get; }

        #endregion

        #region 方法

        // 核心过滤逻辑
        private bool OnFilterLogs(object item)
        {
            if (item is not LogEntry entry) return false;

            // 1. 等级过滤
            if (SelectedFilterLevel != "全部" && entry.Level.ToString() != SelectedFilterLevel)
                return false;

            // 2. 分类过滤
            if (SelectedFilterCategory != "全部" && entry.Category != SelectedFilterCategory)
                return false;

            // 3. 内存中二次关键词搜索 (可选，如果希望在结果中再搜)
            if (!string.IsNullOrEmpty(QueryKeyword))
            {
                bool msgMatch = entry.Message?.Contains(QueryKeyword, StringComparison.OrdinalIgnoreCase) ?? false;
                bool catMatch = entry.Category?.Contains(QueryKeyword, StringComparison.OrdinalIgnoreCase) ?? false;
                return msgMatch || catMatch;
            }

            return true;
        }

        private async Task QueryHistory()
        {
            if (IsQuerying) return;

            IsQuerying = true;
            QueryStatusMessage = "正在查询历史文件...";
            _rawLogsSource.Clear(); // 清空旧数据
            FilterCategories.Clear(); // 重置分类
            FilterCategories.Add("全部");
            SelectedFilterCategory = "全部";

            try
            {
                var queryParams = new LogQueryParams
                {
                    StartTime = StartDate.Date,
                    EndTime = EndDate.Date.AddDays(1).AddTicks(-1),
                    // 注意：这里 Keyword 传 null，把所有数据先查回来，然后在内存里筛选
                    // 这样用户在界面上切换“只看Error”时不需要重新读盘
                    Keyword = null,
                    MaxResults = 10000,
                    OrderByDescending = true
                };

                var results = await Task.Run(() => _logService.QueryHistoricalLogs(queryParams));

                if (results != null && results.Any())
                {
                    // 1. 填充数据
                    foreach (var item in results)
                    {
                        _rawLogsSource.Add(item);
                    }

                    // 2. 动态提取分类
                    var categories = results.Select(x => x.Category)
                                            .Where(c => !string.IsNullOrEmpty(c))
                                            .Distinct()
                                            .OrderBy(c => c);
                    foreach (var cat in categories)
                    {
                        FilterCategories.Add(cat);
                    }

                    QueryStatusMessage = $"查询完成，加载 {results.Count} 条记录";
                }
                else
                {
                    QueryStatusMessage = "未找到符合日期范围的记录";
                }
            }
            catch (Exception ex)
            {
                QueryStatusMessage = $"查询出错: {ex.Message}";
                _logService.Error("历史日志查询失败", "LogManagement", ex);
            }
            finally
            {
                IsQuerying = false;
                LogsView.Refresh(); // 触发一次视图更新
            }
        }

        private void ExportLogs()
        {
            // 获取当前视图中显示的日志（即筛选后的结果）
            var visibleLogs = LogsView.Cast<LogEntry>().ToList();

            if (!visibleLogs.Any())
            {
                MessageBox.Show("当前列表中没有数据可供导出。", "提示");
                return;
            }

            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV 文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt",
                FileName = $"Log_Export_{DateTime.Now:yyyyMMdd_HHmmss}",
                DefaultExt = ".csv"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var sb = new StringBuilder();
                    // CSV Header
                    sb.AppendLine("时间,等级,分类,内容");

                    foreach (var log in visibleLogs)
                    {
                        // 处理内容中的换行和逗号，避免CSV格式错乱
                        string safeMsg = log.Message?.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
                        sb.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{log.Level},{log.Category},\"{safeMsg}\"");
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);

                    _logService.Info($"用户导出了 {visibleLogs.Count} 条日志到 {saveDialog.FileName}", "LogManagement");
                    MessageBox.Show($"导出成功！\n路径: {saveDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }


        #endregion
    }
}