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
