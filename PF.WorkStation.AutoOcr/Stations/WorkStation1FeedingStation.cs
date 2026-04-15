using NPOI.OpenXmlFormats.Wordprocessing;
using NPOI.SS.Formula.Functions;
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

        // ── 跨步序缓存字段 ──────────────────────────────────────────────────────
        private OCRRecipeParam? _cachedRecipe;
        private E_WafeSize _detectedWaferSize;
        private Dictionary<int, List<double>> _rawMappingData = new Dictionary<int, List<double>>();
        private int _totalLayerCount;
        private List<int> _layersToProcess = new();
        private int _currentLayerIndex;

        public enum Station1FeedingStep
        {
            #region 运动前准备

            等待按下工位1启动按钮 = 0,
            验证当前批次产品个数 = 10,
            获取工位1配方参数 = 20,
            识别料盒尺寸 = 30,
            验证尺寸与配方是否匹配 = 40,
            切换物料尺寸=45,
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

            // 业务与数据校验异常
            批次产品个数不正确 = 100001,
            料盒尺寸与配方不匹配 = 100002,
            工位1配方获取失败 = 100003,
            料盒尺寸识别失败 = 100004,

            // 轴状态与运动异常
            Z轴运动条件不满足 = 100010,
            X轴运动条件不满足 = 100011,
            Z轴运动超时 = 100020,
            X轴运动超时 = 100021,

            // 流程特定检测异常
            Z轴寻层扫描异常 = 100030,
            检测到物料错层 = 100031,
            寻层算法过滤异常 = 100032,
            寻层算法空值判定 = 100033,
            #endregion

        }


        public WorkStation1FeedingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("工位1上下料工站", logger)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation1FeedingModule)) as WorkStation1FeedingModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;

            _feedingModule.AlarmTriggered    += OnMechanismAlarm;
            _feedingModule.AlarmAutoCleared  += (_, _) => RaiseStationAlarmAutoCleared();
            _dataModule.AlarmTriggered       += OnMechanismAlarm;
            _dataModule.AlarmAutoCleared     += (_, _) => RaiseStationAlarmAutoCleared();
        }

        private void OnMechanismAlarm(object? sender, MechanismAlarmEventArgs e)
        {
            _logger.Error($"[{StationName}] 接收到模组报警 [{e.HardwareName}]: {e.ErrorMessage}");
            RaiseAlarm(e.ErrorCode ?? AlarmCodes.System.StationSyncError);
        }

        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize); // Uninitialized → Initializing
            try
            {

                _logger.Info($"[{StationName}] 正在初始化上下料模组...");
                if (!await _feedingModule.InitializeAsync(token))
                    throw new Exception($"[{StationName}] 上下料模组初始化失败！");

                if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.ZAxis, token: token))
                {
                    _logger.Error ($"[{StationName}] 初始化失败，Z轴回零失败。");
                    Fire(MachineTrigger.Error );
                    return;
                }
                if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.XAxis, token: token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，X轴回零失败。");
                    Fire(MachineTrigger.Error);
                    return;
                }

               if ( await _feedingModule .InitializeFeedingStateAsync(token :token ))
                {
                    _logger.Success($"[{StationName}] 初始化完成，就绪。");

                    Fire(MachineTrigger.InitializeDone);
                }
               else
                {
                    _logger.Error($"[{StationName}] 初始化失败，模组回归初始化失败。");
                    Fire(MachineTrigger.Error);
                }
 // Initializing → Idle
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
                if (_feedingModule != null)
                    await _feedingModule.ResetAsync(token);

                // 注意：不重置 _currentStep！
                // 断点续跑的恢复节点已在各异常 case 中于 TriggerAlarm() 前设定完毕。

             

                _logger.Success($"[{StationName}] 复位完成，将从步序 [{_currentStep}] 继续执行。");
                await FireAsync(ResetCompletionTrigger);  // Resetting → Idle 或 Uninitialized（取决于报警来源）
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 复位失败: {ex.Message}");
                Fire(MachineTrigger.Error);  // Resetting → Alarm，不卡死在 Resetting
                throw;
            }
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
                    // ══════════════════════════════════════════════════════════
                    //  阶段 A：运动前准备
                    // ══════════════════════════════════════════════════════════

                    case Station1FeedingStep.等待按下工位1启动按钮:
                        CurrentStepDescription = "等待按下工位1启动按钮...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待操作员按下工位1启动按钮...");
                        await  _sync.WaitAsync(WorkstationSignals.工位1启动按钮按下.ToString(), token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 检测到启动信号，开始执行上料流程。");


                        _currentStep = Station1FeedingStep.验证当前批次产品个数;
                        break;

                    case Station1FeedingStep.验证当前批次产品个数:
                        CurrentStepDescription = "验证当前批次产品个数...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (_dataModule.Station1MesDetectionData.Quantity != 0)
                        {
                            _logger.Info($"[{StationName}] 批次产品个数验证通过，数量：{_dataModule.Station1MesDetectionData.Quantity}。");
                            _currentStep = Station1FeedingStep.获取工位1配方参数;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 批次产品个数为0，无法启动生产。");
                            _currentStep = Station1FeedingStep.批次产品个数不正确;
                        }
                        break;

                    case Station1FeedingStep.获取工位1配方参数:
                        CurrentStepDescription = "获取工位1配方参数...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _cachedRecipe = _dataModule.Station1ReciepParam;
                        if (_cachedRecipe != null)
                        {
                            _logger.Info($"[{StationName}] 配方参数获取成功：[{_cachedRecipe.RecipeName}]，晶圆尺寸：{_cachedRecipe.WafeSize}。");
                            _currentStep = Station1FeedingStep.识别料盒尺寸;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 工位1配方参数为空，请确认配方是否已下发。");
                            _currentStep = Station1FeedingStep.工位1配方获取失败;
                        }
                        break;

                    case Station1FeedingStep.识别料盒尺寸:
                        CurrentStepDescription = "识别料盒尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            _detectedWaferSize = await _feedingModule.GetWaferBoxSizeAsync(token).ConfigureAwait(false);
                            _logger.Info($"[{StationName}] 料盒尺寸识别成功：{_detectedWaferSize}。");
                            _currentStep = Station1FeedingStep.验证尺寸与配方是否匹配;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] 料盒尺寸识别失败：{ex.Message}");
                            _currentStep = Station1FeedingStep.料盒尺寸识别失败;
                        }
                        break;

                    case Station1FeedingStep.验证尺寸与配方是否匹配:
                        CurrentStepDescription = "验证料盒尺寸与配方是否匹配...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (_detectedWaferSize == _cachedRecipe.WafeSize)
                        {
                            _logger.Info($"[{StationName}] 料盒尺寸与配方匹配（{_detectedWaferSize}），继续执行。");
                            _currentStep = Station1FeedingStep.切换物料尺寸;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 料盒尺寸不匹配：实际={_detectedWaferSize}，配方要求={_cachedRecipe.WafeSize}。");
                            _currentStep = Station1FeedingStep.料盒尺寸与配方不匹配;
                        }
                        break;

                    case Station1FeedingStep.切换物料尺寸:
                        CurrentStepDescription = "切换物料尺寸...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.SwitchProductionStateAsync(_cachedRecipe.WafeSize,token ).ConfigureAwait(false ))
                        {
                            _logger.Info($"[{StationName}] 切换物料尺寸成功，继续执行。");
                            _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_寻层;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 料盒尺寸不匹配：实际={_detectedWaferSize}，配方要求={_cachedRecipe.WafeSize}。");
                            _currentStep = Station1FeedingStep.料盒尺寸与配方不匹配;
                        }

                        break;

                    case Station1FeedingStep.判断Z轴是否具备运动条件_寻层:
                        CurrentStepDescription = "检查Z轴运动条件（寻层）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station1FeedingStep.Z轴扫描寻层;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴运动条件不满足（寻层阶段），请检查轴状态与互锁信号。");
                            _currentStep = Station1FeedingStep.Z轴运动条件不满足;
                        }
                        break;

                    case Station1FeedingStep.Z轴扫描寻层:
                        CurrentStepDescription = "Z轴扫描寻层...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            _rawMappingData = await _feedingModule.SearchLayerAsync(token:token).ConfigureAwait(false);
                            if (_rawMappingData.Count > 0)
                            {
                                _logger.Info($"[{StationName}] 寻层扫描完成，识别到有效层数：{_totalLayerCount}。");
                                _currentStep = Station1FeedingStep.到初始层点;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 寻层扫描结果为0层，料盒可能为空或扫描异常。");
                                _currentStep = Station1FeedingStep.Z轴寻层扫描异常;
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] Z轴寻层扫描异常：{ex.Message}");
                            _currentStep = Station1FeedingStep.Z轴寻层扫描异常;
                        }
                        break;

                    case Station1FeedingStep.到初始层点:
                        CurrentStepDescription = "Z轴移动到初始层点...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.SwitchToLayerAsync(0, token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] Z轴已到达初始层（第1层）位置。");
                            _currentStep = Station1FeedingStep.阵列层取料位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴移动到初始层点失败（运动超时或条件不满足）。");
                            _currentStep = Station1FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station1FeedingStep.阵列层取料位:
                        CurrentStepDescription = "计算阵列层取料位坐标...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        // 加载对应尺寸的工艺参数、计算所有层坐标阵列
                        await _feedingModule.SwitchProductionStateAsync(_detectedWaferSize, token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] [{_detectedWaferSize}] 阵列层坐标已生成完毕。");
                        _currentStep = Station1FeedingStep.算法过滤层数;
                        break;

                    case Station1FeedingStep.算法过滤层数:
                        CurrentStepDescription = "算法过滤与防呆验证...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        try
                        {
                            var validWafersDict =await  _feedingModule.AnalyzeAndFilterMappingData(_rawMappingData);
                            _layersToProcess = validWafersDict.Keys.OrderBy(layerIndex => layerIndex).ToList();

                            _totalLayerCount = _layersToProcess.Count;
                            _currentLayerIndex = 0;

                            if (_layersToProcess.Count == 0)
                            {
                                _logger.Warn($"[{StationName}] 寻层过滤结果为空，料盒内未检测到任何有效晶圆！");

                                _currentStep = Station1FeedingStep.寻层算法空值判定;
                            }
                            else
                            {
                                // 5. 正常流转
                                _logger.Info($"[{StationName}] 过滤完成！共识别到 {_totalLayerCount} 片有效晶圆。实际存在的层级索引为：{string.Join(", ", _layersToProcess)}");
                                _logger.Info($"[{StationName}] 开始进入后续运动流程...");

                                _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_开始;
                            }
                        }
                        catch (Exception ex)
                        {
                            // 捕获到底层抛出的严重防呆错误（斜片 Cross-slot、重叠片 Double-wafer 等）
                            _logger.Error($"[{StationName}] 寻层算法过滤发生异常: {ex.Message}");
                            _currentStep = Station1FeedingStep.寻层算法过滤异常;
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  阶段 B：X轴介入
                    // ══════════════════════════════════════════════════════════

                    case Station1FeedingStep.判断X轴是否具备运动条件_开始:
                        CurrentStepDescription = "检查X轴运动条件（开始）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station1FeedingStep.X轴到待机位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴运动条件不满足（开始阶段），请检查夹爪状态。");
                            _currentStep = Station1FeedingStep.X轴运动条件不满足;
                        }
                        break;

                    case Station1FeedingStep.X轴到待机位:
                        CurrentStepDescription = "X轴移动到待机位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.XAxis.MoveToPointAsync(nameof(WorkStation1FeedingModule.XAxisPoint.待机位), token: token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] X轴已到达待机位，开始循环取料。");
                            _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴移动到待机位失败（运动超时）。");
                            _currentStep = Station1FeedingStep.X轴运动超时;
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  阶段 C：取料核心循环
                    // ══════════════════════════════════════════════════════════

                    case Station1FeedingStep.判断Z轴是否具备运动条件_取料定位:
                        CurrentStepDescription = $"检查Z轴运动条件（第{_currentLayerIndex + 1}层）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station1FeedingStep.切换到指定层;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴运动条件不满足（取料定位阶段，第{_currentLayerIndex + 1}层）。");
                            _currentStep = Station1FeedingStep.Z轴运动条件不满足;
                        }
                        break;

                    case Station1FeedingStep.切换到指定层:
                        CurrentStepDescription = $"Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.SwitchToLayerAsync(_layersToProcess[_currentLayerIndex], token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] Z轴已到达第{_layersToProcess[_currentLayerIndex] + 1}层取料位。");
                            _currentStep = Station1FeedingStep.错层检测;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层失败（运动超时）。");
                            _currentStep = Station1FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station1FeedingStep.错层检测:
                        CurrentStepDescription = "检测物料错层...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanPullOutMaterialAsync(token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层错层检测通过，可执行拉料。");

                            _sync.Release(WorkstationSignals.工位1允许拉料.ToString());
                            _currentStep = Station1FeedingStep.等待物料拉出完成;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 检测到第{_layersToProcess[_currentLayerIndex] + 1}层物料存在错层翘起，禁止拉料。");
                            _currentStep = Station1FeedingStep.检测到物料错层;
                        }
                        break;




                    case Station1FeedingStep.等待物料拉出完成:
                        CurrentStepDescription = "等待物料拉出完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.WaitUntilMaterialPulledOutAsync(5000, token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料已拉出到位。");
                            _currentStep = Station1FeedingStep.阻塞等待物料回退完成;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 等待物料拉出超时（第{_layersToProcess[_currentLayerIndex] + 1}层）。");
                            _currentStep = Station1FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station1FeedingStep.阻塞等待物料回退完成:
                        CurrentStepDescription = "阻塞等待物料回退完成...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.WaitUntilMaterialReturnedAsync(5000, token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料已回退至安全位置。");
                            _currentStep = Station1FeedingStep.计算下一层位置;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] 等待物料回退超时（第{_layersToProcess[_currentLayerIndex] + 1}层）。");
                            _currentStep = Station1FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station1FeedingStep.计算下一层位置:
                        CurrentStepDescription = "计算下一层位置...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _currentLayerIndex++;
                        if (_currentLayerIndex >= _layersToProcess.Count)
                        {
                            _logger.Info($"[{StationName}] 所有层（共{_layersToProcess.Count}层）取料完毕，进入收尾流程。");
                            _currentStep = Station1FeedingStep.物料全部生产完毕;

                        }
                        else
                        {
                            _logger.Info($"[{StationName}] 继续处理第{_layersToProcess[_currentLayerIndex] + 1}层（{_currentLayerIndex + 1}/{_layersToProcess.Count}）。");
                            _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  阶段 D：生产结束与复位
                    // ══════════════════════════════════════════════════════════

                    case Station1FeedingStep.物料全部生产完毕:
                        CurrentStepDescription = "物料全部生产完毕，更新状态...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Success($"[{StationName}] 本批次所有晶圆已全部完成上料（共{_layersToProcess.Count}层）。");
                        _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_结束;
                        break;

                    case Station1FeedingStep.判断X轴是否具备运动条件_结束:
                        CurrentStepDescription = "检查X轴运动条件（结束）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station1FeedingStep.X轴到挡料位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴运动条件不满足（结束阶段），请检查夹爪状态。");
                            _currentStep = Station1FeedingStep.X轴运动条件不满足;
                        }
                        break;

                    case Station1FeedingStep.X轴到挡料位:
                        CurrentStepDescription = "X轴移动到挡料位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        string xBlockPoint = _detectedWaferSize == E_WafeSize._8寸
                            ? nameof(WorkStation1FeedingModule.XAxisPoint.挡料位_8寸)
                            : nameof(WorkStation1FeedingModule.XAxisPoint.挡料位_12寸);
                        if (await _feedingModule.XAxis.MoveToPointAsync(xBlockPoint, token: token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] X轴已到达挡料位（{_detectedWaferSize}）。");
                            _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_流程结束;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] X轴移动到挡料位失败（运动超时）。");
                            _currentStep = Station1FeedingStep.X轴运动超时;
                        }
                        break;

                    case Station1FeedingStep.判断Z轴是否具备运动条件_流程结束:
                        CurrentStepDescription = "检查Z轴运动条件（流程结束）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                        {
                            _currentStep = Station1FeedingStep.Z轴到待机位;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴运动条件不满足（流程结束阶段），请检查轴状态。");
                            _currentStep = Station1FeedingStep.Z轴运动条件不满足;
                        }
                        break;

                    case Station1FeedingStep.Z轴到待机位:
                        CurrentStepDescription = "Z轴退回待机位...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (await _feedingModule.ZAxis.MoveToPointAsync(nameof(WorkStation1FeedingModule.ZAxisPoint.待机位), token: token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] Z轴已退回待机位。");
                            _currentStep = Station1FeedingStep.通知操作员下料;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] Z轴退回待机位失败（运动超时）。");
                            _currentStep = Station1FeedingStep.Z轴运动超时;
                        }
                        break;

                    case Station1FeedingStep.通知操作员下料:
                        CurrentStepDescription = "通知操作员下料，等待确认...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 已通知操作员下料，等待人工确认信号...");
                        await _sync.WaitAsync("Station1UnloadConfirm", token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 收到操作员下料确认信号。");
                        _currentStep = Station1FeedingStep.生产完毕;
                        break;

                    case Station1FeedingStep.生产完毕:
                        CurrentStepDescription = "本批次生产完毕，复位准备下一批...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Success($"[{StationName}] ══ 本批次上料流程全部完成 ══");
                        // 清理批次缓存，准备下一轮
                        _cachedRecipe = null;
                        _layersToProcess = new List<int>();
                        _currentLayerIndex = 0;
                        _totalLayerCount = 0;
                        _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  阶段 E：异常处理节点（断点续跑）
                    //  每个异常 case：① 记录错误日志
                    //                ② 设定复位后的恢复步序（_currentStep 指向正常节点）
                    //                ③ TriggerAlarm() — 取消 token、推入 Alarm 状态
                    //  ExecuteResetAsync 仅做硬件清警，不再重置 _currentStep，
                    //  下次 Start() 时状态机将从已设定的恢复节点继续执行。
                    // ══════════════════════════════════════════════════════════

                    // ── 策略 3：致命业务异常，必须从头来过 ──────────────────

                    case Station1FeedingStep.批次产品个数不正确:
                        _logger.Error($"[{StationName}] 批次产品个数为 0，无法启动生产。请重新下发 MES 批次数据后复位重启。");
                        // 数据层面问题，必须重新下发数据，退回流程起点
                        _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationDataInvalid);
                        break;

                    case Station1FeedingStep.料盒尺寸与配方不匹配:
                        _logger.Error($"[{StationName}] 料盒尺寸（{_detectedWaferSize}）与配方要求（{_cachedRecipe?.WafeSize}）不匹配。请更换正确料盒或修改配方后复位重启。");
                        // 料盒/配方不对应，需人工干预后从头重新确认
                        _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationDataInvalid);
                        break;

                    case Station1FeedingStep.工位1配方获取失败:
                        _logger.Error($"[{StationName}] 工位1配方参数为空，无法继续。请确认配方已正确下发后复位重启。");
                        // 配方未下发，数据源问题，退回起点
                        _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationDataInvalid);
                        break;

                    // ── 策略 2：退回前置安全节点，重新评估后继续 ─────────────

                    case Station1FeedingStep.料盒尺寸识别失败:
                        _logger.Error($"[{StationName}] 料盒尺寸识别失败（传感器信号异常或料盒未放正）。请检查料盒位置后复位，将重新识别尺寸。");
                        // 配方已缓存，仅需重新识别尺寸，无需从头
                        _currentStep = Station1FeedingStep.识别料盒尺寸;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationSensorError);
                        break;

                    case Station1FeedingStep.Z轴运动条件不满足:
                        _logger.Error($"[{StationName}] Z轴运动条件不满足（轴报警/互锁信号未就绪）。请处理轴故障后复位，将从 Z 轴条件检查节点重新评估。");
                        // 退回最早的 Z 轴条件检查节点，确保 _layersToProcess 完整重建
                        _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_寻层;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMotionFailed);
                        break;

                    case Station1FeedingStep.X轴运动条件不满足:
                        _logger.Error($"[{StationName}] X轴运动条件不满足（夹爪未张开或轴报警）。请处理后复位，将从 X 轴条件检查节点重新评估。");
                        // _layersToProcess 已就绪，退回 X 轴首个条件检查
                        _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_开始;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMotionFailed);
                        break;

                    case Station1FeedingStep.Z轴寻层扫描异常:
                        _logger.Error($"[{StationName}] Z轴寻层扫描异常（扫描结果为空或扫描过程出错）。请检查料盒与传感器后复位，将重新执行寻层扫描。");
                        // 退回 Z 轴条件检查，重新扫描层数
                        _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_寻层;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationSensorError);
                        break;

                    case Station1FeedingStep.检测到物料错层:
                        _logger.Error($"[{StationName}] 检测到第 {(_layersToProcess.Count > _currentLayerIndex ? _layersToProcess[_currentLayerIndex] + 1 : _currentLayerIndex + 1)} 层物料错层翘起！请人工处理后复位，将重新检查该层。");
                        // _currentLayerIndex 保持不变，人工处理后从 Z 轴条件检查重入当前层
                        _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMaterialError);
                        break;

                    // ── 策略 1：偶发/超时类异常，原地重试 ──────────────────

                    case Station1FeedingStep.Z轴运动超时:
                        _logger.Error($"[{StationName}] Z轴运动超时！请确认轴无卡阻后复位，将重新检查 Z 轴条件并重试运动。");
                        // 偶发性超时，退回 Z 轴条件检查重试（覆盖 step 70/130/240 三种触发源）
                        _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMotionFailed);
                        break;

                    case Station1FeedingStep.X轴运动超时:
                        _logger.Error($"[{StationName}] X轴运动超时！请确认轴无卡阻后复位，将重新检查 X 轴条件并重试运动。");
                        // 偶发性超时，退回 X 轴条件检查重试
                        _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_开始;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMotionFailed);
                        break;

                    case Station1FeedingStep.寻层算法空值判定:
                        _logger.Error($"[{StationName}] 寻层算法判定为0层！请确认是否正确放置物料。");
                        _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationAlgorithmError);
                        break;
                    case Station1FeedingStep.寻层算法过滤异常:
                        _logger.Error($"[{StationName}] 寻层算法出现异常！");
                        _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationAlgorithmError);
                        break;

                    default:
                        _logger.Error($"[{StationName}] 进入未定义步序 [{_currentStep}]，触发报警。");
                        TriggerAlarm(AlarmCodesExtensions.Process.StationUnexpectedStep);
                        break;
                }

            }
        }




    }
}
