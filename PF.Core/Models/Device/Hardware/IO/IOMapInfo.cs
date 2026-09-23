using System;

namespace PF.Core.Models.Device.Hardware.IO
{
    /// <summary>
    /// IO 映射信息结构，承载名称和可见性
    /// </summary>
    public class IOMapInfo
    {
        /// <summary>
        /// IO 引脚显示名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 是否在 UI 中可见（对应 [Browsable] 特性）
        /// </summary>
        public bool IsBrowsable { get; set; } = true;

        /// <summary>
        /// UI 显示顺序（对应 [Display(Order = n)] 特性）；未显式指定时为 int.MaxValue，
        /// 表示退化为按物理引脚索引排序（与旧行为一致）
        /// </summary>
        public int Order { get; set; } = int.MaxValue;

        /// <summary>
        /// UI 分组名称（对应 [Display(GroupName = "...")] 特性）；未显式指定时为 null，
        /// 由消费方归入"共享/其他"分组
        /// </summary>
        public string Category { get; set; }
    }
}
