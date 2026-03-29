using PF.SecsGem.DataBase.Entities.Variable;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace PF.Modules.SecsGem.Dialogs.ViewModels
{
    public class VidSelectDialogViewModel : PFDialogViewModelBase
    {
        public VidSelectDialogViewModel()
        {
            Title = "选择变量 (VID)";
            VidItems = new ObservableCollection<VidDisplayItem>();

            ConfirmCommand = new DelegateCommand(ExecuteConfirm, CanConfirm)
                .ObservesProperty(() => SelectedVidItem);
            CancelCommand = new DelegateCommand(ExecuteCancel);
        }

        // ──────────────────────────────────────────────
        // 属性
        // ──────────────────────────────────────────────

        public ObservableCollection<VidDisplayItem> VidItems { get; }

        private VidDisplayItem _selectedVidItem;
        public VidDisplayItem SelectedVidItem
        {
            get => _selectedVidItem;
            set => SetProperty(ref _selectedVidItem, value);
        }

        // ──────────────────────────────────────────────
        // 生命周期
        // ──────────────────────────────────────────────

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            var vids = parameters.GetValue<IEnumerable<VIDEntity>>("Vids");
            if (vids == null) return;

            VidItems.Clear();
            foreach (var v in vids)
                VidItems.Add(new VidDisplayItem(v));

            if (VidItems.Count > 0)
                SelectedVidItem = VidItems[0];
        }

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        private bool CanConfirm() => SelectedVidItem != null;

        private void ExecuteConfirm()
        {
            if (SelectedVidItem == null) return;
            var p = new DialogParameters();
            p.Add("SelectedVid", SelectedVidItem.Entity);
            RequestClose.Invoke(new DialogResult(ButtonResult.OK) { Parameters=p });
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        // ──────────────────────────────────────────────
        // 辅助类
        // ──────────────────────────────────────────────

        public class VidDisplayItem
        {
            public VidDisplayItem(VIDEntity entity)
            {
                Entity = entity;
                DisplayText = $"[{entity.Code}]  {entity.Comment}  ({entity.Type})  [{entity.Description}]";
            }

            public VIDEntity Entity { get; }
            public string DisplayText { get; }
        }
    }
}
