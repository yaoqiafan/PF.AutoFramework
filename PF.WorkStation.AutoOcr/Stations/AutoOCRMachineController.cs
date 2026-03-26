using PF.Core.Events;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station;
using PF.Infrastructure.Station.Basic;

namespace PF.WorkStation.AutoOcr.Stations
{
    
    public class AutoOCRMachineController : BaseMasterController
    {
        public enum WorkstationSignals
        {
           工位1允许拉料,
           工位1拉料完成,
           工位1允许退料,
           工位1退料完成,







            工位2允许拉料,
            工位2拉料完成,
            工位2允许退料,
            工位2退料完成,


        }

        private readonly IStationSyncService _sync;
        public AutoOCRMachineController(ILogService logger,
            HardwareInputEventBus hardwareEventBus,
            IStationSyncService sync,
            IEnumerable<StationBase<StationMemoryBaseParam>> subStations)
            : base(logger, hardwareEventBus, subStations)
        {
            _sync = sync;

            
            _sync.Register(WorkstationSignals.工位1允许拉料.ToString());
            _sync.Register(WorkstationSignals.工位1拉料完成.ToString());
            _sync.Register(WorkstationSignals.工位1允许退料.ToString());
            _sync.Register(WorkstationSignals.工位1退料完成.ToString());






            _sync.Register(WorkstationSignals.工位2允许拉料.ToString());
            _sync.Register(WorkstationSignals.工位2拉料完成.ToString());
            _sync.Register(WorkstationSignals.工位2允许退料.ToString());
            _sync.Register(WorkstationSignals.工位2退料完成.ToString());

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
