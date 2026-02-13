using System;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 模块导航特性，支持区域导航、传参以及弹窗，支持自动分组
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class ModuleNavigationAttribute : Attribute
    {
        public string ViewName { get; }
        public string Title { get; }
        public string Icon { get; set; }
        public int Order { get; set; } = 99;

        // 用于在导航时传递参数（例如参数分类的 Key）
        public string NavigationParameter { get; set; }

        // 标记该页面是否应该以弹窗（Dialog）形式打开
        public bool IsDialog { get; set; }

        // 👇 新增：用于侧边栏分组（例如 "设备参数设置"）
        public string GroupName { get; set; }

        public ModuleNavigationAttribute(string viewName, string title, string groupName = "默认分组")
        {
            ViewName = viewName;
            Title = title;
            GroupName = groupName;
        }
    }
}