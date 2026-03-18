using PF.Core.Attributes;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    [MechanismUI("工位1上晶圆模组", "Workstation1FeedingModelDebugView", 1)]
    public class WorkStation1FeedingModule : BaseMechanism
    {
        public WorkStation1FeedingModule( IHardwareManagerService hardwareManagerService,ILogService logger) : base(E_Mechanisms.工位1上晶圆模组.ToString(), hardwareManagerService, logger)
        {
        }

        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override Task InternalStopAsync()
        {
            throw new NotImplementedException();
        }
    }
}
