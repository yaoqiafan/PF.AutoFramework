using PF.Core.Enums;
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
    }
}
