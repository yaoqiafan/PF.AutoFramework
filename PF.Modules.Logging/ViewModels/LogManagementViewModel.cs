using Microsoft.Win32;
using PF.Infrastructure.Logging;
using PF.UI.Infrastructure.PrismBase;
using PF.Core.Entities.Logging;
using PF.Core.Interfaces.Logging;
using PF.UI.Shared.Data;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PF.UI.Infrastructure.Dialog.Basic;

namespace PF.Modules.Logging.ViewModels
{
    /// <summary>日志管理 ViewModel</summary>
    public class LogManagementViewModel : RegionViewModelBase
    {
        private readonly ILogService _logService;
        private readonly CategoryLogger _uiLogger;

        /// <summary>全量原始数据缓存（从磁盘加载后不再变动）</summary>
        private List<LogEntry> _allLogs = new();

        /// <summary>经过筛选后的数据（分页的数据来源）</summary>
        private List<LogEntry> _filteredLogs = new();

        /// <summary>初始化日志管理 ViewModel</summary>
        public LogManagementViewModel(ILogService logService, IMessageService messageService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _uiLogger = CategoryLoggerFactory.UI(_logService);

            ExportLogsCommand = new DelegateCommand(ExportLogs);
            QueryHistoryCommand = new DelegateCommand(async () => await QueryHistory());

            FilterLevels = new ObservableCollection<string> { "全部" };
            foreach (var level in Enum.GetNames(typeof(Core.Enums.LogLevel)))
                FilterLevels.Add(level);
            SelectedFilterLevel = "全部";

            FilterCategories = new ObservableCollection<string> { "全部" };
            SelectedFilterCategory = "全部";

            StartDate = DateTime.Today;
            EndDate = DateTime.Today;
        }

        #region 数据集合

        /// <summary>当前页显示的日志列表（绑定到 DataGrid）</summary>
        public ObservableCollection<LogEntry> DataList { get; } = new();

        #endregion

        #region 分页属性

        private int _pageSize = 50;
        /// <summary>每页显示条数</summary>
        public int PageSize
        {
            get => _pageSize;
            set
            {
                int safe = value < 1 ? 1 : value;
                if (SetProperty(ref _pageSize, safe))
                    RecalculatePagination();
            }
        }

        private int _pageIndex = 1;
        /// <summary>当前页码</summary>
        public int PageIndex
        {
            get => _pageIndex;
            set => SetProperty(ref _pageIndex, value);
        }

        private int _maxPageCount = 1;
        /// <summary>总页数</summary>
        public int MaxPageCount
        {
            get => _maxPageCount;
            set => SetProperty(ref _maxPageCount, value);
        }

        /// <summary>翻页命令</summary>
        public DelegateCommand<FunctionEventArgs<int>> PageUpdatedCmd => new(PageUpdated);

        private void RecalculatePagination()
        {
            int pages = (int)Math.Ceiling(_filteredLogs.Count / (double)PageSize);
            MaxPageCount = pages > 0 ? pages : 1;
            PageUpdated(new FunctionEventArgs<int>(1));
        }

        private void PageUpdated(FunctionEventArgs<int> info)
        {
            int target = info.Info;
            if (target < 1) target = 1;
            if (target > MaxPageCount) target = MaxPageCount;

            PageIndex = target;
            DataList.Clear();
            foreach (var item in _filteredLogs.Skip((PageIndex - 1) * PageSize).Take(PageSize))
                DataList.Add(item);

            UpdateStatusMessage();
        }

        #endregion

        #region 查询/筛选属性

        private DateTime _startDate;
        /// <summary>开始日期</summary>
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate;
        /// <summary>结束日期</summary>
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private string _queryKeyword;
        /// <summary>关键词搜索</summary>
        public string QueryKeyword
        {
            get => _queryKeyword;
            set
            {
                if (SetProperty(ref _queryKeyword, value))
                    ApplyFilterAndPaginate();
            }
        }

        /// <summary>日志级别过滤选项</summary>
        public ObservableCollection<string> FilterLevels { get; }

        private string _selectedFilterLevel;
        /// <summary>选中的过滤级别</summary>
        public string SelectedFilterLevel
        {
            get => _selectedFilterLevel;
            set
            {
                if (SetProperty(ref _selectedFilterLevel, value))
                    ApplyFilterAndPaginate();
            }
        }

        /// <summary>分类过滤选项</summary>
        public ObservableCollection<string> FilterCategories { get; }

        private string _selectedFilterCategory;
        /// <summary>选中的过滤分类</summary>
        public string SelectedFilterCategory
        {
            get => _selectedFilterCategory;
            set
            {
                if (SetProperty(ref _selectedFilterCategory, value))
                    ApplyFilterAndPaginate();
            }
        }

        #endregion

        #region 状态属性

        private bool _isQuerying;
        /// <summary>是否正在从磁盘读取</summary>
        public bool IsQuerying
        {
            get => _isQuerying;
            set => SetProperty(ref _isQuerying, value);
        }

        private string _queryStatusMessage;
        /// <summary>底部状态栏文字</summary>
        public string QueryStatusMessage
        {
            get => _queryStatusMessage;
            set => SetProperty(ref _queryStatusMessage, value);
        }

        #endregion

        #region 命令

        /// <summary>导出日志命令</summary>
        public DelegateCommand ExportLogsCommand { get; }
        /// <summary>查询历史日志命令</summary>
        public DelegateCommand QueryHistoryCommand { get; }

        #endregion

        #region 方法

        private void ApplyFilterAndPaginate()
        {
            _filteredLogs = _allLogs.Where(OnFilterLog).ToList();
            RecalculatePagination();
        }

        private bool OnFilterLog(LogEntry entry)
        {
            if (SelectedFilterLevel != "全部" && entry.Level.ToString() != SelectedFilterLevel)
                return false;

            if (SelectedFilterCategory != "全部" && entry.Category != SelectedFilterCategory)
                return false;

            if (!string.IsNullOrEmpty(QueryKeyword))
            {
                bool msgMatch = entry.Message?.Contains(QueryKeyword, StringComparison.OrdinalIgnoreCase) ?? false;
                bool catMatch = entry.Category?.Contains(QueryKeyword, StringComparison.OrdinalIgnoreCase) ?? false;
                return msgMatch || catMatch;
            }

            return true;
        }

        private void UpdateStatusMessage()
        {
            if (_allLogs.Count == 0)
            {
                QueryStatusMessage = "暂无数据，请先点击「从磁盘读取」";
                return;
            }

            string filterHint = _filteredLogs.Count < _allLogs.Count
                ? $"已筛选 {_filteredLogs.Count} / 共 {_allLogs.Count} 条"
                : $"共 {_allLogs.Count} 条";

            QueryStatusMessage = $"{filterHint}，第 {PageIndex} / {MaxPageCount} 页";
        }

        private async Task QueryHistory()
        {
            if (IsQuerying) return;

            IsQuerying = true;
            QueryStatusMessage = "正在扫描文件...";
            _allLogs.Clear();
            _filteredLogs.Clear();
            DataList.Clear();
            FilterCategories.Clear();
            FilterCategories.Add("全部");
            SelectedFilterCategory = "全部";

            try
            {
                var queryParams = new LogQueryParams
                {
                    StartTime = StartDate.Date,
                    EndTime = EndDate.Date.AddDays(1).AddTicks(-1),
                    Keyword = null,     // 全量取回，内存二次筛选
                    MaxResults = null,  // 无上限
                    OrderByDescending = true
                };

                var results = await Task.Run(() => _logService.QueryHistoricalLogs(queryParams));

                if (results != null && results.Any())
                {
                    _allLogs = results;

                    var categories = results.Select(x => x.Category)
                                            .Where(c => !string.IsNullOrEmpty(c))
                                            .Distinct()
                                            .OrderBy(c => c);
                    foreach (var cat in categories)
                        FilterCategories.Add(cat);

                    ApplyFilterAndPaginate();
                }
                else
                {
                    QueryStatusMessage = "未找到符合日期范围的记录";
                }
            }
            catch (Exception ex)
            {
                QueryStatusMessage = $"查询出错: {ex.Message}";
                _uiLogger.Error("历史日志查询失败", ex);
            }
            finally
            {
                IsQuerying = false;
            }
        }

        private void ExportLogs()
        {
            if (!_filteredLogs.Any())
            {
                MessageService.ShowMessage("当前列表中没有数据可供导出。", "提示");
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
                    sb.AppendLine("时间,等级,分类,内容");

                    foreach (var log in _filteredLogs)
                    {
                        string safeMsg = log.Message?.Replace("\"", "\"\"").Replace("\r", " ").Replace("\n", " ");
                        sb.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff},{log.Level},{log.Category},\"{safeMsg}\"");
                    }

                    File.WriteAllText(saveDialog.FileName, sb.ToString(), Encoding.UTF8);
                    _uiLogger.Info($"用户导出了 {_filteredLogs.Count} 条日志到 {saveDialog.FileName}");
                    MessageService.ShowMessage($"导出成功！共 {_filteredLogs.Count} 条\n路径: {saveDialog.FileName}",
                        "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageService.ShowMessage($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion
    }
}
