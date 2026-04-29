using Prism.Ioc;
using PF.Core.Attributes;
using PF.Core.Models;
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
    #region State Machine Enums (业务步序枚举)

    /// <summary>
    /// 定义上下料工站的完整生命周期与断点续跑异常状态节点
    /// </summary>
    public enum Station2FeedingStep
    {
        #region 阶段 A：运动前准备 (0 - 100)

        /// <summary>等待按下工位2启动按钮</summary>
        等待按下工位2启动按钮 = 0,
        /// <summary>验证当前批次产品个数</summary>
        验证当前批次产品个数 = 10,
        /// <summary>获取工位2配方参数</summary>
        获取工位2配方参数 = 20,
        /// <summary>识别料盒尺寸</summary>
        识别料盒尺寸 = 30,
        /// <summary>验证尺寸与配方是否匹配</summary>
        验证尺寸与配方是否匹配 = 40,
        /// <summary>切换物料尺寸</summary>
        切换物料尺寸 = 50,
        /// <summary>判断X轴是否具备运动条件_开始</summary>
        判断X轴是否具备运动条件_开始 = 60,
        /// <summary>X轴到待机位</summary>
        X轴到待机位 = 70,
        /// <summary>判断Z轴是否具备运动条件_寻层</summary>
        判断Z轴是否具备运动条件_寻层 = 80,
        /// <summary>Z轴扫描寻层</summary>
        Z轴扫描寻层 = 90,
        /// <summary>算法过滤层数</summary>
        算法过滤层数 = 100,

        #endregion

        #region 阶段 B：取料循环流转 (110 - 160)

        /// <summary>判断Z轴是否具备运动条件_取料定位</summary>
        判断Z轴是否具备运动条件_取料定位 = 110,
        /// <summary>切换到指定层</summary>
        切换到指定层 = 120,
        /// <summary>判断物料可拉出条件</summary>
        判断物料可拉出条件 = 130,
        /// <summary>等待物料拉出完成</summary>
        等待物料拉出完成 = 140,
        /// <summary>阻塞等待物料回退完成</summary>
        阻塞等待物料回退完成 = 150,
        /// <summary>计算下一层位置</summary>
        计算下一层位置 = 160,

        #endregion

        #region 阶段 C：生产结束与安全收尾 (200 - 400)

        /// <summary>物料全部生产完毕</summary>
        物料全部生产完毕 = 200,
        /// <summary>判断X轴是否具备运动条件_结束</summary>
        判断X轴是否具备运动条件_结束 = 210,
        /// <summary>X轴到挡料位</summary>
        X轴到挡料位 = 220,
        /// <summary>判断Z轴是否具备运动条件_流程结束</summary>
        判断Z轴是否具备运动条件_流程结束 = 230,
        /// <summary>Z轴到待机位</summary>
        Z轴到待机位 = 240,
        /// <summary>通知操作员下料</summary>
        通知操作员下料 = 300,
        /// <summary>生产完毕</summary>
        生产完毕 = 400,

        #endregion

        #region 阶段 D：异常拦截与断点续跑节点 (100000+)

        // ── 业务与数据校验异常 (10000X) ──
        /// <summary>批次产品个数为0，无法启动生产</summary>
        批次产品个数不正确 = 100001,
        /// <summary>料盒尺寸与配方不匹配</summary>
        料盒尺寸与配方不匹配 = 100002,
        /// <summary>工位2配方获取失败</summary>
        工位2配方获取失败 = 100003,

        // ── 传感器与硬件状态异常 (10001X) ──
        /// <summary>料盒尺寸识别失败（传感器信号异常）</summary>
        料盒尺寸识别失败 = 100010,
        /// <summary>料盒公用底座未检测到物体</summary>
        料盒公用底座未检测到物体 = 100011,
        /// <summary>8寸晶圆放反</summary>
        八寸晶圆放反 = 100012,
        /// <summary>12寸晶圆放反</summary>
        十二寸晶圆放反 = 100013,
        /// <summary>料盒尺寸传感器信号冲突（8寸/12寸同时触发或均未触发）</summary>
        料盒尺寸传感器信号冲突 = 100014,
        /// <summary>寻层扫描硬件锁存配置失败</summary>
        寻层扫描硬件锁存配置失败 = 100015,

        // ── 运动互锁与条件检测异常 (10002X) ──
        /// <summary>Z轴运动条件不满足</summary>
        Z轴运动条件不满足 = 100020,
        /// <summary>X轴运动条件不满足</summary>
        X轴运动条件不满足 = 100021,
        /// <summary>Z轴互锁失败：料盒未到位禁止升降</summary>
        Z轴互锁失败_料盒未到位 = 100022,
        /// <summary>X轴互锁失败：存在铁环突片</summary>
        X轴互锁失败_存在铁环突片 = 100023,
        /// <summary>拉料互锁失败：晶圆盒挡杆未打开</summary>
        拉料互锁失败_挡杆未打开 = 100024,

        // ── 运动执行与超时异常 (10003X) ──
        /// <summary>Z轴运动超时</summary>
        Z轴运动超时 = 100030,
        /// <summary>X轴运动超时</summary>
        X轴运动超时 = 100031,
        /// <summary>初始化上料状态失败（Z/X轴运动到待机位失败）</summary>
        初始化上料状态失败 = 100032,
        /// <summary>Z轴切换层运动失败</summary>
        Z轴切换层运动失败 = 100033,
        /// <summary>寻层扫描移动到起点失败</summary>
        寻层扫描移动到起点失败 = 100034,
        /// <summary>寻层扫描移动到终点失败</summary>
        寻层扫描移动到终点失败 = 100035,

        // ── 流程特定检测与算法异常 (10004X) ──
        /// <summary>Z轴寻层扫描异常（结果为空或过程出错）</summary>
        Z轴寻层扫描异常 = 100040,
        /// <summary>寻层算法空值判定（判定为0层）</summary>
        寻层算法空值判定 = 100041,
        /// <summary>寻层算法出现严重异常</summary>
        寻层算法过滤异常 = 100042,
        /// <summary>目标层数超出有效范围</summary>
        目标层数超出有效范围 = 100043,
        /// <summary>未找到目标层的阵列点位</summary>
        未找到目标层的阵列点位 = 100044,
        /// <summary>寻层算法理论层坐标未初始化</summary>
        寻层算法理论层坐标未初始化 = 100045,
        /// <summary>寻层算法传感器原始数据不足</summary>
        寻层算法传感器原始数据不足 = 100046,
        /// <summary>寻层算法双传感器识别数量差异过大</summary>
        寻层算法双传感器识别数量差异过大 = 100047,

        // ── 物料姿态具体异常 (10005X) ──
        /// <summary>物料错层翘起，禁止拉料</summary>
        检测到物料错层 = 100050,
        /// <summary>寻层算法检测到严重斜片(Cross-slot)</summary>
        寻层算法检测到严重斜片 = 100051,
        /// <summary>寻层算法检测到重叠片(Double-wafer)</summary>
        寻层算法检测到重叠片 = 100052,
        /// <summary>寻层算法晶圆严重偏离标准槽位（可能未插到底）</summary>
        寻层算法晶圆偏离标准槽位 = 100053,

        // ── 系统级异常 (10009X) ──
        /// <summary>状态机指针漂移，进入未定义步序</summary>
        状态机进入未定义步序 = 100099

        #endregion
    }

    #endregion

    /// <summary>
    /// 【工位2】上下料工站业务流转控制器 (Feeding Station Controller - Station 2)
    ///
    /// <para>架构定位：</para>
    /// 作为工位2的主业务状态机，继承自 <see cref="StationBase{T, TStep}"/>。负责统筹调度底层 <see cref="WS2FeedingModule"/>（硬件机构）
    /// 与 <see cref="WSDataModule"/>（数据中枢），并通过 <see cref="IStationSyncService"/> 与拉料工站、检测工站进行跨工站握手协作。
    /// </summary>
    [StationUI("工位2上下料工站", "WorkStation2FeedingStationDebugView", order: 3)]
    public class WS2FeedingStation<T> : StationBase<T, Station2FeedingStep> where T : StationMemoryBaseParam, new()
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WS2FeedingModule? _feedingModule;
        private readonly WSDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        // ── 跨步序流转的缓存字段 ──
        private OCRRecipeParam? _cachedRecipe;
        private E_WafeSize _detectedWaferSize;
        private Dictionary<int, List<double>> _rawMappingData = [];
        private int _totalLayerCount;
        private List<int> _layersToProcess = [];
        private int _currentLayerIndex;
        

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化工位2上下料工站
        /// </summary>
        public WS2FeedingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            // 接入带枚举泛型的基类，统一管理 _currentStep 和 _resumeStep
            : base(E_WorkStation.工位2上下料工站.ToString(), logger, Station2FeedingStep.等待按下工位2启动按钮)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WS2FeedingModule)) as WS2FeedingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WSDataModule)) as WSDataModule;
            _sync = sync;

            // 订阅底层模组报警，将其上抛至工站级报警流水线
            if (_feedingModule != null)
            {
                _feedingModule.AlarmTriggered += OnMechanismAlarm;
                _feedingModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();
            }

            if (_dataModule != null)
            {
                _dataModule.AlarmTriggered += OnMechanismAlarm;
                _dataModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();
            }
        }

        private void OnMechanismAlarm(object? sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到底层模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
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
            _logger.Info($"[{StationName}] 开始执行断点续跑，当前恢复步序: {_currentStep}");
            try
            {
                switch (_currentStep)
                {
                    case Station2FeedingStep.等待物料拉出完成:
                    case Station2FeedingStep.阻塞等待物料回退完成:
                    case Station2FeedingStep.计算下一层位置:
                        _logger.Info($"[{StationName}] 恢复取料流程，当前处理层: {_currentLayerIndex + 1}/{_totalLayerCount}");
                        await SyncPullingStationStateAsync(token);
                        break;

                    case Station2FeedingStep.Z轴扫描寻层:
                    case Station2FeedingStep.算法过滤层数:
                        _logger.Info($"[{StationName}] 重新初始化寻层状态");
                        _rawMappingData = [];
                        _layersToProcess = [];
                        _currentLayerIndex = 0;
                      
                        break;

                    default:
                        _logger.Info($"[{StationName}] 保持当前步序: {_currentStep}");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 执行断点续跑时发生异常: {ex.Message}");
                _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
            }
        }

        private async Task SyncPullingStationStateAsync(CancellationToken token)
        {
            try
            {
                _sync.Release(nameof(WorkstationSignals.工位2允许拉料), StationName);
                if (_currentStep == Station2FeedingStep.等待物料拉出完成)
                {
                    _logger.Info($"[{StationName}] 等待工位2拉料工站完成信号...");
                    await _sync.WaitAsync(nameof(WorkstationSignals.工位2拉料完成), token, scope: E_WorkStation.工位2拉料工站.ToString()).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 同步拉料工站状态失败: {ex.Message}");
            }
        }

        /// <summary>执行工站初始化</summary>
        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {
                _logger.Info($"[{StationName}] 正在初始化上下料模组...");
                var initAttempts = 0;
                const int maxInitAttempts = 3;

                await _sync.WaitAsync(nameof(WorkstationSignals.工位2拉料复位完成), token: token, "复位");

                while (initAttempts < maxInitAttempts)
                {
                    try
                    {
                        initAttempts++;
                        if (!await _feedingModule.InitializeAsync(token))
                            throw new Exception($"[{StationName}] 上下料模组初始化通信失败！");

                        if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.ZAxis, token: token))
                            throw new Exception("Z轴回零失败");

                        if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.XAxis, token: token))
                            throw new Exception("X轴回零失败");

                        var initResult = await _feedingModule.InitializeFeedingStateAsync(token: token);
                        if (initResult.IsSuccess)
                        {
                            _logger.Success($"[{StationName}] 初始化完成，机构已退回安全位就绪。");
                            _feedingModule.ResumeHealthMonitoring();
                            Fire(MachineTrigger.InitializeDone);
                            break;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 初始化失败：{initResult.ErrorMessage}");
                            if (initAttempts == maxInitAttempts)
                            {
                                TriggerAlarm(initResult.ErrorCode, initResult.ErrorMessage);
                                Fire(MachineTrigger.Error);
                            }
                            else await Task.Delay(1000, token);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{StationName}] 初始化异常: {ex.Message}");
                        if (initAttempts == maxInitAttempts)
                        {
                            Fire(MachineTrigger.Error);
                            throw;
                        }
                        else await Task.Delay(1000, token);
                    }
                }

                var restoreStep = (Station2FeedingStep)MemoryParam.PersistedStep;
                if (!Enum.IsDefined(restoreStep) || (int)restoreStep >= 100000)
                    restoreStep = Station2FeedingStep.等待按下工位2启动按钮;

                _currentStep = restoreStep;
                _resumeStep = restoreStep;

                _sync.ResetScope(StationName);//初始化所有标志位
            }
            catch
            {
                Fire(MachineTrigger.Error);
                throw;
            }
        }

        /// <summary>执行工站复位</summary>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑机制，将恢复至步序：[{_currentStep}]）...");
                var resetAttempts = 0;
                const int maxResetAttempts = 3;

                while (resetAttempts < maxResetAttempts)
                {
                    try
                    {
                        resetAttempts++;
                        if (_feedingModule != null && !await _feedingModule.ResetAsync(token))
                            throw new Exception("硬件模组复位失败");
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.Error($"[{StationName}] 模组复位失败 ({resetAttempts}/{maxResetAttempts}): {ex.Message}");
                        if (resetAttempts == maxResetAttempts) break;
                        await Task.Delay(1000, token);
                    }
                }

                if (CameFromInitAlarm)
                {
                    _logger.Info($"[{StationName}] 重置跨工站信号量...");
                    _sync.ResetScope(StationName);
                }

                _logger.Success($"[{StationName}] 复位完成，将从步序 [{_currentStep}] 继续执行。");

                if (!CameFromInitAlarm)
                {
                    await ExecuteResumeFromBreakpointAsync(token);
                }

                await FireAsync(ResetCompletionTrigger);
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
            if (_feedingModule != null)
                await _feedingModule.StopAsync().ConfigureAwait(false);
        }

        /// <summary>获取关联模组列表</summary>
        protected override IEnumerable<PF.Infrastructure.Mechanisms.BaseMechanism> GetMechanisms()
        {
            if (_feedingModule != null) yield return _feedingModule;
        }

        /// <summary>空跑流程</summary>
        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Main State Machine Loop (主业务循环)

        /// <summary>
        /// 正常模式业务循环 - 工位2上料流程
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 【核心校验】每一轮步序开始前，强行校验取消状态，确保流程即时停止
                    token.ThrowIfCancellationRequested();

                    switch (_currentStep)
                    {
                        #region Phase A：运动前准备 (200 - 300)

                        case Station2FeedingStep.等待按下工位2启动按钮:
                            CurrentStepDescription = "等待按下工位2启动按钮...";
                            _logger.Info($"[{StationName}] 等待操作员按下工位2启动按钮...");

                            // 阻塞等待同步信号：工位2启动
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位2启动按钮按下), token).ConfigureAwait(false);

                            _logger.Info($"[{StationName}] 检测到启动信号，开始执行上料流程。");
                            _currentStep = Station2FeedingStep.验证当前批次产品个数;
                            break;

                        case Station2FeedingStep.验证当前批次产品个数:
                            CurrentStepDescription = "验证当前批次产品个数...";
                            // 业务逻辑：校验MES或本地数据中的批次数量
                            if (_dataModule.Station2MesDetectionData.Quantity != 0)
                            {
                                _logger.Info($"[{StationName}] 批次产品个数验证通过：{_dataModule.Station2MesDetectionData.Quantity}。");
                                _currentStep = Station2FeedingStep.获取工位2配方参数;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 批次产品个数为0，无法启动生产。");
                                RouteToError(Station2FeedingStep.批次产品个数不正确, Station2FeedingStep.等待按下工位2启动按钮);
                            }
                            break;

                        case Station2FeedingStep.获取工位2配方参数:
                            CurrentStepDescription = "获取工位2配方参数...";
                            _cachedRecipe = _dataModule.Station2ReciepParam;
                            if (_cachedRecipe != null)
                            {
                                _logger.Info($"[{StationName}] 配方获取成功：[{_cachedRecipe.RecipeName}] 尺寸：{_cachedRecipe.WafeSize}。");
                                _currentStep = Station2FeedingStep.识别料盒尺寸;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 工位2配方参数为空，请确认配方是否已下发。");
                                RouteToError(Station2FeedingStep.工位2配方获取失败, Station2FeedingStep.等待按下工位2启动按钮);
                            }
                            break;

                        case Station2FeedingStep.识别料盒尺寸:
                            CurrentStepDescription = "识别料盒尺寸...";
                            // 调用底层模块：通过传感器组合识别当前料盒规格
                            var sizeResult = await _feedingModule.GetWaferBoxSizeAsync(token).ConfigureAwait(false);
                            if (sizeResult.IsSuccess)
                            {
                                _detectedWaferSize = sizeResult.Data;
                                _logger.Info($"[{StationName}] 料盒尺寸识别成功：{_detectedWaferSize}。");
                                _currentStep = Station2FeedingStep.验证尺寸与配方是否匹配;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 料盒尺寸识别失败：{sizeResult.ErrorMessage}");
                                var errStep = sizeResult.ErrorCode switch
                                {
                                    AlarmCodesExtensions.WS2Feeding.BoxSizeConflict => Station2FeedingStep.料盒尺寸传感器信号冲突,
                                    AlarmCodesExtensions.WS2Feeding.BoxBaseNotDetected => Station2FeedingStep.料盒公用底座未检测到物体,
                                    AlarmCodesExtensions.WS2Feeding.Wafer8InchReversed => Station2FeedingStep.八寸晶圆放反,
                                    AlarmCodesExtensions.WS2Feeding.Wafer12InchReversed => Station2FeedingStep.十二寸晶圆放反,
                                    _ => Station2FeedingStep.料盒尺寸识别失败
                                };
                                RouteToError(errStep, Station2FeedingStep.识别料盒尺寸, sizeResult.ErrorCode);
                            }
                            break;

                        case Station2FeedingStep.验证尺寸与配方是否匹配:
                            CurrentStepDescription = "验证料盒尺寸与配方是否匹配...";
                            if (_detectedWaferSize == _cachedRecipe.WafeSize)
                            {
                                _logger.Info($"[{StationName}] 料盒尺寸与配方匹配（{_detectedWaferSize}）。");
                                _currentStep = Station2FeedingStep.切换物料尺寸;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 尺寸不匹配：实际={_detectedWaferSize}，配方={_cachedRecipe.WafeSize}。");
                                RouteToError(Station2FeedingStep.料盒尺寸与配方不匹配, Station2FeedingStep.等待按下工位2启动按钮);
                            }
                            break;

                        case Station2FeedingStep.切换物料尺寸:
                            CurrentStepDescription = "切换物料尺寸...";
                            // 执行气缸或调宽电机动作，调整生产线尺寸
                            var switchResult = await _feedingModule.SwitchProductionStateAsync(_cachedRecipe.WafeSize, token).ConfigureAwait(false);
                            if (switchResult.IsSuccess)
                            {
                                _currentStep = Station2FeedingStep.判断X轴是否具备运动条件_开始;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 切换物料尺寸失败：{switchResult.ErrorMessage}");
                                RouteToError(Station2FeedingStep.料盒尺寸与配方不匹配, Station2FeedingStep.等待按下工位2启动按钮, switchResult.ErrorCode);
                            }
                            break;

                        case Station2FeedingStep.判断X轴是否具备运动条件_开始:
                            CurrentStepDescription = "检查X轴运动条件（开始）...";
                            // 互锁检查：检查拉料杆状态、挡片传感器等
                            var canMoveXResult = await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false);
                            if (canMoveXResult.IsSuccess)
                            {
                                _currentStep = Station2FeedingStep.X轴到待机位;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] X轴运动条件不满足：{canMoveXResult.ErrorMessage}");
                                var errStep = canMoveXResult.ErrorCode switch
                                {
                                    AlarmCodesExtensions.WS2Feeding.XAxisTabDetected => Station2FeedingStep.X轴互锁失败_存在铁环突片,
                                    AlarmCodesExtensions.WS2Feeding.PullOutLeverNotOpen => Station2FeedingStep.拉料互锁失败_挡杆未打开,
                                    _ => Station2FeedingStep.X轴运动条件不满足
                                };
                                RouteToError(errStep, Station2FeedingStep.判断X轴是否具备运动条件_开始, canMoveXResult.ErrorCode);
                            }
                            break;

                        case Station2FeedingStep.X轴到待机位:
                            CurrentStepDescription = "X轴移动到待机位...";
                            if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, nameof(WS2FeedingModule.XAxisPoint.待机位), token: token).ConfigureAwait(false))
                            {
                                _logger.Info($"[{StationName}] X轴已到达待机位。");
                                _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_寻层;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] X轴移动到待机位失败（超时）。");
                                RouteToError(Station2FeedingStep.X轴运动超时, Station2FeedingStep.X轴到待机位);
                            }
                            break;

                        case Station2FeedingStep.判断Z轴是否具备运动条件_寻层:
                            CurrentStepDescription = "检查Z轴运动条件（寻层）...";
                            if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                            {
                                _currentStep = Station2FeedingStep.Z轴扫描寻层;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] Z轴条件不满足（寻层阶段）。");
                                RouteToError(Station2FeedingStep.Z轴运动条件不满足, Station2FeedingStep.判断Z轴是否具备运动条件_寻层);
                            }
                            break;

                        case Station2FeedingStep.Z轴扫描寻层:
                            CurrentStepDescription = "Z轴扫描寻层...";
                            // 调用光纤传感器或Mapping模组进行扫层
                            var scanResult = await _feedingModule.SearchLayerAsync(latchNo1: 2,latchNo2:3,token: token).ConfigureAwait(false);
                            if (scanResult.IsSuccess && scanResult.Data.Count > 0)
                            {
                                _rawMappingData = scanResult.Data;
                                _currentStep = Station2FeedingStep.算法过滤层数;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 寻层异常：{(scanResult.IsSuccess ? "结果为0层" : scanResult.ErrorMessage)}");
                               
                                RouteToError(Station2FeedingStep.Z轴寻层扫描异常, Station2FeedingStep.判断Z轴是否具备运动条件_寻层);
                            }
                            break;

                        case Station2FeedingStep.算法过滤层数:
                            CurrentStepDescription = "算法过滤与防呆验证...";
                            // 调用数据过滤算法，剔除无效信号或误触发层
                            var filterResult = await _feedingModule.AnalyzeAndFilterMappingData(_rawMappingData);
                            if (filterResult.IsSuccess)
                            {
                                _layersToProcess = [.. filterResult.Data.Keys.OrderBy(layerIndex => layerIndex)];
                                _totalLayerCount = _layersToProcess.Count;
                                _currentLayerIndex = 0;

                                if (_layersToProcess.Count == 0)
                                {
                                    _logger.Warn($"[{StationName}] 过滤结果为空，料盒内未检测到有效晶圆！");
                                    RouteToError(Station2FeedingStep.寻层算法空值判定, Station2FeedingStep.等待按下工位2启动按钮);
                                }
                                else
                                {
                                    _logger.Info($"[{StationName}] 过滤完成，共识别 {_totalLayerCount} 片。");
                                    _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_取料定位;
                                }
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 寻层算法过滤异常: {filterResult.ErrorMessage}");
                                RouteToError(Station2FeedingStep.寻层算法过滤异常, Station2FeedingStep.等待按下工位2启动按钮);
                            }
                            break;

                        #endregion

                        #region Phase B：取料循环流转 (310 - 360)

                        case Station2FeedingStep.判断Z轴是否具备运动条件_取料定位:
                            CurrentStepDescription = $"检查Z轴运动条件（第{_currentLayerIndex + 1}层）...";
                            if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                            {
                                _currentStep = Station2FeedingStep.切换到指定层;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] Z轴条件不满足（取料定位）。");
                                RouteToError(Station2FeedingStep.Z轴运动条件不满足, Station2FeedingStep.判断Z轴是否具备运动条件_取料定位);
                            }
                            break;

                        case Station2FeedingStep.切换到指定层:
                            CurrentStepDescription = $"Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层...";
                            if (await _feedingModule.SwitchToLayerAsync(_layersToProcess[_currentLayerIndex], token).ConfigureAwait(false))
                            {
                                MemoryParam.PersistedStep = (int)Station2FeedingStep.判断物料可拉出条件;
                                FlushMemory();
                                _currentStep = Station2FeedingStep.判断物料可拉出条件;
                            }
                            else
                            {
                                RouteToError(Station2FeedingStep.Z轴运动超时, Station2FeedingStep.切换到指定层);
                            }
                            break;

                        case Station2FeedingStep.判断物料可拉出条件:
                            CurrentStepDescription = "判断物料可拉出条件...";
                            // 检查物料是否倾斜或错层
                            if (await _feedingModule.CanPullOutMaterialAsync(token).ConfigureAwait(false))
                            {
                                // 释放同步信号：通知拉料工站可以拉料
                                _sync.Release(nameof(WorkstationSignals.工位2允许拉料), StationName);
                                _currentStep = Station2FeedingStep.等待物料拉出完成;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料错层翘起，禁止拉料。");
                                RouteToError(Station2FeedingStep.检测到物料错层, Station2FeedingStep.判断Z轴是否具备运动条件_取料定位);
                            }
                            break;

                        case Station2FeedingStep.等待物料拉出完成:
                            CurrentStepDescription = "等待物料拉出完成...";
                            // 阻塞等待拉料工站完成动作
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位2拉料完成), token, scope: E_WorkStation.工位2拉料工站.ToString()).ConfigureAwait(false);

                            _currentStep = Station2FeedingStep.阻塞等待物料回退完成;
                            // 释放退料允许信号
                            _sync.Release(nameof(WorkstationSignals.工位2允许退料), StationName);
                            break;

                        case Station2FeedingStep.阻塞等待物料回退完成:
                            CurrentStepDescription = "阻塞等待物料回退完成...";
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位2退料完成), token, scope: E_WorkStation.工位2拉料工站.ToString()).ConfigureAwait(false);

                            MemoryParam.PersistedStep = (int)Station2FeedingStep.计算下一层位置;
                            FlushMemory();
                            _currentStep = Station2FeedingStep.计算下一层位置;
                            break;

                        case Station2FeedingStep.计算下一层位置:
                            CurrentStepDescription = "计算下一层位置...";
                            _currentLayerIndex++;
                            if (_currentLayerIndex >= _layersToProcess.Count)
                            {
                                _logger.Info($"[{StationName}] 所有层取料完毕。");
                                _currentStep = Station2FeedingStep.物料全部生产完毕;
                            }
                            else
                            {
                                _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_取料定位;
                            }
                            break;

                        #endregion

                        #region Phase C：收尾与状态复位 (400 - 500)

                        case Station2FeedingStep.物料全部生产完毕:
                            CurrentStepDescription = "物料全部生产完毕，更新状态...";
                            _logger.Success($"[{StationName}] 晶圆上料闭环完毕。");
                            _currentStep = Station2FeedingStep.判断X轴是否具备运动条件_结束;
                            break;

                        case Station2FeedingStep.判断X轴是否具备运动条件_结束:
                            CurrentStepDescription = "检查X轴运动条件（结束）...";
                            if (await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false))
                                _currentStep = Station2FeedingStep.X轴到挡料位;
                            else
                                RouteToError(Station2FeedingStep.X轴运动条件不满足, Station2FeedingStep.判断X轴是否具备运动条件_结束);
                            break;

                        case Station2FeedingStep.X轴到挡料位:
                            CurrentStepDescription = "X轴移动到挡料位...";
                            if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, nameof(WS2FeedingModule.XAxisPoint.挡料位), token: token).ConfigureAwait(false))
                                _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_流程结束;
                            else
                                RouteToError(Station2FeedingStep.X轴运动超时, Station2FeedingStep.X轴到挡料位);
                            break;

                        case Station2FeedingStep.判断Z轴是否具备运动条件_流程结束:
                            CurrentStepDescription = "检查Z轴运动条件（流程结束）...";
                            if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                                _currentStep = Station2FeedingStep.Z轴到待机位;
                            else
                                RouteToError(Station2FeedingStep.Z轴运动条件不满足, Station2FeedingStep.判断Z轴是否具备运动条件_流程结束);
                            break;

                        case Station2FeedingStep.Z轴到待机位:
                            CurrentStepDescription = "Z轴退回待机位...";
                            if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.ZAxis, nameof(WS2FeedingModule.ZAxisPoint.待机位), token: token).ConfigureAwait(false))
                                _currentStep = Station2FeedingStep.通知操作员下料;
                            else
                                RouteToError(Station2FeedingStep.Z轴运动超时, Station2FeedingStep.Z轴到待机位);
                            break;

                        case Station2FeedingStep.通知操作员下料:
                            CurrentStepDescription = "通知操作员下料，等待确认...";
                            // 人工交互同步
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位2人工下料完成), token).ConfigureAwait(false);
                            _currentStep = Station2FeedingStep.生产完毕;
                            break;

                        case Station2FeedingStep.生产完毕:
                            CurrentStepDescription = "本批次生产完毕，复位准备下一批...";
                            // 清理缓存数据
                            _cachedRecipe = null;
                            _layersToProcess = [];
                            _currentLayerIndex = 0;
                            _totalLayerCount = 0;
                            _cachedErrorCode = null;

                            MemoryParam.PersistedStep = (int)Station2FeedingStep.等待按下工位2启动按钮;
                            FlushMemory();
                            _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                            break;

                        #endregion

                        #region Phase D：业务异常流转拦截

                        case Station2FeedingStep.批次产品个数不正确:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.BatchCountZero, "批次产品个数为0，无法启动生产");
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.料盒尺寸与配方不匹配:
                            TriggerAlarm(_cachedErrorCode ?? AlarmCodesExtensions.WS2Feeding.WaferSizeMismatch, "料盒尺寸与配方不匹配");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.工位2配方获取失败:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.RecipeNull, "工位2配方获取失败");
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.寻层算法空值判定:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.AlgorithmZeroLayers, "寻层算法判定为0层");
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.寻层算法过滤异常:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.AlgorithmException, "寻层算法出现严重异常");
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.料盒尺寸识别失败:
                        case Station2FeedingStep.料盒尺寸传感器信号冲突:
                        case Station2FeedingStep.料盒公用底座未检测到物体:
                        case Station2FeedingStep.八寸晶圆放反:
                        case Station2FeedingStep.十二寸晶圆放反:
                            var sizeErrCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Feeding.SizeDetectionSensorFailed;
                            TriggerAlarm(sizeErrCode, $"料盒识别或防呆异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.X轴运动条件不满足:
                        case Station2FeedingStep.X轴互锁失败_存在铁环突片:
                        case Station2FeedingStep.拉料互锁失败_挡杆未打开:
                            var xErrCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Feeding.XAxisPreconditionFailed;
                            TriggerAlarm(xErrCode, $"X轴运动条件不满足: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.Z轴运动条件不满足:
                        case Station2FeedingStep.Z轴互锁失败_料盒未到位:
                            var zErrCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Feeding.ZAxisPreconditionFailed;
                            TriggerAlarm(zErrCode, $"Z轴运动条件不满足: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.X轴运动超时:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.XAxisMoveTimeout, "X轴运动超时");
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.Z轴运动超时:
                        case Station2FeedingStep.Z轴切换层运动失败:
                            var zTimeOutCode = _cachedErrorCode ?? AlarmCodesExtensions.WS2Feeding.ZAxisMoveTimeout;
                            TriggerAlarm(zTimeOutCode, $"Z轴运动异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.Z轴寻层扫描异常:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.LayerScanFailed, "Z轴寻层扫描异常");
                            _currentStep = _resumeStep;
                            break;

                        case Station2FeedingStep.检测到物料错层:
                            TriggerAlarm(AlarmCodesExtensions.WS2Feeding.MaterialTiltedMisaligned, $"物料错层翘起 (第 {_currentLayerIndex + 1} 层)");
                            _currentStep = _resumeStep;
                            break;

                        default:
                            // 兜底：处理未定义的步序
                            if ((int)_currentStep >= 100000)
                            {
                                TriggerAlarm(AlarmCodes.System.UndefinedStep, $"遇到未定义的业务步序: {_currentStep}");
                                _currentStep = _resumeStep != 0 ? _resumeStep : Station2FeedingStep.等待按下工位2启动按钮;
                            }
                            else
                            {
                                TriggerAlarm(AlarmCodes.System.UndefinedStep, $"状态机指针漂移，未定义步序[{_currentStep}]");
                                _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                            }
                            break;

                            #endregion
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 捕获任务取消：记录日志并上抛，由框架底层管理任务状态
                _logger.Warn($"[{StationName}] 流程接收到取消请求。当前状态: {_currentStep} - {CurrentStepDescription}");
                throw;
            }
            catch (Exception ex)
            {
                // 捕获致命/非预期异常：记录Fatal日志，快照当前步序，并上抛触发全局报警或急停
                _logger.Fatal($"[{StationName}] 业务循环崩溃！步序: {_currentStep} ({CurrentStepDescription}), 异常信息: {ex.Message}");
                throw;
            }
        }

        #endregion
    }
}