using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.Dialog.ViewModels
{
    /// <summary>
    /// PFDialogViewModelBase 视图模型
    /// </summary>
    public class WaitDialogViewModel : PFDialogViewModelBase
    {
        private string _message;
        /// <summary>
        /// 初始化实例
        /// </summary>
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        /// <summary>
        /// 处理DialogOpened事件
        /// </summary>
        public override async void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            Message = parameters.GetValue<string>("Message") ?? "请稍候，正在处理中...";
            Title = parameters.GetValue<string>("Title") ?? "请稍候";

            LogService.Info($"[等待弹窗] 用户[{CurrentUserName}] 触发等待任务 | 标题：{Title} | 内容：{Message}", "操作日志");

            var workAction = parameters.GetValue<Func<Task>>("WorkAction");
            if (workAction != null)
            {
                try
                {
                    // 让出一次调度：DialogService 在调用 OnDialogOpened 之后才真正完成窗口显示 /
                    // RequestClose 绑定。若 workAction 足够快（如仅写几条参数），可能在这些收尾
                    // 动作完成前就跑完并在 finally 里调用 RequestClose.Invoke，导致
                    // "DialogCloseListener 未初始化" 崩溃。Task.Yield 把后续执行推到下一次调度，
                    // 确保窗口已完成显示流程后再运行任务。
                    await Task.Yield();
                    await workAction();
                    LogService.Info($"[等待弹窗] 等待任务完成 | 标题：{Title}", "操作日志");
                }
                catch (Exception ex)
                {
                    LogService.Error($"[等待弹窗] 等待任务异常 | 标题：{Title}", "操作日志", ex);
                }
                finally
                {
                    RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                }
            }
        }
    }
}
