using PF.UI.Infrastructure.PrismBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.Dialog.ViewModels
{
    public class WaitDialogViewModel : PFDialogViewModelBase
    {
        private string _message;
        public string Message { get => _message; set => SetProperty(ref _message, value); }

        public override async void OnDialogOpened(IDialogParameters parameters)
        {
            base.OnDialogOpened(parameters);
            Message = parameters.GetValue<string>("Message") ?? "请稍候，正在处理中...";
            Title = parameters.GetValue<string>("Title") ?? "请稍候";

            // 核心机制：获取外部传入的后台任务
            var workAction = parameters.GetValue<Func<Task>>("WorkAction");
            if (workAction != null)
            {
                try
                {
                    // 执行外部的耗时任务
                    await workAction();
                }
                catch (Exception ex)
                {
                    // 处理异常（可选：调用系统错误提示）
                    // ILogService.Error(ex);
                }
                finally
                {
                    // 任务执行完毕后，自己关闭自己！
                    RequestClose.Invoke(new DialogResult(ButtonResult.OK));
                }
            }
        }
    }
}
