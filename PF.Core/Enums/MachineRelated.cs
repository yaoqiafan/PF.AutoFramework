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
        Alarm,          // 报警状态（需人为排故后复位）
        Resetting       // 复位中（正在执行物理复位动作，等待各工站完成）
    }

    public enum MachineTrigger
    {
        Initialize,     // 触发开始初始化（Uninitialized → Initializing）
        InitializeDone, // 初始化完成（Initializing → Idle）
        Start,          // 启动指令（Idle → Running）
        Pause,          // 暂停指令
        Resume,         // 恢复指令
        Stop,           // 停止指令（Running / Paused → Idle）
        Error,          // 内部硬件报错触发
        Reset,                  // 报警复位指令（Alarm → Resetting）
        ResetDone,              // 复位完成（Resetting → Idle）
        ResetDoneUninitialized  // 复位完成后回到未初始化（Resetting → Uninitialized，仅初始化失败后的复位使用）
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
