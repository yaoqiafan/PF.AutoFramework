using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Windows;

namespace PF.Modules.SecsGem.Dialogs.ViewModels
{
    /// <summary>命令编辑对话框视图模型</summary>
    public class CommandEditDialogViewModel : PFDialogViewModelBase
    {
        /// <summary>初始化命令编辑对话框</summary>
        public CommandEditDialogViewModel()
        {
            Title = "新建命令";
            ConfirmCommand = new DelegateCommand(ExecuteConfirm);
            CancelCommand  = new DelegateCommand(ExecuteCancel);
        }

        // ──────────────────────────────────────────────
        // 属性
        // ──────────────────────────────────────────────

        private uint _stream = 1;
        /// <summary>获取或设置流编号</summary>
        public uint Stream
        {
            get => _stream;
            set => SetProperty(ref _stream, value);
        }

        private uint _function = 1;
        /// <summary>获取或设置功能编号</summary>
        public uint Function
        {
            get => _function;
            set => SetProperty(ref _function, value);
        }

        private string _commandName = string.Empty;
        /// <summary>获取或设置命令名称</summary>
        public string CommandName
        {
            get => _commandName;
            set => SetProperty(ref _commandName, value);
        }

        private bool _isStreamReadOnly;
        /// <summary>获取或设置流编号是否只读</summary>
        public bool IsStreamReadOnly
        {
            get => _isStreamReadOnly;
            set
            {
                if (SetProperty(ref _isStreamReadOnly, value))
                    RaisePropertyChanged(nameof(IsStreamEnabled));
            }
        }
        /// <summary>获取流编号输入是否启用</summary>
        public bool IsStreamEnabled => !_isStreamReadOnly;

        private bool _isFunctionReadOnly;
        /// <summary>获取或设置功能编号是否只读</summary>
        public bool IsFunctionReadOnly
        {
            get => _isFunctionReadOnly;
            set
            {
                if (SetProperty(ref _isFunctionReadOnly, value))
                    RaisePropertyChanged(nameof(IsFunctionEnabled));
            }
        }
        /// <summary>获取功能编号输入是否启用</summary>
        public bool IsFunctionEnabled => !_isFunctionReadOnly;

        // ──────────────────────────────────────────────
        // 生命周期
        // ──────────────────────────────────────────────

        /// <summary>对话框打开时调用</summary>
        public override void OnDialogOpened(IDialogParameters parameters)
        {
            Stream           = parameters.GetValue<uint>("DefaultStream") > 0
                               ? parameters.GetValue<uint>("DefaultStream") : 1u;
            Function         = parameters.GetValue<uint>("DefaultFunction") > 0
                               ? parameters.GetValue<uint>("DefaultFunction") : 1u;
            bool lockSF      = parameters.GetValue<bool>("LockSF");
            IsStreamReadOnly   = lockSF;
            IsFunctionReadOnly = lockSF;
            CommandName      = string.Empty;
        }

        // ──────────────────────────────────────────────
        // 命令
        // ──────────────────────────────────────────────

        private void ExecuteConfirm()
        {
            if (string.IsNullOrWhiteSpace(_commandName))
            {
                MessageService.ShowMessage("请输入命令名称。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var p = new DialogParameters();
            p.Add("Stream",      _stream);
            p.Add("Function",    _function);
            p.Add("CommandName", _commandName);
            RequestClose.Invoke(new DialogResult(ButtonResult.OK) { Parameters=p });
        }

        private void ExecuteCancel()
        {
            RequestClose.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}
