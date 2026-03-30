using PF.Core.Attributes;
using PF.Core.Enums;
using PF.Core.Events;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Sync;
using PF.Infrastructure.Station.Basic;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;

namespace PF.WorkStation.AutoOcr.Stations
{

    [StationUI("OCR检测工站", "WorkStationDetectionStationDebugView", order: 2)]
    public class WorkStationDetectionStation<T> : StationBase<T> where T : StationMemoryBaseParam
    {
        private readonly WorkStationDetectionModule? _detectionModule;
        private readonly WorkStationDataModule? _dataModule;
        private readonly IStationSyncService _sync;

        private StationDetectionStep _currentStep = StationDetectionStep.等待工位1或工位2允许检测;

        private E_WorkSpace _currentworkSpace = E_WorkSpace.工位1;

        // ── 跨步序缓存字段 ──────────────────────────────────────────────────────
        private OCRRecipeParam? _cachedRecipe;
        private string _cachedOcrResult = "";
        private MachineDetectionData? _cachedDetectionData;

        public enum StationDetectionStep
        {
            #region 正常流程

            等待工位1或工位2允许检测 = 0,
            去工位1检测位置 = 10,
            去工位2检测位置 = 20,
            触发检测 = 30,
            数据比对 = 40,
            写入检测数据 = 50,
            检测完成 = 60,

            #endregion

            #region 异常步序

            去工位检测位置异常 = 100001,
            触发检测异常 = 100002,
            写入检测数据异常 = 100003,

            #endregion
        }

        public WorkStationDetectionStation(IContainerProvider containerProvider, IStationSyncService sync, ILogService logger) : base("OCR检测工站", logger)
        {
            _detectionModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDetectionModule)) as WorkStationDetectionModule;
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _sync = sync;

            _detectionModule.AlarmTriggered += OnMechanismAlarm;
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
                _logger.Info($"[{StationName}] 正在初始化OCR模组...");
                if (!await _detectionModule.InitializeAsync(token))
                    throw new Exception($"[{StationName}] OCR模组初始化失败！");
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
                if (_detectionModule != null)
                    await _detectionModule.ResetAsync(token);

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

        protected override async Task ProcessDryRunLoopAsync(CancellationToken token)
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
                    //  等待任意工位允许检测
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.等待工位1或工位2允许检测:
                        CurrentStepDescription = "等待工位1或工位2允许检测...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _logger.Info($"[{StationName}] 等待工位1或工位2发出允许检测信号...");

                        // 用独立的 LinkedCTS 防止任意一方先触发后另一方消耗多余信号
                        using (var cts1 = CancellationTokenSource.CreateLinkedTokenSource(token))
                        using (var cts2 = CancellationTokenSource.CreateLinkedTokenSource(token))
                        {
                            var task1 = _sync.WaitAsync(WorkstationSignals.工位1允许检测.ToString(), cts1.Token);
                            var task2 = _sync.WaitAsync(WorkstationSignals.工位2允许检测.ToString(), cts2.Token);
                            var done = await Task.WhenAny(task1, task2).ConfigureAwait(false);
                            if (done == task1)
                            {
                                cts2.Cancel();
                                _currentworkSpace = E_WorkSpace.工位1;
                                _logger.Info($"[{StationName}] 收到工位1允许检测信号，切换到工位1。");
                                _currentStep = StationDetectionStep.去工位1检测位置;
                            }
                            else
                            {
                                cts1.Cancel();
                                _currentworkSpace = E_WorkSpace.工位2;
                                _logger.Info($"[{StationName}] 收到工位2允许检测信号，切换到工位2。");
                                _currentStep = StationDetectionStep.去工位2检测位置;
                            }
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  移动到检测位置
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.去工位1检测位置:
                        CurrentStepDescription = "OCR模组移动到工位1检测位置...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _cachedRecipe = _dataModule?.Station1ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 工位1配方为空，无法移动到检测位置。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                            break;
                        }
                        if (await _detectionModule.MoveToStation1(token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] OCR模组已到达工位1检测位置。");
                            _currentStep = StationDetectionStep.触发检测;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] OCR模组移动到工位1检测位置失败。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                        }
                        break;

                    case StationDetectionStep.去工位2检测位置:
                        CurrentStepDescription = "OCR模组移动到工位2检测位置...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        _cachedRecipe = _dataModule?.Station2ReciepParam;
                        if (_cachedRecipe == null)
                        {
                            _logger.Error($"[{StationName}] 工位2配方为空，无法移动到检测位置。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                            break;
                        }
                        if (await _detectionModule.MoveToStation2(token).ConfigureAwait(false))
                        {
                            _logger.Info($"[{StationName}] OCR模组已到达工位2检测位置。");
                            _currentStep = StationDetectionStep.触发检测;
                        }
                        else
                        {
                            _logger.Error($"[{StationName}] OCR模组移动到工位2检测位置失败。");
                            _currentStep = StationDetectionStep.去工位检测位置异常;
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  触发OCR检测
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.触发检测:
                        CurrentStepDescription = $"触发OCR相机检测（{_currentworkSpace}）...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            _cachedOcrResult = await _detectionModule.CameraTigger(token).ConfigureAwait(false);
                            _logger.Info($"[{StationName}] OCR触发完成，读取结果：[{_cachedOcrResult}]。");
                            _currentStep = StationDetectionStep.数据比对;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] OCR相机触发异常：{ex.Message}");
                            _currentStep = StationDetectionStep.触发检测异常;
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  数据比对
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.数据比对:
                        CurrentStepDescription = "OCR数据与MES数据比对...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        var mesData = _currentworkSpace == E_WorkSpace.工位1
                            ? _dataModule?.Station1MesDetectionData
                            : _dataModule?.Station2MesDetectionData;

                        _cachedDetectionData = new MachineDetectionData
                        {
                            InternalBatchId = mesData?.InternalBatchId ?? "",
                            OcrText = _cachedOcrResult,
                            Barcode1 = _cachedOcrResult,
                            ProductModel = mesData?.ProductModel ?? "",
                            OperatorId = mesData?.OperatorId ?? "",
                            RecipeName = _cachedRecipe?.RecipeName ?? "",
                        };

                        // 用配方中的 GuestStartIndex/GuestLength 提取客批片号子串进行比对
                        bool isMatch = false;
                        string errorMsg = "NONE";
                        try
                        {
                            if (_cachedRecipe != null
                                && !string.IsNullOrEmpty(_cachedOcrResult)
                                && mesData?.CustomerWafers != null
                                && mesData.CustomerWafers.Count > 0)
                            {
                                int startIdx = _cachedRecipe.GuestStartIndex;
                                int length = _cachedRecipe.GuestLength;

                                if (startIdx >= 0 && length > 0 && _cachedOcrResult.Length >= startIdx + length)
                                {
                                    string extractedId = _cachedOcrResult.Substring(startIdx, length);
                                    var matchedWafer = mesData.CustomerWafers
                                        .FirstOrDefault(w => w.WaferId == extractedId);

                                    if (matchedWafer != null)
                                    {
                                        isMatch = true;
                                        _cachedDetectionData.CustomerBatch = matchedWafer.CustomerBatch;
                                        _cachedDetectionData.WaferId = matchedWafer.WaferId;
                                        _logger.Info($"[{StationName}] 数据比对成功：WaferId=[{extractedId}]，客批=[{matchedWafer.CustomerBatch}]。");
                                    }
                                    else
                                    {
                                        errorMsg = $"OCR提取片号[{extractedId}]在MES批次中未找到匹配项";
                                        _logger.Warn($"[{StationName}] 数据比对不匹配：{errorMsg}");
                                    }
                                }
                                else
                                {
                                    errorMsg = $"OCR结果长度[{_cachedOcrResult.Length}]不足以提取配方指定子串（起始={startIdx}，长度={length}）";
                                    _logger.Warn($"[{StationName}] 数据比对跳过：{errorMsg}");
                                }
                            }
                            else
                            {
                                errorMsg = "配方或MES数据为空，跳过比对";
                                _logger.Warn($"[{StationName}] {errorMsg}");
                            }
                        }
                        catch (Exception ex)
                        {
                            errorMsg = $"比对过程异常：{ex.Message}";
                            _logger.Error($"[{StationName}] {errorMsg}");
                        }

                        _cachedDetectionData.IsMatch = isMatch;
                        _cachedDetectionData.ErrorMessage = errorMsg;

                        _currentStep = StationDetectionStep.写入检测数据;
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  写入检测数据
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.写入检测数据:
                        CurrentStepDescription = "写入检测数据...";
                        await CheckPauseAsync(token).ConfigureAwait(false);
                        try
                        {
                            if (_cachedDetectionData != null && _dataModule != null)
                            {
                                await _dataModule.AddMachineDetectionAsync(_currentworkSpace, _cachedDetectionData).ConfigureAwait(false);
                                _logger.Info($"[{StationName}] 检测数据已写入（{_currentworkSpace}），IsMatch={_cachedDetectionData.IsMatch}。");
                            }
                            _currentStep = StationDetectionStep.检测完成;
                        }
                        catch (Exception ex)
                        {
                            _logger.Error($"[{StationName}] 写入检测数据异常：{ex.Message}");
                            _currentStep = StationDetectionStep.写入检测数据异常;
                        }
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  检测完成，释放完成信号
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.检测完成:
                        CurrentStepDescription = "检测完成，释放完成信号...";
                        await CheckPauseAsync(token).ConfigureAwait(false);

                        if (_currentworkSpace == E_WorkSpace.工位1)
                        {
                            _sync.Release(WorkstationSignals.工位1检测完成.ToString());
                            _logger.Success($"[{StationName}] 工位1检测完成，已释放信号。");
                        }
                        else
                        {
                            _sync.Release(WorkstationSignals.工位2检测完成.ToString());
                            _logger.Success($"[{StationName}] 工位2检测完成，已释放信号。");
                        }

                        // 清理本轮缓存，准备下一次检测
                        _cachedRecipe = null;
                        _cachedOcrResult = "";
                        _cachedDetectionData = null;

                        _currentStep = StationDetectionStep.等待工位1或工位2允许检测;
                        break;

                    // ══════════════════════════════════════════════════════════
                    //  阶段 E：异常处理节点（断点续跑）
                    //  每个异常 case：① 记录错误日志
                    //                ② 设定复位后的恢复步序（_currentStep 指向正常节点）
                    //                ③ TriggerAlarm() — 取消 token、推入 Alarm 状态
                    //  ExecuteResetAsync 仅做硬件清警，不再重置 _currentStep，
                    //  下次 Start() 时状态机将从已设定的恢复节点继续执行。
                    // ══════════════════════════════════════════════════════════

                    case StationDetectionStep.去工位检测位置异常:
                        _logger.Error($"[{StationName}] OCR模组移动到{_currentworkSpace}检测位置失败（运动超时或配方为空）。请检查轴状态与配方后复位，将重新尝试移动到检测位置。");
                        // 恢复到对应工位的移动步序，重新尝试
                        _currentStep = _currentworkSpace == E_WorkSpace.工位1
                            ? StationDetectionStep.去工位1检测位置
                            : StationDetectionStep.去工位2检测位置;
                        TriggerAlarm();
                        break;

                    case StationDetectionStep.触发检测异常:
                        _logger.Error($"[{StationName}] OCR相机触发异常（{_currentworkSpace}）。请检查相机状态后复位，将重新触发检测。");
                        _currentStep = StationDetectionStep.触发检测;
                        TriggerAlarm();
                        break;

                    case StationDetectionStep.写入检测数据异常:
                        _logger.Error($"[{StationName}] 写入检测数据异常（{_currentworkSpace}）。请检查数据模块后复位，将重新写入。");
                        _currentStep = StationDetectionStep.写入检测数据;
                        TriggerAlarm();
                        break;

                    default:
                        _logger.Error($"[{StationName}] 进入未定义步序 [{_currentStep}]，触发报警。");
                        TriggerAlarm();
                        break;
                }
            }
        }
    }
}
