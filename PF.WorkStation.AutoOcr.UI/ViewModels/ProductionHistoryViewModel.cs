using Microsoft.Win32;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using PF.Core.Interfaces.Production;
using PF.UI.Infrastructure.PrismBase;
using PF.UI.Shared.Data;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// ProductionHistoryViewModel
    /// </summary>
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
        /// <summary>
        /// 获取或设置 StartTime
        /// </summary>
        public DateTime? StartTime { get => _startTime; set => SetProperty(ref _startTime, value); }

        private DateTime? _endTime = DateTime.Today.AddDays(1).AddTicks(-1);
        /// <summary>
        /// 获取或设置 EndTime
        /// </summary>
        public DateTime? EndTime { get => _endTime; set => SetProperty(ref _endTime, value); }

        // ══════════════════════════════════════════════════════════
        //  业务属性过滤 (内存级精确过滤)
        // ══════════════════════════════════════════════════════════
        private string? _filterWaferId;
        /// <summary>
        /// 获取或设置 FilterWaferId
        /// </summary>
        public string? FilterWaferId { get => _filterWaferId; set => SetProperty(ref _filterWaferId, value); }

        private string? _filterInternalBatchId;
        /// <summary>
        /// 获取或设置 FilterInternalBatchId
        /// </summary>
        public string? FilterInternalBatchId { get => _filterInternalBatchId; set => SetProperty(ref _filterInternalBatchId, value); }

        private string? _filterProductModel;
        /// <summary>
        /// 获取或设置 FilterProductModel
        /// </summary>
        public string? FilterProductModel { get => _filterProductModel; set => SetProperty(ref _filterProductModel, value); }

        // 可空 bool：null表示全部，true表示匹配，false表示不匹配
        private bool? _filterIsMatch = null;
        /// <summary>
        /// 获取或设置 FilterIsMatch
        /// </summary>
        public bool? FilterIsMatch { get => _filterIsMatch; set => SetProperty(ref _filterIsMatch, value); }

        private bool _isBusy;
        /// <summary>
        /// 获取或设置 IsBusy
        /// </summary>
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

        private MachineDetectionDataWrapper _currentMachineDetection;
        /// <summary>获取或设置工位1最新检测数据</summary>
        public MachineDetectionDataWrapper CurrentMachineDetection
        {
            get => _currentMachineDetection;
            set => SetProperty(ref _currentMachineDetection, value);
        }


        // ══════════════════════════════════════════════════════════
        //  命令与构造函数
        // ══════════════════════════════════════════════════════════
        /// <summary>
        /// Search 命令
        /// </summary>
        public DelegateCommand SearchCommand { get; }
        /// <summary>
        /// ClearFilters 命令
        /// </summary>
        public DelegateCommand ClearFiltersCommand { get; }

        /// <summary>
        /// 导出指令
        /// </summary>
        public DelegateCommand ExportLogsCommand { get; }




        /// <summary>
        /// ProductionHistoryViewModel 构造函数
        /// </summary>

        public ProductionHistoryViewModel(IProductionDataService productionDataService)
        {
            _productionDataService = productionDataService;
            SearchCommand = new DelegateCommand(async () => await OnSearchAsync());
            ClearFiltersCommand = new DelegateCommand(OnClearFilters);

            ExportLogsCommand = new DelegateCommand(ExportLogs);
        }

        private void ExportLogs()
        {

            try
            {
                if (!Records.Any())
                {
                    MessageService.ShowMessage("当前列表中没有数据可供导出。", "提示");
                    return;
                }
                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel 文件 (*.xlsx)|*.xlsx",
                    FileName = $"Log_Export_{DateTime.Now:yyyyMMdd_HHmmss}",
                    DefaultExt = ".xlsx"
                };
                if (saveDialog.ShowDialog() == true)
                {
                    using (XSSFWorkbook wk = new XSSFWorkbook())
                    {
                        ISheet sheet = wk.CreateSheet("Data");
                        sheet.CreateRow(0).CreateCell(0).SetCellValue("记录时间");
                        sheet.GetRow(0).CreateCell(1).SetCellValue("内部批次号");
                        sheet.GetRow(0).CreateCell(2).SetCellValue("客户批次");
                        sheet.GetRow(0).CreateCell(3).SetCellValue("晶圆ID");
                        sheet.GetRow(0).CreateCell(4).SetCellValue("产品型号");
                        sheet.GetRow(0).CreateCell(5).SetCellValue("匹配结果");
                        sheet.GetRow(0).CreateCell(6).SetCellValue("异常信息");
                        sheet.GetRow(0).CreateCell(7).SetCellValue("OCR文本");
                        sheet.GetRow(0).CreateCell(8).SetCellValue("条码1");
                        sheet.GetRow(0).CreateCell(9).SetCellValue("条码2");
                        sheet.GetRow(0).CreateCell(10).SetCellValue("条码3");
                        sheet.GetRow(0).CreateCell(11).SetCellValue("操作员工号");
                        sheet.GetRow(0).CreateCell(12).SetCellValue("配方名称");
                        sheet.GetRow(0).CreateCell(13).SetCellValue("图片");
                        sheet.GetRow(0).CreateCell(14).SetCellValue("超链接");
                        for (int i = 0; i < Records?.Count; i++)
                        {
                            sheet.CreateRow(i + 1).CreateCell(0).SetCellValue(Records[i].Data.Time);
                            sheet.GetRow(i + 1).CreateCell(1).SetCellValue(Records[i].Data.InternalBatchId);
                            sheet.GetRow(i + 1).CreateCell(2).SetCellValue(Records[i].Data.CustomerBatch);
                            sheet.GetRow(i + 1).CreateCell(3).SetCellValue(Records[i].Data.WaferId);
                            sheet.GetRow(i + 1).CreateCell(4).SetCellValue(Records[i].Data.ProductModel);
                            sheet.GetRow(i + 1).CreateCell(5).SetCellValue(Records[i].Data.IsMatch);
                            sheet.GetRow(i + 1).CreateCell(6).SetCellValue(Records[i].Data.ErrorMessage);
                            sheet.GetRow(i + 1).CreateCell(7).SetCellValue(Records[i].Data.OcrText);
                            sheet.GetRow(i + 1).CreateCell(8).SetCellValue(Records[i].Data.Barcode1);
                            sheet.GetRow(i + 1).CreateCell(9).SetCellValue(Records[i].Data.Barcode2);
                            sheet.GetRow(i + 1).CreateCell(10).SetCellValue(Records[i].Data.Barcode3);
                            sheet.GetRow(i + 1).CreateCell(11).SetCellValue(Records[i].Data.OperatorId);
                            sheet.GetRow(i + 1).CreateCell(12).SetCellValue(Records[i].Data.RecipeName);
                            WriteImageToExcel(Records[i].Data.ImagePath, wk, sheet, i + 1, 13);
                            var cell = sheet.GetRow(i + 1).CreateCell(14);
                            WritehyperlinkToExcel(Records[i].Data.ImagePath, wk, cell);
                        }
                        using (FileStream fs = new FileStream(saveDialog.FileName, FileMode.Create, FileAccess.Write))
                        {
                            wk.Write(fs);
                        }
                    }
                    MessageService.ShowMessage($"导出成功！\n路径: {saveDialog.FileName}", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"导出失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

           
        }


        /// <summary>
        /// 向指定表格单元格写入图片(需要添加   SkiaSharp  包)
        /// </summary>
        /// <param name="imgPath">图片路径</param>
        /// <param name="workbook">工作薄</param>
        /// <param name="sheet">工作表</param>
        /// <param name="rowindex">行索引</param>
        /// <param name="colindex">列索引</param>

        private void WriteImageToExcel(string imgPath, IWorkbook workbook, ISheet sheet, int rowindex, int colindex)
        {
            try
            {
                if (!File.Exists(imgPath))
                {
                    return;
                }
                byte[] imgBytes = File.ReadAllBytes(imgPath);
                int picIndex = workbook.AddPicture(imgBytes, NPOI.SS.UserModel.PictureType.PNG);
                var drawing = sheet.CreateDrawingPatriarch();
                IClientAnchor anchor = workbook.GetCreationHelper().CreateClientAnchor();
                anchor.Col1 = colindex;
                anchor.Row1 = rowindex;
                anchor.Col2 = colindex + 1;
                anchor.Row2 = rowindex + 1;
                IPicture pic = drawing.CreatePicture(anchor, picIndex);

            }
            catch
            {

            }

        }


        /// <summary>
        /// 向指定表格单元格写入超链接
        /// </summary>
        /// <param name="hyperlink">超链接路径</param>
        /// <param name="workbook">工作薄</param>
        /// <param name="cell">单元格</param>
        private void WritehyperlinkToExcel(string hyperlink, IWorkbook workbook, NPOI.SS.UserModel.ICell cell)
        {
            try
            {
                var createHelper = workbook.GetCreationHelper();
                cell.SetCellValue(hyperlink);
                IHyperlink link2 = createHelper.CreateHyperlink(HyperlinkType.File);
                // 建议使用 file:/// 协议，并处理空格
                link2.Address = $"file:///{hyperlink}";
                cell.Hyperlink = link2;
            }
            catch
            {

            }

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
                    MaxCount = 50000
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
    /// <summary>
    /// MachineDetectionDataWrapper
    /// </summary>

    public class MachineDetectionDataWrapper
    {
        /// <summary>
        /// 获取或设置 RecordTime
        /// </summary>
        public DateTime RecordTime { get; set; }
        /// <summary>
        /// 获取或设置 Data
        /// </summary>
        public MachineDetectionData Data { get; set; } = null!;
    }
}