using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Enums
{
    public enum MachineState
    {
        Uninitialized,  // 未初始化（初始默认状态，硬件尚未就绪）
        Initializing,   // 初始化中（正在执行连接 / 回原点等动作）
        Idle,           // 待机状态（硬件就绪，等待启动指令）
        Running,        // 运行状态
        Paused,         // 暂停状态（流程挂起，可恢复）
        InitAlarm,      // 初始化阶段报警（复位后强制回 Uninitialized，必须重新初始化）
        RunAlarm,       // 运行期报警（复位后回 Idle，坐标系有效，可直接再启动）
        Resetting       // 复位中（正在执行物理复位动作，等待各工站完成）
    }

    public enum MachineTrigger
    {
        Initialize,     // 触发开始初始化（Uninitialized / Idle → Initializing）
        InitializeDone, // 初始化完成（Initializing → Idle）
        Start,          // 启动指令（Idle → Running）
        Pause,          // 暂停指令（Running → Paused）
        Resume,         // 恢复指令（Paused → Running）
        Stop,           // 停止指令（Idle / Running / Paused → Uninitialized）
        Error,          // 内部硬件报错触发（路由到 InitAlarm 或 RunAlarm 视当前状态）
        Reset,          // 报警复位指令（InitAlarm / RunAlarm → Resetting）
        ResetDone,              // 复位完成（Resetting → Idle，来自 RunAlarm 路径）
        ResetDoneUninitialized  // 复位完成（Resetting → Uninitialized，来自 InitAlarm 路径，强制重新初始化）
    }

    /// <summary>
    /// 运行模式
    /// </summary>
    public enum OperationMode
    {
        Normal,     // 正常生产模式（等待真实 IO / 物料信号）
        DryRun      // 空跑模式（跳过真实 IO 等待，快速验证流程逻辑）
    }
}
