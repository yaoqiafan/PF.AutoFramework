using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System.Windows;

namespace PF.Modules.SecsGem.Dialogs.ViewModels
{
    public class CommandEditDialogViewModel : PFDialogViewModelBase
    {
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
        public uint Stream
        {
            get => _stream;
            set => SetProperty(ref _stream, value);
        }

        private uint _function = 1;
        public uint Function
        {
            get => _function;
            set => SetProperty(ref _function, value);
        }

        private string _commandName = string.Empty;
        public string CommandName
        {
            get => _commandName;
            set => SetProperty(ref _commandName, value);
        }

        private bool _isStreamReadOnly;
        public bool IsStreamReadOnly
        {
            get => _isStreamReadOnly;
            set
            {
                if (SetProperty(ref _isStreamReadOnly, value))
                    RaisePropertyChanged(nameof(IsStreamEnabled));
            }
        }
        public bool IsStreamEnabled => !_isStreamReadOnly;

        private bool _isFunctionReadOnly;
        public bool IsFunctionReadOnly
        {
            get => _isFunctionReadOnly;
            set
            {
                if (SetProperty(ref _isFunctionReadOnly, value))
                    RaisePropertyChanged(nameof(IsFunctionEnabled));
            }
        }
        public bool IsFunctionEnabled => !_isFunctionReadOnly;

        // ──────────────────────────────────────────────
        // 生命周期
        // ──────────────────────────────────────────────

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
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, p));
        }

        private void ExecuteCancel()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }
    }
}
