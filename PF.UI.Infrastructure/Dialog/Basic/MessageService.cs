using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.UI.Infrastructure.Dialog.Basic
{
    /// <summary>
    /// IMessageService 服务
    /// </summary>
    public class MessageService : IMessageService
    {
        private readonly IDialogService _dialogService;

        /// <summary>
        /// MessageService 服务
        /// </summary>
        public MessageService(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public MessageBoxResult ShowSystemMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            // 原生系统弹窗
            return MessageBox.Show(message, title, buttons, image);
        }

        /// <summary>
        /// 线程感知的对话框调度：
        ///   · UI 线程调用 → 内联模态显示（方法返回时对话框已关闭、Task 已完成，
        ///     兼容启动路径等对"GetAwaiter().GetResult() 不死锁"的既有依赖）；
        ///   · 后台线程调用 → InvokeAsync 排队到 UI 线程，调用方立即拿到未完成的 Task，
        ///     不再被模态对话框同步阻塞（原 Dispatcher.Invoke 会卡住后台线程直到用户关闭弹窗）。
        /// </summary>
        private static void DispatchToUi(Action showDialogAction)
        {
            var dispatcher = Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                showDialogAction();
            else
                dispatcher.InvokeAsync(showDialogAction);
        }

        /// <summary>
        /// ShowMessageAsync异步操作
        /// </summary>
        public Task<ButtonResult> ShowMessageAsync(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information)
        {
            var tcs = new TaskCompletionSource<ButtonResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parameters = new DialogParameters
            {
                { "Title", title }, { "Message", message }, { "Buttons", buttons }, { "Image", image }
            };

            DispatchToUi(() =>
            {
                _dialogService.ShowDialog("MessageDialog", parameters, result =>
                {
                    tcs.TrySetResult(result.Result);
                });
            });

            return tcs.Task;
        }

        /// <summary>
        /// 初始化实例
        /// </summary>
        public void ShowMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, Action<ButtonResult>? callback = null)
        {
            var parameters = new DialogParameters
            {
                { "Title", title }, { "Message", message }, { "Buttons", buttons }, { "Image", image }
            };

            DispatchToUi(() =>
            {
                // 注意：这里仍然使用 ShowDialog 以保证它是模态的（禁止点击后面内容）
                // 结果通过 callback 返回
                _dialogService.ShowDialog("MessageDialog", parameters, result =>
                {
                    callback?.Invoke(result.Result);
                });
            });
        }

        /// <summary>
        /// ShowInputAsync异步操作
        /// </summary>
        public Task<string?> ShowInputAsync(string message, string title = "输入", string defaultText = "")
        {
            var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parameters = new DialogParameters
            {
                { "Title", title }, { "Message", message }, { "DefaultText", defaultText }
            };

            DispatchToUi(() =>
            {
                _dialogService.ShowDialog("InputDialog", parameters, result =>
                {
                    if (result.Result == ButtonResult.OK)
                    {
                        var res = result.Parameters.GetValue<string>("InputText");

                        tcs.TrySetResult(res);
                    }
                    else
                    {
                        tcs.TrySetResult(null); // 用户点击了取消
                    }
                });
            });

            return tcs.Task;
        }

        /// <summary>
        /// ExecuteWithWaitAsync异步操作
        /// </summary>
        public async Task ExecuteWithWaitAsync(Func<Task> action, string message = "请稍候，正在处理中...", string title = "请稍候")
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var parameters = new DialogParameters
            {
                { "Title", title },
                { "Message", message },
                { "WorkAction", action } // 将任务直接传给弹窗
            };

            DispatchToUi(() =>
            {
                _dialogService.ShowDialog("WaitDialog", parameters, result =>
                {
                    tcs.TrySetResult(true);
                });
            });

            await tcs.Task;
        }
    }
}
