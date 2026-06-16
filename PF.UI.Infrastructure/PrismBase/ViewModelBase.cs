using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Logging;
using PF.UI.Infrastructure.Dialog.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>
    /// IDestructible
    /// </summary>
    public abstract class ViewModelBase : BindableBase, IDestructible
    {
        /// <summary>
        /// 构造
        /// </summary>
        protected ViewModelBase()
        {
            EventAggregator = ContainerLocator.Container.Resolve<IEventAggregator>();
            DialogService = ContainerLocator.Container.Resolve<IDialogService>();

            ServiceProvider = ContainerLocator.Container.Resolve<IServiceProvider>();
            MessageService = ContainerLocator.Container.Resolve<IMessageService>();
            LogService = ContainerLocator.Container.Resolve<ILogService>();
            UserService = ContainerLocator.Container.Resolve<IUserService>();
        }

        /// <summary>
        /// ServiceProvider 服务
        /// </summary>
        public IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// 事件聚合器
        /// </summary>
        protected IEventAggregator EventAggregator { get; }

        /// <summary>
        /// 弹窗服务
        /// </summary>
        protected IDialogService DialogService { get; }

        /// <summary>
        /// 消息服务
        /// </summary>
        protected IMessageService MessageService { get; }

        /// <summary>
        /// 日志服务
        /// </summary>
        protected ILogService LogService { get; }

        /// <summary>
        /// 用户服务
        /// </summary>
        protected IUserService UserService { get; }

        /// <summary>
        /// 当前登录用户名，用于操作日志记录
        /// </summary>
        protected string CurrentUserName => UserService?.CurrentUser?.UserName ?? "未知用户";

        /// <summary>
        /// 销毁
        /// </summary>
        public virtual void Destroy()
        {
        }
    }
}
