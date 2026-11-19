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
    public interface IMessageService
    {
        // 1. 系统原生弹窗（直接阻塞整个 UI 线程）
        /// <summary>
        /// 初始化实例
        /// </summary>
        MessageBoxResult ShowSystemMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information);

        // 2. 自定义提示弹窗 (异步阻塞代码，不阻塞 UI 渲染，推荐使用)
        /// <summary>
        /// ShowMessageAsync异步操作
        /// </summary>
        Task<ButtonResult> ShowMessageAsync(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information);

        // 3. 自定义提示弹窗（结果经回调返回）
        /// <summary>
        /// 显示提示弹窗，结果经 <paramref name="callback"/> 返回。
        /// <para>在 UI 线程上调用时是<b>模态</b>的：方法在弹窗关闭后才返回，回调已在返回前执行完——
        /// 调用方可以在返回后直接读取回调里设置的结果（调试页状态守卫、参数页放弃修改确认都依赖这一点）。
        /// 在后台线程上调用时排队到 UI 线程、立即返回，回调稍后执行。</para>
        /// </summary>
        void ShowMessage(string message, string title = "提示", MessageBoxButton buttons = MessageBoxButton.OK, MessageBoxImage image = MessageBoxImage.Information, Action<ButtonResult>? callback = null);

        // 4. 自定义输入框 (异步阻塞，等待用户输入，返回输入字符串，取消则返回 null)
        /// <summary>
        /// ShowInputAsync异步操作
        /// </summary>
        Task<string?> ShowInputAsync(string message, string title = "输入", string defaultText = "");

        // 5. 耗时操作等待框 (包裹一个后台任务，任务执行期间转圈，执行完毕自动关闭弹窗)
        /// <summary>
        /// ExecuteWithWaitAsync异步操作
        /// </summary>
        Task ExecuteWithWaitAsync(Func<Task> action, string message = "请稍候，正在处理中...", string title = "请稍候");
    }
}
