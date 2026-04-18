
using PF.Core.Constants;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.IO.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.IO
{
    /// <summary>
    /// EtherCAT IO模块实现
    /// </summary>
    public class EtherCatIO : BaseIODevice
    {
        /// <summary>
        /// 构造EtherCAT IO模块
        /// </summary>
        public EtherCatIO(int inputCount,int outputCount, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger) 
        {
            InputCount = inputCount;
            OutputCount = outputCount;
        }


        /// <summary>
        /// 输入端口数量
        /// </summary>
        public override int InputCount { get; }

        /// <summary>
        /// 输出端口数量
        /// </summary>
        public override int OutputCount { get; }

        /// <summary>
        /// 内部连接实现
        /// </summary>
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return Task .FromResult (true );
        }

        /// <summary>
        /// 内部断开连接实现
        /// </summary>
        protected override Task InternalDisconnectAsync()
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// 内部复位实现
        /// </summary>
        protected override Task InternalResetAsync(CancellationToken token)
        {
            return Task.FromResult(true);
        }

        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if ((ParentCard == null || !ParentCard.IsConnected) && !HasAlarm)
                RaiseAlarm(AlarmCodes.Hardware.IoModuleError,
                    "IO 模块所在控制卡已断开，EtherCAT IO 不可用");
            return Task.CompletedTask;
        }
    }
}
