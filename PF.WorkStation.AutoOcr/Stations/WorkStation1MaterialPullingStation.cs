using Org.BouncyCastle.Crypto.Modes.Gcm;
using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    [StationUI("工位1拉料工站", "WorkStation1MaterialPullingStationDebugView", order: 2)]
    public  class WorkStation1MaterialPullingStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {

        private readonly WorkStation1MaterialPullingModule? _pullingModule;

        private readonly WorkStationDataModule? _dataModule;

        private readonly IStationSyncService _sync;

        private Station1PullingStep _currentStep = Station1PullingStep.等待允许取料;

        private OCRRecipeParam? _cachedRecipe;

        public enum Station1PullingStep
        {
            #region 

            等待允许取料,
            获取当前配方,
            判断流道尺寸,
            调整流道尺寸,
            移动到取料位,
            关闭夹爪,
            检测叠料,
            移动到检测位,
            发送拉料完成,
            扫码识别,
            允许检测位检测,
            等待检测位检测完成,
            等待允许送料,
            送料到取料位,
            打开夹爪,
            移动到待机位,
            判断带片,
            发送退料完成,



            #region 异常流程

            获取配方失败,
            调整流道尺寸失败,
            移动到取料位失败,
            关闭夹爪失败,
            检测到叠料异常,
            移动到检测位失败,
            送料到取料位失败,
            打开夹爪失败,
            移动到待机位失败,
            判断带片异常,

            #endregion  异常流程


            #endregion
        }


        public WorkStation1MaterialPullingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("工位1拉料工站", logger)
        {
            _pullingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation1MaterialPullingModule)) as WorkStation1MaterialPullingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;
            _pullingModule.AlarmTriggered += _pullingModule_AlarmTriggered;
            
        }

      

        private void _pullingModule_AlarmTriggered(object? sender, Core.Events.MechanismAlarmEventArgs e)
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
                if (!await _pullingModule.InitializeAsync(token))
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

        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑，恢复步序：[{_currentStep}]）...");

                // 调用模组硬件层复位：遍历清除所有注册轴/IO的报警标志位，无轴运动
                if (_pullingModule != null)
                    await _pullingModule.ResetAsync(token);

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




        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {


                    #region 正常流程
                    case Station1PullingStep.等待允许取料:
                        CurrentStepDescription = "等待允许拉出物料...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待允许拉料信号...");
                        await _sync.WaitAsync(WorkstationSignals.工位1允许拉料.ToString(), token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 检测到允许拉料信号...");
                        _currentStep = Station1PullingStep.获取当前配方;
                        break;


                    case Station1PullingStep.获取当前配方:
                        CurrentStepDescription = "获取当前配方...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 正在获取当前配方...");
                        _cachedRecipe = _dataModule.Station1ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 获取当前配方失败！");
                            _currentStep = Station1PullingStep.获取配方失败;
                            break;
                        }
                        _logger.Info($"[{StationName}] 获取当前配方成功：{_cachedRecipe.RecipeName}");
                        _currentStep = Station1PullingStep.判断流道尺寸;
                        break;

                    case Station1PullingStep.判断流道尺寸:
                        CurrentStepDescription = "判断流道尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _pullingModule.CheckWafeSizeControl(_cachedRecipe.WafeSize, token))
                        {
                            _logger.Info($"[{StationName}] 切换流道尺寸：{_cachedRecipe.WafeSize}");
                            _currentStep = Station1PullingStep.移动到取料位;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.调整流道尺寸;
                        }
                        break;


                    case Station1PullingStep.调整流道尺寸:
                        CurrentStepDescription = "切换流道尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _pullingModule.ChangeWafeSizeControl(_cachedRecipe.WafeSize, token))
                        {
                            this._currentStep = Station1PullingStep.判断流道尺寸;
                        }
                        else
                        {
                            this._currentStep = Station1PullingStep.调整流道尺寸失败;
                        }
                        break;


                    case Station1PullingStep.移动到取料位:
                        CurrentStepDescription = "移动到取料位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _pullingModule.InitialMoveFeeding(token))
                        {
                            _logger.Info($"[{StationName}] 运动到取料位成功");
                            _currentStep = Station1PullingStep.关闭夹爪;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.移动到取料位失败;
                        }
                        break;

                    case Station1PullingStep.关闭夹爪:
                        CurrentStepDescription = "关闭夹爪...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _pullingModule.CloseWafeGipper(token))
                        {
                            _logger.Info($"[{StationName}] 关闭夹爪成功");
                            _currentStep = Station1PullingStep.检测叠料;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.关闭夹爪失败;
                        }

                        break;


                    case Station1PullingStep.检测叠料:
                        CurrentStepDescription = "检测叠料...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _pullingModule.CheckStackedPieces(token))
                        {
                            _logger.Info($"[{StationName}] 判断叠料无异常");
                            _currentStep = Station1PullingStep.移动到检测位;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.检测到叠料异常;
                        }

                        break;


                    case Station1PullingStep.移动到检测位:
                        CurrentStepDescription = "移动到检测位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _pullingModule.MoveDetection(token))
                        {
                            _logger.Info($"[{StationName}] 运动到检测位成功 ");
                            _currentStep = Station1PullingStep.扫码识别;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.移动到检测位失败;
                        }

                        break;


                    case Station1PullingStep.扫码识别:
                        CurrentStepDescription = "扫码识别...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        List<string> coderec = await _pullingModule.CodeScanTigger(token);
                        _logger.Info($"[{StationName}] 扫码识别成功，识别结果：{string.Join(", ", coderec)}");
                        _currentStep = Station1PullingStep.允许检测位检测;
                       
                        break;

                    case Station1PullingStep.允许检测位检测:
                        CurrentStepDescription = "扫码识别...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 允许检测位检测");
                        _sync.Release(WorkstationSignals.工位1允许检测.ToString());
                        _currentStep = Station1PullingStep.等待检测位检测完成;
                        break;
                    case Station1PullingStep.等待检测位检测完成:
                        CurrentStepDescription = "等待检测位检测完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待检测位检测完成信号");
                        await _sync.WaitAsync(WorkstationSignals.工位1检测完成.ToString(), token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待到检测位检测完成信号");
                        _currentStep = Station1PullingStep.等待允许送料;
                        break;

                    case Station1PullingStep.等待允许送料:
                        CurrentStepDescription = "等待允许送料...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待允许送料信号");
                        await _sync.WaitAsync(WorkstationSignals.工位1允许退料.ToString(), token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待到允许送料信号");
                        _currentStep = Station1PullingStep.送料到取料位;
                        break;

                    case Station1PullingStep.送料到取料位:
                        CurrentStepDescription = "等待允许送料...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 送料到取料位 ");
                        if (await _pullingModule.FeedingMaterialToBox(token))
                        {
                            _currentStep = Station1PullingStep.打开夹爪;
                            _logger.Info($"[{StationName}] 送料到取料位成功 ");
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.送料到取料位失败;
                        }
                        break;


                    case Station1PullingStep.打开夹爪:
                        CurrentStepDescription = "打开夹爪...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 打开夹爪 ");
                        if (await _pullingModule.OpenWafeGipper(token))
                        {
                            _logger.Info($"[{StationName}] 松开夹爪成功 ");
                            _currentStep = Station1PullingStep.移动到待机位;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.打开夹爪失败;
                        }
                        break;


                    case Station1PullingStep.移动到待机位:
                        CurrentStepDescription = "移动到待机位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 移动到待机位 ");
                        if (await _pullingModule.MoveInitialNoScan())
                        {
                            _logger.Info($"[{StationName}] 移动到待机位成功 ");
                            _currentStep = Station1PullingStep.判断带片;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.移动到待机位失败;
                        }
                        break;

                    case Station1PullingStep.判断带片:
                        CurrentStepDescription = "判断带片...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 判断带片 ");
                        if (await _pullingModule.CheckGipperInsidePro())
                        {

                            _logger.Info($"[{StationName}] 判断带片结果未带片 ");
                            _currentStep = Station1PullingStep.发送退料完成;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.判断带片异常;
                        }
                        break;

                    case Station1PullingStep.发送退料完成:
                        CurrentStepDescription = "发送退料完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 发送退料完成 ");
                        _sync.Release(WorkstationSignals.工位1退料完成.ToString());
                        _currentStep = Station1PullingStep.等待允许取料;
                        break;


                    #endregion 正常流程


                    #region 异常流程

                    case Station1PullingStep.获取配方失败:
                        _logger.Error($"[{StationName}] 工位1配方参数为空，无法继续。请确认配方已正确下发后复位重启。");
                        _currentStep = Station1PullingStep.等待允许取料;
                        TriggerAlarm();
                        break;


                    case Station1PullingStep.调整流道尺寸失败:
                        _logger.Error($"[{StationName}] 调整流道尺寸失败，流道尺寸{_cachedRecipe .WafeSize }");
                        
                        _currentStep = Station1PullingStep.判断流道尺寸 ;
                        TriggerAlarm();
                        break;


                    case Station1PullingStep.移动到取料位失败:
                        _logger.Error($"[{StationName}] 移动到取料位失败");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.移动到取料位;
                        TriggerAlarm();
                        break;


                    case Station1PullingStep.关闭夹爪失败:
                        _logger.Error($"[{StationName}] 关闭夹爪失败");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.关闭夹爪;
                        TriggerAlarm();
                        break;

                    case Station1PullingStep.检测到叠料异常:
                        _logger.Error($"[{StationName}] 检测到叠料，检查料盒物料");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.检测叠料;
                        TriggerAlarm();
                        break;

                    case Station1PullingStep.移动到检测位失败:
                        _logger.Error($"[{StationName}] 移动到检测位失败,检查是否卡料掉料");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.移动到检测位;
                        TriggerAlarm();
                        break;

                    case Station1PullingStep.送料到取料位失败:
                        _logger.Error($"[{StationName}] 送料到取料位失败,检查是否卡料掉料");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.送料到取料位;
                        TriggerAlarm();
                        break;

                    case Station1PullingStep.打开夹爪失败:
                        _logger.Error($"[{StationName}] 打开夹爪失败,检查气缸信号");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.打开夹爪;
                        TriggerAlarm();
                        break;


                    case Station1PullingStep.移动到待机位失败:
                        _logger.Error($"[{StationName}] 移动到待机位失败");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.移动到待机位;
                        TriggerAlarm();
                        break;

                    case Station1PullingStep.判断带片异常:
                        _logger.Error($"[{StationName}] 夹爪带料");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1PullingStep.判断带片;
                        TriggerAlarm();
                        break;
                        #endregion 异常流程

                }
            }
        }


        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
