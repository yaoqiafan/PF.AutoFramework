using Microsoft.Extensions.DependencyInjection;
using PF.Core.Entities.ProductionData;
using PF.Core.Events;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Production.ViewModels
{
    /// <summary>
    /// 生产数据实时监控 ViewModel。
    /// 订阅 IProductionDataService.DataRecorded 事件，每条新数据写入后自动更新列表，无需轮询。
    /// 内存中最多保留 <see cref="MaxRecords"/> 条，防止无限增长。
    /// </summary>
    public class ProductionMonitorViewModel : ViewModelBase
    {
        private const int MaxRecords = 500;

        private readonly IProductionDataService _service;

        // ══════════════════════════════════════════════════════
        //  属性
        // ══════════════════════════════════════════════════════

        public ObservableCollection<ProductionRecord> RecentRecords { get; } = [];

        private string? _filterDeviceId;
        public string? FilterDeviceId
        {
            get => _filterDeviceId;
            set => SetProperty(ref _filterDeviceId, value);
        }

        private string? _filterRecordType;
        public string? FilterRecordType
        {
            get => _filterRecordType;
            set => SetProperty(ref _filterRecordType, value);
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        // ══════════════════════════════════════════════════════
        //  命令
        // ══════════════════════════════════════════════════════

        public DelegateCommand ClearCommand { get; }
        public DelegateCommand<string?> ExportCommand { get; }

        // ══════════════════════════════════════════════════════
        //  构造
        // ══════════════════════════════════════════════════════

        public ProductionMonitorViewModel()
        {
            _service = ServiceProvider.GetRequiredService<IProductionDataService>();

            ClearCommand = new DelegateCommand(OnClear);
            ExportCommand = new DelegateCommand<string?>(OnExport);

            _service.DataRecorded += OnDataRecorded;
        }

        // ══════════════════════════════════════════════════════
        //  事件处理
        // ══════════════════════════════════════════════════════

        private void OnDataRecorded(object? sender,  ProductionDataRecordedEventArgs e)
        {
            var record = e.Record;
            if (!MatchesFilter(record)) return;

            Application.Current?.Dispatcher.InvokeAsync(() =>
            {
                RecentRecords.Insert(0, record);
                if (RecentRecords.Count > MaxRecords)
                    RecentRecords.RemoveAt(MaxRecords);
                TotalCount = RecentRecords.Count;
            });
        }

        private bool MatchesFilter(ProductionRecord record)
        {
            if (!string.IsNullOrEmpty(FilterDeviceId)
                && record.DeviceId != FilterDeviceId)
                return false;

            if (!string.IsNullOrEmpty(FilterRecordType)
                && record.RecordType != FilterRecordType)
                return false;

            return true;
        }

        // ══════════════════════════════════════════════════════
        //  命令实现
        // ══════════════════════════════════════════════════════

        private void OnClear()
        {
            RecentRecords.Clear();
            TotalCount = 0;
        }

        private async void OnExport(string? format)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"生产监控数据_{DateTime.Now:yyyyMMdd_HHmmss}",
                Filter = format?.ToLower() == "excel"
                    ? "Excel 文件 (*.xlsx)|*.xlsx"
                    : "CSV 文件 (*.csv)|*.csv"
            };

            if (dlg.ShowDialog() != true) return;

            // 导出当前显示数据（时间范围取最新 500 条的时间边界）
            var filter = new ProductionQueryFilter
            {
                DeviceId = string.IsNullOrEmpty(FilterDeviceId) ? null : FilterDeviceId,
                RecordType = string.IsNullOrEmpty(FilterRecordType) ? null : FilterRecordType,
                MaxCount = MaxRecords
            };

            try
            {
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
        //  销毁
        // ══════════════════════════════════════════════════════

        public override void Destroy()
        {
            _service.DataRecorded -= OnDataRecorded;
            base.Destroy();
        }
    }
}
