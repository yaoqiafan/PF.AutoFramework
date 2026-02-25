using System;

namespace PF.Core.Attributes
{
    /// <summary>
    /// 模组调试界面特性，用于自动发现和注册模组UI
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class MechanismUIAttribute : Attribute
    {
        /// <summary>
        /// 模组唯一标识 (例如 "PickPlace")
        /// </summary>
        public string MechanismId { get; }

        /// <summary>
        /// UI显示名称 (例如 "取放模组调试")
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 排序号 (可选，用于在侧边栏列表排序)
        /// </summary>
        public int Order { get; set; } = 99;

        public MechanismUIAttribute(string mechanismId, string title)
        {
            MechanismId = mechanismId;
            Title = title;
        }
    }
}