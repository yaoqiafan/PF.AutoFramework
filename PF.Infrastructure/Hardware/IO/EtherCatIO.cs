
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Hardware.IO.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.Infrastructure.Hardware.IO
{
    public class EtherCatIO : BaseIODevice
    {
        public EtherCatIO(int inputCount,int outputCount, string deviceId, string deviceName, bool isSimulated, ILogService logger)
            : base(deviceId, deviceName, isSimulated, logger) 
        {
            InputCount = inputCount;
            OutputCount = outputCount;
        }


        public override int InputCount { get; }

        public override int OutputCount { get; }

        protected override Task<bool> InternalConnectAsync(CancellationToken token)
        {
            return Task .FromResult (true );
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
