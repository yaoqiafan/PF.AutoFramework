using PF.Core.Enums;
using PF.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Station
{
    /// <summary>
    /// 全局主控调度器接口
    /// </summary>
    public interface IMasterController
    {
        MachineState CurrentState { get; }
        OperationMode CurrentMode { get; }

        event EventHandler<MachineState> MasterStateChanged;
        event EventHandler<string> MasterAlarmTriggered;

        Task InitializeAllAsync();
        Task StartAllAsync();
        Task ResumeAllAsync();
        void PauseAll();
        void StopAll();
        Task ResetAllAsync();
        void EmergencyStop();

        bool SetMode(OperationMode mode);

        /// <summary>
        /// 注册硬件报警复位请求处理委托。
        /// 宿主（Shell）通过此方法将 Prism 事件总线与主控解耦：
        /// Shell 订阅 HardwareResetRequestedEvent，触发时调用此处注册的委托，
        /// 主控从委托中执行工站级物理复位，PF.Infrastructure 无需直接依赖 Prism。
        /// </summary>
        void RegisterHardwareResetHandler(Action<HardwareResetRequest> handler);

        /// <summary>
        /// 系统复位入口：清除 AlarmService 所有活跃报警，然后执行 ResetAllAsync 使系统恢复 Idle。
        /// 由 Shell 在收到 <c>SystemResetRequestedEvent</c> 时调用（BackgroundThread）。
        /// </summary>
        Task RequestSystemResetAsync();
    }
}
