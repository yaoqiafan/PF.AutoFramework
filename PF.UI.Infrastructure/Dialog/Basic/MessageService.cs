using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.UI.Infrastructure.Dialog.Basic
{
    public class MessageService : IMessageService
    {
        private readonly IDialogService _dialogService;

        public MessageService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        public MessageBoxResult ShowSystemMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            // 原生系统弹窗
            return MessageBox.Show(message, title, buttons, image);
        }

        public Task<ButtonResult> ShowMessageAsync(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            var tcs = new TaskCompletionSource<ButtonResult>();
            var parameters = new DialogParameters
            {
                { "Title", title }, { "Message", message }, { "Buttons", buttons }, { "Image", image }
            };

            // 确保在 UI 线程调用
            Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowDialog("MessageDialog", parameters, result =>
                {
                    tcs.SetResult(result.Result);
                });
            });

            return tcs.Task;
        }

        public void ShowMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, Action<ButtonResult>? callback = null)
        {
            var parameters = new DialogParameters
            {
                { "Title", title }, { "Message", message }, { "Buttons", buttons }, { "Image", image }
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                // 注意：这里仍然使用 ShowDialog 以保证它是模态的（禁止点击后面内容）
                // 但由于没有 await，后面的代码会立刻执行，结果通过 callback 返回
                _dialogService.ShowDialog("MessageDialog", parameters, result =>
                {
                    callback?.Invoke(result.Result);
                });
            });
        }

        public Task<string?> ShowInputAsync(string message, string title = "输入", string defaultText = "")
        {
            var tcs = new TaskCompletionSource<string?>();
            var parameters = new DialogParameters
            {
                { "Title", title }, { "Message", message }, { "DefaultText", defaultText }
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowDialog("InputDialog", parameters, result =>
                {
                    if (result.Result == ButtonResult.OK)
                    {
                        tcs.SetResult(result.Parameters.GetValue<string>("InputText"));
                    }
                    else
                    {
                        tcs.SetResult(null); // 用户点击了取消
                    }
                });
            });

            return tcs.Task;
        }

        public async Task ExecuteWithWaitAsync(Func<Task> action, string message = "请稍候，正在处理中...", string title = "请稍候")
        {
            var tcs = new TaskCompletionSource<bool>();
            var parameters = new DialogParameters
            {
                { "Title", title },
                { "Message", message },
                { "WorkAction", action } // 将任务直接传给弹窗
            };

            Application.Current.Dispatcher.Invoke(() =>
            {
                _dialogService.ShowDialog("WaitDialog", parameters, result =>
                {
                    tcs.SetResult(true);
                });
            });

            await tcs.Task;
        }
    }
}
