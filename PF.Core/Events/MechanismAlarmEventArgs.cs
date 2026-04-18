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
        /// <summary>模组名称</summary>
        public string MechanismName { get; set; }
        /// <summary>底层硬件名称</summary>
        public string HardwareName { get; set; }
        /// <summary>结构化报警码</summary>
        public string ErrorCode { get; set; }
        /// <summary>错误消息</summary>
        public string ErrorMessage { get; set; }
        /// <summary>内部异常</summary>
        public Exception InternalException { get; set; }
    }
}
