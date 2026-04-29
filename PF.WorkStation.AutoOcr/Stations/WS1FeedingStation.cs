using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Hardware;
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
    /// 定义上下料工站的完整生命周期与断点续跑异常状态节点
    /// </summary>
    public enum Station1FeedingStep
    {
        #region 阶段 A：运动前准备 (0 - 100)

        /// <summary>等待按下工位1启动按钮</summary>
        等待按下工位1启动按钮 = 0,
        /// <summary>验证当前批次产品个数</summary>
        验证当前批次产品个数 = 10,
        /// <summary>获取工位1配方参数</summary>
        获取工位1配方参数 = 20,
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
        /// <summary>工位1配方获取失败</summary>
        工位1配方获取失败 = 100003,

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
    /// 工位1上下料工站记忆参数，持久化跨重启所需的层数据与作业状态。
    /// </summary>
    public class WS1FeedingMemoryParam : StationMemoryBaseParam
    {
        /// <summary>
        /// 上料流程进行中（作业中）标志；false 表示待机状态。
        /// </summary>
        public bool IsInProgress { get; set; }

        /// <summary>
        /// 当前正在处理的层索引（层号）。
        /// </summary>
        public int CurrentLayerIndex { get; set; }

        /// <summary>
        /// 待处理的层索引集合，记录还需要进行上下料作业的层。
        /// </summary>
        public List<int> LayersToProcess { get; set; } = new();

        /// <summary>
        /// 当前料盒/花篮（Cassette/Magazine）的总层数。
        /// </summary>
        public int TotalLayerCount { get; set; }

        /// <summary>
        /// 检测或识别到的晶圆（Wafer）尺寸。
        /// </summary>
        public E_WafeSize DetectedWaferSize { get; set; }
    }

    /// <summary>
    /// 【工位1】上下料工站业务流转控制器 (Feeding Station Controller)
    ///
    /// <para>架构定位：</para>
    /// 作为工位1的主业务状态机，继承自 <see cref="StationBase{TMemory, TStep}"/>。负责统筹调度底层 <see cref="WS1FeedingModel"/>（硬件机构）
    /// 与 <see cref="WSDataModule"/>（数据中枢），并通过 <see cref="IStationSyncService"/> 与拉料工站、检测工站进行跨工站握手协作。
    /// </summary>
    [StationUI("工位1上下料工站", "WorkStation1FeedingStationDebugView", order: 1)]
    public class WS1FeedingStation : StationBase<WS1FeedingMemoryParam, Station1FeedingStep>
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WS1FeedingModel _feedingModule;
        private readonly WSDataModule _dataModule;
        private readonly IStationSyncService _sync;
        private readonly IHardwareInputMonitor? _hardwareInputMonitor;

        // 注意：_currentStep, _resumeStep, _cachedErrorCode 和 RouteToError() 均已下沉至基类。

        // ── 跨步序流转的缓存字段 ──
        private OCRRecipeParam? _cachedRecipe;
        private E_WafeSize _detectedWaferSize;
        private Dictionary<int, List<double>> _rawMappingData = new();
        private int _totalLayerCount;
        private List<int> _layersToProcess = new();
        private int _currentLayerIndex;

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)
        /// <summary>
        /// 构造
        /// </summary>
        /// <param name="containerProvider"></param>
        /// <param name="sync"></param>
        /// <param name="logger"></param>
        public WS1FeedingStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            // 调用带 TStep 泛型的基类构造函数，并传入初始步序
            : base(E_WorkStation.工位1上下料工站.ToString(), logger, Station1FeedingStep.等待按下工位1启动按钮)
        {
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WS1FeedingModel)) as WS1FeedingModel;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WSDataModule)) as WSDataModule;
            _sync = sync;
            _hardwareInputMonitor = containerProvider.Resolve<IHardwareInputMonitor>();

            _feedingModule.AlarmTriggered += OnMechanismAlarm;
            _feedingModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();

            _dataModule.AlarmTriggered += OnMechanismAlarm;
            _dataModule.AlarmAutoCleared += (_, _) => RaiseStationAlarmAutoCleared();
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
                token.ThrowIfCancellationRequested(); // 【新增】断点续跑入口检查

                switch (_currentStep)
                {
                    case Station1FeedingStep.Z轴扫描寻层:
                    case Station1FeedingStep.算法过滤层数:
                        _logger.Info($"[{StationName}] 重新初始化寻层状态");
                        _rawMappingData = new();
                        _layersToProcess = new();
                        _currentLayerIndex = 0;
                        break;

                    case Station1FeedingStep.判断Z轴是否具备运动条件_取料定位:
                    case Station1FeedingStep.切换到指定层:
                    case Station1FeedingStep.判断物料可拉出条件:
                    case Station1FeedingStep.等待物料拉出完成:
                    case Station1FeedingStep.阻塞等待物料回退完成:
                    case Station1FeedingStep.计算下一层位置:
                        _logger.Info($"[{StationName}] 复位至取料定位阶段起点（Z轴重新定位）");
                        _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                        break;

                    default:
                        _logger.Info($"[{StationName}] 保持当前步序: {_currentStep}");
                        break;
                }
            }
            catch (OperationCanceledException) // 【新增】防吞噬
            {
                _logger.Warn($"[{StationName}] 断点续跑准备工作被取消。");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 执行断点续跑时发生异常: {ex.Message}");
                _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                Fire(MachineTrigger.Error);
                throw;
            }
        }


        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task ExecuteInitializeAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Initialize);
            try
            {
                token.ThrowIfCancellationRequested();
                _logger.Info($"[{StationName}] 正在初始化上下料模组...");

                try
                {
                    _cachedRecipe = _dataModule.Station1ReciepParam;
                    if (!await _feedingModule.SwitchProductionStateAsync(_cachedRecipe.WafeSize))
                    {
                        throw new Exception("物料状态切换失败");
                    }
                    token.ThrowIfCancellationRequested();
                    await _sync.WaitAsync(nameof(WorkstationSignals.工位1拉料复位完成), token: token, "复位");

                    if (!await _feedingModule.InitializeAsync(token))
                        throw new Exception($"[{StationName}] 上下料模组初始化通信失败！");

                    if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.ZAxis, token: token))
                        throw new Exception("Z轴回零失败");

                    if (!await _feedingModule.WaitHomeDoneAsync(_feedingModule.XAxis, token: token))
                        throw new Exception("X轴回零失败");

                    var initResult = await _feedingModule.InitializeFeedingStateAsync(token: token);
                    if (!initResult.IsSuccess)
                    {
                        _logger.Error($"[{StationName}] 初始化失败：{initResult.ErrorMessage}");
                        TriggerAlarm(initResult.ErrorCode, initResult.ErrorMessage);
                        Fire(MachineTrigger.Error);
                        return;
                    }

                    _logger.Success($"[{StationName}] 机构已退回安全位就绪。");
                    _feedingModule.ResumeHealthMonitoring();
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    _logger.Error($"[{StationName}] 初始化异常: {ex.Message}");
                    Fire(MachineTrigger.Error);
                    throw;
                }

                _sync.ResetScope(StationName);

                if (MemoryParam.IsInProgress)
                {
                    if (!await HandleResumeInProgressAsync(token))
                    {
                        // HandleResumeInProgressAsync 内部已经处理了错误触发和日志记录
                        Fire(MachineTrigger.Error);
                        return;
                    }
                }
                else
                {
                    var restoreStep = (Station1FeedingStep)MemoryParam.PersistedStep;
                    if (!Enum.IsDefined(restoreStep) || (int)restoreStep >= 100000)
                        restoreStep = Station1FeedingStep.等待按下工位1启动按钮;
                    _currentStep = restoreStep;
                    _resumeStep = restoreStep;
                }

                Fire(MachineTrigger.InitializeDone);
            }
            catch (OperationCanceledException)
            {
                _logger.Warn($"[{StationName}] 初始化已被外部强行取消。");
                throw;
            }
            catch
            {
                Fire(MachineTrigger.Error);
                throw;
            }
        }

        /// <summary>
        /// 处理设备重启后，上次作业未完成的断点续跑逻辑。
        /// </summary>
        /// <returns>如果恢复成功并准备好继续，则为 true；否则为 false。</returns>
        private async Task<bool> HandleResumeInProgressAsync(CancellationToken token)
        {
            _logger.Info($"[{StationName}] 检测到上次退出时处于作业中状态（步序 {(Station1FeedingStep)MemoryParam.PersistedStep}），执行寻层校验...");

            // 1. 硬件状态检查与准备
            var canMoveXResult = await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false);
            if (!canMoveXResult.IsSuccess)
            {
                _logger.Error($"[{StationName}] 断点续跑前X轴运动条件检查失败：{canMoveXResult.ErrorMessage}");
                TriggerAlarm(canMoveXResult.ErrorCode, "断点续跑前X轴运动条件检查失败");
                return false;
            }

            CurrentStepDescription = "X轴移动到待机位...";
            if (!await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, nameof(WS1FeedingModel.XAxisPoint.待机位), token: token).ConfigureAwait(false))
            {
                _logger.Error($"[{StationName}] X轴移动到待机位失败（超时）。");
                TriggerAlarm(AlarmCodesExtensions.WS1Feeding.XAxisMoveTimeout, "X轴移动到待机位失败（超时）");
                return false;
            }

            CurrentStepDescription = "检查Z轴运动条件（寻层）...";
            if (!await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
            {
                _logger.Error($"[{StationName}] Z轴条件不满足（寻层阶段）。");
                TriggerAlarm(AlarmCodesExtensions.WS1Feeding.ZAxisPreconditionFailed, "Z轴条件不满足（寻层阶段）");
                return false;
            }

            // 2. 重新扫描并验证物料状态
            var scanResult = await _feedingModule.SearchLayerAsync(token: token).ConfigureAwait(false);
            if (!scanResult.IsSuccess || scanResult.Data.Count == 0)
            {
                _logger.Error($"[{StationName}] 断点续跑寻层失败：{scanResult.ErrorMessage}");
                TriggerAlarm(AlarmCodesExtensions.WS1Feeding.LayerScanFailed, "断点续跑寻层失败，无法确认物料状态");
                return false;
            }

            var filterResult = await _feedingModule.AnalyzeAndFilterMappingData(scanResult.Data);
            if (!filterResult.IsSuccess)
            {
                _logger.Error($"[{StationName}] 断点续跑算法过滤失败：{filterResult.ErrorMessage}");
                TriggerAlarm(AlarmCodesExtensions.WS1Feeding.AlgorithmException, "断点续跑算法过滤失败");
                return false;
            }

            // 3. 状态一致性校验与恢复
            var savedStep = (Station1FeedingStep)MemoryParam.PersistedStep;
            bool isMaterialOnTrack = await _feedingModule.IsTrackProExist(token);

            bool isStateConsistent = savedStep switch
            {
                // 物料应在轨道上，但扫描显示不在
                Station1FeedingStep.阻塞等待物料回退完成 => isMaterialOnTrack,
                // 物料可能已拉出或未拉出
                Station1FeedingStep.等待物料拉出完成 => (isMaterialOnTrack && filterResult.Data.Count == MemoryParam.TotalLayerCount) ||
                                               (!isMaterialOnTrack && filterResult.Data.Count == MemoryParam.TotalLayerCount - 1),
                // 其他情况，物料不应在轨道上
                _ => !isMaterialOnTrack
            };

            if (!isStateConsistent)
            {
                _logger.Error("物料扫描状态与上次记录的步序不一致！");
                TriggerAlarm(AlarmCodesExtensions.WS1Feeding.ResumeConsistencyFailed, "物料扫描状态与实际不一致！");
                return false;
            }

            // 4. 恢复状态并设置下一步
            RestoreStateFromMemory();

            if (!await PrepareZAxisForResume(token))
            {
                return false;
            }

            // 根据上次中断的步骤，决定从哪里继续
            if (savedStep == Station1FeedingStep.阻塞等待物料回退完成 || (savedStep == Station1FeedingStep.等待物料拉出完成 && isMaterialOnTrack))
            {
                _sync.Release(nameof(WorkstationSignals.工位1允许退料), StationName);
                _currentStep = Station1FeedingStep.阻塞等待物料回退完成;
                _resumeStep = Station1FeedingStep.阻塞等待物料回退完成;
            }
            else
            {
                _currentStep = Station1FeedingStep.判断物料可拉出条件;
                _resumeStep = Station1FeedingStep.判断物料可拉出条件;
            }

            _logger.Success($"[{StationName}] 断点续跑校验通过，将从 [{_currentStep}] 继续执行，剩余 {_totalLayerCount} 层。");
            return true;
        }

        /// <summary>
        /// 从持久化内存中恢复工站的核心状态变量。
        /// </summary>
        private void RestoreStateFromMemory()
        {
            _layersToProcess = new List<int>(MemoryParam.LayersToProcess);
            _currentLayerIndex = MemoryParam.CurrentLayerIndex;
            _totalLayerCount = MemoryParam.TotalLayerCount;
            _detectedWaferSize = MemoryParam.DetectedWaferSize;
            _rawMappingData = new(); // 扫描数据已用于验证，此处清空
        }

        /// <summary>
        /// 为断点续跑准备Z轴，移动到当前目标层。
        /// </summary>
        /// <returns>成功返回 true，失败返回 false。</returns>
        private async Task<bool> PrepareZAxisForResume(CancellationToken token)
        {
            CurrentStepDescription = $"检查Z轴运动条件（第{_currentLayerIndex + 1}层）...";
            if (!await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
            {
                _logger.Error($"[{StationName}] Z轴条件不满足（取料定位阶段）。");
                TriggerAlarm(AlarmCodesExtensions.WS1Feeding.ZAxisPreconditionFailed, $"[{StationName}] Z轴条件不满足（取料定位阶段）。");
                return false;
            }

            CurrentStepDescription = $"Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层...";
            var switchToLayerRes = await _feedingModule.SwitchToLayerAsync(_layersToProcess[_currentLayerIndex], token);
            if (!switchToLayerRes.IsSuccess)
            {
                _logger.Error($"[{StationName}] Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层异常");
                TriggerAlarm(switchToLayerRes.ErrorCode, switchToLayerRes.ErrorMessage);
                return false;
            }
            return true;
        }
        /// <summary>
        /// 异常清除断点继续
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task ExecuteResetAsync(CancellationToken token)
        {
            Fire(MachineTrigger.Reset);
            try
            {
                token.ThrowIfCancellationRequested(); // 【新增】
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑机制，将恢复至步序：[{_currentStep}]）...");
                _hardwareInputMonitor?.SetSafetyDoorEnabled(nameof(E_InPutName.电磁门锁1_2信号), true);

                try
                {
                    token.ThrowIfCancellationRequested(); // 【新增】
                    if (_feedingModule != null && !await _feedingModule.ResetAsync(token))
                        throw new Exception("硬件模组复位失败");
                }
                catch (OperationCanceledException) // 【新增】
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.Error($"[{StationName}] 模组复位失败 : {ex.Message}");
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
            catch (OperationCanceledException) // 【新增】
            {
                _logger.Warn($"[{StationName}] 复位操作已被外部取消。");
                throw;
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 复位失败: {ex.Message}");
                Fire(MachineTrigger.Error);
                throw;
            }
        }
        /// <summary>
        /// 物理停止
        /// </summary>
        /// <returns></returns>
        protected override async Task OnPhysicalStopAsync()
        {
            _hardwareInputMonitor?.SetSafetyDoorEnabled(nameof(E_InPutName.电磁门锁1_2信号), false);
            if (_feedingModule != null)
                await _feedingModule.StopAsync().ConfigureAwait(false);
        }
        /// <summary>
        /// 获取模组列表
        /// </summary>
        /// <returns></returns>
        protected override IEnumerable<PF.Infrastructure.Mechanisms.BaseMechanism> GetMechanisms()
        {
            if (_feedingModule != null) yield return _feedingModule;
            if (_dataModule != null) yield return _dataModule;
        }
        /// <summary>
        /// 空跑
        /// </summary>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        protected override Task ProcessDryRunLoopAsync(CancellationToken token) => throw new NotImplementedException();

        #endregion

        #region Main State Machine Loop (主业务循环)
        /// <summary>
        /// 正常模式业务循环 - 工位1上下料工站 (重构标准版)
        /// </summary>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 【核心校验】每一轮步序流转前强行校验取消状态，确保流程在停止请求下秒停
                    token.ThrowIfCancellationRequested();

                    switch (_currentStep)
                    {
                        // ══════════════════════════════════════════════════════════
                        //  阶段 A：运动前准备 (流程初始化与料盒识别)
                        // ══════════════════════════════════════════════════════════
                        #region Phase A：运动前准备

                        case Station1FeedingStep.等待按下工位1启动按钮:
                            CurrentStepDescription = "等待按下工位1启动按钮...";
                            _logger.Info($"[{StationName}] 等待操作员按下工位1启动按钮...");

                            // 阻塞等待外部信号触发
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位1启动按钮按下), token).ConfigureAwait(false);

                            _logger.Info($"[{StationName}] 检测到启动信号，开始执行上料流程。");
                            _currentStep = Station1FeedingStep.验证当前批次产品个数;
                            break;

                        case Station1FeedingStep.验证当前批次产品个数:
                            CurrentStepDescription = "验证当前批次产品个数...";
                            // 业务校验：防止空批次启动
                            if (_dataModule.Station1MesDetectionData.Quantity != 0)
                            {
                                _logger.Info($"[{StationName}] 批次产品个数验证通过：{_dataModule.Station1MesDetectionData.Quantity}。");
                                _currentStep = Station1FeedingStep.获取工位1配方参数;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 批次产品个数为0，无法启动生产。");
                                RouteToError(Station1FeedingStep.批次产品个数不正确, Station1FeedingStep.等待按下工位1启动按钮);
                            }
                            break;

                        case Station1FeedingStep.获取工位1配方参数:
                            CurrentStepDescription = "获取工位1配方参数...";
                            _cachedRecipe = _dataModule.Station1ReciepParam;
                            if (_cachedRecipe != null)
                            {
                                _logger.Info($"[{StationName}] 配方获取成功：[{_cachedRecipe.RecipeName}] 尺寸：{_cachedRecipe.WafeSize}。");
                                _currentStep = Station1FeedingStep.识别料盒尺寸;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 工位1配方参数为空，请确认配方是否已下发。");
                                RouteToError(Station1FeedingStep.工位1配方获取失败, Station1FeedingStep.等待按下工位1启动按钮);
                            }
                            break;

                        case Station1FeedingStep.识别料盒尺寸:
                            CurrentStepDescription = "识别料盒尺寸...";
                            // 调用底层传感器组合识别逻辑 (内部裸跑，异常上弹)
                            var sizeResult = await _feedingModule.GetWaferBoxSizeAsync(token).ConfigureAwait(false);
                            if (sizeResult.IsSuccess)
                            {
                                _detectedWaferSize = sizeResult.Data;
                                _logger.Info($"[{StationName}] 料盒尺寸识别成功：{_detectedWaferSize}。");
                                _currentStep = Station1FeedingStep.验证尺寸与配方是否匹配;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 料盒尺寸识别失败：{sizeResult.ErrorMessage}");
                                var errStep = sizeResult.ErrorCode switch
                                {
                                    AlarmCodesExtensions.WS1Feeding.BoxSizeConflict => Station1FeedingStep.料盒尺寸传感器信号冲突,
                                    AlarmCodesExtensions.WS1Feeding.BoxBaseNotDetected => Station1FeedingStep.料盒公用底座未检测到物体,
                                    AlarmCodesExtensions.WS1Feeding.Wafer8InchReversed => Station1FeedingStep.八寸晶圆放反,
                                    AlarmCodesExtensions.WS1Feeding.Wafer12InchReversed => Station1FeedingStep.十二寸晶圆放反,
                                    _ => Station1FeedingStep.料盒尺寸识别失败
                                };
                                RouteToError(errStep, Station1FeedingStep.识别料盒尺寸, sizeResult.ErrorCode);
                            }
                            break;

                        case Station1FeedingStep.验证尺寸与配方是否匹配:
                            CurrentStepDescription = "验证料盒尺寸与配方是否匹配...";
                            if (_detectedWaferSize == _cachedRecipe.WafeSize)
                            {
                                _logger.Info($"[{StationName}] 料盒尺寸与配方匹配（{_detectedWaferSize}）。");
                                _currentStep = Station1FeedingStep.切换物料尺寸;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 尺寸不匹配：实际={_detectedWaferSize}，配方={_cachedRecipe.WafeSize}。");
                                RouteToError(Station1FeedingStep.料盒尺寸与配方不匹配, Station1FeedingStep.等待按下工位1启动按钮);
                            }
                            break;

                        case Station1FeedingStep.切换物料尺寸:
                            CurrentStepDescription = "切换物料尺寸...";
                            // 调整硬件状态（如调宽机构）
                            var switchResult = await _feedingModule.SwitchProductionStateAsync(_cachedRecipe.WafeSize, token).ConfigureAwait(false);
                            if (switchResult.IsSuccess)
                            {
                                _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_开始;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 切换物料尺寸失败：{switchResult.ErrorMessage}");
                                RouteToError(Station1FeedingStep.料盒尺寸与配方不匹配, Station1FeedingStep.等待按下工位1启动按钮, switchResult.ErrorCode);
                            }
                            break;

                        case Station1FeedingStep.判断X轴是否具备运动条件_开始:
                            CurrentStepDescription = "检查X轴运动条件（开始）...";
                            var canMoveXResult = await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false);
                            if (canMoveXResult.IsSuccess)
                            {
                                _currentStep = Station1FeedingStep.X轴到待机位;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] X轴运动条件不满足：{canMoveXResult.ErrorMessage}");
                                var errStep = canMoveXResult.ErrorCode switch
                                {
                                    AlarmCodesExtensions.WS1Feeding.XAxisTabDetected => Station1FeedingStep.X轴互锁失败_存在铁环突片,
                                    AlarmCodesExtensions.WS1Feeding.PullOutLeverNotOpen => Station1FeedingStep.拉料互锁失败_挡杆未打开,
                                    _ => Station1FeedingStep.X轴运动条件不满足
                                };
                                RouteToError(errStep, Station1FeedingStep.判断X轴是否具备运动条件_开始, canMoveXResult.ErrorCode);
                            }
                            break;

                        case Station1FeedingStep.X轴到待机位:
                            CurrentStepDescription = "X轴移动到待机位...";
                            if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, nameof(WS1FeedingModel.XAxisPoint.待机位), token: token).ConfigureAwait(false))
                            {
                                _logger.Info($"[{StationName}] X轴已到达待机位。");
                                _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_寻层;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] X轴移动到待机位失败（超时）。");
                                RouteToError(Station1FeedingStep.X轴运动超时, Station1FeedingStep.X轴到待机位);
                            }
                            break;

                        case Station1FeedingStep.判断Z轴是否具备运动条件_寻层:
                            CurrentStepDescription = "检查Z轴运动条件（寻层）...";
                            if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                            {
                                _currentStep = Station1FeedingStep.Z轴扫描寻层;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] Z轴条件不满足（寻层阶段）。");
                                RouteToError(Station1FeedingStep.Z轴运动条件不满足, Station1FeedingStep.判断Z轴是否具备运动条件_寻层);
                            }
                            break;

                        case Station1FeedingStep.Z轴扫描寻层:
                            CurrentStepDescription = "Z轴扫描寻层...";
                            // 调用底层 Mapping 模组
                            var scanResult = await _feedingModule.SearchLayerAsync(token: token).ConfigureAwait(false);
                            if (scanResult.IsSuccess && scanResult.Data.Count > 0)
                            {
                                _rawMappingData = scanResult.Data;
                                _currentStep = Station1FeedingStep.算法过滤层数;
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 寻层异常：{(scanResult.IsSuccess ? "结果为0层" : scanResult.ErrorMessage)}");
                                RouteToError(Station1FeedingStep.Z轴寻层扫描异常, Station1FeedingStep.判断Z轴是否具备运动条件_寻层);
                            }
                            break;

                        case Station1FeedingStep.算法过滤层数:
                            CurrentStepDescription = "算法过滤与防呆验证...";
                            var filterResult = await _feedingModule.AnalyzeAndFilterMappingData(_rawMappingData);
                            if (filterResult.IsSuccess)
                            {
                                _layersToProcess = new List<int>(filterResult.Data.Keys.OrderBy(layerIndex => layerIndex));
                                _totalLayerCount = _layersToProcess.Count;
                                _currentLayerIndex = 0;

                                if (_layersToProcess.Count == 0)
                                {
                                    _logger.Warn($"[{StationName}] 过滤结果为空，料盒内未检测到有效晶圆！");
                                    RouteToError(Station1FeedingStep.寻层算法空值判定, Station1FeedingStep.等待按下工位1启动按钮);
                                }
                                else
                                {
                                    _logger.Info($"[{StationName}] 过滤完成，共识别 {_totalLayerCount} 片。");
                                    MemoryParam.IsInProgress = true;
                                    MemoryParam.CurrentLayerIndex = 0;
                                    MemoryParam.LayersToProcess = new List<int>(_layersToProcess);
                                    MemoryParam.TotalLayerCount = _totalLayerCount;
                                    MemoryParam.DetectedWaferSize = _detectedWaferSize;
                                    MemoryParam.PersistedStep = (int)Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                                    FlushMemory();
                                    _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                                }
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 寻层算法过滤异常: {filterResult.ErrorMessage}");
                                RouteToError(Station1FeedingStep.寻层算法过滤异常, Station1FeedingStep.等待按下工位1启动按钮);
                            }
                            break;

                        #endregion

                        // ══════════════════════════════════════════════════════════
                        //  阶段 B：取料循环流转 (核心业务闭环)
                        // ══════════════════════════════════════════════════════════
                        #region Phase B：取料循环流转

                        case Station1FeedingStep.判断Z轴是否具备运动条件_取料定位:
                            CurrentStepDescription = $"检查Z轴运动条件（第{_currentLayerIndex + 1}层）...";
                            if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                            {
                                _currentStep = Station1FeedingStep.切换到指定层;
                                MemoryParam.PersistedStep = (int)Station1FeedingStep.切换到指定层;
                                FlushMemory();
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] Z轴条件不满足（取料定位阶段）。");
                                RouteToError(Station1FeedingStep.Z轴运动条件不满足, Station1FeedingStep.判断Z轴是否具备运动条件_取料定位);
                            }
                            break;

                        case Station1FeedingStep.切换到指定层:
                            CurrentStepDescription = $"Z轴切换到第{_layersToProcess[_currentLayerIndex] + 1}层...";
                            var switchToLayerres = await _feedingModule.SwitchToLayerAsync(_layersToProcess[_currentLayerIndex], token);
                            if (switchToLayerres.IsSuccess)
                            {
                                MemoryParam.CurrentLayerIndex = _currentLayerIndex;
                                MemoryParam.PersistedStep = (int)Station1FeedingStep.判断物料可拉出条件;
                                FlushMemory();
                                _currentStep = Station1FeedingStep.判断物料可拉出条件;
                            }
                            else
                            {
                                _cachedErrorCode = switchToLayerres.ErrorCode;
                                RouteToError(Station1FeedingStep.Z轴运动超时, Station1FeedingStep.切换到指定层);
                            }
                            break;

                        case Station1FeedingStep.判断物料可拉出条件:
                            CurrentStepDescription = "判断物料可拉出条件...";
                            if (await _feedingModule.CanPullOutMaterialAsync(token).ConfigureAwait(false))
                            {
                                // 关键握手：通知拉料工站可以拉料
                                _sync.Release(nameof(WorkstationSignals.工位1允许拉料), StationName);
                                _currentStep = Station1FeedingStep.等待物料拉出完成;
                                MemoryParam.PersistedStep = (int)Station1FeedingStep.等待物料拉出完成;
                                FlushMemory();
                            }
                            else
                            {
                                _logger.Error($"[{StationName}] 第{_layersToProcess[_currentLayerIndex] + 1}层物料错层翘起，禁止拉料。");
                                RouteToError(Station1FeedingStep.检测到物料错层, Station1FeedingStep.判断Z轴是否具备运动条件_取料定位);
                            }
                            break;

                        case Station1FeedingStep.等待物料拉出完成:
                            CurrentStepDescription = "等待物料拉出完成...";
                            // 等待拉料工站反馈 Y 轴已拉出至安全位
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位1拉料完成), token, scope: E_WorkStation.工位1拉料工站.ToString()).ConfigureAwait(false);

                            _currentStep = Station1FeedingStep.阻塞等待物料回退完成;
                            // 发放退料通行证
                            _sync.Release(nameof(WorkstationSignals.工位1允许退料), StationName);
                            MemoryParam.PersistedStep = (int)Station1FeedingStep.阻塞等待物料回退完成;
                            FlushMemory();
                            break;

                        case Station1FeedingStep.阻塞等待物料回退完成:
                            CurrentStepDescription = "阻塞等待物料回退完成...";
                            // 等待拉料工站反馈 Y 轴已完全退回
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位1退料完成), token, scope: E_WorkStation.工位1拉料工站.ToString()).ConfigureAwait(false);

                            MemoryParam.CurrentLayerIndex = _currentLayerIndex;
                            MemoryParam.PersistedStep = (int)Station1FeedingStep.计算下一层位置;
                            FlushMemory();
                            _currentStep = Station1FeedingStep.计算下一层位置;
                            break;

                        case Station1FeedingStep.计算下一层位置:
                            CurrentStepDescription = "计算下一层位置...";
                            _currentLayerIndex++;
                            if (_currentLayerIndex >= _layersToProcess.Count)
                            {
                                _logger.Info($"[{StationName}] 所有层取料完毕。");
                                _currentStep = Station1FeedingStep.物料全部生产完毕;
                            }
                            else
                            {
                                // 继续取下一片
                                _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_取料定位;
                            }
                            break;

                        #endregion

                        // ══════════════════════════════════════════════════════════
                        //  阶段 C：生产结束与安全收尾
                        // ══════════════════════════════════════════════════════════
                        #region Phase C：生产结束与安全收尾

                        case Station1FeedingStep.物料全部生产完毕:
                            CurrentStepDescription = "物料全部生产完毕，更新状态...";
                            _logger.Success($"[{StationName}] 晶圆上料闭环完毕。");
                            _currentStep = Station1FeedingStep.判断X轴是否具备运动条件_结束;
                            MemoryParam.IsInProgress = false;
                            MemoryParam.CurrentLayerIndex = 0;
                            MemoryParam.LayersToProcess = new();
                            MemoryParam.TotalLayerCount = 0;
                            MemoryParam.PersistedStep = (int)Station1FeedingStep.等待按下工位1启动按钮;
                            FlushMemory();

                            break;

                        case Station1FeedingStep.判断X轴是否具备运动条件_结束:
                            CurrentStepDescription = "检查X轴运动条件（结束阶段）...";
                            if (await _feedingModule.CanMoveXAxesAsync(token).ConfigureAwait(false))
                                _currentStep = Station1FeedingStep.X轴到挡料位;
                            else
                                RouteToError(Station1FeedingStep.X轴运动条件不满足, Station1FeedingStep.判断X轴是否具备运动条件_结束);
                            break;

                        case Station1FeedingStep.X轴到挡料位:
                            CurrentStepDescription = "X轴移动到挡料位...";
                            if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.XAxis, nameof(WS1FeedingModel.XAxisPoint.挡料位), token: token).ConfigureAwait(false))
                                _currentStep = Station1FeedingStep.判断Z轴是否具备运动条件_流程结束;
                            else
                                RouteToError(Station1FeedingStep.X轴运动超时, Station1FeedingStep.X轴到挡料位);
                            break;

                        case Station1FeedingStep.判断Z轴是否具备运动条件_流程结束:
                            CurrentStepDescription = "检查Z轴运动条件（流程结束阶段）...";
                            if (await _feedingModule.CanMoveZAxesAsync(token).ConfigureAwait(false))
                                _currentStep = Station1FeedingStep.Z轴到待机位;
                            else
                                RouteToError(Station1FeedingStep.Z轴运动条件不满足, Station1FeedingStep.判断Z轴是否具备运动条件_流程结束);
                            break;

                        case Station1FeedingStep.Z轴到待机位:
                            CurrentStepDescription = "Z轴退回待机位...";
                            if (await _feedingModule.MoveToPointAndWaitAsync(_feedingModule.ZAxis, nameof(WS1FeedingModel.ZAxisPoint.待机位), token: token).ConfigureAwait(false))
                                _currentStep = Station1FeedingStep.通知操作员下料;
                            else
                                RouteToError(Station1FeedingStep.Z轴运动超时, Station1FeedingStep.Z轴到待机位);
                            break;

                        case Station1FeedingStep.通知操作员下料:
                            CurrentStepDescription = "通知操作员下料，等待确认...";
                            // 等待人工确认完成下料
                            await _sync.WaitAsync(nameof(WorkstationSignals.工位1人工下料完成), token).ConfigureAwait(false);
                            _currentStep = Station1FeedingStep.生产完毕;
                            break;

                        case Station1FeedingStep.生产完毕:
                            CurrentStepDescription = "本批次生产完毕，清理缓存数据...";
                            _cachedRecipe = null;
                            _layersToProcess = new();
                            _currentLayerIndex = 0;
                            _totalLayerCount = 0;


                            _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                            break;

                        #endregion

                        // ══════════════════════════════════════════════════════════
                        //  阶段 D：业务异常流转 (Logic-Driven Exceptions)
                        // ══════════════════════════════════════════════════════════
                        #region Phase D：异常拦截逻辑分发

                        case Station1FeedingStep.批次产品个数不正确:
                            TriggerAlarm(AlarmCodesExtensions.WS1Feeding.BatchCountZero, "批次产品个数为0");
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.料盒尺寸与配方不匹配:
                            TriggerAlarm(_cachedErrorCode ?? AlarmCodesExtensions.WS1Feeding.WaferSizeMismatch, "尺寸与配方不匹配");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.工位1配方获取失败:
                            TriggerAlarm(AlarmCodesExtensions.WS1Feeding.RecipeNull, "配方参数为空");
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.寻层算法空值判定:
                            TriggerAlarm(AlarmCodesExtensions.WS1Feeding.AlgorithmZeroLayers, "算法判定为0层");
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.料盒尺寸识别失败:
                        case Station1FeedingStep.料盒尺寸传感器信号冲突:
                        case Station1FeedingStep.料盒公用底座未检测到物体:
                        case Station1FeedingStep.八寸晶圆放反:
                        case Station1FeedingStep.十二寸晶圆放反:
                            var sizeErrCode = _cachedErrorCode ?? AlarmCodesExtensions.WS1Feeding.SizeDetectionSensorFailed;
                            TriggerAlarm(sizeErrCode, $"识别或防呆异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.X轴运动条件不满足:
                        case Station1FeedingStep.X轴互锁失败_存在铁环突片:
                        case Station1FeedingStep.拉料互锁失败_挡杆未打开:
                            var xErrCode = _cachedErrorCode ?? AlarmCodesExtensions.WS1Feeding.XAxisPreconditionFailed;
                            TriggerAlarm(xErrCode, $"X轴互锁异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.Z轴运动条件不满足:
                        case Station1FeedingStep.Z轴互锁失败_料盒未到位:
                            var zErrCode = _cachedErrorCode ?? AlarmCodesExtensions.WS1Feeding.ZAxisPreconditionFailed;
                            TriggerAlarm(zErrCode, $"Z轴互锁异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.X轴运动超时:
                            TriggerAlarm(AlarmCodesExtensions.WS1Feeding.XAxisMoveTimeout, "X轴运动超时");
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.Z轴运动超时:
                        case Station1FeedingStep.Z轴切换层运动失败:
                            var zTimeOutCode = _cachedErrorCode ?? AlarmCodesExtensions.WS1Feeding.ZAxisMoveTimeout;
                            TriggerAlarm(zTimeOutCode, $"Z轴运动异常: {_currentStep}");
                            _cachedErrorCode = null;
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.Z轴寻层扫描异常:
                            TriggerAlarm(AlarmCodesExtensions.WS1Feeding.LayerScanFailed, "Mapping寻层异常");
                            _currentStep = _resumeStep;
                            break;

                        case Station1FeedingStep.检测到物料错层:
                            TriggerAlarm(AlarmCodesExtensions.WS1Feeding.MaterialTiltedMisaligned, $"物料错层 (第 {_currentLayerIndex + 1} 层)");
                            _currentStep = _resumeStep;
                            break;

                        default:
                            if ((int)_currentStep >= 100000)
                            {
                                TriggerAlarm(AlarmCodes.System.UndefinedStep, $"遇到未定义的业务异常步序: {_currentStep}");
                                _currentStep = (int)_resumeStep != 0 ? _resumeStep : Station1FeedingStep.等待按下工位1启动按钮;
                            }
                            else
                            {
                                TriggerAlarm(AlarmCodes.System.UndefinedStep, $"状态机指针漂移，步序[{_currentStep}]未定义");
                                _currentStep = Station1FeedingStep.等待按下工位1启动按钮;
                            }
                            break;

                            #endregion
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 专门捕获取消信号，确保工站任务优雅退出
                _logger.Warn($"[{StationName}] 接收到取消信号，任务正常退出。退出时状态: {_currentStep}");
                throw;
            }
            catch (Exception ex)
            {
                // 捕获所有由于硬件通讯故障、底层驱动崩溃等引发的非预期异常
                _logger.Fatal($"[{StationName}] 业务大循环异常崩溃！步序快照: {_currentStep} ({CurrentStepDescription}), 错误: {ex.Message}");
                throw; // 必须上抛，以便底层基类框架接管急停或报警流程
            }
        }

        #endregion
    }
}