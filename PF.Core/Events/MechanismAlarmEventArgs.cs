using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Events
{
    /// <summary>
    /// 模组报警事件参数
    /// </summary>
    public class MechanismAlarmEventArgs : EventArgs
    {
        public string MechanismName { get; set; }
        public string HardwareName { get; set; } // 哪个底层硬件引发的报警
        public string ErrorMessage { get; set; }
        public Exception InternalException { get; set; }
    }
}
