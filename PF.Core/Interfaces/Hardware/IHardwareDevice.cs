using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Core.Interfaces.Hardware
{
        /// <summary>
        /// 工业硬件设备基础生命周期接口
        /// </summary>
        public interface IHardwareDevice : IDisposable
        {
            #region 身份标识 (Identity)

            /// <summary>设备唯一ID (可在配置文件中定义)</summary>
            string DeviceId { get; }

            /// <summary>设备易读名称 (用于UI展示和日志，如 "X轴电机")</summary>
            string DeviceName { get; }

            #endregion

            #region 状态指示 (State)

            /// <summary>是否已建立物理/网络连接</summary>
            bool IsConnected { get; }

            /// <summary>设备是否处于报警或故障状态</summary>
            bool HasAlarm { get; }

            /// <summary>是否为模拟设备（用于脱机调试模式）</summary>
            bool IsSimulated { get; }

            #endregion

            #region 生命周期控制 (Lifecycle)

            /// <summary>
            /// 异步建立连接
            /// </summary>
            Task<bool> ConnectAsync(CancellationToken token = default);

            /// <summary>
            /// 异步断开连接
            /// </summary>
            Task DisconnectAsync();

            /// <summary>
            /// 异步复位设备（用于清除硬件报警状态）
            /// </summary>
            Task<bool> ResetAsync(CancellationToken token = default);

            #endregion

            #region 事件订阅 (Events)

            /// <summary>设备连接状态发生改变时触发 (可用于UI状态指示灯)</summary>
            event EventHandler<bool> ConnectionChanged;

            /// <summary>设备发生底层硬件报警时触发 (抛给上层统一处理)</summary>
            event EventHandler<DeviceAlarmEventArgs> AlarmTriggered;

            #endregion
        }

        /// <summary>
        /// 设备报警事件参数载荷
        /// </summary>
        public class DeviceAlarmEventArgs : EventArgs
        {
            public string ErrorCode { get; set; }
            public string ErrorMessage { get; set; }
            public Exception InternalException { get; set; }
        }
    
}
