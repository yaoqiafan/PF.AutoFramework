using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Infrastructure.Station;
using PF.Infrastructure.Station.Basic;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    public class AutoOCRMachineController : BaseMasterController
    {
        public AutoOCRMachineController(ILogService logger, PhysicalButtonEventBus hardwareEventBus, IEnumerable<StationBase> subStations) : base(logger, hardwareEventBus, subStations)
        {

        }
    }
}
