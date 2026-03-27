using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.UI.Models
{
    public class RawMappingItem
    {
        public int Index { get; set; }       // 捕获序号 (第几次触发)
        public double ZPosition { get; set; } // 硬件触发的Z坐标
    }

    public class FilteredMappingItem
    {
        public int LayerIndex { get; set; } // 槽位/层号
        public double ActualZ { get; set; } // 补偿后的实际Z坐标
    }

  
}
