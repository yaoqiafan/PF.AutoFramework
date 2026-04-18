using MathNet.Numerics;
using NPOI.SS.Formula.Functions;
using PF.Core.Attributes;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
using PF.Core.Interfaces.Production;
using PF.Infrastructure.Mechanisms;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{
    /// <summary>
    /// 全局数据中枢模块 (WorkStation Data Module)
    /// 
    /// <para>架构定位：</para>
    /// 作为整个设备的“数据大脑”，负责统管所有与流转相关的业务数据，包括：工位配方（Recipe）、
    /// MES 交互回传数据、机台底层实时检测数据（OCR/Barcode），以及数据的本地持久化（Json Snapshot）。
    /// </summary>
    /// <remarks>
    /// 💡 【核心机制】
    /// 
    /// - 内存与持久化分离：通过内部类 <see cref="WorkStationDataModuleSnapshot"/> 剥离业务逻辑，仅序列化纯净的 DTO 数据。
    /// - 批次生命周期闭环：在 <see cref="AddMachineDetectionAsync"/> 中收集单片晶圆数据，
    ///   并通过 <see cref="CheckBatchCompletionAsync"/> 后台线程自动判定批次完工并触发上报或存库。
    /// - 跨线程 UI 更新：通过触发 <see cref="DataChanged"/> 事件，通知外部 ViewModel 刷新界面绑定。
    /// </remarks>
    [MechanismUI("数据模块", "WorkStationDataModuleDebugView", 1)]
    public class WorkStationDataModule : BaseMechanism
    {
        #region Fields & Properties (依赖服务与核心事件)

        private readonly IProductionDataService _productionDataService;

        /// <summary>
        /// 本地数据快照的存储路径
        /// </summary>
        private readonly string _filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam\\MemoryData.json";

        /// <summary>
        /// 数据变化事件（供上层 ViewModel / UI 订阅，以便在数据刷新时同步更新界面界面）
        /// </summary>
        public event EventHandler? DataChanged;

        /// <summary>
        /// 触发数据变化事件
        /// </summary>
        private void RaiseDataChanged()
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Constructor & Lifecycle (构造与生命周期)

        /// <summary>
        /// 初始化数据中枢模块
        /// </summary>
        public WorkStationDataModule(
            IHardwareManagerService hardwareManagerService,
            IParamService paramService,
            IProductionDataService productionDataService,
            ILogService logger)
            : base("数据模块", hardwareManagerService, paramService, logger)
        {
            _productionDataService = productionDataService;
        }

        /// <summary>
        /// 模块初始化：自动加载本地缓存的 Json 数据快照
        /// </summary>
        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            Load(_filepath);
            return Task.FromResult(true);
        }

        /// <summary>
        /// 模块停止：自动保存当前内存数据到本地，防止意外丢失
        /// </summary>
        protected override Task InternalStopAsync()
        {
            Save(_filepath);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 资源释放：确保在软件关闭/析构时强制落盘
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            Save(_filepath);
        }

        #endregion

        #region MES Data Interaction (MES 交互层)

        /// <summary>
        /// 向 MES 系统发起批次校验并拉取该批次的晶圆明细。
        /// </summary>
        /// <param name="LotID">内部批次号 (通常通过扫码载具获取)</param>
        /// <param name="UserID">当前登录的检测人工号</param>
        /// <param name="token">异步取消令牌</param>
        /// <returns>返回反序列化后的 <see cref="MesDetectionParam"/> 实体；若通讯失败返回 null</returns>
        public async Task<MesDetectionParam> QueryMesAsync(string LotID, string UserID, CancellationToken token = default)
        {
            try
            {
                // TODO: 替换为实际的 HTTP/WebAPI/TCP 客户端调用逻辑。当前为 Mock 数据生成。
                MesDetectionParam param = new MesDetectionParam();
                param.InternalBatchId = LotID;
                param.OperatorId = UserID;
                param.ProductModel = "PF-Work";
                param.Quantity = 25; // 标准料盒通常为 25 片
                param.DetectionStatus = E_DetectionStatus.待检测;
                param.CustomerWafers = new List<WaferInfo>();
                param.RecipeName = "TestRecipe";

                for (int i = 1; i <= 25; i++)
                {
                    param.CustomerWafers.Add(new WaferInfo()
                    {
                        CustomerBatch = $"PB237Z", // 模拟的客户批次标识
                        WaferId = $"{i:D2}"        // 模拟的晶圆槽位编号 (01~25)
                    });
                }
                return param;
            }
            catch (Exception ex)
            {
                _logger?.Error($"{MechanismName} 查询MES数据失败: {ex.Message}");
                return null;
            }
        }

        #endregion 

        #region Station Recipe Parameters (工位配方缓存)

        [JsonInclude]
        private OCRRecipeParam _station1ReciepParam = new OCRRecipeParam();

        /// <summary>工位1工艺配方参数 (如：截取规则、比对长度等)</summary>
        public OCRRecipeParam Station1ReciepParam => _station1ReciepParam;

        [JsonInclude]
        private OCRRecipeParam _station2ReciepParam = new OCRRecipeParam();

        /// <summary>工位2工艺配方参数</summary>
        public OCRRecipeParam Station2ReciepParam => _station2ReciepParam;

        /// <summary>
        /// 动态下发并更新指定工位的配方参数，同时触发 UI 刷新
        /// </summary>
        public bool UpdateStationRecipeParam(E_WorkSpace Station, OCRRecipeParam Param)
        {
            if (Station == E_WorkSpace.工位1)
            {
                _station1ReciepParam = Param;
                RaiseDataChanged();
                return true;
            }

            if (Station == E_WorkSpace.工位2)
            {
                _station2ReciepParam = Param;
                RaiseDataChanged();
                return true;
            }

            return false;
        }

        #endregion 

        #region Local Detection Data (机台实时检测数据)

        [JsonInclude]
        private MesDetectionParam _station1MesDetectionData = new MesDetectionParam();

        /// <summary>工位1当前正在执行的 MES 批次详情</summary>
        public MesDetectionParam Station1MesDetectionData => _station1MesDetectionData;

        [JsonInclude]
        private MesDetectionParam _station2MesDetectionData = new MesDetectionParam();

        /// <summary>工位2当前正在执行的 MES 批次详情</summary>
        public MesDetectionParam Station2MesDetectionData => _station2MesDetectionData;

        /// <summary>
        /// 批次期望数量映射表 (Key: InternalBatchId, Value: 该批次期望的晶圆总数)
        /// 用于辅助判断当前批次是否已经扫描/检测完毕。
        /// </summary>
        [JsonInclude]
        private ConcurrentDictionary<string, int> _batchQuantityMap = new ConcurrentDictionary<string, int>();

        [JsonInclude]
        private List<MachineDetectionData> _sation1MachineDetectionData = new List<MachineDetectionData>();

        /// <summary>工位1机台实时产生的单片检测结果列表 (注: 拼写保留原代码 Sation 以防破坏 UI 绑定)</summary>
        public List<MachineDetectionData> Sation1MachineDetectionData => _sation1MachineDetectionData;

        [JsonInclude]
        private List<MachineDetectionData> _sation2MachineDetectionData = new List<MachineDetectionData>();

        /// <summary>工位2机台实时产生的单片检测结果列表</summary>
        public List<MachineDetectionData> Sation2MachineDetectionData => _sation2MachineDetectionData;

        /// <summary>
        /// 全局缓存：根据内部批次号 (InternalBatchId) 归档的所有机器检测数据字典。
        /// 采用 <see cref="ConcurrentDictionary{TKey, TValue}"/> 确保多工位并行写入的线程安全。
        /// </summary>
        [JsonInclude]
        private ConcurrentDictionary<string, List<MachineDetectionData>> _machineDataByBatch = new ConcurrentDictionary<string, List<MachineDetectionData>>();

        /// <summary>获取按批次归档的检测数据字典</summary>
        public ConcurrentDictionary<string, List<MachineDetectionData>> MachineDataByBatch => _machineDataByBatch;

        /// <summary>
        /// 扫码换批逻辑：更新指定工位的 MES 校验基准信息，并清空对应工位的旧版机台检测列表（开始全新批次）
        /// </summary>
        public async Task<bool> UpdateStationMesInfoAsync(E_WorkSpace Station, MesDetectionParam Data, CancellationToken token = default)
        {
            if (Station == E_WorkSpace.工位1)
            {
                _station1MesDetectionData = Data;
                _sation1MachineDetectionData.Clear();
            }
            else if (Station == E_WorkSpace.工位2)
            {
                _station2MesDetectionData = Data;
                _sation2MachineDetectionData.Clear();
            }
            else
            {
                return false;
            }

            RaiseDataChanged();
            return true;
        }

        /// <summary>
        /// 数据上报入口：新增一条晶圆机台检测数据 (OCR/Barcode 结果)。
        /// </summary>
        public async Task<bool> AddMachineDetectionAsync(E_WorkSpace Station, MachineDetectionData Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                // 查找当前列表是否已经存在同条码的数据（可能属于重扫或复判）
                var kk = _sation1MachineDetectionData.Where(x => x.Barcode1 == Data.Barcode1).FirstOrDefault();
                if (kk != null)
                {
                    // 复判逻辑：如果新数据匹配成功，或者旧数据本身是不匹配的，则用新数据覆盖旧数据
                    if (Data.IsMatch || !kk.IsMatch)
                    {
                        _sation1MachineDetectionData.Remove(kk);
                        _sation1MachineDetectionData.Add(Data);
                    }
                }
                else
                {
                    _sation1MachineDetectionData.Add(Data);
                }
            }
            else if (Station == E_WorkSpace.工位2)
            {
                var kk = _sation2MachineDetectionData.Where(x => x.Barcode1 == Data.Barcode1).FirstOrDefault();
                if (kk != null)
                {
                    if (Data.IsMatch || !kk.IsMatch)
                    {
                        _sation2MachineDetectionData.Remove(kk);
                        _sation2MachineDetectionData.Add(Data);
                    }
                }
                else
                {
                    _sation2MachineDetectionData.Add(Data);
                }
            }

            // 将数据异步追加到全局批次字典中
            await AddAllDic(Station, Data);
            RaiseDataChanged();
            return true;
        }

        /// <summary>
        /// 内部辅助：将单片数据归档到对应的批次集合，并更新期望数量
        /// </summary>
        private async Task AddAllDic(E_WorkSpace station, MachineDetectionData data)
        {
            _machineDataByBatch.AddOrUpdate(
               data.InternalBatchId,
               _ => new List<MachineDetectionData> { data },
               (_, list) =>
               {
                   list.Add(data);
                   return list;
               });

            // 如果该批次的期望晶圆总数尚未记录，则以当前工位的 MES 目标数量为准进行登记
            _batchQuantityMap.TryAdd(data.InternalBatchId,
                station == E_WorkSpace.工位1 ? _station1MesDetectionData.Quantity : _station2MesDetectionData.Quantity);

            // 触发批次完工判定
            await CheckBatchCompletionAsync();
        }

        /// <summary>
        /// 后台批次守卫：脱离主线程检查当前内存中的各批次数据是否已经达到期望总数。
        /// 达到总数则触发数据库落盘/MES上传，并自动清空内存，防止内存泄漏。
        /// </summary>
        private Task CheckBatchCompletionAsync()
        {
            return Task.Run(async () =>
            {
                foreach (var kvp in _machineDataByBatch.ToList())
                {
                    if (_batchQuantityMap.TryGetValue(kvp.Key, out var expected))
                    {
                        // 判定条件：实际收集到的单片数量 == MES 预期的该批次总片数
                        if (kvp.Value.Count == expected)
                        {
                            /*********** 批次数据已收集完整：触发上传或后处理逻辑 ***********/
                            try
                            {
                                // TODO: 在此处调用 MES 的 UploadBatch 接口
                                _logger?.Info($"{MechanismName} 批次 {kvp.Key} 收集完成，准备上报。");
                            }
                            catch (Exception ex)
                            {
                                _logger?.Error($"{MechanismName} 上报批次 {kvp.Key} 失败: {ex.Message}");
                            }

                            List<MachineDetectionData> recorddata = new List<MachineDetectionData>();

                            // 从内存字典中移除已完工的批次数据，释放资源
                            _machineDataByBatch.TryRemove(kvp.Key, out recorddata);
                            if (recorddata != null && recorddata.Count != 0)
                            {
                                // 遍历写入本地数据库（例如 SQLite 或 MySQL）
                                foreach (var item in recorddata)
                                {
                                    await _productionDataService.RecordAsync<MachineDetectionData>(item);
                                }
                            }
                            _logger?.Info($"批次 {kvp.Key} 本地数据库记录完成！");

                            // 同步清理目标计数字典
                            _batchQuantityMap.TryRemove(kvp.Key, out _);
                        }
                    }
                }
            });
        }

        #endregion

        #region Verification Logic (条码与 OCR 数据合法性校验)

        /// <summary>
        /// 校验扫码枪读取的 Barcode 是否属于当前 MES 下发的待验名单
        /// </summary>
        /// <param name="station">发起请求的工位</param>
        /// <param name="codes">扫码枪提取出的原始条码列表</param>
        /// <param name="token">异步取消令牌</param>
        /// <returns>Item1: 是否合法通过；Item2: 过滤后的合规条码列表；Item3: 匹配出的具体晶圆实体</returns>
        public Task<(bool, List<string>, WaferInfo)> CheckCodeAsync(E_WorkSpace station, List<string> codes, CancellationToken token = default)
        {
            WaferInfo info = null;
            try
            {
                if (station == E_WorkSpace.工位1)
                {
                    List<string> OKcodes = new List<string>();

                    // 1. 按照当前配方规则 (GuestStartIndex, GuestLength) 截取 MES 下发的标准客户批次号
                    var kk = _station1MesDetectionData.CustomerWafers.Select(x => new WaferInfo()
                    {
                        CustomerBatch = x.CustomerBatch.Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength),
                        WaferId = x.WaferId
                    }).ToList();

                    // 2. 遍历扫描枪读出的原始条码
                    for (int i = 0; i < codes?.Count; i++)
                    {
                        // 现代 C# 模式匹配语法：尝试按 '-' 分割，若成功且长度为2，则装载进 parts 数组
                        if (codes[i].Split('-') is { Length: 2 } parts)
                        {
                            // 按照同样的配方规则截取扫码内容
                            string code = parts[0].Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength);

                            // 校验比对：客户批次号必须一致，且 WaferID 必须匹配
                            if (kk.Any(x => x.CustomerBatch == code && x.WaferId == parts[1].Substring(0, 2)))
                            {
                                OKcodes.Add(codes[i]);
                                info = kk.Where(x => x.CustomerBatch == code && x.WaferId == parts[1].Substring(0, 2)).FirstOrDefault();
                            }
                        }
                    }

                    // 若比对成功的数量达到了配方要求的必须扫码条数，则放行
                    if (OKcodes.Count == _station1ReciepParam.CodeCount)
                    {
                        return Task.FromResult((true, OKcodes, info));
                    }
                    else
                    {
                        return Task.FromResult((false, codes, info));
                    }
                }
                else if (station == E_WorkSpace.工位2)
                {
                    // 与工位1逻辑一致，操作 Station2 的数据源
                    List<string> OKcodes = new List<string>();
                    var kk = _station2MesDetectionData.CustomerWafers.Select(x => new WaferInfo()
                    {
                        CustomerBatch = x.CustomerBatch.Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength),
                        WaferId = x.WaferId
                    }).ToList();

                    for (int i = 0; i < codes?.Count; i++)
                    {
                        if (codes[i].Split('-') is { Length: 2 } parts)
                        {
                            string code = parts[0].Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength);

                            if (kk.Any(x => x.CustomerBatch == code && x.WaferId == parts[1].Substring(0, 2)))
                            {
                                OKcodes.Add(codes[i]);
                                info = kk.Where(x => x.CustomerBatch == code && x.WaferId == parts[1].Substring(0, 2)).FirstOrDefault();
                            }
                        }
                    }

                    if (OKcodes.Count == _station2ReciepParam.CodeCount)
                    {
                        return Task.FromResult((true, OKcodes, info));
                    }
                    else
                    {
                        return Task.FromResult((false, codes, info));
                    }
                }
                else
                {
                    return Task.FromResult((false, codes, info));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult((false, codes, info));
            }
        }

        /// <summary>
        /// 校验相机识别出的 OCR 字符是否符合 MES 名单要求
        /// </summary>
        /// <param name="station">工站名</param>
        /// <param name="ocrtext">相机视觉工具返回的原始 OCR 字符串</param>
        /// <returns>Item1: 校验是否成功；Item2: 匹配的晶圆实体</returns>
        public Task<(bool, WaferInfo)> CheckOcrTextAsync(E_WorkSpace station, string ocrtext, CancellationToken token = default)
        {
            WaferInfo info = null;
            try
            {
                if (station == E_WorkSpace.工位1)
                {
                    var kk = _station1MesDetectionData.CustomerWafers.Select(x => new WaferInfo()
                    {
                        CustomerBatch = x.CustomerBatch.Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength),
                        WaferId = x.WaferId
                    }).ToList();

                    // 兼容两种 OCR 格式：包含3段的（例如 批次-槽位-校验码） 或 2段的（批次-槽位）
                    if (ocrtext.Split('-') is { Length: 3 } parts)
                    {
                        string ocr = ocrtext.Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength);
                        if (kk.Any(x => x.CustomerBatch == ocr && x.WaferId == parts[1].Substring(0, 2)))
                        {
                            info = kk.Where(x => x.CustomerBatch == ocr && x.WaferId == parts[1].Substring(0, 2)).FirstOrDefault();
                            return Task.FromResult((true, info));
                        }
                        else
                        {
                            return Task.FromResult((false, info));
                        }
                    }
                    else if (ocrtext.Split('-') is { Length: 2 } parts1)
                    {
                        string ocr = ocrtext.Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength);
                        if (kk.Any(x => x.CustomerBatch == ocr && x.WaferId == parts1[1].Substring(0, 2)))
                        {
                            info = kk.Where(x => x.CustomerBatch == ocr && x.WaferId == parts1[1].Substring(0, 2)).FirstOrDefault();
                            return Task.FromResult((true, info));
                        }
                        else
                        {
                            return Task.FromResult((false, info));
                        }
                    }
                    else
                    {
                        return Task.FromResult((false, info));
                    }
                }
                else if (station == E_WorkSpace.工位2)
                {
                    // 与工位1逻辑一致
                    var kk = _station2MesDetectionData.CustomerWafers.Select(x => new WaferInfo()
                    {
                        CustomerBatch = x.CustomerBatch.Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength),
                        WaferId = x.WaferId
                    }).ToList();

                    if (ocrtext.Split('-') is { Length: 3 } parts)
                    {
                        string ocr = ocrtext.Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength);
                        if (kk.Any(x => x.CustomerBatch == ocr && x.WaferId == parts[1].Substring(0, 2)))
                        {
                            info = kk.Where(x => x.CustomerBatch == ocr && x.WaferId == parts[1].Substring(0, 2)).FirstOrDefault();
                            return Task.FromResult((true, info));
                        }
                        else
                        {
                            return Task.FromResult((false, info));
                        }
                    }
                    else if (ocrtext.Split('-') is { Length: 2 } parts1)
                    {
                        string ocr = ocrtext.Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength);
                        if (kk.Any(x => x.CustomerBatch == ocr && x.WaferId == parts1[1].Substring(0, 2)))
                        {
                            info = kk.Where(x => x.CustomerBatch == ocr && x.WaferId == parts1[1].Substring(0, 2)).FirstOrDefault();
                            return Task.FromResult((true, info));
                        }
                        else
                        {
                            return Task.FromResult((false, info));
                        }
                    }
                    else
                    {
                        return Task.FromResult((false, info));
                    }
                }
                else
                {
                    return Task.FromResult((false, info));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult((false, info));
            }
        }

        #endregion

        #region Serialization & Persistence (序列化与数据持久化快照)

        /// <summary>
        /// 【快照模式】用于持久化的纯数据 DTO。
        /// 隔离手段：避免直接使用 JsonSerializer 序列化包含 DI 服务、Event 委托的机制类本身，
        /// 否则极易引发循环引用 (Circular Reference) 异常或严重内存泄漏。
        /// </summary>
        private class WorkStationDataModuleSnapshot
        {
            public OCRRecipeParam Station1ReciepParam { get; set; } = new OCRRecipeParam();
            public OCRRecipeParam Station2ReciepParam { get; set; } = new OCRRecipeParam();
            public MesDetectionParam Station1MesDetectionData { get; set; } = new MesDetectionParam();
            public MesDetectionParam Station2MesDetectionData { get; set; } = new MesDetectionParam();
            public List<MachineDetectionData> Sation1MachineDetectionData { get; set; } = new List<MachineDetectionData>();
            public List<MachineDetectionData> Sation2MachineDetectionData { get; set; } = new List<MachineDetectionData>();
            public ConcurrentDictionary<string, List<MachineDetectionData>> MachineDataByBatch { get; set; } = new ConcurrentDictionary<string, List<MachineDetectionData>>();
            public ConcurrentDictionary<string, int> BatchQuantityMap { get; set; } = new ConcurrentDictionary<string, int>();
        }

        /// <summary>
        /// 序列化并保存当前内存数据到本地 Json 文件
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
                // 将活跃内存组装成纯净快照
                var snapshot = new WorkStationDataModuleSnapshot
                {
                    Station1ReciepParam = _station1ReciepParam,
                    Station2ReciepParam = _station2ReciepParam,
                    Station1MesDetectionData = _station1MesDetectionData,
                    Station2MesDetectionData = _station2MesDetectionData,
                    Sation1MachineDetectionData = _sation1MachineDetectionData,
                    Sation2MachineDetectionData = _sation2MachineDetectionData,
                    MachineDataByBatch = _machineDataByBatch,
                    BatchQuantityMap = _batchQuantityMap,
                };

                var options = new JsonSerializerOptions { WriteIndented = true };

                var fileInfo = new FileInfo(filePath);
                var folderPath = fileInfo.DirectoryName;
                if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string json = JsonSerializer.Serialize(snapshot, options);
                System.IO.File.WriteAllText(filePath, json);
                _logger?.Info($"{this.MechanismName} 数据已成功保存至: {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"{MechanismName} 序列化保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 反序列化：从本地 Json 文件加载缓存数据到内存
        /// </summary>
        public bool Load(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                // 文件不存在时，生成一份默认的空结构文件
                Save(filePath);
                return false;
            }

            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions();

                var tempModule = JsonSerializer.Deserialize<WorkStationDataModuleSnapshot>(json, options);
                if (tempModule != null)
                {
                    // 手动将解析出的数据字段，同步赋值给当前已经过 DI (依赖注入) 实例化的单例对象中
                    this._station1ReciepParam = tempModule.Station1ReciepParam;
                    this._station2ReciepParam = tempModule.Station2ReciepParam;
                    this._station1MesDetectionData = tempModule.Station1MesDetectionData;
                    this._station2MesDetectionData = tempModule.Station2MesDetectionData;
                    this._sation1MachineDetectionData = tempModule.Sation1MachineDetectionData;
                    this._sation2MachineDetectionData = tempModule.Sation2MachineDetectionData;
                    this._machineDataByBatch = tempModule.MachineDataByBatch;
                    this._batchQuantityMap = tempModule.BatchQuantityMap;

                    _logger?.Info($"{MechanismName} 历史数据加载成功");

                    // 通知 UI 层数据上下文已刷新
                    RaiseDataChanged();
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"{MechanismName} 反序列化加载失败: {ex.Message}");
                return false;
            }
        }

        #endregion
    }

    #region Data Models (业务数据模型 DTO)

    /// <summary>
    /// MES 交互返回的批次工单数据集合
    /// </summary>
    public class MesDetectionParam
    {
        /// <summary>内部批次号 (系统内流转唯一标识)</summary>
        public string InternalBatchId { get; set; } = "";

        /// <summary>当前批次的检测产品型号</summary>
        public string ProductModel { get; set; } = "";

        /// <summary>该批次下包含的所有晶圆客批与槽号刻号信息集合</summary>
        public List<WaferInfo> CustomerWafers { get; set; } = new List<WaferInfo>();

        /// <summary>当前批次期望的产品总个数 (如：25)</summary>
        public int Quantity { get; set; } = 0;

        /// <summary>整体检测状态机</summary>
        public E_DetectionStatus DetectionStatus { get; set; } = E_DetectionStatus.检测中;

        /// <summary>当前登录执行检测的人员工号</summary>
        public string OperatorId { get; set; } = "NONE";

        /// <summary>MES 下发的关联配方名称</summary>
        public string RecipeName { get; set; } = "NONE";
    }

    /// <summary>
    /// 机台底层单片检测结果集合（包含 OCR 视觉结果、条码结果、追溯图片路径等）
    /// </summary>
    public class MachineDetectionData
    {
        /// <summary>触发检测的时间戳 (转换为 OADate 存储)</summary>
        public double Time { get; set; } = DateTime.Now.ToOADate();

        /// <summary>关联的内部批次号</summary>
        public string InternalBatchId { get; set; } = "";

        /// <summary>关联的客户批次号</summary>
        public string CustomerBatch { get; set; } = "";

        /// <summary>该片晶圆在料盒中的 ID 号 (如 "01", "25")</summary>
        public string WaferId { get; set; } = "";

        /// <summary>视觉相机读取到的原生 OCR 字符串</summary>
        public string OcrText { get; set; } = "";

        /// <summary>硬件扫码枪读取到的条码1</summary>
        public string Barcode1 { get; set; } = "";

        /// <summary>硬件扫码枪读取到的条码2 (预留多码扫码)</summary>
        public string Barcode2 { get; set; } = "";

        /// <summary>硬件扫码枪读取到的条码3 (预留多码扫码)</summary>
        public string Barcode3 { get; set; } = "";

        /// <summary>当前晶圆的数据与 MES 下发数据是否比对通过（OK/NG）</summary>
        public bool IsMatch { get; set; }

        /// <summary>若比对异常，记录的具体错误信息原因</summary>
        public string ErrorMessage { get; set; } = "NONE";

        /// <summary>检测产品型号溯源</summary>
        public string ProductModel { get; set; } = "";

        /// <summary>检测人工号溯源</summary>
        public string OperatorId { get; set; } = "NONE";

        /// <summary>配方名称溯源</summary>
        public string RecipeName { get; set; } = "NONE";

        /// <summary>不良排查使用的视觉留存原图路径</summary>
        public string ImagePath { get; set; } = "NONE";

        /// <summary>返回检测数据的字符串表示</summary>
        public override string ToString()
        {
            return $"Time: {DateTime.FromOADate(Time):yyyy-MM-dd HH:mm:ss} \r\n OCR: {OcrText}";
        }
    }

    /// <summary>
    /// 单片晶圆基础身份信息 DTO
    /// </summary>
    public class WaferInfo
    {
        /// <summary>晶圆的客户批次标识码</summary>
        public string CustomerBatch { get; set; }

        /// <summary>晶圆所在的物理槽位 ID 号</summary>
        public string WaferId { get; set; }
    }

    #endregion
}