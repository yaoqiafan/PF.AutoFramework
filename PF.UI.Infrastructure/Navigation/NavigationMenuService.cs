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
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }

            var viewTypes = types.Where(t => t.IsDefined(typeof(ModuleNavigationAttribute), false));

            foreach (var type in viewTypes)
            {
                // 👇 关键修改：获取该类上的所有导航特性（支持一个视图挂多个菜单）
                var attributes = type.GetCustomAttributes<ModuleNavigationAttribute>();

                foreach (var attr in attributes)
                {
                    var groupName = string.IsNullOrEmpty(attr.GroupName) ? "默认分组" : attr.GroupName;

                    var group = MenuItems.FirstOrDefault(m => m.Title == groupName);
                    if (group == null)
                    {
                        group = new NavigationItem
                        {
                            Title = groupName,
                            // 👇 关键修改 1：分组的排序字段使用 GroupOrder
                            Order = attr.GroupOrder,
                            Icon = attr.GroupIcon
                        };
                        MenuItems.Add(group);
                    }
                    else
                    {
                        // 👇 关键修改 2：如果同一个分组的其他标签指定了更小的 GroupOrder，则更新它（容错处理）
                        if (attr.GroupOrder < group.Order)
                            group.Order = attr.GroupOrder;

                        if (string.IsNullOrEmpty(group.Icon) && !string.IsNullOrEmpty(attr.GroupIcon))
                            group.Icon = attr.GroupIcon;
                    }

                    // 添加子节点（子节点自身的排序依然使用 Order）
                    group.Children.Add(new NavigationItem
                    {
                        Title = attr.Title,
                        ViewName = attr.ViewName,
                        Icon = attr.Icon,
                        NavigationParameter = attr.NavigationParameter,
                        Order = attr.Order // 👈 子节点用 Order
                    });
                }
            }

            SortMenu();
        }

        private void SortMenu()
        {
            var sortedGroups = MenuItems.OrderBy(m => m.Order).ToList();
            MenuItems.Clear();
            foreach (var group in sortedGroups)
            {
                if (group.Children.Any())
                {
                    var sortedChildren = group.Children.OrderBy(c => c.Order).ToList();
                    group.Children.Clear();
                    foreach (var child in sortedChildren) group.Children.Add(child);
                }
                MenuItems.Add(group);
            }
        }
    }
}