using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;

namespace PF.WorkStation.AutoOcr.Stations
{

    [StationUI("OCR检测工站", "WorkStationDetectionStationDebugView", order: 2)]
    public class WorkStationDetectionStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {
        private readonly WorkStationDetectionModule? _detectionModule;
        private readonly WorkStationDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        private StationDetectionStep _currentStep = StationDetectionStep.等待工位1或工位2允许检测;

        private E_WorkSpace _currentworkSpace = E_WorkSpace.工位1;

        public enum StationDetectionStep
        {
            等待工位1或工位2允许检测,
            去工位1检测位置,

            去工位2检测位置,
            触发检测,

            数据比对,

            写入检测数据,
            检测完成,







            去工位一检测位置异常,
            触发检测异常,

        }

        public WorkStationDetectionStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("OCR检测工站", logger)
        {
            _detectionModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDetectionModule)) as WorkStationDetectionModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;

            _detectionModule.AlarmTriggered += OnMechanismAlarm;
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
                _logger.Info($"[{StationName}] 正在初始化OCR模组...");
                if (!await _detectionModule.InitializeAsync(token))
                    throw new Exception($"[{StationName}] OCR模组初始化失败！");
                _logger.Success($"[{StationName}] 初始化完成，就绪。");
                Fire(MachineTrigger.InitializeDone); // Initializing → Idle
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }
        }

        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑，恢复步序：[{_currentStep}]）...");

                // 调用模组硬件层复位：遍历清除所有注册轴/IO的报警标志位，无轴运动
                if (_detectionModule != null)
                    await _detectionModule.ResetAsync(token);

                // 注意：不重置 _currentStep！
                // 断点续跑的恢复节点已在各异常 case 中于 TriggerAlarm() 前设定完毕。

                _logger.Success($"[{StationName}] 复位完成，回到就绪状态，将从步序 [{_currentStep}] 继续执行。");
                await FireAsync(MachineTrigger.ResetDone);  // Resetting → Idle
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 复位失败: {ex.Message}");
                Fire(MachineTrigger.Error);  // Resetting → Alarm，不卡死在 Resetting
                throw;
            }
        }

        protected override async Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    case StationDetectionStep.等待工位1或工位2允许检测:
                        break;
                    case StationDetectionStep.去工位1检测位置:
                        break;
                    case StationDetectionStep.去工位2检测位置:
                        break;
                    case StationDetectionStep.触发检测:
                        break;
                    case StationDetectionStep.数据比对:
                        break;
                    case StationDetectionStep.写入检测数据:
                        break;
                    case StationDetectionStep.检测完成:
                        break;
                    case StationDetectionStep.去工位一检测位置异常:
                        break;
                    case StationDetectionStep.触发检测异常:
                        break;
                    default:
                        break;
                }

            }
        }
    }
}
