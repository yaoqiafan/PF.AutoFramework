using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.UI.Models
{
    /// <summary>
    /// RawMappingItem
    /// </summary>
    public class RawMappingItem
    {
        /// <summary>
        /// 获取或设置 Index
        /// </summary>
        public int Index { get; set; }       // 捕获序号 (第几次触发)
        /// <summary>
        /// 获取或设置 ZPosition
        /// </summary>
        public double ZPosition { get; set; } // 硬件触发的Z坐标
    }
    /// <summary>
    /// FilteredMappingItem
    /// </summary>

    public class FilteredMappingItem
    {
        /// <summary>
        /// 获取或设置 LayerIndex
        /// </summary>
        public int LayerIndex { get; set; } // 槽位/层号
        /// <summary>
        /// 获取或设置 ActualZ
        /// </summary>
        public double ActualZ { get; set; } // 补偿后的实际Z坐标
    }

  
}
