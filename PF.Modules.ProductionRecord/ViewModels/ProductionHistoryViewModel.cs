using Microsoft.Extensions.DependencyInjection;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace PF.Modules.Production.ViewModels
{
    /// <summary>
    /// 生产数据历史查询 ViewModel。
    /// 提供按时间范围、记录类型、关键词进行过滤查询的功能；结果直接返回到集合中（无分页）。
    /// 支持将查询结果导出为 CSV 或 Excel 文件，并支持选中单条记录查看格式化后的 JSON 详情。
    /// </summary>
    public class ProductionHistoryViewModel : RegionViewModelBase
    {
        /// <summary>
        /// 生产数据服务，用于执行底层的查询和导出逻辑
        /// </summary>
        private readonly IProductionDataService _service;

        // ══════════════════════════════════════════════════════
        //  查询条件
        // ══════════════════════════════════════════════════════

        private DateTime _startTime = DateTime.Today;
        /// <summary>
        /// 获取或设置查询的起始时间（默认为今天 00:00:00）
        /// </summary>
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private DateTime _endTime = DateTime.Now;
        /// <summary>
        /// 获取或设置查询的结束时间（默认为当前时间）
        /// </summary>
        public DateTime EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        private string? _recordType;
        /// <summary>
        /// 获取或设置要过滤的生产记录类型（可选）
        /// </summary>
        public string? RecordType
        {
            get => _recordType;
            set => SetProperty(ref _recordType, value);
        }

        private string? _keyword;
        /// <summary>
        /// 获取或设置查询关键词（如产品条码、批次号等，可选）
        /// </summary>
        public string? Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private int? _maxCount = 1000;
        /// <summary>
        /// 获取或设置最大返回记录数，防止数据量过大导致 UI 卡顿或内存溢出（默认为 1000）
        /// </summary>
        public int? MaxCount
        {
            get => _maxCount;
            set => SetProperty(ref _maxCount, value);
        }

        // ══════════════════════════════════════════════════════
        //  查询结果
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 获取用于在数据表格（DataGrid）中绑定的查询结果集合
        /// </summary>
        public ObservableCollection<ProductionRecord> QueryResults { get; } = [];

        private int _totalCount;
        /// <summary>
        /// 获取或设置当前查询结果的总记录数，用于在 UI 底部状态栏显示
        /// </summary>
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private bool _isLoading;
        /// <summary>
        /// 获取或设置当前是否正在执行耗时操作（如查询、导出），用于控制 UI 遮罩层或进度条显示
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private ProductionRecord? _selectedRecord;
        /// <summary>
        /// 获取或设置当前在列表中被选中的记录，并联动更新其详情信息
        /// </summary>
        public ProductionRecord? SelectedRecord
        {
            get => _selectedRecord;
            set
            {
                if (SetProperty(ref _selectedRecord, value))
                    UpdateDetailJson(value);
            }
        }

        private string _detailJson = string.Empty;
        /// <summary>
        /// 获取或设置选中记录格式化后的 JSON 字符串，用于在侧边栏或详情面板中展示
        /// </summary>
        public string DetailJson
        {
            get => _detailJson;
            set => SetProperty(ref _detailJson, value);
        }

        // ══════════════════════════════════════════════════════
        //  命令
        // ══════════════════════════════════════════════════════

        /// <summary>执行查询操作的命令</summary>
        public DelegateCommand QueryCommand { get; }

        /// <summary>重置所有查询条件并清空结果的命令</summary>
        public DelegateCommand ResetCommand { get; }

        /// <summary>导出查询结果的命令，命令参数指示导出格式（如 "excel" 或 "csv"）</summary>
        public DelegateCommand<string?> ExportCommand { get; }

        // ══════════════════════════════════════════════════════
        //  构造
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 初始化 <see cref="ProductionHistoryViewModel"/> 类的新实例
        /// </summary>
        public ProductionHistoryViewModel()
        {
            // 通过服务提供者获取生产数据服务依赖
            _service = ServiceProvider.GetRequiredService<IProductionDataService>();

            // 初始化查询命令，并将其可执行状态绑定到 IsLoading 属性，防止重复点击
            QueryCommand = new DelegateCommand(async () => await OnQueryAsync(),
                () => !IsLoading)
                .ObservesProperty(() => IsLoading);

            ResetCommand = new DelegateCommand(OnReset);
            ExportCommand = new DelegateCommand<string?>(async format => await OnExportAsync(format));
        }

        /// <summary>
        /// 拦截 Prism 导航事件，当页面被导航进入时触发
        /// </summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            // 导航进入时，如果当前没有数据，则自动执行一次默认的查询（通常是今天的数据）
            if (!QueryResults.Any())
                QueryCommand.Execute();
        }

        // ══════════════════════════════════════════════════════
        //  命令实现
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 异步执行数据查询
        /// </summary>
        private async Task OnQueryAsync()
        {
            IsLoading = true;
            QueryResults.Clear();
            DetailJson = string.Empty;

            try
            {
                var filter = BuildFilter();
                var results = await _service.QueryAsync(filter);

                foreach (var r in results)
                    QueryResults.Add(r);

                TotalCount = QueryResults.Count;
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"查询失败：{ex.Message}", "错误");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// 重置查询条件到默认状态，并清空当前视图中的数据
        /// </summary>
        private void OnReset()
        {
            StartTime = DateTime.Today;
            EndTime = DateTime.Now;
            RecordType = null;
            Keyword = null;
            MaxCount = 1000;
            QueryResults.Clear();
            TotalCount = 0;
            DetailJson = string.Empty;
        }

        /// <summary>
        /// 异步导出当前筛选条件下的数据
        /// </summary>
        /// <param name="format">导出格式（支持 "excel" 或 "csv"）</param>
        private async Task OnExportAsync(string? format)
        {
            // 配置保存文件对话框
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"生产历史数据_{DateTime.Now:yyyyMMdd_HHmmss}",
                Filter = format?.ToLower() == "excel"
                    ? "Excel 文件 (*.xlsx)|*.xlsx"
                    : "CSV 文件 (*.csv)|*.csv"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var filter = BuildFilter();

                // 根据传入的格式参数调用对应的底层导出服务
                if (format?.ToLower() == "excel")
                    await _service.ExportToExcelAsync(filter, dlg.FileName);
                else
                    await _service.ExportToCsvAsync(filter, dlg.FileName);

                MessageService.ShowMessage($"导出成功：{dlg.FileName}", "提示");
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"导出失败：{ex.Message}", "错误");
            }
        }

        // ══════════════════════════════════════════════════════
        //  私有辅助
        // ══════════════════════════════════════════════════════

        /// <summary>
        /// 根据当前的 UI 属性值构建用于底层服务的查询过滤器参数
        /// </summary>
        private ProductionQueryFilter BuildFilter() => new()
        {
            StartTime = StartTime,
            EndTime = EndTime,
            RecordType = string.IsNullOrWhiteSpace(RecordType) ? null : RecordType,
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword,
            MaxCount = MaxCount
        };

        /// <summary>
        /// 尝试解析并格式化所选记录的 JSON 字符串，以便于在 UI 上更美观地展示
        /// </summary>
        /// <param name="record">当前被选中的生产记录</param>
        private void UpdateDetailJson(ProductionRecord? record)
        {
            if (record == null)
            {
                DetailJson = string.Empty;
                return;
            }

            try
            {
                // 格式化 JSON 以便阅读 (设置 WriteIndented = true 实现缩进)
                var doc = JsonDocument.Parse(record.JsonValue);
                DetailJson = JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                // 若解析失败（例如内容不是合法的 JSON），则直接显示原文
                DetailJson = record.JsonValue;
            }
        }
    }
}