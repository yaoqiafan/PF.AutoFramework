using System;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 模块导航特性，支持区域导航、传参以及弹窗，支持自动分组
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ModuleNavigationAttribute : Attribute
    {
        public string ViewName { get; }
        public string Title { get; }
        public string Icon { get; set; }
        public int GroupOrder { get; set; } = 99;
        public string GroupIcon { get; set; }
        public int Order { get; set; } = 99;
        public string NavigationParameter { get; set; }
        public string GroupName { get; set; }

        public ModuleNavigationAttribute(string viewName, string title, string groupName = "默认分组")
        {
            ViewName = viewName;
            Title = title;
            GroupName = groupName;
        }
    }
}