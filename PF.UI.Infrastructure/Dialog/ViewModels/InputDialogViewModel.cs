using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.Dialog.ViewModels
{
    public class InputDialogViewModel : PFDialogViewModelBase
    {
        private string _message;
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        private string _inputText;
        public string InputText { get => _inputText; set => SetProperty(ref _inputText, value); }

        public DelegateCommand OkCommand { get; }
        public DelegateCommand CancelCommand { get; }

        public InputDialogViewModel()
        {
            OkCommand = new DelegateCommand(ExecuteOk);
            CancelCommand = new DelegateCommand(() => RequestClose.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        private void ExecuteOk()
        {
            // 将用户输入的结果通过参数返回
            var parameters = new DialogParameters { { "InputText", InputText } };
            RequestClose.Invoke(new DialogResult(ButtonResult.OK));
        }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            Message = parameters.GetValue<string>("Message") ?? "请输入内容：";
            Title = parameters.GetValue<string>("Title") ?? "输入";
            InputText = parameters.GetValue<string>("DefaultText") ?? ""; // 可选的默认填充文本
        }
    }
}
