using Microsoft.Extensions.DependencyInjection;
using PF.Core.Entities.ProductionData;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace PF.Modules.Production.ViewModels
{
    /// <summary>
    /// 生产数据历史查询 ViewModel。
    /// 支持时间范围、设备、类型、批次、关键词过滤；结果直接返回集合，无分页。
    /// 支持导出 CSV / Excel。
    /// </summary>
    public class ProductionHistoryViewModel : RegionViewModelBase
    {
        private readonly IProductionDataService _service;

        // ══════════════════════════════════════════════════════
        //  查询条件
        // ══════════════════════════════════════════════════════

        private DateTime _startTime = DateTime.Today;
        public DateTime StartTime
        {
            get => _startTime;
            set => SetProperty(ref _startTime, value);
        }

        private DateTime _endTime = DateTime.Now;
        public DateTime EndTime
        {
            get => _endTime;
            set => SetProperty(ref _endTime, value);
        }

        private string? _deviceId;
        public string? DeviceId
        {
            get => _deviceId;
            set => SetProperty(ref _deviceId, value);
        }

        private string? _recordType;
        public string? RecordType
        {
            get => _recordType;
            set => SetProperty(ref _recordType, value);
        }

        private string? _batchId;
        public string? BatchId
        {
            get => _batchId;
            set => SetProperty(ref _batchId, value);
        }

        private string? _keyword;
        public string? Keyword
        {
            get => _keyword;
            set => SetProperty(ref _keyword, value);
        }

        private int? _maxCount = 1000;
        public int? MaxCount
        {
            get => _maxCount;
            set => SetProperty(ref _maxCount, value);
        }

        // ══════════════════════════════════════════════════════
        //  查询结果
        // ══════════════════════════════════════════════════════

        public ObservableCollection<ProductionRecord> QueryResults { get; } = [];

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private ProductionRecord? _selectedRecord;
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
        public string DetailJson
        {
            get => _detailJson;
            set => SetProperty(ref _detailJson, value);
        }

        // ══════════════════════════════════════════════════════
        //  命令
        // ══════════════════════════════════════════════════════

        public DelegateCommand QueryCommand { get; }
        public DelegateCommand ResetCommand { get; }
        public DelegateCommand<string?> ExportCommand { get; }

        // ══════════════════════════════════════════════════════
        //  构造
        // ══════════════════════════════════════════════════════

        public ProductionHistoryViewModel()
        {
            _service = ServiceProvider.GetRequiredService<IProductionDataService>();

            QueryCommand = new DelegateCommand(async () => await OnQueryAsync(),
                () => !IsLoading)
                .ObservesProperty(() => IsLoading);

            ResetCommand = new DelegateCommand(OnReset);
            ExportCommand = new DelegateCommand<string?>(async format => await OnExportAsync(format));
        }

        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);
            // 导航进入时自动查询今日数据
            if (!QueryResults.Any())
                QueryCommand.Execute();
        }

        // ══════════════════════════════════════════════════════
        //  命令实现
        // ══════════════════════════════════════════════════════

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

        private void OnReset()
        {
            StartTime = DateTime.Today;
            EndTime = DateTime.Now;
            DeviceId = null;
            RecordType = null;
            BatchId = null;
            Keyword = null;
            MaxCount = 1000;
            QueryResults.Clear();
            TotalCount = 0;
            DetailJson = string.Empty;
        }

        private async Task OnExportAsync(string? format)
        {
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

        private ProductionQueryFilter BuildFilter() => new()
        {
            StartTime = StartTime,
            EndTime = EndTime,
            DeviceId = string.IsNullOrWhiteSpace(DeviceId) ? null : DeviceId,
            RecordType = string.IsNullOrWhiteSpace(RecordType) ? null : RecordType,
            BatchId = string.IsNullOrWhiteSpace(BatchId) ? null : BatchId,
            Keyword = string.IsNullOrWhiteSpace(Keyword) ? null : Keyword,
            MaxCount = MaxCount
        };

        private void UpdateDetailJson(ProductionRecord? record)
        {
            if (record == null)
            {
                DetailJson = string.Empty;
                return;
            }

            try
            {
                // 格式化 JSON 以便阅读
                var doc = JsonDocument.Parse(record.JsonValue);
                DetailJson = JsonSerializer.Serialize(doc, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
            }
            catch
            {
                DetailJson = record.JsonValue;
            }
        }
    }
}
