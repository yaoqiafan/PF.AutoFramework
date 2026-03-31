using Microsoft.Win32;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Commands;
using System;
using System.Collections.Generic;
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
        //  分页相关属性与逻辑
        // ══════════════════════════════════════════════════════════
        private int _pageSize = 20;
        /// <summary>
        /// 每页显示的条数 (绑定到 UI，用户切换时自动重新计算分页)
        /// </summary>
        public int PageSize
        {
            get => _pageSize;
            set
            {
                // 确保 PageSize 至少为 1，防止除以 0 的异常
                int safeValue = value < 1 ? 1 : value;

                // 如果值发生了实际改变，则重新计算分页并刷新第一页
                if (SetProperty(ref _pageSize, safeValue))
                {
                    RecalculatePagination();
                }
            }
        }

        private int _pageIndex = 1;
        /// <summary>
        /// 当前页码
        /// </summary>
        public int PageIndex
        {
            get => _pageIndex;
            set => SetProperty(ref _pageIndex, value);
        }

        private int _maxPageCount = 1;
        /// <summary>
        /// 总页数
        /// </summary>
        public int MaxPageCount
        {
            get => _maxPageCount;
            set => SetProperty(ref _maxPageCount, value);
        }

        /// <summary>
        /// 页码改变命令
        /// </summary>
        public DelegateCommand<FunctionEventArgs<int>> PageUpdatedCmd => new(PageUpdated);

        /// <summary>
        /// 重新计算总页数，并强制跳回第一页刷新 UI
        /// </summary>
        private void RecalculatePagination()
        {
            if (Records == null) return;

            // 计算总页数 (向上取整)
            int calculatedPages = (int)Math.Ceiling(Records.Count / (double)PageSize);
            MaxPageCount = calculatedPages > 0 ? calculatedPages : 1;

            // 强制重置为第一页，并刷新数据
            PageUpdated(new FunctionEventArgs<int>(1));
        }

        /// <summary>
        /// 执行翻页操作
        /// </summary>
        private void PageUpdated(FunctionEventArgs<int> info)
        {
            if (Records == null) return;

            // 防御性编程：确保传入的页码在合法范围内
            int targetPage = info.Info;
            if (targetPage < 1) targetPage = 1;
            if (targetPage > MaxPageCount) targetPage = MaxPageCount;

            // 同步当前页码
            PageIndex = targetPage;

            // 清空当前 UI 绑定的列表，并注入新一页的数据
            DataList.Clear();
            var pagedData = Records.Skip((PageIndex - 1) * PageSize).Take(PageSize);
            foreach (var item in pagedData)
            {
                DataList.Add(item);
            }
        }

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

        // ══════════════════════════════════════════════════════════
        //  数据集合
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// 全量数据缓存底池（不需要通知 UI，使用 List 提高性能）
        /// </summary>
        private List<MachineDetectionDataWrapper> Records { get; set; } = new List<MachineDetectionDataWrapper>();

        /// <summary>
        /// 当前页显示的数据集合（绑定到 UI 的 DataGrid/ItemsControl）
        /// </summary>
        public ObservableCollection<MachineDetectionDataWrapper> DataList { get; } = new ObservableCollection<MachineDetectionDataWrapper>();

        // ══════════════════════════════════════════════════════════
        //  命令与构造函数
        // ══════════════════════════════════════════════════════════
        public DelegateCommand SearchCommand { get; }
        public DelegateCommand ClearFiltersCommand { get; }

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

                // 1. 数据库层过滤（利用时间和最大条数）
                var dbFilter = new ProductionQueryFilter
                {
                    StartTime = this.StartTime,
                    EndTime = this.EndTime,
                    MaxCount = 5000
                };

                var results = await _productionDataService.QueryAsync(dbFilter);

                // 2. 内存层组合与反序列化
                var query = results.Select(r => new MachineDetectionDataWrapper
                {
                    RecordTime = r.RecordTime,
                    Data = r.Deserialize<MachineDetectionData>()!
                }).Where(x => x.Data != null);

                // 3. 内存层精确条件过滤 (加入 null 检查防崩溃)
                if (!string.IsNullOrWhiteSpace(FilterWaferId))
                {
                    var keyword = FilterWaferId.Trim();
                    query = query.Where(x => !string.IsNullOrEmpty(x.Data.WaferId) &&
                                             x.Data.WaferId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(FilterInternalBatchId))
                {
                    var keyword = FilterInternalBatchId.Trim();
                    query = query.Where(x => !string.IsNullOrEmpty(x.Data.InternalBatchId) &&
                                             x.Data.InternalBatchId.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                if (!string.IsNullOrWhiteSpace(FilterProductModel))
                {
                    var keyword = FilterProductModel.Trim();
                    query = query.Where(x => !string.IsNullOrEmpty(x.Data.ProductModel) &&
                                             x.Data.ProductModel.Contains(keyword, StringComparison.OrdinalIgnoreCase));
                }

                if (FilterIsMatch.HasValue)
                {
                    query = query.Where(x => x.Data.IsMatch == FilterIsMatch.Value);
                }

                // 4. 将最终结果转入全量缓存列表
                Records = query.ToList();

                // 5. 计算总页数并自动刷新第一页的数据
                RecalculatePagination();
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