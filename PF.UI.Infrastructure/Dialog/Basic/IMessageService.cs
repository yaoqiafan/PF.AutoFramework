using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.UI.Infrastructure.Dialog.Basic
{
    public interface IMessageService
    {
        // 1. 系统原生弹窗（直接阻塞整个 UI 线程）
        MessageBoxResult ShowSystemMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information);

        // 2. 自定义提示弹窗 (异步阻塞代码，不阻塞 UI 渲染，推荐使用)
        Task<ButtonResult> ShowMessageAsync(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information);

        // 3. 自定义提示弹窗 (非阻塞，提供回调函数，代码会立即往下执行)
        void ShowMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, Action<ButtonResult>? callback = null);

        // 4. 自定义输入框 (异步阻塞，等待用户输入，返回输入字符串，取消则返回 null)
        Task<string?> ShowInputAsync(string message, string title = "输入", string defaultText = "");

        // 5. 耗时操作等待框 (包裹一个后台任务，任务执行期间转圈，执行完毕自动关闭弹窗)
        Task ExecuteWithWaitAsync(Func<Task> action, string message = "请稍候，正在处理中...", string title = "请稍候");
    }
}
