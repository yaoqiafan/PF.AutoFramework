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
    /// 【工位2】上下料工站业务流转控制器 (Feeding Station Controller - Station 2)
    ///
    /// <para>架构定位：</para>
    /// 作为工位2的主业务状态机，继承自 <see cref="StationBase{T}"/>。负责统筹调度底层 <see cref="WorkStation2FeedingModule"/>（硬件机构）
    /// 与 <see cref="WorkStationDataModule"/>（数据中枢），并通过 <see cref="IStationSyncService"/> 与拉料工站、检测工站进行跨工站握手协作。
    /// </summary>
    [StationUI("工位2上下料工站", "WorkStation2FeedingStationDebugView", order: 3)]
    public class WorkStation2FeedingStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WorkStation2FeedingModule? _feedingModule;
        private readonly WorkStationDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        /// <summary>
        /// 状态机当前执行的业务步序指针
        /// </summary>
        private Station2FeedingStep _currentStep = Station2FeedingStep.等待按下工位2启动按钮;

        // ── 跨步序流转的缓存字段 ──
        private OCRRecipeParam? _cachedRecipe;
        private E_WafeSize _detectedWaferSize;
        private Dictionary<int, List<double>> _rawMappingData = new Dictionary<int, List<double>>();
        private int _totalLayerCount;
        private List<int> _layersToProcess = new();
        private int _currentLayerIndex;

        #endregion

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

            // 业务与数据校验异常
            /// <summary>批次产品个数不正确</summary>
            批次产品个数不正确 = 100001,
            /// <summary>料盒尺寸与配方不匹配</summary>
            料盒尺寸与配方不匹配 = 100002,
            /// <summary>工位2配方获取失败</summary>
            工位2配方获取失败 = 100003,
            /// <summary>料盒尺寸识别失败</summary>
            料盒尺寸识别失败 = 100004,

            // 轴状态与运动异常
            /// <summary>Z轴运动条件不满足</summary>
            Z轴运动条件不满足 = 100010,
            /// <summary>X轴运动条件不满足</summary>
            X轴运动条件不满足 = 100011,
            /// <summary>Z轴运动超时</summary>
            Z轴运动超时 = 100020,
            /// <summary>X轴运动超时</summary>
            X轴运动超时 = 100021,

            // 流程特定检测异常
            /// <summary>Z轴寻层扫描异常</summary>
            Z轴寻层扫描异常 = 100030,
            /// <summary>检测到物料错层</summary>
            检测到物料错层 = 100031,
            /// <summary>寻层算法过滤异常</summary>
            寻层算法过滤异常 = 100032,
            /// <summary>寻层算法空值判定</summary>
            寻层算法空值判定 = 100033,

            #endregion
        }

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化工位2上下料工站
        /// </summary>
        public WorkStation2FeedingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            : base(E_WorkStation.工位2上下料工站.ToString(), logger)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation2FeedingModule)) as WorkStation2FeedingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
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

        /// <summary>执行工站初始化</summary>
        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {
                _logger.Info($"[{StationName}] 正在初始化上下料模组...");
                if (!await _feedingModule.InitializeAsync(token))
                    throw new Exception($"[{StationName}] 上下料模组初始化通信失败！");

                if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.ZAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，Z轴回零失败。");
                    Fire(MachineTrigger.Error);
                    return;
                }
                if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.XAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，X轴回零失败。");
                    Fire(MachineTrigger.Error);
                    return;
                }

                if (await _feedingModule.InitializeFeedingStateAsync(token: token))
                {
                    _logger.Success($"[{StationName}] 初始化完成，机构已退回安全位就绪。");
                    Fire(MachineTrigger.InitializeDone); // Initializing → Idle
                }
                else
                {
                    _logger.Error($"[{StationName}] 初始化失败，模组未能回归安全状态。");
                    Fire(MachineTrigger.Error);
                }
            }
            catch
            {
                Fire(MachineTrigger.Error); // Initializing → Alarm
                throw;
            }
            this._currentStep = Station2FeedingStep.等待按下工位2启动按钮;
        }

        /// <summary>执行工站复位</summary>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);  // Alarm → Resetting
            try
            {
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑机制，将恢复至步序：[{_currentStep}]）...");

                // 调用模组硬件层复位：清除伺服报警标志位，无轴物理运动
                if (_feedingModule != null)
                    await _feedingModule.ResetAsync(token);

                // 仅初始化报警复位时重置信号量；运行期报警复位保留信号量以支持断点续跑
                if (CameFromInitAlarm)
                    _sync.ResetScope(StationName);

                // ⚠️ 核心设计：不重置 _currentStep！断点续跑的恢复节点已在各异常 case 中设定完毕。

                _logger.Success($"[{StationName}] 复位完成，将从步序 [{_currentStep}] 继续执行。");
                await FireAsync(ResetCompletionTrigger);  // Resetting → Idle 或 Uninitialized
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 复位失败: {ex.Message}");
                Fire(MachineTrigger.Error);  // 确保不卡死在 Resetting 状态
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

        /// <summary>正常生产主循环</summary>
        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    // ══════════════════════════════════════════════════════════
                    //  阶段 A：运动前准备 (配方加载、尺寸防呆、安全避让与寻层)
                    // ══════════════════════════════════════════════════════════
                    #region Phase A

                    case Station2FeedingStep.等待按下工位2启动按钮:
                        CurrentStepDescription = "等待按下工位2启动按钮...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待操作员按下工位2启动按钮...");

                        // 阻塞等待主控释放的全局物理按钮信号
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2启动按钮按下), token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 检测到启动信号，开始执行上料流程。");
                        _currentStep = Station2FeedingStep.验证当前批次产品个数;
                        break;

                    case Station2FeedingStep.验证当前批次产品个数:
                        CurrentStepDescription = "验证当前批次产品个数...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (_dataModule.Station2MesDetectionData.Quantity != 0)
                        {
                            _logger.Info($"[{StationName}] 批次产品个数验证通过，数量：{_dataModule.Station2MesDetectionData.Quantity}。");
                            _currentStep = Station2FeedingStep.获取工位2配方参数;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 批次产品个数为0，无法启动生产。");
                            _currentStep = Station2FeedingStep.批次产品个数不正确;
                        }
                        break;

                    case Station2FeedingStep.获取工位2配方参数:
                        CurrentStepDescription = "获取工位2配方参数...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _cachedRecipe = _dataModule.Station2ReciepParam;
                        if (_cachedRecipe != null)
                        {
                            _logger.Info($"[{StationName}] 配方参数获取成功：[{_cachedRecipe.RecipeName}]，晶圆尺寸：{_cachedRecipe.WafeSize}。");
                            _currentStep = Station2FeedingStep.识别料盒尺寸;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 工位2配方参数为空，请确认配方是否已下发。");
                            _currentStep = Station2FeedingStep.工位2配方获取失败;
                        }
                        break;

                    case Station2FeedingStep.识别料盒尺寸:
                        CurrentStepDescription = "识别料盒尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            _detectedWaferSize = await _feedingModule.GetWaferBoxSizeAsync(token).ConfigureAwait(false);
                            _logger.Info($"[{StationName}] 料盒尺寸识别成功：{_detectedWaferSize}。");
                            _currentStep = Station2FeedingStep.验证尺寸与配方是否匹配;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] 料盒尺寸识别失败：{ex.Message}");
                            _currentStep = Station2FeedingStep.料盒尺寸识别失败;
                        }
                        break;

                    case Station2FeedingStep.验证尺寸与配方是否匹配:
                        CurrentStepDescription = "验证料盒尺寸与配方是否匹配...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (_detectedWaferSize == _cachedRecipe.WafeSize)
                        {
                            _logger.Info($"[{StationName}] 料盒尺寸与配方匹配（{_detectedWaferSize}），继续执行。");
                            _currentStep = Station2FeedingStep.切换物料尺寸;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 料盒尺寸不匹配：实际={_detectedWaferSize}，配方要求={_cachedRecipe.WafeSize}。");
                            _currentStep = Station2FeedingStep.料盒尺寸与配方不匹配;
                        }
                        break;

                    case Station2FeedingStep.切换物料尺寸:
                        CurrentStepDescription = "切换物料尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.SwitchProductionStateAsync(_cachedRecipe.WafeSize, token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] 切换物料尺寸成功，继续执行。");
                            _currentStep = Station2FeedingStep.判断X轴是否具备运动条件_开始;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 料盒尺寸不匹配：实际={_detectedWaferSize}，配方要求={_cachedRecipe.WafeSize}。");
                            _currentStep = Station2FeedingStep.料盒尺寸与配方不匹配;
                        }
                        break;

                    case Station2FeedingStep.判断X轴是否具备运动条件_开始:
                        CurrentStepDescription = "检查X轴运动条件（开始）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station2FeedingStep.X轴到待机位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴运动条件不满足（开始阶段），请检查夹爪状态。");
                            _currentStep = Station2FeedingStep.X轴运动条件不满足;
                        }
                        break;

                    case Station2FeedingStep.X轴到待机位:
                        CurrentStepDescription = "X轴移动到待机位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, nameof(WorkStation2FeedingModule.XAxisPoint.待机位), token: token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] X轴已到达待机位，开始循环取料。");
                            _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_寻层;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴移动到待机位失败（运动超时）。");
                            _currentStep = Station2FeedingStep.X轴运动超时;
                        }
                        break;

                    case Station2FeedingStep.判断Z轴是否具备运动条件_寻层:
                        CurrentStepDescription = "检查Z轴运动条件（寻层）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station2FeedingStep.Z轴扫描寻层;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴运动条件不满足（寻层阶段），请检查轴状态与互锁信号。");
                            _currentStep = Station2FeedingStep.Z轴运动条件不满足;
                        }
                        break;

                    case Station2FeedingStep.Z轴扫描寻层:
                        CurrentStepDescription = "Z轴扫描寻层...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            _rawMappingData = await _feedingModule.SearchLayerAsync(token: token).ConfigureAwait(false);
                            if (_rawMappingData.Count > 0)
                            {
                                _logger.Info($"[{StationName}] 寻层扫描完成，进入算法过滤。");
                                _currentStep = Station2FeedingStep.算法过滤层数;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 寻层扫描结果为0层，料盒可能为空或扫描异常。");
                                _currentStep = Station2FeedingStep.Z轴寻层扫描异常;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] Z轴寻层扫描异常：{ex.Message}");
                            _currentStep = Station2FeedingStep.Z轴寻层扫描异常;
                        }
                        break;

                    case Station2FeedingStep.算法过滤层数:
                        CurrentStepDescription = "算法过滤与防呆验证...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            var validWafersDict = await _feedingModule.AnalyzeAndFilterMappingData(_rawMappingData);
                            _layersToProcess = validWafersDict.Keys.OrderBy(layerIndex => layerIndex).ToList();

                            _totalLayerCount = _layersToProcess.Count;
                            _currentLayerIndex = 0;

                            if (_layersToProcess.Count == 0)
                            {
                                _logger.Warn($"[{StationName}] 寻层过滤结果为空，料盒内未检测到任何有效晶圆！");
                                _currentStep = Station2FeedingStep.寻层算法空值判定;
                            }
                            else
                            {
                                _logger.Info($"[{StationName}] 过滤完成！共识别到 {_totalLayerCount} 片有效晶圆。实际存在的层级索引为：{string.Join(", ", _layersToProcess)}");
                                _logger.Info($"[{StationName}] 开始进入后续取料循环运动流程...");
                                _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_取料定位;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 捕获到底层抛出的严重防呆错误（斜片、重叠片等）
                            _logger.Error($"[{StationName}] 寻层算法过滤发生异常: {ex.Message}");
                            _currentStep = Station2FeedingStep.寻层算法过滤异常;
                        }
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 B：取料核心循环 (逐层联动拉料与放料)
                    // ══════════════════════════════════════════════════════════
                    #region Phase B

                    case Station2FeedingStep.判断Z轴是否具备运动条件_取料定位:
                        CurrentStepDescription = $"检查Z轴运动条件（第{_currentLayerIndex + 1}层）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station2FeedingStep.切换到指定层;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴运动条件不满足（取料定位阶段，第{_currentLayerIndex + 1}层）。");
                            _currentStep = Station2FeedingStep.Z轴运动条件不满足;
                        }
                        break;

                    case Station2FeedingStep.切换到指定层:
                        CurrentStepDescription = $"Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.SwitchToLayerAsync(_layersToProcess[_currentLayerIndex], token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] Z轴已到达第{_layersToProcess[_currentLayerIndex] + 1}层取料位。");
                            _currentStep = Station2FeedingStep.判断物料可拉出条件;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层失败（运动超时）。");
                            _currentStep = Station2FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station2FeedingStep.判断物料可拉出条件:
                        CurrentStepDescription = "判断物料可拉出条件...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanPullOutMaterialAsync(token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料可拉出条件通过，可执行拉料。");

                            // 通知拉料工站：可以进场取片
                            _sync.Release(nameof(WorkstationSignals.工位2允许拉料), StationName);
                            _currentStep = Station2FeedingStep.等待物料拉出完成;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 检测到第{_layersToProcess[_currentLayerIndex] + 1}层物料存在错层翘起，禁止拉料。");
                            _currentStep = Station2FeedingStep.检测到物料错层;
                        }
                        break;

                    case Station2FeedingStep.等待物料拉出完成:
                        CurrentStepDescription = "等待物料拉出完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        // 阻塞等待拉料工站的完成信号
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2拉料完成), token, scope: E_WorkStation.工位2拉料工站.ToString()).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料已拉出到位。");
                        _currentStep = Station2FeedingStep.阻塞等待物料回退完成;

                        // 自身 Z 轴避让确认完毕后，通知拉料工站可以执行退料推入
                        _sync.Release(nameof(WorkstationSignals.工位2允许退料), StationName);
                        break;

                    case Station2FeedingStep.阻塞等待物料回退完成:
                        CurrentStepDescription = "阻塞等待物料回退完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        // 阻塞等待拉料工站将晶圆送回料盒的完成信号
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2退料完成), token, scope: E_WorkStation.工位2拉料工站.ToString()).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料已安全回退至料盒。");
                        _currentStep = Station2FeedingStep.计算下一层位置;
                        break;

                    case Station2FeedingStep.计算下一层位置:
                        CurrentStepDescription = "计算下一层位置...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _currentLayerIndex++;
                        if (_currentLayerIndex >= _layersToProcess.Count)
                        {
                            _logger.Info($"[{StationName}] 所有层（共{_layersToProcess.Count}层）取料完毕，进入收尾流程。");
                            _currentStep = Station2FeedingStep.物料全部生产完毕;
                        }
                        else
                        {
                            _logger.Info($"[{StationName}] 继续处理第{_layersToProcess[_currentLayerIndex] + 1}层（{_currentLayerIndex + 1}/{_layersToProcess.Count}）。");
                            _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        }
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 C：生产结束与安全收尾
                    // ══════════════════════════════════════════════════════════
                    #region Phase C

                    case Station2FeedingStep.物料全部生产完毕:
                        CurrentStepDescription = "物料全部生产完毕，更新状态...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Success($"[{StationName}] 本批次所有晶圆已全部完成上料（共{_layersToProcess.Count}层）。");
                        _currentStep = Station2FeedingStep.判断X轴是否具备运动条件_结束;
                        break;

                    case Station2FeedingStep.判断X轴是否具备运动条件_结束:
                        CurrentStepDescription = "检查X轴运动条件（结束）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station2FeedingStep.X轴到挡料位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴运动条件不满足（结束阶段），请检查夹爪状态。");
                            _currentStep = Station2FeedingStep.X轴运动条件不满足;
                        }
                        break;

                    case Station2FeedingStep.X轴到挡料位:
                        CurrentStepDescription = "X轴移动到挡料位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        string xBlockPoint = nameof(WorkStation2FeedingModule.XAxisPoint.挡料位);
                        if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, xBlockPoint, token: token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] X轴已到达挡料位（适应尺寸：{_detectedWaferSize}）。");
                            _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_流程结束;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴移动到挡料位失败（运动超时）。");
                            _currentStep = Station2FeedingStep.X轴运动超时;
                        }
                        break;

                    case Station2FeedingStep.判断Z轴是否具备运动条件_流程结束:
                        CurrentStepDescription = "检查Z轴运动条件（流程结束）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station2FeedingStep.Z轴到待机位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴运动条件不满足（流程结束阶段），请检查轴状态。");
                            _currentStep = Station2FeedingStep.Z轴运动条件不满足;
                        }
                        break;

                    case Station2FeedingStep.Z轴到待机位:
                        CurrentStepDescription = "Z轴退回待机位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.ZAxis, nameof(WorkStation2FeedingModule.ZAxisPoint.待机位), token: token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] Z轴已安全退回最高待机位。");
                            _currentStep = Station2FeedingStep.通知操作员下料;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴退回待机位失败（运动超时）。");
                            _currentStep = Station2FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station2FeedingStep.通知操作员下料:
                        CurrentStepDescription = "通知操作员下料，等待确认...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 已通知操作员下料，等待人工确认信号...");

                        // 等待界面 UI 按钮或物理确认按钮的下料完毕信号
                        await _sync.WaitAsync(nameof(WorkstationSignals.工位2人工下料完成), token).ConfigureAwait(false);

                        _logger.Info($"[{StationName}] 收到操作员下料确认信号。");
                        _currentStep = Station2FeedingStep.生产完毕;
                        break;

                    case Station2FeedingStep.生产完毕:
                        CurrentStepDescription = "本批次生产完毕，复位准备下一批...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Success($"[{StationName}] ══ 本批次上料流程全部闭环完成 ══");

                        // 清理批次内存状态，重置指针准备迎接下一整盒料
                        _cachedRecipe = null;
                        _layersToProcess = new List<int>();
                        _currentLayerIndex = 0;
                        _totalLayerCount = 0;
                        _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 D：异常拦截与断点续跑处理 (Exception & Recovery)
                    //  执行逻辑：
                    //  1. 记录错误日志
                    //  2. 设定恢复步序（_currentStep 回退至正常的逻辑重试入口）
                    //  3. TriggerAlarm 触发全局报警并挂起当前状态机，等待外部复位
                    // ══════════════════════════════════════════════════════════
                    #region Phase D (Exceptions)

                    // ── 策略 1：致命业务异常，必须从头来过 (退回启动位) ──

                    case Station2FeedingStep.批次产品个数不正确:
                        _logger.Error($"[{StationName}] 批次产品个数为 0，无法启动生产。请重新下发 MES 批次数据后复位重启。");
                        _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.BatchCountZero, "批次产品个数为0，无法启动生产");
                        break;

                    case Station2FeedingStep.料盒尺寸与配方不匹配:
                        _logger.Error($"[{StationName}] 料盒尺寸（{_detectedWaferSize}）与配方要求（{_cachedRecipe?.WafeSize}）不匹配。请更换正确料盒或修改配方后复位重启。");
                        _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.WaferSizeMismatch, $"料盒尺寸({_detectedWaferSize})与配方({_cachedRecipe?.WafeSize})不匹配");
                        break;

                    case Station2FeedingStep.工位2配方获取失败:
                        _logger.Error($"[{StationName}] 工位2配方参数为空，无法继续。请确认配方已正确下发后复位重启。");
                        _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.RecipeNull, "工位2配方参数为空");
                        break;

                    case Station2FeedingStep.寻层算法空值判定:
                        _logger.Error($"[{StationName}] 寻层算法判定为0层！请确认是否正确放置物料。");
                        _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.AlgorithmZeroLayers, "寻层算法判定为0层");
                        break;

                    case Station2FeedingStep.寻层算法过滤异常:
                        _logger.Error($"[{StationName}] 寻层算法出现严重异常！");
                        _currentStep = Station2FeedingStep.等待按下工位2启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.AlgorithmException, "寻层算法出现严重异常");
                        break;


                    // ── 策略 2：退回前置安全节点，重新评估后继续 (重试) ──

                    case Station2FeedingStep.料盒尺寸识别失败:
                        _logger.Error($"[{StationName}] 料盒尺寸识别失败（传感器信号异常或料盒未放正）。请检查料盒位置后复位，将重新识别尺寸。");
                        _currentStep = Station2FeedingStep.识别料盒尺寸; // 仅需重新识别
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.SizeDetectionSensorFailed, "料盒尺寸识别失败，传感器信号异常");
                        break;

                    case Station2FeedingStep.Z轴运动条件不满足:
                        _logger.Error($"[{StationName}] Z轴运动条件不满足（轴报警或互锁信号未就绪）。请处理轴故障后复位，将重新评估 Z 轴状态。");
                        _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_寻层; // 退回最早的 Z 轴安全检查点
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.ZAxisPreconditionFailed, "Z轴运动条件不满足");
                        break;

                    case Station2FeedingStep.X轴运动条件不满足:
                        _logger.Error($"[{StationName}] X轴运动条件不满足（夹爪未张开或轴报警）。请处理后复位，将重新评估 X 轴状态。");
                        _currentStep = Station2FeedingStep.判断X轴是否具备运动条件_开始;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.XAxisPreconditionFailed, "X轴运动条件不满足");
                        break;

                    case Station2FeedingStep.Z轴寻层扫描异常:
                        _logger.Error($"[{StationName}] Z轴寻层扫描异常（扫描结果为空或过程出错）。请检查料盒与传感器后复位，将重新执行扫描。");
                        _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_寻层; // 重新寻层
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.LayerScanFailed, "Z轴寻层扫描异常");
                        break;

                    case Station2FeedingStep.检测到物料错层:
                        _logger.Error($"[{StationName}] 检测到第 {(_layersToProcess.Count > _currentLayerIndex ? _layersToProcess[_currentLayerIndex] + 1 : _currentLayerIndex + 1)} 层物料错层翘起！请人工处理后复位，将重新检查该层。");
                        _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_取料定位; // 索引不变，原地重入该层
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.MaterialTiltedMisaligned, "物料错层翘起");
                        break;

                    case Station2FeedingStep.Z轴运动超时:
                        _logger.Error($"[{StationName}] Z轴运动超时！请确认轴无卡阻后复位，将重新检查 Z 轴条件并重试运动。");
                        _currentStep = Station2FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.ZAxisMoveTimeout, "Z轴运动超时");
                        break;

                    case Station2FeedingStep.X轴运动超时:
                        _logger.Error($"[{StationName}] X轴运动超时！请确认轴无卡阻后复位，将重新检查 X 轴条件并重试运动。");
                        _currentStep = Station2FeedingStep.判断X轴是否具备运动条件_开始;
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.XAxisMoveTimeout, "X轴运动超时");
                        break;

                    default:
                        _logger.Error($"[{StationName}] 状态机指针漂移，进入未定义步序 [{_currentStep}]，触发保护性报警。");
                        TriggerAlarm(AlarmCodesExtensions.WS2Feeding.UndefinedStep, $"状态机指针漂移，未定义步序[{_currentStep}]");
                        break;

                        #endregion
                }
            }
        }

        #endregion
    }
}
