using PF.Core.Attributes;
using PF.Core.Constants;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Hardware.Motor.Basic;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Ioc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Stations
{
    /// <summary>
    /// 【OCR视觉检测工站】业务流转控制器 (Detection Station Controller)
    /// 
    /// <para>架构定位：</para>
    /// 继承自 <see cref="StationBase{T}"/>。这是整个机台中唯一一个跨工位的**公共/共享工站**。
    /// 负责调度 <see cref="WorkStationDetectionModule"/>，在工位1和工位2的拉料工站发出检测请求时，
    /// 驱动龙门机构前往对应工位拍照解码，并通过 <see cref="WorkStationDataModule"/> 写入 MES 对比结果。
    /// </summary>
    [StationUI("OCR检测工站", "WorkStationDetectionStationDebugView", order: 3)]
    public class WorkStationDetectionStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {
        #region Fields & Dependencies (依赖服务与缓存字段)

        private readonly WorkStationDetectionModule? _detectionModule;
        private readonly WorkStationDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        /// <summary>
        /// 状态机当前执行的业务步序指针
        /// </summary>
        private StationDetectionStep _currentStep = StationDetectionStep.等待工位1或工位2允许检测;

        /// <summary>
        /// 记录当前正在服务（抢占成功）的工位标识
        /// </summary>
        private E_WorkSpace _currentworkSpace = E_WorkSpace.工位1;

        // ── 跨步序流转的缓存字段 ──
        private OCRRecipeParam? _cachedRecipe;

        /// <summary>
        /// 缓存相机执行结果。Item1: OCR 解码文本串；Item2: 原图物理路径
        /// </summary>
        private (string, string) _cachedOcrResult;

        private MachineDetectionData? _cachedDetectionData;

        #endregion

        #region State Machine Enums (业务步序枚举)

        /// <summary>
        /// 定义检测工站的完整生命周期与断点续跑异常状态节点
        /// </summary>
        public enum StationDetectionStep
        {
            #region 正常流程 (0 - 80)

            等待工位1或工位2允许检测 = 0,
            去工位1检测位置 = 10,
            去工位2检测位置 = 20,
            触发检测 = 30,

            检测完成Z轴回安全位 = 40,

            数据比对 = 50,
            写入检测数据 = 60,
            检测完成后避位 = 70,
            检测完成 = 80,

            #endregion

            #region 异常流程 (100000+)

            去工位检测位置异常 = 100001,
            触发检测异常 = 100002,
            写入检测数据异常 = 100003,
            检测完成Z轴回安全位异常 = 100004,

            #endregion
        }

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        public WorkStationDetectionStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger)
            : base(nameof(E_WorkStation.OCR检测工站), logger)
        {
            _detectionModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDetectionModule)) as WorkStationDetectionModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
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
                if (!await _detectionModule.MoveInitial(token))
                {
                    _logger.Error($"[{StationName}] 初始化失败，模组移动至待机避让位失败。");
                    Fire(MachineTrigger.Error);
                }
                else
                {
                    _logger.Success($"[{StationName}] 初始化完成，就绪。");
                    Fire(MachineTrigger.InitializeDone); // Initializing → Idle
                }
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
                _logger.Info($"[{StationName}] 正在执行工站复位清警（断点续跑，将恢复至步序：[{_currentStep}]）...");

                if (_detectionModule != null)
                    await _detectionModule.ResetAsync(token);

                // 仅初始化报警复位时重置信号量；运行期报警复位保留信号量以支持断点续跑
                if (CameFromInitAlarm)
                    _sync.ResetScope(StationName);

                _logger.Success($"[{StationName}] 复位完成，将从步序 [{_currentStep}] 继续执行。");
                await FireAsync(ResetCompletionTrigger);  // Resetting → Idle 或 Uninitialized
            }
            catch (Exception ex)
            {
                _logger.Error($"[{StationName}] 复位失败: {ex.Message}");
                Fire(MachineTrigger.Error);
                throw;
            }
        }

        protected override async Task OnPhysicalStopAsync()
        {
            if (_detectionModule != null)
                await _detectionModule.StopAsync().ConfigureAwait(false);
        }

        protected override IEnumerable<PF.Infrastructure.Mechanisms.BaseMechanism> GetMechanisms()
        {
            if (_detectionModule != null) yield return _detectionModule;
        }

        protected override Task ProcessDryRunLoopAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        #endregion

        #region Main State Machine Loop (主业务循环)

        protected override async Task ProcessNormalLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                switch (_currentStep)
                {
                    // ══════════════════════════════════════════════════════════
                    //  阶段 A：工位任务竞争 (共享资源的抢占调度)
                    // ══════════════════════════════════════════════════════════
                    #region Phase A (Task Competition)

                    case StationDetectionStep.等待工位1或工位2允许检测:
                        CurrentStepDescription = "等待工位1或工位2发起的检测请求...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
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

                            // 异常安全网：若竞争过程发生底层故障
                            if (doneTask.IsFaulted || doneTask.IsCanceled)
                            {
                                _logger.Error($"[{StationName}] 等待任务池异常中断。错误信息: {doneTask.Exception?.InnerException?.Message}");
                                _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
                                TriggerAlarm(AlarmCodesExtensions.Process.StationSignalTimeout, "等待工位检测信号任务池异常中断");
                                break;
                            }

                            // 任务正常结束：判定究竟是哪个工站赢得了竞争
                            if (doneTask == task1)
                            {
                                // 💡 必须取消未胜出的 task2，释放其底层的定时器资源，且避免其在未来意外获取工位2的信号
                                raceCts.Cancel();
                                _currentworkSpace = E_WorkSpace.工位1;
                                _logger.Info($"[{StationName}] 工位1 胜出，抢占视觉检测资源成功。");
                                _currentStep = StationDetectionStep.去工位1检测位置;
                            }
                            else if (doneTask == task2)
                            {
                                raceCts.Cancel();
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
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        _cachedRecipe = _dataModule?.Station1ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 工位1配方为空，无法获取目标坐标，中断移动。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                            break;
                        }

                        if (await _detectionModule.MoveToStation1(token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] OCR 龙门已就位工位1上空。");
                            _currentStep = StationDetectionStep.触发检测;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] OCR 模组移动到工位1超时。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                        }
                        break;

                    case StationDetectionStep.去工位2检测位置:
                        CurrentStepDescription = "OCR模组移动到工位2检测位置...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        _cachedRecipe = _dataModule?.Station2ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 工位2配方为空，无法获取目标坐标，中断移动。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                            break;
                        }

                        if (await _detectionModule.MoveToStation2(token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] OCR 龙门已就位工位2上空。");
                            _currentStep = StationDetectionStep.触发检测;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] OCR 模组移动到工位2超时。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                        }
                        break;

                    case StationDetectionStep.触发检测:
                        CurrentStepDescription = $"触发OCR相机拍照解码（对象：{_currentworkSpace}）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            // 发送拍照指令，且直接使用相机原始识别结果 (false 标识不在此处校验，交由后续阶段比对)
                            _cachedOcrResult = await _detectionModule.CameraTigger(false, _currentworkSpace, token: token).ConfigureAwait(false);
                            _logger.Info($"[{StationName}] 拍照解码完成，读取原始条码串：[{_cachedOcrResult.Item1}]。");

                            _currentStep = StationDetectionStep.检测完成Z轴回安全位;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] OCR相机物理触发失败或通讯异常：{ex.Message}");
                            _currentStep = StationDetectionStep.触发检测异常;
                        }
                        break;

                    case StationDetectionStep.检测完成Z轴回安全位:
                        CurrentStepDescription = $"提升 Z 轴脱离干涉区（{_currentworkSpace}）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        // 为了允许拉料工站尽早退料，必须先将视觉 Z 轴抬高，解除空间干涉
                        if (!await _detectionModule.MoveZSafePos(token))
                        {
                            _logger.Error($"[{StationName}] 提升相机避位失败，禁止继续，防止撞机！");
                            _currentStep = StationDetectionStep.检测完成Z轴回安全位异常;
                        }
                        else
                        {
                            _currentStep = StationDetectionStep.数据比对;
                        }
                        break;

                    case StationDetectionStep.数据比对:
                        CurrentStepDescription = "OCR数据与MES工单数据交叉比对...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        // 请求数据中枢验证当前字符串是否在 MES 允许名单内
                        var kk = await _dataModule.CheckOcrTextAsync(_currentworkSpace, _cachedOcrResult.Item1, token).ConfigureAwait(false);
                        string path = string.Empty;

                        // [图像存根]
                        if (!kk.Item1)
                        {
                            // 验证 NG：生成缺陷存档图
                            path = await _detectionModule.SaveImage(_cachedOcrResult.Item2, _currentworkSpace, new WaferInfo() { CustomerBatch = "Error", WaferId = $"ERR_{DateTime.Now:HHmmss}" }, token);
                        }
                        else
                        {
                            // 验证 OK：按批次/槽位号正规存档
                            path = await _detectionModule.SaveImage(_cachedOcrResult.Item2, _currentworkSpace, kk.Item2, token);
                        }

                        // 装配用于写入数据库与推给 MES 的单片检测快照实体
                        _cachedDetectionData = new MachineDetectionData()
                        {
                            CustomerBatch = kk.Item2?.CustomerBatch ?? "ERROR",
                            WaferId = kk.Item2?.WaferId ?? "ERROR",
                            InternalBatchId = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.InternalBatchId : _dataModule.Station2MesDetectionData.InternalBatchId,
                            Barcode1 = "CODE1",
                            Barcode2 = "CODE2",
                            Barcode3 = "CODE3",
                            IsMatch = kk.Item1,
                            ErrorMessage = kk.Item1 ? "NONE" : "OCR结果与MES工单不匹配",
                            ProductModel = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.ProductModel : _dataModule.Station2MesDetectionData.ProductModel,
                            OperatorId = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.OperatorId : _dataModule.Station2MesDetectionData.OperatorId,
                            RecipeName = _currentworkSpace == E_WorkSpace.工位1 ? _dataModule.Station1MesDetectionData.RecipeName : _dataModule.Station2MesDetectionData.RecipeName,
                            ImagePath = path
                        };


                        //var mesData = _currentworkSpace == E_WorkSpace.工位1
                        //    ? _dataModule?.Station1MesDetectionData
                        //    : _dataModule?.Station2MesDetectionData;

                        //_cachedDetectionData = new MachineDetectionData
                        //{
                        //    InternalBatchId = mesData?.InternalBatchId ?? "",
                        //    OcrText = _cachedOcrResult.Item1,
                        //    Barcode1 = _cachedOcrResult.Item1,
                        //    ProductModel = mesData?.ProductModel ?? "",
                        //    OperatorId = mesData?.OperatorId ?? "",
                        //    RecipeName = _cachedRecipe?.RecipeName ?? "",
                        //};

                        //// 用配方中的 GuestStartIndex/GuestLength 提取客批片号子串进行比对
                        //bool isMatch = false;
                        //string errorMsg = "NONE";
                        //try
                        //{
                        //    if (_cachedRecipe != null
                        //        && !string.IsNullOrEmpty(_cachedOcrResult.Item1)
                        //        && mesData?.CustomerWafers != null
                        //        && mesData.CustomerWafers.Count > 0)
                        //    {
                        //        int startIdx = _cachedRecipe.GuestStartIndex;
                        //        int length = _cachedRecipe.GuestLength;

                        //        if (startIdx >= 0 && length > 0 && _cachedOcrResult.Item1.Length >= startIdx + length)
                        //        {
                        //            string extractedId = _cachedOcrResult.Item1.Substring(startIdx, length);
                        //            var matchedWafer = mesData.CustomerWafers
                        //                .FirstOrDefault(w => w.WaferId == extractedId);

                        //            if (matchedWafer != null)
                        //            {
                        //                isMatch = true;
                        //                _cachedDetectionData.CustomerBatch = matchedWafer.CustomerBatch;
                        //                _cachedDetectionData.WaferId = matchedWafer.WaferId;
                        //                _logger.Info($"[{StationName}] 数据比对成功：WaferId=[{extractedId}]，客批=[{matchedWafer.CustomerBatch}]。");
                        //            }
                        //            else
                        //            {
                        //                errorMsg = $"OCR提取片号[{extractedId}]在MES批次中未找到匹配项";
                        //                _logger.Warn($"[{StationName}] 数据比对不匹配：{errorMsg}");
                        //            }
                        //        }
                        //        else
                        //        {
                        //            errorMsg = $"OCR结果长度[{_cachedOcrResult.Item1.Length}]不足以提取配方指定子串（起始={startIdx}，长度={length}）";
                        //            _logger.Warn($"[{StationName}] 数据比对跳过：{errorMsg}");
                        //        }
                        //    }
                        //    else
                        //    {
                        //        errorMsg = "配方或MES数据为空，跳过比对";
                        //        _logger.Warn($"[{StationName}] {errorMsg}");
                        //    }
                        //}
                        //catch (Exception ex)
                        //{
                        //    errorMsg = $"比对过程异常：{ex.Message}";
                        //    _logger.Error($"[{StationName}] {errorMsg}");
                        //}

                        //_cachedDetectionData.IsMatch = isMatch;
                        //_cachedDetectionData.ErrorMessage = errorMsg;

                        _currentStep = StationDetectionStep.写入检测数据;
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 C：结果入库与交接
                    // ══════════════════════════════════════════════════════════
                    #region Phase C (Archive & Handover)

                    case StationDetectionStep.写入检测数据:
                        CurrentStepDescription = "将检测结果写入持久化存储...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            if (_cachedDetectionData != null && _dataModule != null)
                            {
                                await _dataModule.AddMachineDetectionAsync(_currentworkSpace, _cachedDetectionData).ConfigureAwait(false);
                                _logger.Info($"[{StationName}] 检测数据已推入中枢（{_currentworkSpace}），匹配结果：{_cachedDetectionData.IsMatch}。");
                            }
                            _currentStep = StationDetectionStep.检测完成;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] 数据库写入异常：{ex.Message}");
                            _currentStep = StationDetectionStep.写入检测数据异常;
                        }
                        break;

                    case StationDetectionStep.检测完成:
                        CurrentStepDescription = "业务闭环，释放通行证...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

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
                        _cachedOcrResult = ("", "");
                        _cachedDetectionData = null;

                        _currentStep = StationDetectionStep.检测完成后避位;
                        break;

                    case StationDetectionStep.检测完成后避位:
                        CurrentStepDescription = "龙门退回全局待机原点...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        // 确保整个大龙门机构退回最高最深处，不挡任何人的道
                        if (!await _detectionModule.MoveInitial(token))
                        {
                            _logger.Error($"[{StationName}] 归位途中遭遇干涉或超时。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                        }
                        else
                        {
                            // 完美收官，回到首个节点继续轮询等待
                            _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
                        }
                        break;

                    #endregion

                    // ══════════════════════════════════════════════════════════
                    //  阶段 D：异常拦截与断点续跑处理
                    // ══════════════════════════════════════════════════════════
                    #region Phase D (Exceptions)

                    case StationDetectionStep.去工位检测位置异常:
                        _logger.Error($"[{StationName}] 龙门模组移动到 {_currentworkSpace} 失败（可能系配方丢档或伺服报警）。请复位，将原路重试定位。");
                        _currentStep = _currentworkSpace == E_WorkSpace.工位1
                            ? StationDetectionStep.去工位1检测位置
                            : StationDetectionStep.去工位2检测位置;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMotionFailed, $"龙门模组移动到{_currentworkSpace}检测位置失败");
                        break;

                    case StationDetectionStep.触发检测异常:
                        _logger.Error($"[{StationName}] 相机握手失败，光源或相机可能掉线。请复位，将重新尝试发指令。");
                        _currentStep = StationDetectionStep.触发检测;
                        TriggerAlarm(AlarmCodesExtensions.Process.CameraTriggerFailed, "相机握手失败，光源或相机可能掉线");
                        break;

                    case StationDetectionStep.检测完成Z轴回安全位异常:
                        _logger.Error($"[{StationName}] 相机 Z 轴无法抬起！为避免下发通行证后拉料机构撞击相机，已将其紧急锁死。");
                        _currentStep = StationDetectionStep.检测完成Z轴回安全位;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationMotionFailed, "相机Z轴无法抬起避位");
                        break;

                    case StationDetectionStep.写入检测数据异常:
                        _logger.Error($"[{StationName}] 本地磁盘存图或写入内存数据库失败。请复位重写，防止断档丢单。");
                        _currentStep = StationDetectionStep.写入检测数据;
                        TriggerAlarm(AlarmCodesExtensions.Process.StationDataWriteFailed, "检测数据写入失败");
                        break;

                    default:
                        _logger.Error($"[{StationName}] 状态机越界：步序 [{_currentStep}] 未定义。");
                        TriggerAlarm(AlarmCodesExtensions.Process.StationUnexpectedStep, $"状态机越界，未定义步序[{_currentStep}]");
                        break;

                        #endregion
                }
            }
        }

        #endregion
    }
}