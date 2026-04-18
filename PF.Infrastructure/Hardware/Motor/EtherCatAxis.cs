using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Motor.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Motor
{
    /// <summary>
    /// EtherCAT轴设备实现
    /// </summary>
    public class EtherCatAxis : BaseAxisDevice
    {
        /// <summary>
        /// 构造EtherCAT轴设备
        /// </summary>
        public EtherCatAxis(string deviceId, int axisIndex, AxisParam  axisParam, string deviceName, bool isSimulated, ILogService logger, string dataDirectory)
            : base(
                deviceId: deviceId,
                deviceName: deviceName,
                isSimulated: isSimulated,
                logger: logger,
                dataDirectory: dataDirectory)
        {
            AxisIndex = axisIndex;
            Category = Core.Enums.HardwareCategory.Axis;
            Param = axisParam ;
        }

        /// <summary>
        /// 轴索引号
        /// </summary>
        public override int AxisIndex { get; }

        /// <summary>
        /// 轴参数
        /// </summary>
        public override AxisParam Param { get; set; } = new AxisParam();

        /// <summary>
        /// 内部连接实现
        /// </summary>
        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return Task.FromResult(true);
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
            EnsureCardAttached();
            if (IsSimulated)
            {
                return Task .CompletedTask ;
            }
            return ParentCard!.ClearAxisError (AxisIndex);
        }



        // ── 私有工具 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 检查父板卡是否已挂载，未挂载则记录错误日志并抛出 InvalidOperationException。
        /// </summary>
        private void EnsureCardAttached([CallerMemberName] string caller = "")
        {
            if (ParentCard is null)
            {
                var msg = $"[{DeviceName}] '{caller}'：设备尚未挂载到板卡，请先调用 AttachToCard()。";
                _logger?.Error(msg);
                throw new InvalidOperationException(msg);
            }
        }

        /// <summary>
        /// 内部健康检查实现
        /// </summary>
        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (ParentCard == null) return Task.CompletedTask;

            if (IsSimulated )
            {
                return Task.CompletedTask;
            }
            var ios = ParentCard.GetMotionIOStatus(AxisIndex);
            if (ios.ALM && !HasAlarm )
                RaiseAlarm(AlarmCodes.Hardware.ServoError,
                    $"轴[{AxisIndex}]伺服驱动器报警（ALM 信号有效）");
            else if ((ios.PEL || ios.MEL) && !HasAlarm && !ios.Homing)
                RaiseAlarm(AlarmCodes.Hardware.AxisLimitError,
                    $"轴[{AxisIndex}]限位保护触发（PEL={ios.PEL}, MEL={ios.MEL}）");
            return Task.CompletedTask;
        }
    }
}
