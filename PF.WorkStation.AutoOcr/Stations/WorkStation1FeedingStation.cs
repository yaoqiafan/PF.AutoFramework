using PF.Core.Attributes;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Station.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    [StationUI("工位1上下料工站", "WorkStation1FeedingStationDebugView", order: 1)]
    public class WorkStation1FeedingStation : StationBase
    {
        public enum Station1FeedingStep
        {

        }


        public WorkStation1FeedingStation( ILogService logger) : base("工位1上下料工站", logger)
        {

        }

        public override Task ExecuteInitializeAsync(CancellationToken token)
        {
            return base.ExecuteInitializeAsync(token);
        }

        public override Task ExecuteResetAsync(CancellationToken token)
        {
            return base.ExecuteResetAsync(token);
        }

        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override Task ProcessNormalLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
