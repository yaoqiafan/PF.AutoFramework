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
    [StationUI("工位1拉料工站", "WorkStation1MaterialPullingStationDebugView", order: 1)]
    internal class WorkStation1MaterialPullingStation<T> : StationBase<T> where T : StationMemoryBaseParam
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
            运动到取料位,
            关闭夹爪,
            判断叠料,
            移动到检测位,
            发送拉料完成,
            扫码识别,
            允许检测位检测,
            等待检测位检测完成,
            等待允许送料,
            移动到取料位,
            松开夹爪,
            移动到待机位,
            判断带片,
            发送送料完成,



            #region 异常流程

            获取配方失败,
            调整流道尺寸失败,
            移动到取料位失败,
            关闭夹爪失败,
            检测到叠料异常,
            移动到检测位失败,

            #endregion  异常流程


            #endregion
        }


        public WorkStation1MaterialPullingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("工位1拉料工站", logger)
        {
            _pullingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation1MaterialPullingModule)) as WorkStation1MaterialPullingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;
            _pullingModule.AlarmTriggered += _pullingModule_AlarmTriggered;
            _dataModule.AlarmTriggered += _dataModule_AlarmTriggered;
        }

        private void _dataModule_AlarmTriggered(object? sender, Core.Events.MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            TriggerAlarm();
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
                        if (await _pullingModule.CheckWafeSizeControl(_cachedRecipe.WafeSize, token))
                        {
                            _currentStep = Station1PullingStep.运动到取料位;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.调整流道尺寸;
                        }
                        break;


                    case Station1PullingStep.调整流道尺寸:
                        if (await _pullingModule.ChangeWafeSizeControl(_cachedRecipe.WafeSize, token))
                        {
                            this._currentStep = Station1PullingStep.判断流道尺寸;
                        }
                        else
                        {
                            this._currentStep = Station1PullingStep.调整流道尺寸失败;
                        }
                        break;


                    case Station1PullingStep.运动到取料位:
                        if (await _pullingModule.InitialMoveFeeding(token))
                        {
                            _currentStep = Station1PullingStep.关闭夹爪;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.移动到取料位失败;
                        }
                        break;

                    case Station1PullingStep.关闭夹爪:
                        if (await _pullingModule.CloseWafeGipper(token))
                        {
                            _currentStep = Station1PullingStep.判断叠料;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.关闭夹爪失败;
                        }

                        break;


                    case Station1PullingStep.判断叠料:
                        if (await _pullingModule.CheckStackedPieces(token))
                        {
                            _currentStep = Station1PullingStep.移动到检测位;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.检测到叠料异常;
                        }

                        break;


                    case Station1PullingStep.移动到检测位:
                        if (await _pullingModule.MoveDetection(token))
                        {
                            _currentStep = Station1PullingStep.扫码识别;
                        }
                        else
                        {
                            _currentStep = Station1PullingStep.移动到检测位失败;
                        }

                        break;


                    case Station1PullingStep.扫码识别:
                        List<string> coderec = await _pullingModule.CodeScanTigger(token);
                        _sync.Release(WorkstationSignals.工位1允许检测.ToString());

                        _currentStep = Station1PullingStep.允许检测位检测;
                       
                        break;

                    case Station1PullingStep.允许检测位检测:
                        CurrentStepDescription = "等待检测位检测完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待检测位检测完成...");
                        await _sync.WaitAsync(WorkstationSignals.工位1检测完成.ToString(), token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 检测位检测完成...");
                        _currentStep = Station1PullingStep.获取当前配方;
                        break;


                }
            }
        }


        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
