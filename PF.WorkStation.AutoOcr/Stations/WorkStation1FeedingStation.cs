using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    [StationUI("工位1上下料工站", "WorkStation1FeedingStationDebugView", order: 1)]
    public class WorkStation1FeedingStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {
        private readonly WorkStation1FeedingModule? _feedingModule;
        private readonly IStationSyncService _sync;


        private Station1FeedingStep _currentStep = Station1FeedingStep.test1;
        public enum Station1FeedingStep
        {
            test1,
        }


        public WorkStation1FeedingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("工位1上下料工站", logger)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation1FeedingModule)) as WorkStation1FeedingModule;
            _sync = sync;

            _feedingModule.AlarmTriggered += OnMechanismAlarm;
        }

        private void OnMechanismAlarm(object? sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            TriggerAlarm();
        }

        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {
                _logger.Info($"[{StationName}] 正在初始化上下料模组...");
                if (!await _feedingModule.InitializeAsync(token))
                    throw new Exception($"[{StationName}] 上下料模组初始化失败！");
                _logger.Success($"[{StationName}] 初始化完成，就绪。");
                Fire(MachineTrigger.InitializeDone); // Initializing → Idle
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }
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
