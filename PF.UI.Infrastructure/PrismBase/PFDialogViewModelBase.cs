using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace PF.Common.Core.PrismBase
{
    public abstract class PFDialogViewModelBase: ViewModelBase, IDialogAware
    {
        // 私有字段：对话框标题
        private string _Title = string.Empty;

        /// <summary>
        /// 对话框标题属性
        /// 用于绑定到对话框窗口的标题栏
        /// </summary>
        public string Title
        {
            get { return _Title; }
            set { SetProperty(ref _Title, value); }
        }

        /// <summary>
        /// 确认命令
        /// 绑定到对话框的确定/提交按钮
        /// 执行创建轴节点并关闭对话框的逻辑
        /// 只有当轴名称不为空时命令才可执行
        /// </summary>
        public ICommand ConfirmCommand { get; set; }

        /// <summary>
        /// 取消命令
        /// 绑定到对话框的取消/关闭按钮
        /// 执行取消操作并关闭对话框的逻辑
        /// 通常取消命令始终可执行
        /// </summary>
        public ICommand CancelCommand { get; set; }


        /// <summary>
        /// 对话框关闭监听器
        /// 实现IDialogAware接口，用于请求关闭对话框
        /// 由对话框服务在适当时机设置
        /// </summary>
        public DialogCloseListener RequestClose { get; set; }

        public virtual bool CanCloseDialog()
        {
            return true;
        }

        public virtual void OnDialogClosed()
        {
           
        }

        public virtual void OnDialogOpened(IDialogParameters parameters)
        {
           
        }
    }
}
