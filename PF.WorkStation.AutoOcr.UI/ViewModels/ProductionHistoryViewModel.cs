using Microsoft.Win32;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    public class ProductionHistoryViewModel : RegionViewModelBase
    {
        private readonly IProductionDataService _productionDataService;

        // ══════════════════════════════════════════════════════════
        //  时间过滤 (传给底层接口)
        // ══════════════════════════════════════════════════════════
        private DateTime? _startTime = DateTime.Today;
        public DateTime? StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }

        private DateTime? _endTime = DateTime.Today.AddDays(1).AddTicks(-1);
        public DateTime? EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }

        // ══════════════════════════════════════════════════════════
        //  业务属性过滤 (内存级精确过滤)
        // ══════════════════════════════════════════════════════════
        private string? _filterWaferId;
        public string? FilterWaferId { get => _filterWaferId; set => SetProperty(ref _filterWaferId, value); }

        private string? _filterInternalBatchId;
        public string? FilterInternalBatchId { get => _filterInternalBatchId; set => SetProperty(ref _filterInternalBatchId, value); }

        private string? _filterProductModel;
        public string? FilterProductModel { get => _filterProductModel; set => SetProperty(ref _filterProductModel, value); }

        // 可空 bool：null表示全部，true表示匹配，false表示不匹配
        private bool? _filterIsMatch = null;
        public bool? FilterIsMatch { get => _filterIsMatch; set => SetProperty(ref _filterIsMatch, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }

        // 数据集合
        public ObservableCollection<MachineDetectionDataWrapper> Records { get; set; } = new ObservableCollection<MachineDetectionDataWrapper>();

        // 命令
        public DelegateCommand SearchCommand { get; }
        public DelegateCommand ClearFiltersCommand { get; }
        // ... (Export 命令同之前，为节省篇幅略过重复代码)

        public ProductionHistoryViewModel(IProductionDataService productionDataService)
        {
            _productionDataService = productionDataService;
            SearchCommand = new DelegateCommand(async () => await OnSearchAsync());
            ClearFiltersCommand = new DelegateCommand(OnClearFilters);
        }

        private void OnClearFilters()
        {
            FilterWaferId = string.Empty;
            FilterInternalBatchId = string.Empty;
            FilterProductModel = string.Empty;
            FilterIsMatch = null;
        }

        private async Task OnSearchAsync()
        {
            try
            {
                IsBusy = true;
                Records.Clear();

                // 1. 数据库层过滤（利用时间和最大条数）
                var dbFilter = new ProductionQueryFilter
                {
                    StartTime = this.StartTime,
                    EndTime = this.EndTime,
                    MaxCount = 5000 // 稍微调大一点，因为后续还有内存过滤
                };

                var results = await _productionDataService.QueryAsync(dbFilter);

                // 2. 内存层组合与反序列化
                var query = results.Select(r => new MachineDetectionDataWrapper
                {
                    RecordTime = r.RecordTime,
                    Data = r.Deserialize<MachineDetectionData>()!
                }).Where(x => x.Data != null);

                // 3. 内存层精确条件过滤 (LINQ)
                if (!string.IsNullOrWhiteSpace(FilterWaferId))
                    query = query.Where(x => x.Data.WaferId.Contains(FilterWaferId.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(FilterInternalBatchId))
                    query = query.Where(x => x.Data.InternalBatchId.Contains(FilterInternalBatchId.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!string.IsNullOrWhiteSpace(FilterProductModel))
                    query = query.Where(x => x.Data.ProductModel.Contains(FilterProductModel.Trim(), StringComparison.OrdinalIgnoreCase));

                if (FilterIsMatch.HasValue)
                    query = query.Where(x => x.Data.IsMatch == FilterIsMatch.Value);

                // 4. 绑定到UI
                foreach (var item in query)
                {
                    Records.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"查询失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class MachineDetectionDataWrapper
    {
        public DateTime RecordTime { get; set; }
        public MachineDetectionData Data { get; set; } = null!;
    }
}