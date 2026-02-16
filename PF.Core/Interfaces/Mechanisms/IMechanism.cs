using PF.Core.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Mechanisms
{
    /// <summary>
    /// 工业机构模组基础接口
    /// 代表一个由多个底层硬件（轴、IO等）组合而成的逻辑功能单元
    /// </summary>
    public interface IMechanism
    {
        /// <summary>模组名称（如：上料取料模组）</summary>
        string MechanismName { get; }

        /// <summary>
        /// 模组是否已初始化完成（通常代表内部的轴是否已回原点，IO是否处于安全初始状态）
        /// 未初始化的模组禁止执行业务动作
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 模组是否处于报警状态（只要内部有任何一个硬件报警，该状态即为 true）
        /// </summary>
        bool HasAlarm { get; }

        event EventHandler<MechanismAlarmEventArgs> AlarmTriggered;

        /// <summary>
        /// 异步初始化模组（执行回原点、设置气缸初始状态等）
        /// </summary>
        Task<bool> InitializeAsync(CancellationToken token = default);

        /// <summary>
        /// 异步复位模组（尝试清除内部所有硬件的报警状态）
        /// </summary>
        Task<bool> ResetAsync(CancellationToken token = default);

        /// <summary>
        /// 紧急停止该模组内的所有动作（所有轴减速停，所有危险输出IO关闭）
        /// </summary>
        Task StopAsync();
    }
}
