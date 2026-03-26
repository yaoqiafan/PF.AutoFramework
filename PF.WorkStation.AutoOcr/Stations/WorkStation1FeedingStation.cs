using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Events;
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
                    // ══════════════════════════════════════════════════════════
                    //  阶段 A：运动前准备
                    // ══════════════════════════════════════════════════════════

                    case Station1FeedingStep.等待按下工位1启动按钮:
                        CurrentStepDescription = "等待按下工位1启动按钮...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待操作员按下工位1启动按钮...");
                        await _sync.WaitAsync("Station1Start", token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 检测到启动信号，开始执行上料流程。");
                        _currentStep = Station1FeedingStep.验证当前批次产品个数;
                        break;

                    case Station1FeedingStep.验证当前批次产品个数:
                        CurrentStepDescription = "验证当前批次产品个数...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        if (_dataModule.Station1MesDetectionData.QtyCount != 0)
                        {
                            _logger.Info($"[{StationName}] 批次产品个数验证通过，数量：{_dataModule.Station1MesDetectionData.QtyCount}。");
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
                            _totalLayerCount = await _feedingModule.SearchLayerAsync(token).ConfigureAwait(false);
                            if (_totalLayerCount > 0)
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
                        CurrentStepDescription = "算法过滤有效层数...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        // 根据寻层扫描结果构建待加工层列表（索引从0开始）
                        // 此处保留所有扫描到的有效层；实际工程可在此插入空层过滤逻辑
                        _layersToProcess = Enumerable.Range(0, _totalLayerCount).ToList();
                        _currentLayerIndex = 0;
                        _logger.Info($"[{StationName}] 过滤后待加工层数：{_layersToProcess.Count}，开始进入运动流程。");
                        _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_开始;
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
                    //  阶段 E：异常处理节点（所有值 > 100000 的步序）
                    //  记录错误日志 → 触发工站报警 → 挂起等待人工复位
                    // ══════════════════════════════════════════════════════════

                    default:
                        _logger.Error($"[{StationName}] 流程进入异常步序 [{_currentStep}]，触发工站报警，等待人工处理后复位。");
                        TriggerAlarm();
                        await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                        break;
                }

            }
        }




    }
}
