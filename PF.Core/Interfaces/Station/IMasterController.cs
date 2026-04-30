using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Station
{
    /// <summary>
    /// 全局主控调度器接口。
    /// 负责统筹和协调整个机台或生产线上所有子工站的生命周期、状态流转以及运行模式控制。
    /// </summary>
    public interface IMasterController: IDisposable
    {
        /// <summary>
        /// 获取当前主控调度器所处的设备运行状态（如：未初始化、待机、运行中、报警等）。
        /// </summary>
        MachineState CurrentState { get; }

        /// <summary>
        /// 获取当前设备的运行模式（如：正常生产、空跑、维修调试等）。
        /// </summary>
        OperationMode CurrentMode { get; }

        /// <summary>
        /// 当主控调度器的运行状态发生流转/改变时触发的事件。
        /// </summary>
        event EventHandler<MachineState> MasterStateChanged;

        /// <summary>
        /// 丰富报警事件，当主控或任一子工站触发真实报警时引发，携带硬件名、运行时消息及错误码等上下文。
        /// </summary>
        event EventHandler<StationAlarmEventArgs> MasterAlarmTriggered;

        /// <summary>
        /// 异步执行全线初始化。
        /// 指挥所有受控子工站执行上电、回原点、连接检测等初始化准备动作。
        /// </summary>
        /// <returns>表示异步初始化操作的任务</returns>
        Task InitializeAllAsync();

        /// <summary>
        /// 异步启动全线设备。
        /// 指挥所有受控子工站进入正式的生产循环流程。
        /// </summary>
        /// <returns>表示异步启动操作的任务</returns>
        Task StartAllAsync();

        /// <summary>
        /// 异步恢复全线运行。
        /// 将处于暂停状态（Paused）的所有子工站唤醒，继续执行未完成的生产动作。
        /// </summary>
        /// <returns>表示异步恢复操作的任务</returns>
        Task ResumeAllAsync();

        /// <summary>
        /// 同步挂起全线设备。
        /// 触发所有子工站的暂停逻辑，安全地挂起当前轴运动或工艺流程（通常不切断伺服使能）。
        /// </summary>
        void PauseAll();

        /// <summary>
        /// 异步停止全线设备。
        /// 打断所有子工站的生产循环，执行机构级物理制动，并等待各工站安全停稳。
        /// </summary>
        /// <returns>表示异步停止操作的任务</returns>
        Task StopAllAsync();

        /// <summary>
        /// 异步复位全线设备。
        /// 用于在报警解除后，指挥各机构执行清错、退刀、回安全位等自恢复动作，使设备重返待机状态。
        /// </summary>
        /// <returns>表示异步复位操作的任务</returns>
        Task ResetAllAsync();

        /// <summary>
        /// 设置设备的全局运行模式（通常仅在设备处于 Idle 待机状态时允许切换）。
        /// </summary>
        /// <param name="mode">要设置的目标运行模式</param>
        /// <returns>如果当前状态允许切换且设置成功，返回 true；否则返回 false</returns>
        bool SetMode(OperationMode mode);

        /// <summary>
        /// 注册硬件报警复位请求处理委托。
        /// 宿主（Shell）通过此方法将 Prism 事件总线与主控解耦：
        /// Shell 订阅 HardwareResetRequestedEvent，触发时调用此处注册的委托，
        /// 主控从委托中执行工站级物理复位，从而使 PF.Infrastructure 层无需直接依赖 Prism 框架。
        /// </summary>
        /// <param name="handler">处理硬件复位请求的动作委托</param>
        void RegisterHardwareResetHandler(Action<HardwareResetRequest> handler);

        /// <summary>
        /// 系统全局复位入口：清除 AlarmService 中所有的活跃报警记录，
        /// 然后执行 <see cref="ResetAllAsync"/> 使整个系统恢复至 Idle 待机状态。
        /// 通常由 Shell 界面在收到 <c>SystemResetRequestedEvent</c> 时于后台线程调用。
        /// </summary>
        /// <returns>表示异步系统复位操作的任务</returns>
        Task RequestSystemResetAsync();

        /// <summary>
        /// 清空机台上所有子工站的记忆参数（仅允许在未初始化状态下调用）。
        /// 将每个工站的内存参数重置为默认值，并删除对应的磁盘持久化文件。
        /// </summary>
        /// <exception cref="InvalidOperationException">当设备不处于未初始化状态时抛出。</exception>
        void ClearAllStationMemory();

        /// <summary>
        /// 清空指定名称工站的记忆参数（仅允许在未初始化状态下调用）。
        /// </summary>
        /// <param name="stationName">工站名称，对应 <c>StationBase.StationName</c></param>
        /// <exception cref="InvalidOperationException">当设备不处于未初始化状态时抛出。</exception>
        void ClearStationMemory(string stationName);
    }
}