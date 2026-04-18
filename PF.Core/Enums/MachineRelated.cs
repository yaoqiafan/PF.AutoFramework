using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    /// <summary>
    /// 设备状态
    /// </summary>
    public enum MachineState
    {
        /// <summary>未初始化</summary>
        Uninitialized,
        /// <summary>初始化中</summary>
        Initializing,
        /// <summary>待机状态</summary>
        Idle,
        /// <summary>运行状态</summary>
        Running,
        /// <summary>暂停状态</summary>
        Paused,
        /// <summary>初始化阶段报警</summary>
        InitAlarm,
        /// <summary>运行期报警</summary>
        RunAlarm,
        /// <summary>复位中</summary>
        Resetting
    }

    /// <summary>
    /// 设备状态触发器
    /// </summary>
    public enum MachineTrigger
    {
        /// <summary>触发开始初始化</summary>
        Initialize,
        /// <summary>初始化完成</summary>
        InitializeDone,
        /// <summary>启动指令</summary>
        Start,
        /// <summary>暂停指令</summary>
        Pause,
        /// <summary>恢复指令</summary>
        Resume,
        /// <summary>停止指令</summary>
        Stop,
        /// <summary>内部硬件报错触发</summary>
        Error,
        /// <summary>报警复位指令</summary>
        Reset,
        /// <summary>复位完成（回到Idle）</summary>
        ResetDone,
        /// <summary>复位完成（回到Uninitialized）</summary>
        ResetDoneUninitialized
    }

    /// <summary>
    /// 运行模式
    /// </summary>
    public enum OperationMode
    {
        /// <summary>正常生产模式</summary>
        Normal,
        /// <summary>空跑模式</summary>
        DryRun
    }
}
