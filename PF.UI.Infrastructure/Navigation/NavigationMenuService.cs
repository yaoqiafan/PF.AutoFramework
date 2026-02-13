using PF.Core.Attributes;
using PF.Core.Interfaces.Configuration;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;

namespace PF.UI.Infrastructure.Navigation
{
    public class NavigationMenuService : INavigationMenuService
    {
        public ObservableCollection<NavigationItem> MenuItems { get; } = new ObservableCollection<NavigationItem>();

        public void RegisterAssembly(Assembly assembly)
        {
            Type[] types;
            try
            {
                // 安全获取类型，防止某些未加载依赖项导致 ReflectionTypeLoadException
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(t => t != null).ToArray();
            }

            // 找到所有标记了 ModuleNavigationAttribute 的类
            var viewTypes = types.Where(t => t.IsDefined(typeof(ModuleNavigationAttribute), false));

            // 1. 遍历收集阶段（只添加，不排序）
            foreach (var type in viewTypes)
            {
                var attr = type.GetCustomAttribute<ModuleNavigationAttribute>();

                // 处理分组名为空的边界情况
                var groupName = string.IsNullOrEmpty(attr.GroupName) ? "默认分组" : attr.GroupName;

                // 查找是否已经存在该分组
                var group = MenuItems.FirstOrDefault(m => m.Title == groupName);
                if (group == null)
                {
                    // 如果分组不存在，创建一个父级节点
                    group = new NavigationItem
                    {
                        Title = groupName,
                        Order = attr.Order // 取第一个注册视图的Order作为初始参考
                    };
                    MenuItems.Add(group);
                }
                else
                {
                    // 让分组排序更智能：取组内页面中最小的 Order 作为外层分组的 Order
                    if (attr.Order < group.Order) group.Order = attr.Order;
                }

                // 将该视图作为子项直接添加到分组中
                group.Children.Add(new NavigationItem
                {
                    Title = attr.Title,
                    ViewName = attr.ViewName,
                    Icon = attr.Icon,
                    NavigationParameter = attr.NavigationParameter,
                    IsDialog = attr.IsDialog,
                    Order = attr.Order
                });
            }

            // 2. 排序阶段（等所有视图都添加完成后，统一执行一次排序）
            SortMenu();
        }

        /// <summary>
        /// 统一对菜单进行排序
        /// </summary>
        private void SortMenu()
        {
            // 对外层分组进行排序
            var sortedGroups = MenuItems.OrderBy(m => m.Order).ToList();
            MenuItems.Clear();

            foreach (var group in sortedGroups)
            {
                // 对每个分组内部的子项进行排序
                if (group.Children.Any())
                {
                    var sortedChildren = group.Children.OrderBy(c => c.Order).ToList();
                    group.Children.Clear();
                    foreach (var child in sortedChildren)
                    {
                        group.Children.Add(child);
                    }
                }

                // 把排好序的分组加回主集合
                MenuItems.Add(group);
            }
        }
    }
}