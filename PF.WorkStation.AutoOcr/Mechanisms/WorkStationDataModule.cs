
using PF.Core.Attributes;
using PF.Core.Events;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Logging;
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
        public WorkStationDataModule(
            IHardwareManagerService hardwareManagerService,
            IParamService paramService,
            ILogService logger)
            : base("数据模块", hardwareManagerService, paramService, logger)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            Save(filepath);
        }


        private readonly string filepath =
            $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam\\MemoryData.json";

        /// <summary>
        /// 数据变化事件（供上层 ViewModel / UI 订阅）
        /// </summary>
        public event EventHandler? DataChanged;

        private void OnDataChanged()
        {
            DataChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
            Load(filepath);
            return Task.FromResult(true);
        }

        protected override Task InternalStopAsync()
        {
            Save(filepath);
            return Task.CompletedTask;
        }

        #region MES数据交互

        /// <summary>
        /// 根据工单号获取相关比对信息
        /// </summary>
        /// <param name="LotID">内部批号</param>
        /// <param name="UserID">人员工号</param>
        /// <param name="token">取消令牌</param>
        /// <returns></returns>
        public async Task<MesDetectionParam> QueryFromMes(string LotID, string UserID, CancellationToken token = default)
        {
            try
            {
                MesDetectionParam param = new MesDetectionParam();
                param.InternalBatches = LotID;
                param.UserID = UserID;
                param.ProDuctModel = "PF-Work";
                param.QtyCount = 25; // 假设每批次25片
                param.DetectionStatus = E_DetectionStatus.待检测;
                param.CustomerWaferIDBatches = new List<WaferInfo>();
                param.RecipeName = "TestRecipe";
                for (int i = 1; i <= 25; i++)
                {
                    param.CustomerWaferIDBatches.Add(new WaferInfo()
                    {
                        CustomerBatch = $"PB237Z",
                        WaferID = $"{i:D2}"
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
        private OCRRecipeParam _Station1ReciepParam = new OCRRecipeParam();

        /// <summary>
        /// 工位1配方参数
        /// </summary>
        public OCRRecipeParam Station1ReciepParam => _Station1ReciepParam;

        [JsonInclude]
        private OCRRecipeParam _Station2ReciepParam = new OCRRecipeParam();

        /// <summary>
        /// 工位2配方参数
        /// </summary>
        public OCRRecipeParam Station2ReciepParam => _Station2ReciepParam;

        /// <summary>
        /// 切换工位配方参数
        /// </summary>
        public bool ChangedStationRecipeParam(E_WorkSpace Station, OCRRecipeParam Param)
        {
            if (Station == E_WorkSpace.工位1)
            {
                _Station1ReciepParam = Param;
                OnDataChanged();
                return true;
            }

            if (Station == E_WorkSpace.工位2)
            {
                _Station2ReciepParam = Param;
                OnDataChanged();
                return true;
            }

            return false;
        }

        #endregion 工位配方参数

        #region 检测数据

        [JsonInclude]
        private MesDetectionParam _Station1MesDetectionData = new MesDetectionParam();

        /// <summary>
        /// 工位1检测MES返回数据
        /// </summary>
        public MesDetectionParam Station1MesDetectionData => _Station1MesDetectionData;

        [JsonInclude]
        private MesDetectionParam _Station2MesDetectionData = new MesDetectionParam();

        /// <summary>
        /// 工位2检测MES返回数据
        /// </summary>
        public MesDetectionParam Station2MesDetectionData => _Station2MesDetectionData;

        /// <summary>
        /// 批次数量
        /// </summary>
        [JsonInclude]
        private ConcurrentDictionary<string, int> BathQuantityDic = new ConcurrentDictionary<string, int>();

        /// <summary>
        /// 工位1机台检测数据列表
        /// </summary>
        [JsonInclude]
        private List<MachineDetectionData> _Sation1MachineDetectionData =
            new List<MachineDetectionData>();

        public List<MachineDetectionData> Sation1MachineDetectionData =>
            _Sation1MachineDetectionData;

        /// <summary>
        /// 工位2机台检测数据列表
        /// </summary>
        [JsonInclude]
        private List<MachineDetectionData> _Sation2MachineDetectionData =
            new List<MachineDetectionData>();

        public List<MachineDetectionData> Sation2MachineDetectionData =>
            _Sation2MachineDetectionData;



        /// <summary>
        /// 根据内部批次号索引的检测数据字典
        /// </summary>
        [JsonInclude]
        private ConcurrentDictionary<string, List<MachineDetectionData>> _MachineDetectionDataDic =
            new ConcurrentDictionary<string, List<MachineDetectionData>>();

        public ConcurrentDictionary<string, List<MachineDetectionData>> MachineDetectionDataDic =>
            _MachineDetectionDataDic;

        /// <summary>
        /// 切换工位MES检测数据（同时清空对应工位的检测列表）
        /// </summary>
        public async Task<bool> ChangedStationMesDetectionData(E_WorkSpace Station, MesDetectionParam Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                // 为了不破坏绑定，优先同步字段内容而非直接替换对象引用
                //_Station1MesDetectionData.InternalBatches = Data.InternalBatches;

                //_Station1MesDetectionData.CustomerWaferIDBatches.Clear();
                //foreach (var w in Data.CustomerWaferIDBatches)
                //{
                //    _Station1MesDetectionData.CustomerWaferIDBatches.Add(w);
                //}
                _Station1MesDetectionData = Data;
                _Sation1MachineDetectionData.Clear();
            }
            else if (Station == E_WorkSpace.工位2)
            {
                //_Station2MesDetectionData.InternalBatches = Data.InternalBatches;

                //_Station2MesDetectionData.CustomerWaferIDBatches.Clear();
                //foreach (var w in Data.CustomerWaferIDBatches)
                //{
                //    _Station2MesDetectionData.CustomerWaferIDBatches.Add(w);
                //}
                _Station2MesDetectionData = Data;
                _Sation2MachineDetectionData.Clear();
            }
            else
            {
                return false;
            }

            OnDataChanged();
            return true;
        }

        /// <summary>
        /// 新增一条机台检测数据
        /// </summary>
        public async Task<bool> AddMachineDetectionData(E_WorkSpace Station, MachineDetectionData Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                var kk = _Sation1MachineDetectionData.Where(x => x.CodeValue1 == Data.CodeValue1).FirstOrDefault();
                if (kk != null)
                {
                    if (Data.DetResult || !kk.DetResult)
                    {
                        _Sation1MachineDetectionData.Remove(kk);
                        _Sation1MachineDetectionData.Add(Data);
                    }
                }
                else
                {
                    _Sation1MachineDetectionData.Add(Data);
                }


            }
            else if (Station == E_WorkSpace.工位2)
            {
                var kk = _Sation2MachineDetectionData.Where(x => x.CodeValue1 == Data.CodeValue1).FirstOrDefault();
                if (kk != null)
                {
                    if (Data.DetResult || !kk.DetResult)
                    {
                        _Sation2MachineDetectionData.Remove(kk);
                        _Sation2MachineDetectionData.Add(Data);
                    }
                }
                else
                {
                    _Sation2MachineDetectionData.Add(Data);
                }
            }
            await AddAllDic(Station, Data);
            OnDataChanged();
            return true;
        }



        private async Task AddAllDic(E_WorkSpace Station, MachineDetectionData Data)
        {
            if (!_MachineDetectionDataDic.ContainsKey(Data.InternalBatches))
            {
                _MachineDetectionDataDic.TryAdd (Data.InternalBatches, new List<MachineDetectionData>() { Data });
            }
            else
            {
                _MachineDetectionDataDic[Data.InternalBatches].Add(Data);
            }
            if (!BathQuantityDic.ContainsKey(Data.InternalBatches))
            {
                BathQuantityDic.TryAdd(Data.InternalBatches, Station == E_WorkSpace.工位1 ? _Station1MesDetectionData.QtyCount : _Station2MesDetectionData.QtyCount);
            }
            await CheckAllDic();
        }


        private Task CheckAllDic()
        {
            return Task.Run(() =>
               {
                   foreach (var kvp in _MachineDetectionDataDic)
                   {
                       if (BathQuantityDic.ContainsKey(kvp.Key))
                       {
                           if (kvp.Value.Count == BathQuantityDic[kvp.Key])
                           {
                               /***********数据上传**********/


                               _MachineDetectionDataDic.TryRemove(kvp);
                               BathQuantityDic.Remove(kvp.Key, out var value);
                           }
                       }
                   }
               });
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

            public ConcurrentDictionary<string, List<MachineDetectionData>> AllMachineDetectionDataDic { get; set; } = new ConcurrentDictionary<string, List<MachineDetectionData>>();
            public ConcurrentDictionary<string, int> BathQuantityDic { get; set; } = new ConcurrentDictionary<string, int>();
            #endregion  检测数据


            #region 序列化与反序列化


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
                    Station1ReciepParam = _Station1ReciepParam,
                    Station2ReciepParam = _Station2ReciepParam,
                    Station1MesDetectionData = _Station1MesDetectionData,
                    Station2MesDetectionData = _Station2MesDetectionData,
                    Sation1MachineDetectionData = _Sation1MachineDetectionData,
                    Sation2MachineDetectionData = _Sation2MachineDetectionData,
                    AllMachineDetectionDataDic = _MachineDetectionDataDic,
                    BathQuantityDic = BathQuantityDic,
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
                    this._Station1ReciepParam = tempModule.Station1ReciepParam;
                    this._Station2ReciepParam = tempModule.Station2ReciepParam;
                    this._Station1MesDetectionData = tempModule.Station1MesDetectionData;
                    this._Station2MesDetectionData = tempModule.Station2MesDetectionData;
                    this._Sation1MachineDetectionData = tempModule.Sation1MachineDetectionData;
                    this._Sation2MachineDetectionData = tempModule.Sation2MachineDetectionData;
                    this._MachineDetectionDataDic = tempModule.AllMachineDetectionDataDic;
                    this.BathQuantityDic = tempModule.BathQuantityDic;

                    _logger?.Info($"{MechanismName} 数据加载成功");
                    OnDataChanged();
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
        public string InternalBatches { get; set; } = "";


        /// <summary>
        /// 检测产品型号
        /// </summary>
        public string ProDuctModel { get; set; } = "";


        /// <summary>
        /// 客批和刻号集合
        /// </summary>
        public List<WaferInfo> CustomerWaferIDBatches { get; set; } = new List<WaferInfo>();


        /// <summary>
        /// 当前批次产品个数
        /// </summary>
        public int QtyCount { get; set; } = 0;


        /// <summary>
        /// 检测状态
        /// </summary>
        public E_DetectionStatus DetectionStatus { get; set; } = E_DetectionStatus.检测中;


        /// <summary>
        /// 检测人工号
        /// </summary>
        public string UserID { get; set; } = "NONE";

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
        public string InternalBatches { get; set; } = "";

        /// <summary>
        /// 客户批次
        /// </summary>
        public string CustomerBatches { get; set; } = "";

        /// <summary>
        /// 晶圆ID号
        /// </summary>
        public string WaferID { get; set; } = "";

        /// <summary>
        /// 读取OCR值
        /// </summary>
        public string OCRValue { get; set; } = "";

        /// <summary>
        /// 条码1值
        /// </summary>
        public string CodeValue1 { get; set; } = "";

        /// <summary>
        /// 条码2值
        /// </summary>
        public string CodeValue2 { get; set; } = "";


        /// <summary>
        /// 条码3值
        /// </summary>
        public string OcrCodeValue { get; set; } = "";


        /// <summary>
        /// 比对结果
        /// </summary>
        public bool DetResult { get; set; }

        /// <summary>
        /// 比对异常输出
        /// </summary>
        public string OutError { get; set; } = "NONE";

        /// <summary>
        /// 检测产品型号
        /// </summary>
        public string ProDuctModel { get; set; } = "";


        /// <summary>
        /// 检测人工号
        /// </summary>
        public string UserID { get; set; } = "NONE";

        /// <summary>
        /// 配方名称
        /// </summary>
        public string RecipeName { get; set; } = "NONE";

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
        public string WaferID { get; set; }
    }

}
