using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Station.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    public class WorkStation1FeedingStation : StationBase
    {
        public WorkStation1FeedingStation(string name, ILogService logger) : base(name, logger)
        {
        }

        protected override Task ProcessLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
