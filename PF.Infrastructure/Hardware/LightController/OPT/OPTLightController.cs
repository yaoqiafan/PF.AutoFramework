using PF.Core.Interfaces.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.LightController.OPT
{
    internal class OPTLightController : BaseLightController
    {
        public OPTLightController(string deviceId, string deviceName, bool isSimulated, ILogService logger) : base(deviceId, deviceName, isSimulated, logger)
        {
        }

        public override Task SetLightValue(int Channel, int LightValue)
        {
            throw new NotImplementedException();
        }

        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override Task InternalDisconnectAsync()
        {
            throw new NotImplementedException();
        }

        protected override Task InternalResetAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
