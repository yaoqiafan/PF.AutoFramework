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
    /// 定义检测工站的完整生命周期与断点续跑异常状态节点
    /// </summary>
    public enum StationDetectionStep
    {
        #region 正常流程 (0 - 80)

        /// <summary>等待工位1或工位2允许检测</summary>
        等待工位1或工位2允许检测 = 0,
        /// <summary>去工位1检测位置</summary>
        去工位1检测位置 = 10,
        /// <summary>去工位2检测位置</summary>
        去工位2检测位置 = 20,
        /// <summary>触发检测</summary>
        触发检测 = 30,

        /// <summary>检测完成Z轴回安全位</summary>
        检测完成Z轴回安全位 = 40,

        /// <summary>数据比单</summary>
        数据比对 = 50,
        /// <summary>写入检测数据</summary>
        写入检测数据 = 60,
        /// <summary>检测完成后避位</summary>
        检测完成后避位 = 70,
        /// <summary>检测完成</summary>
        检测完成 = 80,

        #endregion

        #region 异常拦截与断点续跑节点 (100000+)

        // ── 业务与数据校验异常 (10000X) ──
        /// <summary>工位1配方为空，无法获取目标坐标</summary>
        工位1配方为空 = 100001,
        /// <summary>工位2配方为空，无法获取目标坐标</summary>
        工位2配方为空 = 100002,
        /// <summary>检测数据写入数据库或本地失败</summary>
        写入检测数据异常 = 100003,

        // ── 相机与视觉控制异常 (10001X) ──
        /// <summary>相机握手失败（光源或相机掉线）</summary>
        相机握手失败 = 100010,
        /// <summary>切换到工位1的OCR配方失败</summary>
        工位1相机配方切换失败 = 100011,
        /// <summary>切换到工位2的OCR配方失败</summary>
        工位2相机配方切换失败 = 100012,
        /// <summary>相机拍照触发失败或通讯异常</summary>
        触发检测异常 = 100013,

        // ── 龙门移动与定位异常 (10002X) ──
        /// <summary>龙门模组移动到检测位置失败</summary>
        去工位检测位置异常 = 100020,
        /// <summary>移动到工位1轴运动触发失败</summary>
        移动到工位1运动触发失败 = 100021,
        /// <summary>移动到工位1 XYZ轴运动超时</summary>
        移动到工位1运动超时 = 100022,
        /// <summary>移动到工位2轴运动触发失败</summary>
        移动到工位2运动触发失败 = 100023,
        /// <summary>移动到工位2 XYZ轴运动超时</summary>
        移动到工位2运动超时 = 100024,
        /// <summary>移动到待机位失败（Z轴或XY轴运动失败）</summary>
        移动到待机位失败 = 100025,
        /// <summary>Z轴安全位移动失败</summary>
        Z轴安全位移动失败 = 100026,
        /// <summary>相机Z轴无法抬起避位（紧急锁死防撞）</summary>
        检测完成Z轴回安全位异常 = 100027,

        // ── 系统与信号交互异常 (10009X) ──
        /// <summary>等待工位检测信号任务池异常中断</summary>
        等待检测信号异常中断 = 100090,
        /// <summary>状态机越界，进入未定义步序</summary>
        状态机进入未定义步序 = 100099

        #endregion
    }

    #endregion

    /// <summary>
    /// 【OCR视觉检测工站】业务流转控制器 (Detection Station Controller)
    /// 
    /// <para>架构定位：</para>
    /// 继承自 <see cref="StationBase{T, TStep}"/>。这是整个机台中唯一一个跨工位的**公共/共享工站**。
    /// 负责调度 <see cref="WSDetectionModule"/>，在工位1和工位2的拉料工站发出检测请求时，
    /// 驱动龙门机构前往对应工位拍照解码，并通过 <see cref="WSDataModule"/> 写入 MES 对比结果。
    /// </summary>
    [StationUI("OCR检测工站", "WorkStationDetectionStationDebugView", order: 5)]
    public class WSDetectionStation<T> : StationBase<T, StationDetectionStep> where T : StationMemoryBaseParam, new()
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WSDetectionModule? _detectionModule;
        private readonly WSDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        /// <summary>
        /// 记录当前正在服务（抢占成功）的工位标识
        /// </summary>
        private E_WorkSpace _currentworkSpace = E_WorkSpace.工位1;

        // ── 跨步序流转的缓存字段 ──
        private OCRRecipeParam? _cachedRecipe;

        /// <summary>
        /// 缓存相机执行结果。OcrText: OCR 解码文本串；ImagePath: 原图物理路径
        /// </summary>
        private (string OcrText, string ImagePath) _cachedOcrResult;

        private MachineDetectionData? _cachedDetectionData;

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化检测工站
        /// </summary>
        public WSDetectionStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            // 接入新架构，将生命周期步序枚举托付给基类管理
            : base(nameof(E_WorkStation.OCR检测工站), logger, StationDetectionStep.等待工位1或工位2允许检测)
        {
            _detectionModule = containerProvider.Resolve<IMechanism>(nameof(WSDetectionModule)) as WSDetectionModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WSDataModule)) as WSDataModule;
            _sync = sync;

            // 订阅底层模组报警，将其上抛至工站级报警流水线
            if (_detectionModule != null)
            {
                _detectionModule.AlarmTriggered += OnMechanismAlarm;
                _detectionModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();
            }
        }

        private void OnMechanismAlarm(object? sender, MechanismAlarmEventArgs e)
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
                    case StationDetectionStep.等待工位1或工位2允许检测:
                        _logger.Info($"[{StationName}] 恢复进入双工位信号抢占任务池...");
                        break;
                    default:
                        _logger.Info($"[{StationName}] 保持当前龙门动作节点: {_currentStep}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 执行断点续跑时发生异常: {ex.Message}");
                _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
            }
        }

        /// <summary>执行工站初始化</summary>
        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {
                _logger.Info($"[{StationName}] 正在初始化 OCR 视觉模组...");

                // 强制 Z 轴优先回零，保证 XYZ 联动安全
                if (!await _detectionModule.WaitHomeDoneAsync(_detectionModule.ZAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，Z轴回零失败。");
                    Fire(MachineTrigger.Error);
                    return;
                }

                if (!await _detectionModule.WaitHomeDoneAsync(_detectionModule.XAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，X轴回零失败。");
                    Fire(MachineTrigger.Error);
                    return;
                }

                if (!await _detectionModule.WaitHomeDoneAsync(_detectionModule.YAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，Y轴回零失败。");
                    Fire(MachineTrigger.Error);
                    return;
                }

                // 三轴回零后，驱动龙门回到最高最深处的绝对安全待机位
                var initMoveResult = await _detectionModule.MoveInitial(token);
                if (!initMoveResult.IsSuccess)
                {
                    _logger.Error($"[{StationName}] 初始化失败，模组移动至待机避让位失败。");
                    Fire(MachineTrigger.Error);
                }
                else
                {
                    _logger.Success($"[{StationName}] 初始化完成，就绪。");
                    _detectionModule.ResumeHealthMonitoring();
                    Fire(MachineTrigger.InitializeDone); // Initializing → Idle
                }
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }

            _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
            _resumeStep = StationDetectionStep.等待工位1或工位2允许检测;
            _sync.ResetScope(StationName);//初始化所有标志位
        }

        /// <summary>执行工站复位</summary>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑，将恢复至步序：[{_currentStep}]）...");

                if (_detectionModule != null)
                    await _detectionModule.ResetAsync(token);

                // 仅初始化报警复位时重置信号量；运行期报警复位保留信号量以支持断点续跑
                if (CameFromInitAlarm)
                    _sync.ResetScope(StationName);

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
                Fire(MachineTrigger.Error);
                throw;
            }
        }

        /// <summary>物理急停回调</summary>
        protected override async Task OnPhysicalStopAsync()
        {
            if (_detectionModule != null)
                await _detectionModule.StopAsync().ConfigureAwait(false);
        }

        /// <summary>获取关联模组列表</summary>
        protected override IEnumerable<PF.Infrastructure.Mechanisms.BaseMechanism> GetMechanisms()
        {
            if (_detectionModule != null) yield return _detectionModule;
        }

        /// <summary>空跑流程</summary>
        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Main State Machine Loop (主业务循环)

        /// <summary>正常生产主循环 - OCR检测工站</summary>
        /// <param name="token">取消令牌</param>
        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 【核心校验】每一轮步序流转前，强行校验取消状态，确保流程即时响应停止请求
                    token.ThrowIfCancellationRequested();

                    switch (_currentStep)
                    {
                        // ══════════════════════════════════════════════════════════
                        //  阶段 A：工位任务竞争 (共享资源的抢占调度)
                        // ══════════════════════════════════════════════════════════
                        #region Phase A (Task Competition)

                        case StationDetectionStep.等待工位1或工位2允许检测:
                            CurrentStepDescription = "等待工位1或工位2发起的检测请求...";
                            _logger.Info($"[{StationName}] 在公共资源池中等待工位1或工位2的检测信号...");

                            // 💡 优雅并发设计：创建一个独立的可取消源，用于随时结束未抢占到资源的那个工位的等待任务
                            using (var raceCts = CancellationTokenSource.CreateLinkedTokenSource(token))
                            {
                                // 创建两个独立的等待任务
                                var task1 = _sync.WaitAsync(nameof(WorkstationSignals.工位1允许检测), raceCts.Token, scope: nameof(E_WorkStation.工位1拉料工站));
                                var task2 = _sync.WaitAsync(nameof(WorkstationSignals.工位2允许检测), raceCts.Token, scope: nameof(E_WorkStation.工位2拉料工站));

                                // 谁先发出信号，谁就获得 OCR 相机龙门的使用权
                                var doneTask = await Task.WhenAny(task1, task2).ConfigureAwait(false);

                                // 如果是操作员按下了停止或急停，立即响应全局 token 抛出取消异常退出
                                token.ThrowIfCancellationRequested();

                                // 异常安全网：若竞争过程发生底层故障（无需局部 catch，判断任务状态即可）
                                if (doneTask.IsFaulted || doneTask.IsCanceled)
                                {
                                    _logger.Error($"[{StationName}] 等待任务池异常中断。错误信息: {doneTask.Exception?.InnerException?.Message}");
                                    RouteToError(StationDetectionStep.等待检测信号异常中断, StationDetectionStep.等待工位1或工位2允许检测);
                                    break;
                                }

                                // 任务正常结束：判定究竟是哪个工站赢得了竞争
                                if (doneTask == task1)
                                {
                                    raceCts.Cancel(); // 终止另一方的等待任务

                                    // 检测失败方是否吞没了信号量并补发，防止活锁
                                    await SafeReleaseLoserSignalAsync(task2,
                                        nameof(WorkstationSignals.工位2允许检测),
                                        nameof(E_WorkStation.工位2拉料工站)).ConfigureAwait(false);

                                    _currentworkSpace = E_WorkSpace.工位1;
                                    _logger.Info($"[{StationName}] 工位1 胜出，抢占视觉检测资源成功。");
                                    _currentStep = StationDetectionStep.去工位1检测位置;
                                }
                                else if (doneTask == task2)
                                {
                                    raceCts.Cancel();
                                    await SafeReleaseLoserSignalAsync(task1,
                                        nameof(WorkstationSignals.工位1允许检测),
                                        nameof(E_WorkStation.工位1拉料工站)).ConfigureAwait(false);

                                    _currentworkSpace = E_WorkSpace.工位2;
                                    _logger.Info($"[{StationName}] 工位2 胜出，抢占视觉检测资源成功。");
                                    _currentStep = StationDetectionStep.去工位2检测位置;
                                }
                            }
                            break;

                        #endregion

                        // ══════════════════════════════════════════════════════════
                        //  阶段 B：视觉运动、抓拍与数据校验
                        // ══════════════════════════════════════════════════════════
                        #region Phase B (Vision & Checking)

                        case StationDetectionStep.去工位1检测位置:
                            CurrentStepDescription = "OCR模组移动到工位1检测位置...";

                            _cachedRecipe = _dataModule?.Station1ReciepParam;
                            if (_cachedRecipe == null)
                            {
                                _logger.Error($"[{StationName}] 工位1配方为空，无法获取目标坐标，中断移动。");
                                RouteToError(StationDetectionStep.工位1配方为空, StationDetectionStep.等待工位1或工位2允许检测);
                                break;
                            }

                            var move1Result = await _detectionModule.MoveToStation1(token).ConfigureAwait(false);
                            if (move1Result.IsSuccess)
                            {
                                _logger.Info($"[{StationName}] OCR 龙门已就位工位1上空。");
                                _currentStep = StationDetectionStep.触发检测;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] OCR 模组移动到工位1失败: {move1Result.ErrorMessage}");
                                var errStep = move1Result.ErrorCode switch
                                {
                                    AlarmCodesExtensions.Detection.MoveToStation1MoveFailed => StationDetectionStep.移动到工位1运动触发失败,
                                    AlarmCodesExtensions.Detection.MoveToStation1MoveTimeout => StationDetectionStep.移动到工位1运动超时,
                                    AlarmCodesExtensions.Detection.MoveToStation1RecipeSwitchFailed => StationDetectionStep.工位1相机配方切换失败,
                                    _ => StationDetectionStep.去工位检测位置异常
                                };
                                RouteToError(errStep, StationDetectionStep.去工位1检测位置, move1Result.ErrorCode);
                            }
                            break;

                        case StationDetectionStep.去工位2检测位置:
                            CurrentStepDescription = "OCR模组移动到工位2检测位置...";

                            _cachedRecipe = _dataModule?.Station2ReciepParam;
                            if (_cachedRecipe == null)
                            {
                                _logger.Error($"[{StationName}] 工位2配方为空，无法获取目标坐标，中断移动。");
                                RouteToError(StationDetectionStep.工位2配方为空, StationDetectionStep.等待工位1或工位2允许检测);
                                break;
                            }

                            var move2Result = await _detectionModule.MoveToStation2(token).ConfigureAwait(false);
                            if (move2Result.IsSuccess)
                            {
                                _logger.Info($"[{StationName}] OCR 龙门已就位工位2上空。");
                                _currentStep = StationDetectionStep.触发检测;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] OCR 模组移动到工位2失败: {move2Result.ErrorMessage}");
                                var errStep = move2Result.ErrorCode switch
                                {
                                    AlarmCodesExtensions.Detection.MoveToStation2MoveFailed => StationDetectionStep.移动到工位2运动触发失败,
                                    AlarmCodesExtensions.Detection.MoveToStation2MoveTimeout => StationDetectionStep.移动到工位2运动超时,
                                    AlarmCodesExtensions.Detection.MoveToStation2RecipeSwitchFailed => StationDetectionStep.工位2相机配方切换失败,
                                    _ => StationDetectionStep.去工位检测位置异常
                                };
                                RouteToError(errStep, StationDetectionStep.去工位2检测位置, move2Result.ErrorCode);
                            }
                            break;

                        case StationDetectionStep.触发检测:
                            CurrentStepDescription = $"触发OCR相机拍照解码（对象：{_currentworkSpace}）...";

                            // 发送拍照指令，直接使用相机原始识别结果（裸跑 await，异常会上弹）
                            var camResult = await _detectionModule.CameraTigger(false, _currentworkSpace, token: token).ConfigureAwait(false);
                            if (!camResult.IsSuccess)
                            {
                                _logger.Error($"[{StationName}] OCR相机拍照失败: {camResult.ErrorMessage}");
                                RouteToError(StationDetectionStep.触发检测异常, StationDetectionStep.触发检测, camResult.ErrorCode);
                            }
                            else
                            {
                                _cachedOcrResult = camResult.Data;
                                _logger.Info($"[{StationName}] 拍照解码完成，读取原始条码串：[{_cachedOcrResult.OcrText}]。");
                                _currentStep = StationDetectionStep.检测完成Z轴回安全位;
                            }
                            break;

                        case StationDetectionStep.检测完成Z轴回安全位:
                            CurrentStepDescription = $"提升 Z 轴脱离干涉区（{_currentworkSpace}）...";

                            // 为了允许拉料工站尽早退料，必须先将视觉 Z 轴抬高，解除空间干涉
                            var zRetractResult = await _detectionModule.MoveZSafePos(token);
                            if (!zRetractResult.IsSuccess)
                            {
                                _logger.Error($"[{StationName}] 提升相机避位失败，禁止继续，防止撞机！");
                                RouteToError(StationDetectionStep.检测完成Z轴回安全位异常, StationDetectionStep.检测完成Z轴回安全位, zRetractResult.ErrorCode);
                            }
                            else
                            {
                                _currentStep = StationDetectionStep.数据比对;
                            }
                            break;

                        case StationDetectionStep.数据比对:
                            CurrentStepDescription = "OCR数据与MES工单数据交叉比对...";

                            // 请求数据中枢验证当前字符串是否在 MES 允许名单内
                            var kk = await _dataModule.CheckOcrTextAsync(_currentworkSpace, _cachedOcrResult.OcrText, token).ConfigureAwait(false);
                            string path = string.Empty;

                            // [图像存根]
                            if (!kk.IsSuccess)
                            {
                                // 验证 NG：生成缺陷存档图
                                path = await _detectionModule.SaveImage(_cachedOcrResult.ImagePath, _currentworkSpace, new WaferInfo() { CustomerBatch = "Error", WaferId = $"ERR_{DateTime.Now:HHmmss}" }, token);
                            }
                            else
                            {
                                // 验证 OK：按批次/槽位号正规存档
                                path = await _detectionModule.SaveImage(_cachedOcrResult.ImagePath, _currentworkSpace, kk.Data, token);
                            }

                            // 装配用于写入数据库与推给 MES 的单片检测快照实体
                            _cachedDetectionData = new MachineDetectionData()
                            {
                                CustomerBatch = kk.Data?.CustomerBatch ?? "ERROR",
                                WaferId = kk.Data?.WaferId ?? "ERROR",
                                InternalBatchId = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.InternalBatchId : _dataModule.Station2MesDetectionData.InternalBatchId,
                                Barcode1 = "CODE1",
                                Barcode2 = "CODE2",
                                Barcode3 = "CODE3",
                                IsMatch = kk.IsSuccess,
                                ErrorMessage = kk.IsSuccess ? "NONE" : "OCR结果与MES工单不匹配",
                                ProductModel = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.ProductModel : _dataModule.Station2MesDetectionData.ProductModel,
                                OperatorId = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.OperatorId : _dataModule.Station2MesDetectionData.OperatorId,
                                RecipeName = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.RecipeName : _dataModule.Station2MesDetectionData.RecipeName,
                                ImagePath = path
                            };

                            _currentStep = StationDetectionStep.写入检测数据;
                            break;

                        #endregion

                        // ══════════════════════════════════════════════════════════
                        //  阶段 C：结果入库与交接
                        // ══════════════════════════════════════════════════════════
                        #region Phase C (Archive & Handover)

                        case StationDetectionStep.写入检测数据:
                            CurrentStepDescription = "将检测结果写入持久化存储...";

                            if (_cachedDetectionData != null && _dataModule != null)
                            {
                                // 裸跑 DB 写入（异常会上弹）
                                await _dataModule.AddMachineDetectionAsync(_currentworkSpace, _cachedDetectionData).ConfigureAwait(false);
                                _logger.Info($"[{StationName}] 检测数据已推入中枢（{_currentworkSpace}），匹配结果：{_cachedDetectionData.IsMatch}。");
                            }
                            _currentStep = StationDetectionStep.检测完成;
                            break;

                        case StationDetectionStep.检测完成:
                            CurrentStepDescription = "业务闭环，释放通行证...";

                            // 释放本轮执行工位的放行信号，通知目标工位的拉料机构可以将晶圆退回料盒了
                            if (_currentworkSpace == E_WorkSpace.工位1)
                            {
                                _sync.Release(nameof(WorkstationSignals.工位1检测完成), StationName);
                                _logger.Success($"[{StationName}] 工位1 视觉鉴定流转闭环，通行信号已下发。");
                            }
                            else
                            {
                                _sync.Release(nameof(WorkstationSignals.工位2检测完成), StationName);
                                _logger.Success($"[{StationName}] 工位2 视觉鉴定流转闭环，通行信号已下发。");
                            }

                            // 清理缓存，准备下一次抢占
                            _cachedRecipe = null;
                            _cachedOcrResult = default;
                            _cachedDetectionData = null;

                            _currentStep = StationDetectionStep.检测完成后避位;
                            break;

                        case StationDetectionStep.检测完成后避位:
                            CurrentStepDescription = "龙门退回全局待机原点...";

                            // 确保整个大龙门机构退回最高最深处，不挡任何人的道
                            var retreatResult = await _detectionModule.MoveInitial(token);
                            if (!retreatResult.IsSuccess)
                            {
                                _logger.Error($"[{StationName}] 归位途中遭遇干涉或超时。");
                                var errStep = retreatResult.ErrorCode switch
                                {
                                    AlarmCodesExtensions.Detection.MoveZSafePosFailed => StationDetectionStep.Z轴安全位移动失败,
                                    _ => StationDetectionStep.移动到待机位失败
                                };
                                RouteToError(errStep, StationDetectionStep.检测完成后避位, retreatResult.ErrorCode);
                            }
                            else
                            {
                                // 完美收官，回到首个节点继续轮询等待
                                _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
                            }
                            break;

                        #endregion

                        // ══════════════════════════════════════════════════════════
                        //  阶段 D：异常拦截与断点续跑处理 (业务逻辑层面)
                        // ══════════════════════════════════════════════════════════
                        #region Phase D (Exceptions)

                        case StationDetectionStep.工位1配方为空:
                            TriggerAlarm(AlarmCodesExtensions.Detection.MoveToStation1RecipeNull, "工位1配方为空");
                            _currentStep = _resumeStep;
                            break;

                        case StationDetectionStep.工位2配方为空:
                            TriggerAlarm(AlarmCodesExtensions.Detection.MoveToStation2RecipeNull, "工位2配方为空");
                            _currentStep = _resumeStep;
                            break;

                        case StationDetectionStep.写入检测数据异常:
                            TriggerAlarm(AlarmCodesExtensions.Detection.DataWriteFailed, "检测数据写入失败");
                            _currentStep = _resumeStep;
                            break;

                        case StationDetectionStep.相机握手失败:
                        case StationDetectionStep.工位1相机配方切换失败:
                        case StationDetectionStep.工位2相机配方切换失败:
                        case StationDetectionStep.触发检测异常:
                            var camCode = _cachedErrorCode ?? AlarmCodesExtensions.Detection.CameraTiggerFailed;
                            TriggerAlarm(camCode, $"相机控制异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case StationDetectionStep.去工位检测位置异常:
                        case StationDetectionStep.移动到工位1运动触发失败:
                        case StationDetectionStep.移动到工位1运动超时:
                        case StationDetectionStep.移动到工位2运动触发失败:
                        case StationDetectionStep.移动到工位2运动超时:
                        case StationDetectionStep.移动到待机位失败:
                        case StationDetectionStep.Z轴安全位移动失败:
                            var motCode = _cachedErrorCode ?? AlarmCodesExtensions.Detection.GantryMoveFailed;
                            TriggerAlarm(motCode, $"龙门运动异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case StationDetectionStep.检测完成Z轴回安全位异常:
                            TriggerAlarm(AlarmCodesExtensions.Detection.ZAxisRetractAfterScan, "相机Z轴无法抬起避位");
                            _currentStep = _resumeStep;
                            break;

                        case StationDetectionStep.等待检测信号异常中断:
                            TriggerAlarm(AlarmCodesExtensions.Detection.SignalWaitFault, "等待工位检测信号任务池异常中断");
                            _currentStep = _resumeStep;
                            break;

                        default:
                            if ((int)_currentStep >= 100000)
                            {
                                TriggerAlarm(AlarmCodes.System.UndefinedStep, $"遇到未定义的业务步序: {_currentStep}");
                                _currentStep = (int)_resumeStep != 0 ? _resumeStep : StationDetectionStep.等待工位1或工位2允许检测;
                            }
                            else
                            {
                                TriggerAlarm(AlarmCodes.System.UndefinedStep, $"状态机指针漂移，步序[{_currentStep}]未定义");
                                _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
                            }
                            break;

                            #endregion
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.Warn($"[{StationName}] 视觉检测流程接收到取消请求。当前状态: {_currentStep}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Fatal($"[{StationName}] 视觉检测大循环崩溃！步序: {_currentStep} ({CurrentStepDescription}), 异常: {ex.Message}");
                throw;
            }
        }

        #endregion

        #region Signal Race Safety (信号竞争安全保护)

        /// <summary>
        /// 安全释放竞争失败方可能吞没的信号量。
        ///
        /// 场景：Task.WhenAny 竞争中，失败方可能在被取消前已经通过
        /// SemaphoreSlim.WaitAsync 消费了信号量，但其结果被 WhenAny 丢弃。
        /// 若不补发，该信号永久丢失，下一轮循环中对应工站的 WaitAsync 将永久阻塞 → 活锁。
        ///
        /// 本方法等待失败方任务完成落定，检测其是否成功消费了信号量（RanToCompletion），
        /// 若是则立即释放回等量的信号，恢复流水线正常节拍。
        /// </summary>
        /// <param name="loserTask">竞争失败的等待任务</param>
        /// <param name="signalName">该任务等待的信号量名称</param>
        /// <param name="scope">信号量所属 scope（工站）</param>
        private async Task SafeReleaseLoserSignalAsync(Task loserTask, string signalName, string scope)
        {
            // 等待失败方任务完成落定，确保能准确读取其最终状态
            try
            {
                await loserTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) { /* 预期取消，安全忽略 */ }
            catch { /* 其他异常也忽略，不影响胜出方的正常流程 */ }

            // RanToCompletion 说明失败方在被取消前已成功获取信号量
            // 该信号量已被消费但结果被 WhenAny 丢弃，必须补发防止活锁
            if (loserTask.Status == TaskStatus.RanToCompletion)
            {
                _sync.Release(signalName, scope);
                _logger.Warn($"[{StationName}] 检测到竞争失败方已消费信号 [{scope}/{signalName}]，已补发防止活锁。");
            }
        }

        #endregion
    }
}