using PF.Core.Enums;
using PF.Core.Interfaces.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PF.UI.Infrastructure.PrismBase
{
    /// <summary>导航防呆条件描述</summary>
    public record NavigationGuard(bool IsSatisfied, string FailMessage);

    /// <summary>
    /// IConfirmNavigationRequest
    /// </summary>
    public abstract class RegionViewModelBase : ViewModelBase, INavigationAware, IConfirmNavigationRequest
    {
        private readonly IUserService _userService;

        /// <summary>
        /// 构造
        /// </summary>
        public RegionViewModelBase()
        {
            RegionManager = ContainerLocator.Container.Resolve<IRegionManager>();
            _userService  = ContainerLocator.Container.Resolve<IUserService>();
        }

        /// <summary>
        /// 区域管理器
        /// </summary>
        protected IRegionManager RegionManager { get; }

        /// <summary>
        /// 返回导航前需满足的防呆条件列表。超级用户跳过所有检查。
        /// 子类覆写此方法以声明进入本视图所需的前置条件。
        /// </summary>
        protected virtual IEnumerable<NavigationGuard> GetNavigationGuards(NavigationContext? context = null)
            => Enumerable.Empty<NavigationGuard>();

        /// <summary>
        /// 导航时确认方法(虚方法)
        /// </summary>
        public virtual void ConfirmNavigationRequest(NavigationContext navigationContext, Action<bool> continuationCallback)
        {
            if (_userService?.CurrentUser?.Root >= UserLevel.SuperUser)
            {
                continuationCallback(true);
                return;
            }

            var guards = GetNavigationGuards(navigationContext).ToList();
            var failed = guards.FirstOrDefault(g => !g.IsSatisfied);
            if (failed != null)
            {
                MessageService.ShowMessage(failed.FailMessage, "导航防呆", MessageBoxButton.OK, MessageBoxImage.Warning);
                continuationCallback(false);
                return;
            }

            continuationCallback(true);
        }

        /// <summary>
        /// 是否启用已创建好的实例 true:是  false:否
        /// </summary>
        /// <param name="navigationContext"></param>
        /// <returns></returns>
        public virtual bool IsNavigationTarget(NavigationContext navigationContext)
        {
            return false;
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
