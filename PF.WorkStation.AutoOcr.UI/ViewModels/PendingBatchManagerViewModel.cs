using Microsoft.Win32;
using PF.Core.Enums;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Commands;
using Prism.Ioc;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// 未完成批次管理弹窗 ViewModel。
    /// 展示当前工位所有未达到期望片数的批次，并提供删除、数据存储、导出操作（仅显示，不允许切换当前批次）。
    /// </summary>
    public class PendingBatchManagerViewModel : PFDialogViewModelBase
    {
        private readonly WSDataModule? _dataModule;

        public ObservableCollection<PendingBatchItemViewModel> Batches { get; } = [];

        public PendingBatchManagerViewModel(IContainerProvider containerProvider)
        {
            Title = "未完成批次管理";
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WSDataModule)) as WSDataModule;
            CloseCommand = new DelegateCommand(() => RequestClose.Invoke(ButtonResult.Cancel));
        }

        public DelegateCommand CloseCommand { get; }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            Refresh();
        }

        private void Refresh()
        {
            Batches.Clear();
            if (_dataModule == null) return;

            // 两工位当前活跃批次 ID，用于标记"使用中"行
            var ws1ActiveId = _dataModule.Station1MesDetectionData.InternalBatchId;
            var ws2ActiveId = _dataModule.Station2MesDetectionData.InternalBatchId;

            foreach (var info in _dataModule.GetAllPendingBatches())
            {
                bool isCurrent = (!string.IsNullOrEmpty(ws1ActiveId) && info.BatchId == ws1ActiveId)
                              || (!string.IsNullOrEmpty(ws2ActiveId) && info.BatchId == ws2ActiveId);
                var item = new PendingBatchItemViewModel(info, _dataModule, isCurrent);
                item.Deleted += (_, _) => Refresh();
                Batches.Add(item);
            }
        }
    }

    /// <summary>
    /// 单条未完成批次行 ViewModel，包含删除、数据存储、导出 3 个操作命令。
    /// </summary>
    public class PendingBatchItemViewModel : ViewModelBase
    {
        private readonly WSDataModule _dataModule;
        public PendingBatchInfo Info { get; }

        public string BatchId        => Info.BatchId;
        public string RecipeName     => Info.RecipeName;
        public int    TotalCount     => Info.TotalCount;
        public int    CompletedCount => Info.CompletedCount;

        /// <summary>该批次是否为当前工位正在使用的活跃批次</summary>
        public bool IsCurrentBatch { get; }

        /// <summary>是否允许操作：非当前活跃批次 + Engineer 及以上权限</summary>
        public bool CanOperate => !IsCurrentBatch && UserService.IsAuthorized(UserLevel.Engineer);

        public event EventHandler? Deleted;

        public DelegateCommand DeleteCommand   { get; }
        public DelegateCommand SaveToDbCommand { get; }
        public DelegateCommand ExportCommand   { get; }

        public PendingBatchItemViewModel(PendingBatchInfo info, WSDataModule dataModule, bool isCurrentBatch)
        {
            Info           = info;
            _dataModule    = dataModule;
            IsCurrentBatch = isCurrentBatch;

            DeleteCommand = new DelegateCommand(async () =>
            {
                var r = await MessageService.ShowMessageAsync(
                    $"确定删除批次 {BatchId}？\n此操作不可撤销，内存数据将永久丢失。",
                    "删除确认", MessageBoxButton.OKCancel, MessageBoxImage.Warning);
                if (r !=  ButtonResult.OK) return;
                var result = _dataModule.DeletePendingBatch(BatchId);
                if (!result.IsSuccess)
                    MessageService.ShowMessage($"删除失败：{result.ErrorMessage}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                else
                    Deleted?.Invoke(this, EventArgs.Empty);
            });

            SaveToDbCommand = new DelegateCommand(async () =>
            {
                var r = await MessageService.ShowMessageAsync(
                    $"将批次 {BatchId}（已完成 {CompletedCount}/{TotalCount} 片）强制落盘到数据库？",
                    "确认", MessageBoxButton.OKCancel, MessageBoxImage.Question);
                if (r != ButtonResult.OK) return;
                var result = await _dataModule.ForceCommitBatchAsync(BatchId);
                MessageService.ShowMessage(
                    result.IsSuccess ? "数据已成功写入生产数据库。" : $"写入失败：{result.ErrorMessage}",
                    result.IsSuccess ? "完成" : "错误",
                    MessageBoxButton.OK,
                    result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
            });

            ExportCommand = new DelegateCommand(async () =>
            {
                var dlg = new SaveFileDialog
                {
                    Title      = "导出批次数据",
                    Filter     = "CSV 文件 (*.csv)|*.csv",
                    FileName   = $"{BatchId}_{DateTime.Now:yyyyMMddHHmmss}.csv"
                };
                if (dlg.ShowDialog() != true) return;
                var result = _dataModule.ExportBatchToCsv(BatchId, dlg.FileName);
                MessageService.ShowMessage(
                    result.IsSuccess ? $"已导出至：{dlg.FileName}" : $"导出失败：{result.ErrorMessage}",
                    result.IsSuccess ? "完成" : "错误",
                    MessageBoxButton.OK,
                    result.IsSuccess ? MessageBoxImage.Information : MessageBoxImage.Error);
            });
        }
    }
}
