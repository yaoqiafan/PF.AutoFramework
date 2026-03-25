
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
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static NPOI.HSSF.UserModel.HeaderFooter;

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
        /// 所有机台检测数据汇总（便于 UI 统一查看）
        /// </summary>
        [JsonInclude]
        private List<MachineDetectionData> _AllMachineDetectionData =
            new List<MachineDetectionData>();

        public List<MachineDetectionData> AllMachineDetectionData =>
            _AllMachineDetectionData;

        /// <summary>
        /// 根据内部批次号索引的检测数据字典
        /// </summary>
        [JsonInclude]
        private Dictionary<string, MachineDetectionData> _MachineDetectionDataDic =
            new Dictionary<string, MachineDetectionData>();

        public Dictionary<string, MachineDetectionData> MachineDetectionDataDic =>
            _MachineDetectionDataDic;

        /// <summary>
        /// 切换工位MES检测数据（同时清空对应工位的检测列表）
        /// </summary>
        public bool ChangedStationMesDetectionData(E_WorkSpace Station, MesDetectionParam Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                // 为了不破坏绑定，优先同步字段内容而非直接替换对象引用
                _Station1MesDetectionData.InternalBatches = Data.InternalBatches;

                _Station1MesDetectionData.CustomerWaferIDBatches.Clear();
                foreach (var w in Data.CustomerWaferIDBatches)
                {
                    _Station1MesDetectionData.CustomerWaferIDBatches.Add(w);
                }

                _Sation1MachineDetectionData.Clear();
            }
            else if (Station == E_WorkSpace.工位2)
            {
                _Station2MesDetectionData.InternalBatches = Data.InternalBatches;

                _Station2MesDetectionData.CustomerWaferIDBatches.Clear();
                foreach (var w in Data.CustomerWaferIDBatches)
                {
                    _Station2MesDetectionData.CustomerWaferIDBatches.Add(w);
                }

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
        public bool AddMachineDetectionData(E_WorkSpace Station, MachineDetectionData Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                _Sation1MachineDetectionData.Add(Data);
            }
            else if (Station == E_WorkSpace.工位2)
            {
                _Sation2MachineDetectionData.Add(Data);
            }

            _AllMachineDetectionData.Add(Data);

            if (!_MachineDetectionDataDic.ContainsKey(Data.InternalBatches))
            {
                _MachineDetectionDataDic.Add(Data.InternalBatches, Data);
            }
            else
            {
                _MachineDetectionDataDic[Data.InternalBatches] = Data;
            }

            OnDataChanged();
            return true;
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

            public List<MachineDetectionData> Sation1MachineDetectionData { get; set; } =
                new List<MachineDetectionData>();

            public List<MachineDetectionData> Sation2MachineDetectionData { get; set; } =
                new List<MachineDetectionData>();

            public List<MachineDetectionData> AllMachineDetectionData { get; set; } =
                new List<MachineDetectionData>();

            public Dictionary<string, MachineDetectionData> MachineDetectionDataDic { get; set; } =
                new Dictionary<string, MachineDetectionData>();
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
                    AllMachineDetectionData = _AllMachineDetectionData,
                    MachineDetectionDataDic = _MachineDetectionDataDic
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

                var json = JsonSerializer.Serialize(snapshot, options);
                System.IO.File.WriteAllText(filePath, json);
                _logger?.Info($"{MechanismName} 数据已保存至: {filePath}");
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

                var snapshot = JsonSerializer.Deserialize<WorkStationDataModuleSnapshot>(json, options);
                if (snapshot == null)
                {
                    return false;
                }

                _Station1ReciepParam = snapshot.Station1ReciepParam ?? new OCRRecipeParam();
                _Station2ReciepParam = snapshot.Station2ReciepParam ?? new OCRRecipeParam();
                _Station1MesDetectionData = snapshot.Station1MesDetectionData ?? new MesDetectionParam();
                _Station2MesDetectionData = snapshot.Station2MesDetectionData ?? new MesDetectionParam();
                _Sation1MachineDetectionData = snapshot.Sation1MachineDetectionData
                                               ?? new List<MachineDetectionData>();
                _Sation2MachineDetectionData = snapshot.Sation2MachineDetectionData
                                               ?? new List<MachineDetectionData>();
                _AllMachineDetectionData = snapshot.AllMachineDetectionData
                                            ?? new List<MachineDetectionData>();
                _MachineDetectionDataDic = snapshot.MachineDetectionDataDic
                                           ?? new Dictionary<string, MachineDetectionData>();

                _logger?.Info($"{MechanismName} 数据加载成功");
                OnDataChanged();
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
        /// 客批和刻号集合
        /// </summary>
        public List<WaferInfo> CustomerWaferIDBatches { get; set; } = new List<WaferInfo>();


        /// <summary>
        /// 当前批次产品个数
        /// </summary>
        public int QtyCount { get; set; } = 0;

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
