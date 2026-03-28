using PF.Core.Enums;
using PF.SecsGem.DataBase.Entities.Variable;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace PF.Modules.SecsGem.Dialogs.ViewModels
{
    public class SecsNodeConfigDialogViewModel : PFDialogViewModelBase
    {
        private IEnumerable<VIDEntity> _availableVids;

        public SecsNodeConfigDialogViewModel()
        {
            Title = "添加节点";
            DataTypeOptions = new ObservableCollection<DataType>(
                (DataType[])Enum.GetValues(typeof(DataType)));

            ConfirmCommand = new DelegateCommand(ExecuteConfirm);
            CancelCommand  = new DelegateCommand(ExecuteCancel);
            SelectVidCommand = new DelegateCommand(ExecuteSelectVid,
                () => CanBindVariable && IsVariableNode)
                .ObservesProperty(() => CanBindVariable)
                .ObservesProperty(() => IsVariableNode);
        }

        // ──────────────────────────────────────────────
        // 核心属性
        // ──────────────────────────────────────────────

        public ObservableCollection<DataType> DataTypeOptions { get; }

        private DataType _selectedDataType = DataType.ASCII;
        public DataType SelectedDataType
        {
            get => _selectedDataType;
            set
            {
                if (SetProperty(ref _selectedDataType, value))
                {
                    if (value == DataType.LIST)
                        IsVariableNode = false;

                    RaisePropertyChanged(nameof(CanBindVariable));
                    RaisePropertyChanged(nameof(IsListTipVisible));
                    RaisePropertyChanged(nameof(IsVariablePanelVisible));
                    RaisePropertyChanged(nameof(IsValueInputVisible));
                }
            }
        }

        private bool _isVariableNode;
        public bool IsVariableNode
        {
            get => _isVariableNode;
            set
            {
                if (SetProperty(ref _isVariableNode, value))
                {
                    RaisePropertyChanged(nameof(IsVariablePanelVisible));
                    RaisePropertyChanged(nameof(IsValueInputVisible));
                }
            }
        }

        private uint _variableCode;
        public uint VariableCode
        {
            get => _variableCode;
            set => SetProperty(ref _variableCode, value);
        }

        private string _variableDescription = string.Empty;
        public string VariableDescription
        {
            get => _variableDescription;
            set => SetProperty(ref _variableDescription, value);
        }

        private string _value = string.Empty;
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        // ──────────────────────────────────────────────
        // 计算可见性属性
        // ──────────────────────────────────────────────

        public bool CanBindVariable => _selectedDataType != DataType.LIST;

        public Visibility IsListTipVisible =>
            _selectedDataType == DataType.LIST ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IsVariablePanelVisible =>
            (_isVariableNode && _selectedDataType != DataType.LIST)
                ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IsValueInputVisible =>
            (!_isVariableNode && _selectedDataType != DataType.LIST)
                ? Visibility.Visible : Visibility.Collapsed;

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        public DelegateCommand SelectVidCommand { get; }

        // ──────────────────────────────────────────────
        // 生命周期
        // ──────────────────────────────────────────────

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            _availableVids = parameters.GetValue<IEnumerable<VIDEntity>>("Vids");
            SelectedDataType = DataType.ASCII;
            IsVariableNode   = false;
            VariableCode     = 0;
            VariableDescription = string.Empty;
            Value            = string.Empty;
        }

        // ──────────────────────────────────────────────
        // 命令实现
        // ──────────────────────────────────────────────

        private void ExecuteSelectVid()
        {
            var vids = _availableVids;
            if (vids == null)
            {
                MessageService.ShowMessage("变量库 (VID) 为空，请先导入参数配置。",
                    "提示", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                return;
            }

            var p = new DialogParameters();
            p.Add("Vids", vids);

            DialogService.ShowDialog("VidSelectDialog", p, result =>
            {
                if (result.Result != ButtonResult.OK) return;
                var vid = result.Parameters.GetValue<VIDEntity>("SelectedVid");
                if (vid == null) return;
                VariableCode = vid.Code;
                VariableDescription = $"VID:{vid.Code} [{vid.Description}]";
            });
        }

        private void ExecuteConfirm()
        {
            if (_isVariableNode && _variableCode == 0)
            {
                MessageService.ShowMessage("已勾选变量绑定，但尚未选择 VID，请先点击「选择 VID」。",
                    "提示", System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var p = new DialogParameters();
            p.Add("DataType",           _selectedDataType);
            p.Add("IsVariableNode",     _isVariableNode);
            p.Add("VariableCode",       _variableCode);
            p.Add("VariableDescription", _variableDescription);
            p.Add("Value",              _value);
            RequestClose.Invoke(new DialogResult(ButtonResult.OK) { Parameters=p });
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}
