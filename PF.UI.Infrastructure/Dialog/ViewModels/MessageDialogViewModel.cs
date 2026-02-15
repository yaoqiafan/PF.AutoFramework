using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.UI.Infrastructure.Dialog.ViewModels
{
    public class MessageDialogViewModel : PFDialogViewModelBase
    {
        private string _message;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private string _iconText;
        public string IconText
        {
            get => _iconText;
            set => SetProperty(ref _iconText, value);
        }

        private string _iconColor = "#333333";
        public string IconColor
        {
            get => _iconColor;
            set => SetProperty(ref _iconColor, value);
        }

        // 控制按钮显示的属性
        private Visibility _okVisibility = Visibility.Collapsed;
        public Visibility OkVisibility
        {
            get => _okVisibility;
            set => SetProperty(ref _okVisibility, value);
        }

        private Visibility _cancelVisibility = Visibility.Collapsed;
        public Visibility CancelVisibility
        {
            get => _cancelVisibility;
            set => SetProperty(ref _cancelVisibility, value);
        }

        private Visibility _yesVisibility = Visibility.Collapsed;
        public Visibility YesVisibility
        {
            get => _yesVisibility;
            set => SetProperty(ref _yesVisibility, value);
        }

        private Visibility _noVisibility = Visibility.Collapsed;
        public Visibility NoVisibility
        {
            get => _noVisibility;
            set => SetProperty(ref _noVisibility, value);
        }

        // 命令
        public DelegateCommand<string> CloseDialogCommand { get; }

        public MessageDialogViewModel()
        {
            CloseDialogCommand = new DelegateCommand<string>(ExecuteCloseDialog);
        }

        private void ExecuteCloseDialog(string parameter)
        {
            ButtonResult result = ButtonResult.None;
            switch (parameter?.ToLower())
            {
                case "ok": result = ButtonResult.OK; break;
                case "cancel": result = ButtonResult.Cancel; break;
                case "yes": result = ButtonResult.Yes; break;
                case "no": result = ButtonResult.No; break;
            }
            RequestClose.Invoke(new DialogResult(result));
        }

        public override void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);

            Message = parameters.GetValue<string>("Message") ?? "";
            Title = parameters.GetValue<string>("Title") ?? "提示";

            var buttons = parameters.GetValue<MessageBoxButton>("Buttons");
            var image = parameters.GetValue<MessageBoxImage>("Image");

            ConfigureButtons(buttons);
            ConfigureIcon(image);
        }

        private void ConfigureButtons(MessageBoxButton buttons)
        {
            OkVisibility = CancelVisibility = YesVisibility = NoVisibility = Visibility.Collapsed;

            switch (buttons)
            {
                case MessageBoxButton.OK:
                    OkVisibility = Visibility.Visible;
                    break;
                case MessageBoxButton.OKCancel:
                    OkVisibility = CancelVisibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNo:
                    YesVisibility = NoVisibility = Visibility.Visible;
                    break;
                case MessageBoxButton.YesNoCancel:
                    YesVisibility = NoVisibility = CancelVisibility = Visibility.Visible;
                    break;
            }
        }

        private void ConfigureIcon(MessageBoxImage image)
        {
            switch (image)
            {
                case MessageBoxImage.Information:
                    IconText = "ℹ️"; IconColor = "#2196F3"; break; // Info blue
                case MessageBoxImage.Warning:
                    IconText = "⚠️"; IconColor = "#FF9800"; break; // Warning orange
                case MessageBoxImage.Error:
                    IconText = "❌"; IconColor = "#F44336"; break; // Error red
                case MessageBoxImage.Question:
                    IconText = "❓"; IconColor = "#4CAF50"; break; // Question green
                default:
                    IconText = ""; break;
            }
        }
    }
}
