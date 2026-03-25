using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station;
using PF.Infrastructure.Station.Basic;
using PF.WorkStation.AutoOcr.Sync;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    public class AutoOCRMachineController : BaseMasterController
    {
        private readonly IStationSyncService _sync;
        public AutoOCRMachineController(ILogService logger,
            PhysicalButtonEventBus hardwareEventBus,
            IStationSyncService sync,
            IEnumerable<StationBase<StationMemoryBaseParam>> subStations)
            : base(logger, hardwareEventBus, subStations)
        {
            _sync = sync;

            
            //_sync.Register(WorkstationSignals.SlotEmpty, initialCount: 1, maxCount: 1);
            //_sync.Register(WorkstationSignals.ProductReady, initialCount: 0, maxCount: 1);
        }


        /// <summary>
        /// 重写父类钩子：在所有子工站物理复位成功后，安全重置本机器的流水线信号量
        /// </summary>
        protected override void OnAfterResetSuccess()
        {
            _logger.Info("【Demo机台主控】各工站物理复位完毕，正在重置流水线信号量...");
            _sync.ResetAll();
        }

    }
}
