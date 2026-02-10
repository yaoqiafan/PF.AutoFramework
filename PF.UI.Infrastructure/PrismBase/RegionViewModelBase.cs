using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Common.Core.PrismBase
{
    public abstract class RegionViewModelBase : ViewModelBase, INavigationAware, IConfirmNavigationRequest
    {
        /// <summary>
        /// 构造
        /// </summary>
        public RegionViewModelBase()
        {
            RegionManager = ContainerLocator.Container.Resolve<IRegionManager>();
        }

        /// <summary>
        /// 区域管理器
        /// </summary>
        protected IRegionManager RegionManager { get; }

        /// <summary>
        /// 导航时确认方法(虚方法)
        /// </summary>
        /// <param name="navigationContext"></param>
        /// <param name="continuationCallback"></param>
        public virtual void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
        {
            continuationCallback(true);
        }

        /// <summary>
        /// 是否启用已创建好的实例 true:是  false:否
        /// </summary>
        /// <param name="navigationContext"></param>
        /// <returns></returns>
        public virtual bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return true;
        }

        /// <summary>
        /// 导航前触发(虚方法)
        /// </summary>
        /// <param name="navigationContext"></param>
        public virtual void OnNavigatedFrom(NavigationContext navigationContext)
        {
        }

        /// <summary>
        /// 导航后触发(虚方法)
        /// </summary>
        /// <param name="navigationContext"></param>
        public virtual void OnNavigatedTo(NavigationContext navigationContext)
        {
        }
    }

}
