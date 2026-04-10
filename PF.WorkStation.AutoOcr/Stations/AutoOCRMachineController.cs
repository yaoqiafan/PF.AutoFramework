using PF.Core.Events;
using PF.Core.Interfaces.Alarm;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station;
using PF.Infrastructure.Station.Basic;
using PF.WorkStation.AutoOcr.CostParam;

namespace PF.WorkStation.AutoOcr.Stations
{
    public enum WorkstationSignals
    {
        工位1启动按钮按下,
        工位1允许拉料,
        工位1拉料完成,
        工位1允许退料,
        工位1退料完成,
        工位1允许检测,
        工位1检测完成,






        工位2启动按钮按下,
        工位2允许拉料,
        工位2拉料完成,
        工位2允许退料,
        工位2退料完成,
        工位2允许检测,
        工位2检测完成,

    }
    public class AutoOCRMachineController : BaseMasterController
    {

        private readonly IHardwareInputMonitor _hardwareInputMonitor;
        private readonly IStationSyncService _sync;
        public AutoOCRMachineController(ILogService logger,
            IAlarmService alarmService,
            HardwareInputEventBus hardwareEventBus,
            IHardwareInputMonitor hardwareInputMonitor,
            IStationSyncService sync,
            IEnumerable<StationBase<StationMemoryBaseParam>> subStations)
            : base(logger, alarmService, hardwareEventBus, subStations)
        {
            _hardwareInputMonitor = hardwareInputMonitor;
            _sync = sync;

            _sync.Register(WorkstationSignals.工位1启动按钮按下.ToString());
            _sync.Register(WorkstationSignals.工位1允许拉料.ToString());
            _sync.Register(WorkstationSignals.工位1拉料完成.ToString());
            _sync.Register(WorkstationSignals.工位1允许退料.ToString());
            _sync.Register(WorkstationSignals.工位1退料完成.ToString());
            _sync.Register(WorkstationSignals.工位1允许检测.ToString());
            _sync.Register(WorkstationSignals.工位1检测完成.ToString());



            _sync.Register(WorkstationSignals.工位2启动按钮按下.ToString());
            _sync.Register(WorkstationSignals.工位2允许拉料.ToString());
            _sync.Register(WorkstationSignals.工位2拉料完成.ToString());
            _sync.Register(WorkstationSignals.工位2允许退料.ToString());
            _sync.Register(WorkstationSignals.工位2退料完成.ToString());
            _sync.Register(WorkstationSignals.工位2允许检测.ToString());
            _sync.Register(WorkstationSignals.工位2检测完成.ToString());

        }



        protected override void OnHardwareInputReceived(string inputType)
        {
            base.OnHardwareInputReceived(inputType);

            switch (inputType)
            {
                case HardwareInputTypeExtension.WorkStation1Start:
                    _sync.Release(WorkstationSignals.工位1启动按钮按下.ToString());
                    _hardwareInputMonitor.StartSafetyMonitoring();
                    break;

                case HardwareInputTypeExtension.WorkStation2Start:
                    _sync.Release(WorkstationSignals.工位2启动按钮按下.ToString());
                    _hardwareInputMonitor.StartSafetyMonitoring();

                    break;

                default:
                    break;
            }
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
