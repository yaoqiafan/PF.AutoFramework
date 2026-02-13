using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace PF.UI.Infrastructure.Navigation
{
    /// <summary>
    /// 导航菜单服务接口
    /// </summary>
    public interface INavigationMenuService
    {
        /// <summary>
        /// 供 UI 绑定的菜单集合
        /// </summary>
        ObservableCollection<NavigationItem> MenuItems { get; }

        /// <summary>
        /// 扫描程序集并自动注册包含 ModuleNavigation 特性的视图
        /// </summary>
        /// <param name="assembly">需要扫描的程序集</param>
        void RegisterAssembly(Assembly assembly);
    }
}
