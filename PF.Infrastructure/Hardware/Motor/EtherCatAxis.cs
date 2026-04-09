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
    }
}
