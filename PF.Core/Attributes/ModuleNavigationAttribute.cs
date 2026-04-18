using System;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 模块导航特性，支持区域导航、传参以及弹窗，支持自动分组
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    public class ModuleNavigationAttribute : Attribute
    {
        /// <summary>
        /// 视图名称
        /// </summary>
        public string ViewName { get; }
        /// <summary>
        /// 显示标题
        /// </summary>
        public string Title { get; }
        /// <summary>
        /// 图标
        /// </summary>
        public string Icon { get; set; }
        /// <summary>
        /// 分组排序号
        /// </summary>
        public int GroupOrder { get; set; } = 99;
        /// <summary>
        /// 分组图标
        /// </summary>
        public string GroupIcon { get; set; }
        /// <summary>
        /// 排序号
        /// </summary>
        public int Order { get; set; } = 99;
        /// <summary>
        /// 导航参数
        /// </summary>
        public string NavigationParameter { get; set; }
        /// <summary>
        /// 分组名称
        /// </summary>
        public string GroupName { get; set; }

        /// <summary>
        /// 初始化模块导航特性
        /// </summary>
        public ModuleNavigationAttribute(string viewName, string title, string groupName = "默认分组")
        {
            ViewName = viewName;
            Title = title;
            GroupName = groupName;
        }
    }
}