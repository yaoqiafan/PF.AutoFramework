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

        private readonly WorkStationDataModule? _dataModule;

        private readonly IStationSyncService _sync;


        private Station1FeedingStep _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
        public enum Station1FeedingStep
        {
            #region 运动前准备

            等待按下工位1启动按钮 = 0,
            验证当前批次产品个数 = 10,
            获取工位1配方参数 = 20,
            识别料盒尺寸 = 30,
            验证尺寸与配方是否匹配 = 40,
            判断Z轴是否具备运动条件_寻层 = 50,
            Z轴扫描寻层 = 60,
            到初始层点 = 70,
            阵列层取料位 = 80,
            算法过滤层数 = 90,

            #endregion

            #region 运动流程

            判断X轴是否具备运动条件_开始 = 100,
            X轴到待机位 = 110,

            #region 循环流程

            判断Z轴是否具备运动条件_取料定位 = 120,
            切换到指定层 = 130,
            错层检测 = 140,

            等待物料拉出完成 = 150,
            阻塞等待物料回退完成 = 160,

            计算下一层位置 = 170,

            #endregion

            物料全部生产完毕 = 200,
            判断X轴是否具备运动条件_结束 = 210,
            X轴到挡料位 = 220,
            判断Z轴是否具备运动条件_流程结束 = 230,
            Z轴到待机位 = 240,
            #endregion

            通知操作员下料 = 300,
            生产完毕 = 400,


            #region 异常
            批次产品个数不正确 = 100001,
            料盒尺寸与配方不匹配 = 100002,

            #endregion

        }


        public WorkStation1FeedingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("工位1上下料工站", logger)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation1FeedingModule)) as WorkStation1FeedingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;

            _feedingModule.AlarmTriggered += OnMechanismAlarm;
            _dataModule.AlarmTriggered += _dataModule_AlarmTriggered;
        }

        private void _dataModule_AlarmTriggered(object? sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            TriggerAlarm();
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

        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    case Station1FeedingStep.等待按下工位1启动按钮:
                        break;
                    case Station1FeedingStep.验证当前批次产品个数:
                        break;
                    case Station1FeedingStep.获取工位1配方参数:
                        break;
                    case Station1FeedingStep.识别料盒尺寸:
                        break;
                    case Station1FeedingStep.验证尺寸与配方是否匹配:
                        break;
                    case Station1FeedingStep.判断Z轴是否具备运动条件_寻层:
                        break;
                    case Station1FeedingStep.Z轴扫描寻层:
                        break;
                    case Station1FeedingStep.到初始层点:
                        break;
                    case Station1FeedingStep.阵列层取料位:
                        break;
                    case Station1FeedingStep.算法过滤层数:
                        break;
                    case Station1FeedingStep.判断X轴是否具备运动条件_开始:
                        break;
                    case Station1FeedingStep.X轴到待机位:
                        break;
                    case Station1FeedingStep.判断Z轴是否具备运动条件_取料定位:
                        break;
                    case Station1FeedingStep.切换到指定层:
                        break;
                    case Station1FeedingStep.错层检测:
                        break;
                    case Station1FeedingStep.等待物料拉出完成:
                        break;
                    case Station1FeedingStep.阻塞等待物料回退完成:
                        break;
                    case Station1FeedingStep.计算下一层位置:
                        break;
                    case Station1FeedingStep.物料全部生产完毕:
                        break;
                    case Station1FeedingStep.判断X轴是否具备运动条件_结束:
                        break;
                    case Station1FeedingStep.X轴到挡料位:
                        break;
                    case Station1FeedingStep.判断Z轴是否具备运动条件_流程结束:
                        break;
                    case Station1FeedingStep.Z轴到待机位:
                        break;
                    case Station1FeedingStep.通知操作员下料:
                        break;
                    case Station1FeedingStep.生产完毕:
                        break;
                    case Station1FeedingStep.批次产品个数不正确:
                        break;
                    case Station1FeedingStep.料盒尺寸与配方不匹配:
                        break;
                    default:
                        break;
                }

            }
        }




    }
}
