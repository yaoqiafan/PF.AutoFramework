
using MathNet.Numerics;
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
using System.Threading.Tasks;

namespace PF.WorkStation.AutoOcr.Mechanisms
{

    /// <summary>
    /// 数据模块，所有交互数据基于此模块（工位配方、检测数据、原始数据）
    /// </summary>
    [MechanismUI("数据模块", "WorkStationDataModuleDebugView", 1)]
    public class WorkStationDataModule : BaseMechanism
    {
        private readonly IProductionDataService _productionDataService;


        public WorkStationDataModule(
            IHardwareManagerService hardwareManagerService,
            IParamService paramService,
             IProductionDataService productionDataService,
            ILogService logger)
            : base("数据模块", hardwareManagerService, paramService, logger)
        {
            _productionDataService = productionDataService;
        }

        public override void Dispose()
        {
            base.Dispose();
            Save(_filepath);
        }


        private readonly string _filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam\\MemoryData.json";

        /// <summary>
        /// 数据变化事件（供上层 ViewModel / UI 订阅）
        /// </summary>
        public event EventHandler? DataChanged;

        private void RaiseDataChanged()
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            Load(_filepath);
            return Task.FromResult(true);
        }

        protected override Task InternalStopAsync()
        {
            Save(_filepath);
            return Task.CompletedTask;
        }

        #region MES数据交互

        /// <summary>
        /// 查询 MES（示例实现，当前为模拟数据）
        /// </summary>
        /// <param name="lotId">内部批次号</param>
        /// <param name="operatorId">检测人工号</param>
        /// <param name="token">取消令牌</param>
        /// <returns>MesDetectionInfo 或 null（失败时）</returns>
        public async Task<MesDetectionParam> QueryMesAsync(string LotID, string UserID, CancellationToken token = default)
        {
            try
            {
                MesDetectionParam param = new MesDetectionParam();
                param.InternalBatchId = LotID;
                param.OperatorId = UserID;
                param.ProductModel = "PF-Work";
                param.Quantity = 25; // 假设每批次25片
                param.DetectionStatus = E_DetectionStatus.待检测;
                param.CustomerWafers = new List<WaferInfo>();
                param.RecipeName = "TestRecipe";
                for (int i = 1; i <= 25; i++)
                {
                    param.CustomerWafers.Add(new WaferInfo()
                    {
                        CustomerBatch = $"PB237Z",
                        WaferId = $"{i:D2}"
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


        #endregion MES数据交互



        #region 工位配方参数

        [JsonInclude]
        private OCRRecipeParam _station1ReciepParam = new OCRRecipeParam();

        /// <summary>
        /// 工位1配方参数
        /// </summary>
        public OCRRecipeParam Station1ReciepParam => _station1ReciepParam;

        [JsonInclude]
        private OCRRecipeParam _station2ReciepParam = new OCRRecipeParam();

        /// <summary>
        /// 工位2配方参数
        /// </summary>
        public OCRRecipeParam Station2ReciepParam => _station2ReciepParam;

        /// <summary>
        /// 更新指定工位的配方参数并触发数据变更通知
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

        #endregion 工位配方参数

        #region 检测数据

        [JsonInclude]
        private MesDetectionParam _station1MesDetectionData = new MesDetectionParam();

        /// <summary>
        /// 工位1检测MES返回数据
        /// </summary>
        public MesDetectionParam Station1MesDetectionData => _station1MesDetectionData;

        [JsonInclude]
        private MesDetectionParam _station2MesDetectionData = new MesDetectionParam();

        /// <summary>
        /// 工位2检测MES返回数据
        /// </summary>
        public MesDetectionParam Station2MesDetectionData => _station2MesDetectionData;

        /// <summary>
        /// 批次数量
        /// </summary>
        [JsonInclude]
        private ConcurrentDictionary<string, int> _batchQuantityMap = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// 工位1机台检测数据列表
        /// </summary>
        [JsonInclude]
        private List<MachineDetectionData> _sation1MachineDetectionData =
            new List<MachineDetectionData>();

        public List<MachineDetectionData> Sation1MachineDetectionData =>
            _sation1MachineDetectionData;

        /// <summary>
        /// 工位2机台检测数据列表
        /// </summary>
        [JsonInclude]
        private List<MachineDetectionData> _sation2MachineDetectionData =
            new List<MachineDetectionData>();

        public List<MachineDetectionData> Sation2MachineDetectionData =>
            _sation2MachineDetectionData;



        /// <summary>
        /// 根据内部批次号索引的检测数据字典
        /// </summary>
        [JsonInclude]
        private ConcurrentDictionary<string, List<MachineDetectionData>> _machineDataByBatch =
            new ConcurrentDictionary<string, List<MachineDetectionData>>();

        public ConcurrentDictionary<string, List<MachineDetectionData>> MachineDataByBatch =>
            _machineDataByBatch;

        /// <summary>
        ///  更新指定工位的 MES 检测信息，并清空对应工位的机台检测列表（以便开始新批次）
        /// </summary>
        public async Task<bool> UpdateStationMesInfoAsync(E_WorkSpace Station, MesDetectionParam Data, CancellationToken token = default)
        {
            if (Station == E_WorkSpace.工位1)
            {
                // 为了不破坏绑定，优先同步字段内容而非直接替换对象引用
                //_station1MesDetectionData.InternalBatchId = Data.InternalBatchId;

                //_station1MesDetectionData.CustomerWafers.Clear();
                //foreach (var w in Data.CustomerWafers)
                //{
                //    _station1MesDetectionData.CustomerWafers.Add(w);
                //}
                _station1MesDetectionData = Data;
                _sation1MachineDetectionData.Clear();
            }
            else if (Station == E_WorkSpace.工位2)
            {
                //_station2MesDetectionData.InternalBatchId = Data.InternalBatchId;

                //_station2MesDetectionData.CustomerWafers.Clear();
                //foreach (var w in Data.CustomerWafers)
                //{
                //    _station2MesDetectionData.CustomerWafers.Add(w);
                //}
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
        /// 新增一条机台检测数据（根据工位维护不同列表），并尝试将数据加入批次字典以判断批次完成
        /// </summary>
        public async Task<bool> AddMachineDetectionAsync(E_WorkSpace Station, MachineDetectionData Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                var kk = _sation1MachineDetectionData.Where(x => x.Barcode1 == Data.Barcode1).FirstOrDefault();
                if (kk != null)
                {
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
            await AddAllDic(Station, Data);
            RaiseDataChanged();
            return true;
        }



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

            // 如果批次期望数量尚未记录，则以当前工位的 MES 信息为准进行记录
            _batchQuantityMap.TryAdd(data.InternalBatchId,
                station == E_WorkSpace.工位1 ? _station1MesDetectionData.Quantity : _station2MesDetectionData.Quantity);

            await CheckBatchCompletionAsync();
        }


        private Task CheckBatchCompletionAsync()
        {
            return Task.Run(async () =>
               {
                   foreach (var kvp in _machineDataByBatch.ToList())
                   {
                       if (_batchQuantityMap.TryGetValue(kvp.Key, out var expected))
                       {
                           if (kvp.Value.Count == expected)
                           {
                               /*********** 批次数据已收集完整：触发上传或后处理逻辑 ***********/
                               try
                               {
                                   // TODO: 在此处调用上传/上报逻辑，例如：UploadBatch(kvp.Key, kvp.Value);
                                   _logger?.Info($"{MechanismName} 批次 {kvp.Key} 收集完成，准备上报。");
                               }
                               catch (Exception ex)
                               {
                                   _logger?.Error($"{MechanismName} 上报批次 {kvp.Key} 失败: {ex.Message}");
                               }


                               List<MachineDetectionData> recorddata = new List<MachineDetectionData>();
                               // 从内存字典中移除已处理批次
                               _machineDataByBatch.TryRemove(kvp.Key, out recorddata);
                               if (recorddata != null && recorddata.Count != 0)
                               {
                                   foreach (var item in recorddata)
                                   {
                                       await _productionDataService.RecordAsync<MachineDetectionData>(item);
                                   }
                               }
                               _logger?.Info($"批次 {kvp.Key} 本地数据库记录完成！。");

                               _batchQuantityMap.TryRemove(kvp.Key, out _);
                           }
                       }
                   }
               });
        }


        /// <summary>
        /// 检查扫到条码是否符合标准
        /// </summary>
        /// <param name="station">工位</param>
        /// <param name="codes">条码列表</param>
        /// <param name="token">取消令牌</param>
        /// <returns>item1 :扫到条码是否符合要求  item2:返回条码 </returns>
        public Task<(bool, List<string>)> CheckCode(E_WorkSpace station, List<string> codes, CancellationToken token = default)
        {
            try
            {
                if (station == E_WorkSpace.工位1)
                {
                    List<string> OKcodes = new List<string>();
                    var kk = _station1MesDetectionData.CustomerWafers.Select(x => new WaferInfo() { CustomerBatch = x.CustomerBatch.Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength), WaferId = x.WaferId }).ToList();
                    for (int i = 0; i < codes?.Count; i++)
                    {
                        if (codes[i].Split('-') is { Length: 2 } parts)
                        {
                            string code = parts[0].Substring(_station1ReciepParam.GuestStartIndex, _station1ReciepParam.GuestLength);

                            if (kk.Any(x => x.CustomerBatch == code && x.WaferId == parts[1].Substring(0, 2)))
                            {
                                OKcodes.Add(codes[i]);
                            }
                        }
                    }
                    if (OKcodes.Count == _station1ReciepParam.CodeCount)
                    {
                        return Task.FromResult((true, OKcodes));
                    }
                    else
                    {
                        return Task.FromResult((false, codes));
                    }
                }
                else if (station == E_WorkSpace.工位2)
                {
                    List<string> OKcodes = new List<string>();
                    var kk = _station2MesDetectionData.CustomerWafers.Select(x => new WaferInfo() { CustomerBatch = x.CustomerBatch.Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength), WaferId = x.WaferId }).ToList();
                    for (int i = 0; i < codes?.Count; i++)
                    {
                        if (codes[i].Split('-') is { Length: 2 } parts)
                        {
                            string code = parts[0].Substring(_station2ReciepParam.GuestStartIndex, _station2ReciepParam.GuestLength);

                            if (kk.Any(x => x.CustomerBatch == code && x.WaferId == parts[1].Substring(0, 2)))
                            {
                                OKcodes.Add(codes[i]);
                            }
                        }
                    }
                    if (OKcodes.Count == _station2ReciepParam.CodeCount)
                    {
                        return Task.FromResult((true, OKcodes));
                    }
                    else
                    {
                        return Task.FromResult((false, codes));
                    }
                }

                else
                {
                    return Task.FromResult((false, codes));
                }

            }
            catch (Exception ex)
            {
                return Task.FromResult((false, codes));
            }
        }






        #endregion  检测数据

        #region 序列化与反序列化

        /// <summary>
        /// 用于持久化的纯数据 DTO，避免直接序列化机制类本身
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
        /// 序列化保存数据
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
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

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                var fileInfo = new FileInfo(filePath);
                var folderPath = fileInfo.DirectoryName;
                if (!string.IsNullOrEmpty(folderPath) && !Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                string json = JsonSerializer.Serialize(this, options);
                System.IO.File.WriteAllText(filePath, json);
                _logger?.Info($"{this.MechanismName} 数据已保存至: {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"{MechanismName} 保存失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 反序列化加载数据
        /// </summary>
        public bool Load(string filePath)
        {
            if (!System.IO.File.Exists(filePath))
            {
                Save(filePath); // 如果文件不存在，先保存一个默认的空数据文件
                return false;
            }

            try
            {
                var json = System.IO.File.ReadAllText(filePath);
                var options = new JsonSerializerOptions();

                var tempModule = JsonSerializer.Deserialize<WorkStationDataModuleSnapshot>(json, options);
                if (tempModule != null)
                {
                    //手动将数据同步到当前经过 DI 初始化的实例
                    this._station1ReciepParam = tempModule.Station1ReciepParam;
                    this._station2ReciepParam = tempModule.Station2ReciepParam;
                    this._station1MesDetectionData = tempModule.Station1MesDetectionData;
                    this._station2MesDetectionData = tempModule.Station2MesDetectionData;
                    this._sation1MachineDetectionData = tempModule.Sation1MachineDetectionData;
                    this._sation2MachineDetectionData = tempModule.Sation2MachineDetectionData;
                    this._machineDataByBatch = tempModule.MachineDataByBatch;
                    this._batchQuantityMap = tempModule.BatchQuantityMap;

                    _logger?.Info($"{MechanismName} 数据加载成功");
                    RaiseDataChanged();
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error($"{MechanismName} 加载失败: {ex.Message}");
                return false;
            }
        }

        #endregion 序列化与反序列化
    }

    /// <summary>
    /// Mes返回数据集合
    /// </summary>
    public class MesDetectionParam
    {
        /// <summary>
        /// 内部批次号
        /// </summary>
        public string InternalBatchId { get; set; } = "";


        /// <summary>
        /// 检测产品型号
        /// </summary>
        public string ProductModel { get; set; } = "";


        /// <summary>
        /// 客批和刻号集合
        /// </summary>
        public List<WaferInfo> CustomerWafers { get; set; } = new List<WaferInfo>();


        /// <summary>
        /// 当前批次产品个数
        /// </summary>
        public int Quantity { get; set; } = 0;


        /// <summary>
        /// 检测状态
        /// </summary>
        public E_DetectionStatus DetectionStatus { get; set; } = E_DetectionStatus.检测中;


        /// <summary>
        /// 检测人工号
        /// </summary>
        public string OperatorId { get; set; } = "NONE";

        /// <summary>
        /// 配方名称
        /// </summary>
        public string RecipeName { get; set; } = "NONE";

    }



    /// <summary>
    /// 机台检测数据集合（OCR值、Code值等）
    /// </summary>
    public class MachineDetectionData
    {
        /// <summary>
        /// 内部批次号
        /// </summary>
        public double Time { get; set; } = DateTime.Now.ToOADate();


        /// <summary>
        /// 内部批次号
        /// </summary>
        public string InternalBatchId { get; set; } = "";

        /// <summary>
        /// 客户批次
        /// </summary>
        public string CustomerBatch { get; set; } = "";

        /// <summary>
        /// 晶圆ID号
        /// </summary>
        public string WaferId { get; set; } = "";

        /// <summary>
        /// 读取OCR值
        /// </summary>
        public string OcrText { get; set; } = "";

        /// <summary>
        /// 条码1值
        /// </summary>
        public string Barcode1 { get; set; } = "";

        /// <summary>
        /// 条码2值
        /// </summary>
        public string Barcode2 { get; set; } = "";


        /// <summary>
        /// 条码3值
        /// </summary>
        public string Barcode3 { get; set; } = "";


        /// <summary>
        /// 比对结果
        /// </summary>
        public bool IsMatch { get; set; }

        /// <summary>
        /// 比对异常输出
        /// </summary>
        public string ErrorMessage { get; set; } = "NONE";

        /// <summary>
        /// 检测产品型号
        /// </summary>
        public string ProductModel { get; set; } = "";


        /// <summary>
        /// 检测人工号
        /// </summary>
        public string OperatorId { get; set; } = "NONE";

        /// <summary>
        /// 配方名称
        /// </summary>
        public string RecipeName { get; set; } = "NONE";


        /// <summary>
        /// 配方名称
        /// </summary>
        public string ImagePath { get; set; } = "NONE";


        public override string ToString()
        {
            return $"Time: {DateTime.FromOADate(Time) } \r\n OCR: {OcrText}";
        }

    }



    /// <summary>
    /// 晶圆信息
    /// </summary>
    public class WaferInfo
    {

        /// <summary>
        /// 晶圆的客户批次
        /// </summary>
        public string CustomerBatch { get; set; }

        /// <summary>
        /// 晶圆的ID号
        /// </summary>
        public string WaferId { get; set; }
    }

}
