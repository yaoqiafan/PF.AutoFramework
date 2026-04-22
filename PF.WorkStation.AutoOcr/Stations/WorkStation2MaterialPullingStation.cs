using Prism.Ioc;
using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Core.Models;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    #region State Machine Enums (业务步序枚举)

    /// <summary>
    /// 定义拉料工站的完整生命周期与断点续跑异常状态节点
    /// </summary>
    public enum Station2PullingStep
    {
        #region 阶段 A：取料前置准备与动作 (0 - 80)

        /// <summary>等待允许取料</summary>
        等待允许取料 = 0,
        /// <summary>获取当前配方</summary>
        获取当前配方 = 10,
        /// <summary>判断流道尺寸</summary>
        判断流道尺寸 = 20,
        /// <summary>调整流道尺寸</summary>
        调整流道尺寸 = 30,
        /// <summary>移动到取料位</summary>
        移动到取料位 = 40,
        /// <summary>关闭夹爪</summary>
        关闭夹爪 = 50,
        /// <summary>检测叠料</summary>
        检测叠料 = 60,

        #endregion

        #region 阶段 B：检测与视觉交互 (100 - 150)

        /// <summary>移动到检测位</summary>
        移动到检测位 = 100,
        /// <summary>发送拉料完成</summary>
        发送拉料完成 = 110,
        /// <summary>扫码识别</summary>
        扫码识别 = 120,
        /// <summary>允许检测位检测</summary>
        允许检测位检测 = 130,
        /// <summary>等待检测位检测完成</summary>
        等待检测位检测完成 = 140,

        #endregion

        #region 阶段 C：退料与收尾 (200 - 250)

        /// <summary>等待允许送料</summary>
        等待允许送料 = 200,
        /// <summary>送料到取料位</summary>
        送料到取料位 = 210,
        /// <summary>打开夹爪</summary>
        打开夹爪 = 220,
        /// <summary>移动到待机位</summary>
        移动到待机位 = 230,
        /// <summary>判断带片</summary>
        判断带片 = 240,
        /// <summary>发送退料完成</summary>
        发送退料完成 = 250,

        #endregion

        #region 阶段 D：异常拦截与断点续跑节点 (100000+)

        // ── 业务与数据校验异常 (10000X) ──
        /// <summary>配方参数为空</summary>
        配方参数为空 = 100001,

        // ── 传感器与物料防呆异常 (10001X) ──
        /// <summary>检测到叠料异常</summary>
        检测到叠料异常 = 100010,
        /// <summary>退回安全位后夹爪仍检测到带料</summary>
        退回安全位后夹爪仍检测到带料 = 100011,
        /// <summary>轨道有物料阻止尺寸切换</summary>
        轨道有物料阻止尺寸切换 = 100012,
        /// <summary>夹爪闭合后未检测到铁环</summary>
        夹爪闭合后未检测到铁环 = 100013,
        /// <summary>待机位检测到残留物料</summary>
        待机位检测到残留物料 = 100014,
        /// <summary>卸料后夹爪物料粘连未脱落</summary>
        卸料后夹爪物料粘连未脱落 = 100015,

        // ── 气缸与执行器异常 (10002X) ──
        /// <summary>关闭夹爪失败（未感应到闭合信号）</summary>
        关闭夹爪失败 = 100020,
        /// <summary>打开夹爪失败（未感应到张开信号）</summary>
        打开夹爪失败 = 100021,
        /// <summary>尺寸切换气缸IO操作失败</summary>
        尺寸切换气缸IO操作失败 = 100022,
        /// <summary>尺寸切换气缸动作超时</summary>
        尺寸切换气缸超时 = 100023,
        /// <summary>夹爪张开气缸操作失败</summary>
        夹爪张开气缸操作失败 = 100024,
        /// <summary>夹爪张开超时</summary>
        夹爪张开超时 = 100025,
        /// <summary>夹爪闭合气缸操作失败</summary>
        夹爪闭合气缸操作失败 = 100026,
        /// <summary>夹爪闭合超时</summary>
        夹爪闭合超时 = 100027,

        // ── 基础定位与运动异常 (10003X) ──
        /// <summary>调整流道尺寸失败（电机异常）</summary>
        调整流道尺寸电机异常 = 100030,
        /// <summary>初始化拉料流程失败</summary>
        初始化拉料流程失败 = 100031,
        /// <summary>Y轴移动到取料位置失败</summary>
        Y轴移动到取料位置失败 = 100032,
        /// <summary>Y轴退回待机位失败</summary>
        Y轴退回待机位失败 = 100033,
        /// <summary>移动到待机位失败（强制复位）</summary>
        移动到待机位失败_无检测模式 = 100034,
        /// <summary>移动到取出安全位置失败</summary>
        移动到取出安全位置失败 = 100035,

        // ── 核心拉送片过程与防呆拦截异常 (10004X) ──
        /// <summary>拉出至检测位失败（运动被中断）</summary>
        拉出至检测位失败_被中断 = 100040,
        /// <summary>推回至料盒失败（运动被中断）</summary>
        推回至料盒失败_被中断 = 100041,
        /// <summary>拉出运动触发失败</summary>
        拉出运动触发失败 = 100042,
        /// <summary>拉出过程卡料报警</summary>
        拉出过程卡料报警 = 100043,
        /// <summary>拉出过程丢料报警</summary>
        拉出过程丢料报警 = 100044,
        /// <summary>拉出运动超时</summary>
        拉出运动超时 = 100045,
        /// <summary>送入运动触发失败</summary>
        送入运动触发失败 = 100046,
        /// <summary>送入过程卡料报警</summary>
        送入过程卡料报警 = 100047,
        /// <summary>送入过程丢料报警</summary>
        送入过程丢料报警 = 100048,
        /// <summary>送入运动超时</summary>
        送入运动超时 = 100049,

        // ── 相机与视觉异常 (10005X) ──
        /// <summary>扫码失败或校验不合法</summary>
        扫码失败 = 100050,

        // ── 系统级异常 (10009X) ──
        /// <summary>状态机指针漂移，进入未定义步序</summary>
        状态机进入未定义步序 = 100099

        #endregion
    }

    #endregion

    /// <summary>
    /// 【工位2】拉料工站业务流转控制器 (Material Pulling Station Controller - Station 2)
    ///
    /// <para>架构定位：</para>
    /// 继承自 <see cref="StationBase{T, TStep}"/>，作为拉料业务的独立状态机。
    /// 负责调度 <see cref="WorkStation2MaterialPullingModule"/> 执行具体的水平 Y 轴推拉动作与气爪控制。
    ///
    /// <para>跨工站协同：</para>
    /// 通过 <see cref="IStationSyncService"/> 与 <see cref="WorkStation2FeedingStation{T}"/> (上下料 Z 轴)
    /// 以及 <see cref="WorkStationDetectionModule"/> (OCR视觉) 进行信号握手，实现互不干涉的并发流转。
    /// </summary>
    [StationUI("工位2拉料工站", "WorkStation2MaterialPullingStationDebugView", order: 4)]
    public class WorkStation2MaterialPullingStation<T> : StationBase<T, Station2PullingStep> where T : StationMemoryBaseParam
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WorkStation2MaterialPullingModule? _pullingModule;
        private readonly WorkStationDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        /// <summary>
        /// 当前批次缓存的工位工艺配方
        /// </summary>
        private OCRRecipeParam? _cachedRecipe;

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化工位2拉料工站
        /// </summary>
        public WorkStation2MaterialPullingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            // 接入带枚举泛型的基类，统一管理 _currentStep 和 _resumeStep
            : base(E_WorkStation.工位2拉料工站.ToString(), logger, Station2PullingStep.等待允许取料)
        {
            _pullingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation2MaterialPullingModule)) as WorkStation2MaterialPullingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;

            // 订阅底层模组的硬件报警并上抛
            if (_pullingModule != null)
            {
                _pullingModule.AlarmTriggered += PullingModule_AlarmTriggered;
                _pullingModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();
            }
        }

        private void PullingModule_AlarmTriggered(object? sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            RaiseAlarm(new StationAlarmEventArgs
            {
                ErrorCode = e.ErrorCode ?? AlarmCodes.System.StationSyncError,
                RuntimeMessage = e.ErrorMessage,
                HardwareName = e.HardwareName,
                InternalException = e.InternalException
            });
        }

        private async Task ExecuteResumeFromBreakpointAsync(CancellationToken token)
        {
            _logger.Info($"[{StationName}] 开始执行断点续跑恢复，当前恢复步序: {_currentStep}");
            try
            {
                switch (_currentStep)
                {
                    case Station2PullingStep.等待允许取料:
                    case Station2PullingStep.等待检测位检测完成:
                    case Station2PullingStep.等待允许送料:
                        _logger.Info($"[{StationName}] 恢复跨工站信号等待状态...");
                        break;
                    default:
                        _logger.Info($"[{StationName}] 保持当前业务动作节点: {_currentStep}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 执行断点续跑时发生异常: {ex.Message}");
                _currentStep = Station2PullingStep.等待允许取料;
            }
        }

        /// <summary>执行工站初始化</summary>
        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {
                _logger.Info($"[{StationName}] 正在初始化拉料模组...");

                if (!await _pullingModule.WaitHomeDoneAsync(_pullingModule.YAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，Y轴回零异常。");
                    Fire(MachineTrigger.Error);
                    return;
                }

                var initResult = await _pullingModule.MoveInitialNoScan(token);
                if (!initResult.IsSuccess)
                {
                    _logger.Error($"[{StationName}] 初始化失败，Y轴移动到待机位异常。");
                    Fire(MachineTrigger.Error);
                    return;
                }
                _sync.ResetScope(StationName);//初始化所有标志位
                _logger.Success($"[{StationName}] 初始化完成，就绪。");
                _pullingModule.ResumeHealthMonitoring();
                Fire(MachineTrigger.InitializeDone); // Initializing → Idle
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }

            _currentStep = Station2PullingStep.等待允许取料;
            _resumeStep = Station2PullingStep.等待允许取料;
        }

        /// <summary>执行工站复位</summary>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑，将恢复至步序：[{_currentStep}]）...");

                // 调用模组硬件层复位：遍历清除所有注册轴/IO的报警标志位
                if (_pullingModule != null)
                    await _pullingModule.ResetAsync(token);

                // 仅初始化报警复位时重置信号量；运行期报警复位保留信号量以支持断点续跑
                if (CameFromInitAlarm)
                    _sync.ResetScope(StationName);

                // ⚠️ 不重置 _currentStep！断点续跑的恢复节点已在 RouteToError() 中设定。
                _logger.Success($"[{StationName}] 复位完成，将从步序 [{_currentStep}] 继续执行。");

                if (!CameFromInitAlarm)
                {
                    await ExecuteResumeFromBreakpointAsync(token);
                }

                await FireAsync(ResetCompletionTrigger);  // Resetting → Idle 或 Uninitialized
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 复位失败: {ex.Message}");
                Fire(MachineTrigger.Error);  // 防止卡死在 Resetting
                throw;
            }
        }

        /// <summary>物理急停回调</summary>
        protected override async Task OnPhysicalStopAsync()
        {
            if (_pullingModule != null)
                await _pullingModule.StopAsync().ConfigureAwait(false);
        }

        /// <summary>获取关联模组列表</summary>
        protected override IEnumerable<PF.Infrastructure.Mechanisms.BaseMechanism> GetMechanisms()
        {
            if (_pullingModule != null) yield return _pullingModule;
        }

        /// <summary>空跑流程</summary>
        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            // 空跑模式预留
            return Task.CompletedTask;
        }

        #endregion

        #region Main State Machine Loop (主业务循环)

        /// <summary>正常生产主循环</summary>
        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    // ══════════════════════════════════════════════════════════
                    //  阶段 A：取料前置准备与动作
                    // ══════════════════════════════════════════════════════════
                    #region Phase A

                    case Station2PullingStep.等待允许取料:
                        CurrentStepDescription = "等待允许拉出物料...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待上下料工站的允许拉料信号...");

                        // 阻塞等待 Z 轴工站给出安全的取料口令
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2允许拉料), token, scope: nameof(E_WorkStation.工位2上下料工站)).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 检测到允许拉料信号，开始执行拉料流程...");
                        _currentStep = Station2PullingStep.获取当前配方;
                        break;

                    case Station2PullingStep.获取当前配方:
                        CurrentStepDescription = "获取当前配方...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 正在获取当前配方...");

                        _cachedRecipe = _dataModule.Station2ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 获取当前配方失败！");
                            RouteToError(Station2PullingStep.配方参数为空, Station2PullingStep.等待允许取料);
                            break;
                        }

                        _logger.Info($"[{StationName}] 获取当前配方成功：{_cachedRecipe.RecipeName}");
                        _currentStep = Station2PullingStep.判断流道尺寸;
                        break;

                    case Station2PullingStep.判断流道尺寸:
                        CurrentStepDescription = "判断流道尺寸...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.CheckWafeSizeControl(_cachedRecipe.WafeSize, token))
                        {
                            _logger.Info($"[{StationName}] 当前流道尺寸符合配方要求：{_cachedRecipe.WafeSize}");
                            _currentStep = Station2PullingStep.移动到取料位;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.调整流道尺寸;
                        }
                        break;

                    case Station2PullingStep.调整流道尺寸:
                        CurrentStepDescription = "切换流道尺寸...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        var changeResult = await _pullingModule.ChangeWafeSizeControl(_cachedRecipe.WafeSize, token);
                        if (changeResult.IsSuccess)
                        {
                            // 调整完成后，退回判断节点二次确认防呆
                            _currentStep = Station2PullingStep.判断流道尺寸;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.调整流道尺寸电机异常, Station2PullingStep.判断流道尺寸, changeResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.移动到取料位:
                        CurrentStepDescription = "移动到取料位...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        var moveResult = await _pullingModule.InitialMoveFeeding(token);
                        if (moveResult.IsSuccess)
                        {
                            _logger.Info($"[{StationName}] 运动到取料位成功");
                            _currentStep = Station2PullingStep.关闭夹爪;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.Y轴移动到取料位置失败, Station2PullingStep.移动到取料位, moveResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.关闭夹爪:
                        CurrentStepDescription = "关闭夹爪...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        var closeResult = await _pullingModule.CloseWafeGipper(token);
                        if (closeResult.IsSuccess)
                        {
                            _logger.Info($"[{StationName}] 关闭夹爪成功");
                            _currentStep = Station2PullingStep.检测叠料;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.关闭夹爪失败, Station2PullingStep.关闭夹爪, closeResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.检测叠料:
                        CurrentStepDescription = "检测叠料...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.CheckStackedPieces(token))
                        {
                            _logger.Info($"[{StationName}] 判断叠料无异常");
                            _currentStep = Station2PullingStep.移动到检测位;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.检测到叠料异常, Station2PullingStep.检测叠料);
                        }
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 B：检测与视觉交互 (拉出并交给 OCR 工站)
                    // ══════════════════════════════════════════════════════════
                    #region Phase B

                    case Station2PullingStep.移动到检测位:
                        CurrentStepDescription = "移动到检测位...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        var detectMoveResult = await _pullingModule.MoveDetection(token);
                        if (detectMoveResult.IsSuccess)
                        {
                            _logger.Info($"[{StationName}] 运动到检测位成功");

                            // 通知上下料工站：Y轴已经撤出安全区域，Z轴可以放心移动
                            _sync.Release(nameof(WorkstationSignals.工位2拉料完成), StationName);
                            _currentStep = Station2PullingStep.扫码识别;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.拉出至检测位失败_被中断, Station2PullingStep.移动到检测位, detectMoveResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.扫码识别:
                        CurrentStepDescription = "扫码识别...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        var scanResult = await _pullingModule.CodeScanTigger(token);
                        _logger.Info($"[{StationName}] 扫码识别完成，识别结果：{(scanResult.IsSuccess ? string.Join(", ", scanResult.Data) : "未扫到码或校验不合法")}");

                        _currentStep = Station2PullingStep.允许检测位检测;
                        break;

                    case Station2PullingStep.允许检测位检测:
                        CurrentStepDescription = "通知 OCR 视觉执行检测...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 发送允许检测信号，交给视觉工站处理");
                        // 通知独立调度的 OCR 视觉龙门可以开拍
                        _sync.Release(nameof(WorkstationSignals.工位2允许检测), StationName);

                        _currentStep = Station2PullingStep.等待检测位检测完成;
                        break;

                    case Station2PullingStep.等待检测位检测完成:
                        CurrentStepDescription = "等待检测位检测完成...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 等待视觉工站检测完成信号...");
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2检测完成), token, scope: nameof(E_WorkStation.OCR检测工站)).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 收到视觉检测完成信号");
                        _currentStep = Station2PullingStep.等待允许送料;
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 C：退料与收尾 (将晶圆推回料盒)
                    // ══════════════════════════════════════════════════════════
                    #region Phase C

                    case Station2PullingStep.等待允许送料:
                        CurrentStepDescription = "等待允许退料...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 等待上下料工站 Z 轴安全避让信号...");
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2允许退料), token, scope: nameof(E_WorkStation.工位2上下料工站)).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 收到 Z 轴允许退料信号");
                        _currentStep = Station2PullingStep.送料到取料位;
                        break;

                    case Station2PullingStep.送料到取料位:
                        CurrentStepDescription = "正在退料回料盒...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] Y 轴送料回料盒...");

                        var feedOpenResult = await _pullingModule.OpenWafeGipper(token);
                        if (!feedOpenResult.IsSuccess)
                        {
                            RouteToError(Station2PullingStep.打开夹爪失败, Station2PullingStep.送料到取料位, feedOpenResult.ErrorCode);
                            break;
                        }

                        var feedResult = await _pullingModule.FeedingMaterialToBox(token);
                        if (feedResult.IsSuccess)
                        {
                            _currentStep = Station2PullingStep.打开夹爪;
                            _logger.Info($"[{StationName}] 退料回料盒成功");
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.推回至料盒失败_被中断, Station2PullingStep.送料到取料位, feedResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.打开夹爪:
                        CurrentStepDescription = "打开夹爪...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 正在松开夹爪...");

                        var openResult = await _pullingModule.OpenWafeGipper(token);
                        if (openResult.IsSuccess)
                        {
                            _logger.Info($"[{StationName}] 松开夹爪成功");
                            _currentStep = Station2PullingStep.移动到待机位;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.打开夹爪失败, Station2PullingStep.打开夹爪, openResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.移动到待机位:
                        CurrentStepDescription = "移动到待机位...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] Y 轴撤回待机避让位...");

                        var putOverResult = await _pullingModule.PutOverMove(token);
                        if (putOverResult.IsSuccess)
                        {
                            _logger.Info($"[{StationName}] Y 轴移动到待机位成功");
                            _currentStep = Station2PullingStep.判断带片;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.Y轴退回待机位失败, Station2PullingStep.移动到待机位, putOverResult.ErrorCode);
                        }
                        break;

                    case Station2PullingStep.判断带片:
                        CurrentStepDescription = "检查夹爪是否残留带片...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 执行防呆：判断夹爪是否粘连带片...");

                        // 确保退回安全位后，夹爪内无料
                        if (await _pullingModule.CheckGipperInsidePro(token))
                        {
                            _logger.Info($"[{StationName}] 判断通过，夹爪安全空置");
                            _currentStep = Station2PullingStep.发送退料完成;
                        }
                        else
                        {
                            RouteToError(Station2PullingStep.退回安全位后夹爪仍检测到带料, Station2PullingStep.判断带片);
                        }
                        break;

                    case Station2PullingStep.发送退料完成:
                        CurrentStepDescription = "发送退料完成信号...";
                       // await CheckPauseAsync(token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 释放退料完成信号，闭环结束本层动作");
                        _sync.Release(nameof(WorkstationSignals.工位2退料完成), StationName);

                        // 回归初始态，等待下一层的允许取料信号
                        _currentStep = Station2PullingStep.等待允许取料;
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 D：异常拦截与断点续跑处理
                    // ══════════════════════════════════════════════════════════
                    #region Phase D (Exceptions)

                    // ── 1. 致命数据异常 ──
                    case Station2PullingStep.配方参数为空:
                        _logger.Error($"[{StationName}] 工位2配方参数为空，无法继续。请确认配方已正确下发后复位。");
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.RecipeNull, "工位2配方参数为空");
                        _currentStep = _resumeStep;
                        break;

                    // ── 2. 物料防呆异常 ──
                    case Station2PullingStep.检测到叠料异常:
                    case Station2PullingStep.退回安全位后夹爪仍检测到带料:
                    case Station2PullingStep.轨道有物料阻止尺寸切换:
                    case Station2PullingStep.夹爪闭合后未检测到铁环:
                    case Station2PullingStep.待机位检测到残留物料:
                    case Station2PullingStep.卸料后夹爪物料粘连未脱落:
                        var matCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Pulling.StackedPiecesDetected;
                        TriggerAlarm(matCode, $"物料防呆异常: {_currentStep}");
                        _cachedErrorCode = null;
                        _currentStep = _resumeStep;
                        break;

                    // ── 3. 气缸与执行器异常 ──
                    case Station2PullingStep.关闭夹爪失败:
                    case Station2PullingStep.打开夹爪失败:
                    case Station2PullingStep.尺寸切换气缸IO操作失败:
                    case Station2PullingStep.尺寸切换气缸超时:
                    case Station2PullingStep.夹爪张开气缸操作失败:
                    case Station2PullingStep.夹爪张开超时:
                    case Station2PullingStep.夹爪闭合气缸操作失败:
                    case Station2PullingStep.夹爪闭合超时:
                        var actCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Pulling.GripperCloseFailed;
                        TriggerAlarm(actCode, $"执行器/气缸异常: {_currentStep}");
                        _cachedErrorCode = null;
                        _currentStep = _resumeStep;
                        break;

                    // ── 4. 基础定位与运动异常 ──
                    case Station2PullingStep.调整流道尺寸电机异常:
                    case Station2PullingStep.初始化拉料流程失败:
                    case Station2PullingStep.Y轴移动到取料位置失败:
                    case Station2PullingStep.Y轴退回待机位失败:
                    case Station2PullingStep.移动到待机位失败_无检测模式:
                    case Station2PullingStep.移动到取出安全位置失败:
                        var motCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Pulling.YAxisToPickupFailed;
                        TriggerAlarm(motCode, $"基础运动异常: {_currentStep}");
                        _cachedErrorCode = null;
                        _currentStep = _resumeStep;
                        break;

                    // ── 5. 核心拉送片防呆运动异常 ──
                    case Station2PullingStep.拉出至检测位失败_被中断:
                    case Station2PullingStep.推回至料盒失败_被中断:
                    case Station2PullingStep.拉出运动触发失败:
                    case Station2PullingStep.拉出过程卡料报警:
                    case Station2PullingStep.拉出过程丢料报警:
                    case Station2PullingStep.拉出运动超时:
                    case Station2PullingStep.送入运动触发失败:
                    case Station2PullingStep.送入过程卡料报警:
                    case Station2PullingStep.送入过程丢料报警:
                    case Station2PullingStep.送入运动超时:
                        var pullMotCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Pulling.PullOutToInspectionFailed;
                        TriggerAlarm(pullMotCode, $"核心拉送片异常: {_currentStep}");
                        _cachedErrorCode = null;
                        _currentStep = _resumeStep;
                        break;

                    // ── 6. 视觉系统异常 ──
                    case Station2PullingStep.扫码失败:
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.CodeScanFailed, "扫码失败或校验不合法");
                        _currentStep = _resumeStep;
                        break;

                    // ── 7. 兜底防护 ──
                    default:
                        if ((int)_currentStep >= 100000)
                        {
                            TriggerAlarm(AlarmCodes.System.UndefinedStep, $"遇到未定义的异常步序: {_currentStep}");
                            _currentStep = (int)_resumeStep != 0 ? _resumeStep : Station2PullingStep.等待允许取料;
                        }
                        else
                        {
                            TriggerAlarm(AlarmCodes.System.UndefinedStep, $"状态机指针漂移，未定义步序[{_currentStep}]");
                            _currentStep = Station2PullingStep.等待允许取料;
                        }
                        break;

                        #endregion
                }
            }
        }

        #endregion
    }
}