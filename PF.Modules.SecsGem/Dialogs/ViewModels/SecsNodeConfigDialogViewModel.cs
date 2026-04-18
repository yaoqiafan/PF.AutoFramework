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
    /// <summary>Secs节点配置对话框视图模型</summary>
    public class SecsNodeConfigDialogViewModel : PFDialogViewModelBase
    {
        private IEnumerable<VIDEntity> _availableVids;

        /// <summary>初始化Secs节点配置对话框</summary>
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

        /// <summary>获取数据类型选项集合</summary>
        public ObservableCollection<DataType> DataTypeOptions { get; }

        private DataType _selectedDataType = DataType.ASCII;
        /// <summary>获取或设置选中的数据类型</summary>
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
        /// <summary>获取或设置是否为变量节点</summary>
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
        /// <summary>获取或设置变量代码</summary>
        public uint VariableCode
        {
            get => _variableCode;
            set => SetProperty(ref _variableCode, value);
        }

        private string _variableDescription = string.Empty;
        /// <summary>获取或设置变量描述</summary>
        public string VariableDescription
        {
            get => _variableDescription;
            set => SetProperty(ref _variableDescription, value);
        }

        private string _value = string.Empty;
        /// <summary>获取或设置值</summary>
        public string Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }

        // ──────────────────────────────────────────────
        // 计算可见性属性
        // ──────────────────────────────────────────────

        /// <summary>获取是否可以绑定变量</summary>
        public bool CanBindVariable => _selectedDataType != DataType.LIST;

        /// <summary>获取列表提示是否可见</summary>
        public Visibility IsListTipVisible =>
            _selectedDataType == DataType.LIST ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>获取变量面板是否可见</summary>
        public Visibility IsVariablePanelVisible =>
            (_isVariableNode && _selectedDataType != DataType.LIST)
                ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>获取值输入区域是否可见</summary>
        public Visibility IsValueInputVisible =>
            (!_isVariableNode && _selectedDataType != DataType.LIST)
                ? Visibility.Visible : Visibility.Collapsed;

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        /// <summary>选择VID命令</summary>
        public DelegateCommand SelectVidCommand { get; }

        // ──────────────────────────────────────────────
        // 生命周期
        // ──────────────────────────────────────────────

        /// <summary>对话框打开时调用</summary>
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
