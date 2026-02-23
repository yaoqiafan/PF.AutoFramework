using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    public enum MachineState
    {
        Idle,       // 待机状态
        Running,    // 运行状态
        Paused,     // 暂停状态（流程挂起，可恢复）
        Alarm       // 报警状态（需人为排故后复位）
    }

    public enum MachineTrigger
    {
        Start,      // 启动指令
        Pause,      // 暂停指令
        Resume,     // 恢复指令
        Stop,       // 停止指令 (回到待机)
        Error,      // 内部硬件报错触发
        Reset       // 报警复位指令
    }
}
