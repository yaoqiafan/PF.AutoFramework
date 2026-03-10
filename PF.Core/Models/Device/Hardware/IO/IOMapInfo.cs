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
    }
}
