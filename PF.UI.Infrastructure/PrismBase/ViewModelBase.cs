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
        /// 销毁
        /// </summary>
        public virtual void Destroy()
        {
        }
    }
}
