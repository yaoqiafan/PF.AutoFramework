using Microsoft.Extensions.DependencyInjection;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.Production.ViewModels
{
    /// <summary>
    /// 生产数据实时监控 ViewModel。
    /// 订阅 IProductionDataService.DataRecorded 事件，每条新数据写入后自动更新列表，无需轮询。
    /// 内存中最多保留 <see cref="MaxRecords"/> 条，防止无限增长。
    /// </summary>
    public class ProductionMonitorViewModel : RegionViewModelBase
    {
        private const int MaxRecords = 500;

        private readonly IProductionDataService _service;

        // ══════════════════════════════════════════════════════
        //  属性
        // ══════════════════════════════════════════════════════

        /// <summary>获取最近的生产记录列表</summary>
        public ObservableCollection<ProductionRecord> RecentRecords { get; } = [];

        private string? _filterRecordType;
        /// <summary>获取或设置过滤记录类型</summary>
        public string? FilterRecordType
        {
            get => _filterRecordType;
            set => SetProperty(ref _filterRecordType, value);
        }

        private int _totalCount;
        /// <summary>获取或设置总记录数</summary>
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        // ══════════════════════════════════════════════════════
        //  命令
        // ══════════════════════════════════════════════════════

        /// <summary>清空记录命令</summary>
        public DelegateCommand ClearCommand { get; }
        /// <summary>导出记录命令</summary>
        public DelegateCommand<string?> ExportCommand { get; }

        // ══════════════════════════════════════════════════════
        //  构造
        // ══════════════════════════════════════════════════════

        /// <summary>初始化生产监控 ViewModel</summary>
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

        private void OnDataRecorded(object? sender, ProductionDataRecordedEventArgs e)
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

            // 导出当前显示数据
            var filter = new ProductionQueryFilter
            {
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

        /// <summary>销毁 ViewModel 并取消事件订阅</summary>
        public override void Destroy()
        {
            _service.DataRecorded -= OnDataRecorded;
            base.Destroy();
        }
    }
}
