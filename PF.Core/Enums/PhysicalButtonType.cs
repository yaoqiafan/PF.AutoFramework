using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{/// <summary>
 /// 实体操作面板按键类型
 /// </summary>
    [System.Obsolete("Use PF.Core.Constants.HardwareInputType string constants instead.")]
    public enum PhysicalButtonType
    {
        Start,
        Pause,
        Reset,
        EStop
    }
}
