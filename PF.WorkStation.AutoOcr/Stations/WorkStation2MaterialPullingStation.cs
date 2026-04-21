using Prism.Ioc;
using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
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
    /// <summary>
    /// 【工位2】拉料工站业务流转控制器 (Material Pulling Station Controller - Station 2)
    ///
    /// <para>架构定位：</para>
    /// 继承自 <see cref="StationBase{T}"/>，作为拉料业务的独立状态机。
    /// 负责调度 <see cref="WorkStation2MaterialPullingModule"/> 执行具体的水平 Y 轴推拉动作与气爪控制。
    ///
    /// <para>跨工站协同：</para>
    /// 通过 <see cref="IStationSyncService"/> 与 <see cref="WorkStation2FeedingStation{T}"/> (上下料 Z 轴)
    /// 以及 <see cref="WorkStationDetectionModule"/> (OCR视觉) 进行信号握手，实现互不干涉的并发流转。
    /// </summary>
    [StationUI("工位2拉料工站", "WorkStation2MaterialPullingStationDebugView", order: 4)]
    public class WorkStation2MaterialPullingStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WorkStation2MaterialPullingModule? _pullingModule;
        private readonly WorkStationDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        /// <summary>
        /// 状态机当前执行的业务步序指针
        /// </summary>
        private Station2PullingStep _currentStep = Station2PullingStep.等待允许取料;

        /// <summary>
        /// 当前批次缓存的工位工艺配方
        /// </summary>
        private OCRRecipeParam? _cachedRecipe;

        #endregion

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

            /// <summary>获取配方失败</summary>
            获取配方失败 = 100001,
            /// <summary>调整流道尺寸失败</summary>
            调整流道尺寸失败 = 100002,
            /// <summary>移动到取料位失败</summary>
            移动到取料位失败 = 100003,
            /// <summary>关闭夹爪失败</summary>
            关闭夹爪失败 = 100004,
            /// <summary>检测到叠料异常</summary>
            检测到叠料异常 = 100005,
            /// <summary>移动到检测位失败</summary>
            移动到检测位失败 = 100006,
            /// <summary>送料到取料位失败</summary>
            送料到取料位失败 = 100007,
            /// <summary>打开夹爪失败</summary>
            打开夹爪失败 = 100008,
            /// <summary>移动到待机位失败</summary>
            移动到待机位失败 = 100009,
            /// <summary>判断带片异常</summary>
            判断带片异常 = 100010,

            #endregion
        }

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化工位2拉料工站
        /// </summary>
        public WorkStation2MaterialPullingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            : base(E_WorkStation.工位2拉料工站.ToString(), logger)
        {
            _pullingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation2MaterialPullingModule)) as WorkStation2MaterialPullingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;

            // 订阅底层模组的硬件报警并上抛
            if (_pullingModule != null)
            {
                _pullingModule.AlarmTriggered += _pullingModule_AlarmTriggered;
                _pullingModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();
            }
        }

        private void _pullingModule_AlarmTriggered(object? sender, MechanismAlarmEventArgs e)
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

                if (!await _pullingModule.MoveInitialNoScan(token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，Y轴移动到待机位异常。");
                    Fire(MachineTrigger.Error);
                    return;
                }

                _logger.Success($"[{StationName}] 初始化完成，就绪。");
                _pullingModule.ResumeHealthMonitoring();
                Fire(MachineTrigger.InitializeDone); // Initializing → Idle
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }
            this._currentStep = Station2PullingStep.等待允许取料;
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

                // ⚠️ 不重置 _currentStep！断点续跑的恢复节点已在 TriggerAlarm() 之前设定。

                _logger.Success($"[{StationName}] 复位完成，将从步序 [{_currentStep}] 继续执行。");
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
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待上下料工站的允许拉料信号...");

                        // 阻塞等待 Z 轴工站给出安全的取料口令
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2允许拉料), token, scope: nameof(E_WorkStation.工位2上下料工站)).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 检测到允许拉料信号，开始执行拉料流程...");
                        _currentStep = Station2PullingStep.获取当前配方;
                        break;

                    case Station2PullingStep.获取当前配方:
                        CurrentStepDescription = "获取当前配方...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 正在获取当前配方...");

                        _cachedRecipe = _dataModule.Station2ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 获取当前配方失败！");
                            _currentStep = Station2PullingStep.获取配方失败;
                            break;
                        }

                        _logger.Info($"[{StationName}] 获取当前配方成功：{_cachedRecipe.RecipeName}");
                        _currentStep = Station2PullingStep.判断流道尺寸;
                        break;

                    case Station2PullingStep.判断流道尺寸:
                        CurrentStepDescription = "判断流道尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

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
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.ChangeWafeSizeControl(_cachedRecipe.WafeSize, token))
                        {
                            // 调整完成后，退回判断节点二次确认防呆
                            this._currentStep = Station2PullingStep.判断流道尺寸;
                        }
                        else
                        {
                            this._currentStep = Station2PullingStep.调整流道尺寸失败;
                        }
                        break;

                    case Station2PullingStep.移动到取料位:
                        CurrentStepDescription = "移动到取料位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.InitialMoveFeeding(token))
                        {
                            _logger.Info($"[{StationName}] 运动到取料位成功");
                            _currentStep = Station2PullingStep.关闭夹爪;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.移动到取料位失败;
                        }
                        break;

                    case Station2PullingStep.关闭夹爪:
                        CurrentStepDescription = "关闭夹爪...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.CloseWafeGipper(token))
                        {
                            _logger.Info($"[{StationName}] 关闭夹爪成功");
                            _currentStep = Station2PullingStep.检测叠料;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.关闭夹爪失败;
                        }
                        break;

                    case Station2PullingStep.检测叠料:
                        CurrentStepDescription = "检测叠料...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.CheckStackedPieces(token))
                        {
                            _logger.Info($"[{StationName}] 判断叠料无异常");
                            _currentStep = Station2PullingStep.移动到检测位;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.检测到叠料异常;
                        }
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 B：检测与视觉交互 (拉出并交给 OCR 工站)
                    // ══════════════════════════════════════════════════════════
                    #region Phase B

                    case Station2PullingStep.移动到检测位:
                        CurrentStepDescription = "移动到检测位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        if (await _pullingModule.MoveDetection(token))
                        {
                            _logger.Info($"[{StationName}] 运动到检测位成功");

                            // 通知上下料工站：Y轴已经撤出安全区域，Z轴可以放心移动
                            _sync.Release(nameof(WorkstationSignals.工位2拉料完成), StationName);
                            _currentStep = Station2PullingStep.扫码识别;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.移动到检测位失败;
                        }
                        break;

                    case Station2PullingStep.扫码识别:
                        CurrentStepDescription = "扫码识别...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        List<string> coderec = await _pullingModule.CodeScanTigger(token);
                        _logger.Info($"[{StationName}] 扫码识别完成，识别结果：{(coderec != null ? string.Join(", ", coderec) : "未扫到码或校验不合法")}");

                        _currentStep = Station2PullingStep.允许检测位检测;
                        break;

                    case Station2PullingStep.允许检测位检测:
                        CurrentStepDescription = "通知 OCR 视觉执行检测...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 发送允许检测信号，交给视觉工站处理");
                        // 通知独立调度的 OCR 视觉龙门可以开拍
                        _sync.Release(nameof(WorkstationSignals.工位2允许检测), StationName);

                        _currentStep = Station2PullingStep.等待检测位检测完成;
                        break;

                    case Station2PullingStep.等待检测位检测完成:
                        CurrentStepDescription = "等待检测位检测完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

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
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 等待上下料工站 Z 轴安全避让信号...");
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2允许退料), token, scope: nameof(E_WorkStation.工位2上下料工站)).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 收到 Z 轴允许退料信号");
                        _currentStep = Station2PullingStep.送料到取料位;
                        break;

                    case Station2PullingStep.送料到取料位:
                        CurrentStepDescription = "正在退料回料盒...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] Y 轴送料回料盒...");

                        if (!await _pullingModule.OpenWafeGipper(token))
                        {
                            _currentStep = Station2PullingStep.送料到取料位失败;
                            break;
                        }

                        if (await _pullingModule.FeedingMaterialToBox(token))
                        {
                            _currentStep = Station2PullingStep.打开夹爪;
                            _logger.Info($"[{StationName}] 退料回料盒成功");
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.送料到取料位失败;
                        }
                        break;

                    case Station2PullingStep.打开夹爪:
                        CurrentStepDescription = "打开夹爪...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 正在松开夹爪...");

                        if (await _pullingModule.OpenWafeGipper(token))
                        {
                            _logger.Info($"[{StationName}] 松开夹爪成功");
                            _currentStep = Station2PullingStep.移动到待机位;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.打开夹爪失败;
                        }
                        break;

                    case Station2PullingStep.移动到待机位:
                        CurrentStepDescription = "移动到待机位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] Y 轴撤回待机避让位...");

                        if (await _pullingModule.PutOverMove(token))
                        {
                            _logger.Info($"[{StationName}] Y 轴移动到待机位成功");
                            _currentStep = Station2PullingStep.判断带片;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.移动到待机位失败;
                        }
                        break;

                    case Station2PullingStep.判断带片:
                        CurrentStepDescription = "检查夹爪是否残留带片...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 执行防呆：判断夹爪是否粘连带片...");

                        // 确保退回安全位后，夹爪内无料
                        if (await _pullingModule.CheckGipperInsidePro(token))
                        {
                            _logger.Info($"[{StationName}] 判断通过，夹爪安全空置");
                            _currentStep = Station2PullingStep.发送退料完成;
                        }
                        else
                        {
                            _currentStep = Station2PullingStep.判断带片异常;
                        }
                        break;

                    case Station2PullingStep.发送退料完成:
                        CurrentStepDescription = "发送退料完成信号...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

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

                    case Station2PullingStep.获取配方失败:
                        _logger.Error($"[{StationName}] 工位2配方参数为空，无法继续。请确认配方已正确下发后复位。");
                        _currentStep = Station2PullingStep.等待允许取料; // 致命数据异常，退回初始点重接
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.RecipeNull, "工位2配方参数为空");
                        break;

                    case Station2PullingStep.调整流道尺寸失败:
                        _logger.Error($"[{StationName}] 调整流道尺寸失败，当前配方尺寸要求：{_cachedRecipe?.WafeSize}");
                        _currentStep = Station2PullingStep.判断流道尺寸; // 偶发异常，原地重试
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.TrackSizeMotorFailed, $"调整流道尺寸失败，配方要求:{_cachedRecipe?.WafeSize}");
                        break;

                    case Station2PullingStep.移动到取料位失败:
                        _logger.Error($"[{StationName}] Y轴移动到取料位失败，请检查伺服是否报警或超时。");
                        _currentStep = Station2PullingStep.移动到取料位;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.YAxisToPickupFailed, "Y轴移动到取料位失败");
                        break;

                    case Station2PullingStep.关闭夹爪失败:
                        _logger.Error($"[{StationName}] 关闭夹爪失败，未感应到气缸闭合信号。");
                        _currentStep = Station2PullingStep.关闭夹爪;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.GripperCloseFailed, "关闭夹爪失败，未感应到闭合信号");
                        break;

                    case Station2PullingStep.检测到叠料异常:
                        _logger.Error($"[{StationName}] 检测到叠料！请人工干预检查料盒内物料状态。");
                        _currentStep = Station2PullingStep.检测叠料;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.StackedPiecesDetected, "检测到叠料异常");
                        break;

                    case Station2PullingStep.移动到检测位失败:
                        _logger.Error($"[{StationName}] 拉出至检测位失败，运动被中断。可能触发了【卡料】或【掉料】防呆！");
                        _currentStep = Station2PullingStep.移动到检测位;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.PullOutToInspectionFailed, "拉出至检测位失败，运动被中断");
                        break;

                    case Station2PullingStep.送料到取料位失败:
                        _logger.Error($"[{StationName}] 推回至料盒失败，运动被中断。可能触发了防呆拦截！");
                        _currentStep = Station2PullingStep.送料到取料位;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.PushBackToCassetteFailed, "推回至料盒失败，运动被中断");
                        break;

                    case Station2PullingStep.打开夹爪失败:
                        _logger.Error($"[{StationName}] 打开夹爪失败，请检查气缸与传感器信号。");
                        _currentStep = Station2PullingStep.打开夹爪;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.GripperOpenFailed, "打开夹爪失败");
                        break;

                    case Station2PullingStep.移动到待机位失败:
                        _logger.Error($"[{StationName}] Y 轴退回待机位失败，请检查伺服报警。");
                        _currentStep = Station2PullingStep.移动到待机位;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.YAxisRetractFailed, "Y轴退回待机位失败");
                        break;

                    case Station2PullingStep.判断带片异常:
                        _logger.Error($"[{StationName}] 异常：退回安全位后，夹爪仍检测到带料（未能成功留在料盒中）。请人工排查。");
                        _currentStep = Station2PullingStep.判断带片;
                        TriggerAlarm(AlarmCodesExtensions.WS2Pulling.WaferStuckInGripper, "退回安全位后夹爪仍检测到带料");
                        break;

                        #endregion
                }
            }
        }

        #endregion
    }
}
