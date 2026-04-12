using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.Motor.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.Motor
{
    public class EtherCatAxis : BaseAxisDevice
    {
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

        public override int AxisIndex { get; }

        public override AxisParam Param { get; set; } = new AxisParam();

        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return Task.FromResult(true);
        }

        protected override Task InternalDisconnectAsync()
        {
            return Task.FromResult(true);
        }


        protected override Task InternalResetAsync(CancellationToken token)
        {
            return Task.FromResult(true);
        }

        protected override Task InternalCheckHealthAsync(CancellationToken token)
        {
            if (ParentCard == null) return Task.CompletedTask;

            var ios = ParentCard.GetMotionIOStatus(AxisIndex);
            if (ios.ALM && !HasAlarm)
                RaiseAlarm(AlarmCodes.Hardware.ServoError,
                    $"轴[{AxisIndex}]伺服驱动器报警（ALM 信号有效）");
            else if ((ios.PEL || ios.MEL) && !HasAlarm)
                RaiseAlarm(AlarmCodes.Hardware.AxisLimitError,
                    $"轴[{AxisIndex}]限位保护触发（PEL={ios.PEL}, MEL={ios.MEL}）");
            return Task.CompletedTask;
        }
    }
}
