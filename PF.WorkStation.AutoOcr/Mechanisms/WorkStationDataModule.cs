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
    public  class WorkStationDataModule : BaseMechanism
    {
        public WorkStationDataModule(IHardwareManagerService hardwareManagerService, IParamService paramService, ILogService logger) : base("数据模块", hardwareManagerService, paramService, logger)
        {

        }


        private string filepath = $"{PF.Core.Constants.ConstGlobalParam.ConfigPath}\\StationMemoryParam\\MemoryData.json";
        protected override Task<bool> InternalInitializeAsync(CancellationToken token)
        {
           Load(filepath);
            return Task.FromResult(true);
        }

        protected override Task InternalStopAsync()
        {
            this.Save(filepath);
            return Task.CompletedTask;
        }



        #region 工位配方参数
        [JsonInclude]
        private OCRRecipeParam _Station1ReciepParam =new OCRRecipeParam ();


        /// <summary>
        /// 工位1配方参数
        /// </summary>
        public OCRRecipeParam Station1ReciepParam => _Station1ReciepParam;
        [JsonInclude]
        private OCRRecipeParam _Station2ReciepParam=new OCRRecipeParam ();


        /// <summary>
        /// 工位2配方参数
        /// </summary>
        public OCRRecipeParam Station2ReciepParam => _Station2ReciepParam;


        /// <summary>
        /// 切换工位配方参数
        /// </summary>
        /// <param name="Station">工位名</param>
        /// <param name="Param">配方参数</param>
        /// <returns></returns>
        public bool ChangedStationRecipeParam(E_WorkSpace Station, OCRRecipeParam Param)
        {
            if (Station == E_WorkSpace.工位1)
            {
                _Station1ReciepParam = Param;
                return true;
            }
            else if (Station == E_WorkSpace.工位2)
            {
                _Station2ReciepParam = Param;
                return true;
            }
            else
            {
                return false;
            }
        }



        #endregion 工位配方参数



        #region 检测数据
        [JsonInclude]
        private MesDetectionParam _Station1MesDetectionData=new MesDetectionParam ();

        /// <summary>
        /// 工位1检测MES返回数据
        /// </summary>
        public MesDetectionParam Station1MesDetectionData => _Station1MesDetectionData;
        [JsonInclude]
        private MesDetectionParam _Station2MesDetectionData=new MesDetectionParam ();
        /// <summary>
        /// 工位2检测MES返回数据
        /// </summary>
        public MesDetectionParam Station2MesDetectionData => _Station2MesDetectionData;

        /// <summary>
        /// 切换工位MES检测数据
        /// </summary>
        /// <param name="Station"></param>
        /// <param name="Data"></param>
        /// <returns></returns>
        public bool ChangedStationMesDetectionData(E_WorkSpace Station, MesDetectionParam Data)
        {
            if (Station == E_WorkSpace.工位1)
            {
                _Station1MesDetectionData = Data;
                _Sation1MachineDetectionData.Clear();
                return true;
            }
            else if (Station == E_WorkSpace.工位2)
            {
                _Station2MesDetectionData = Data;
                _Sation2MachineDetectionData.Clear();
                return true;
            }
            else
            {
                return false;
            }
        }

        [JsonInclude]
        private List<MachineDetectionData> _Sation1MachineDetectionData=new List<MachineDetectionData> ();


        public List<MachineDetectionData> Sation1MachineDetectionData => _Sation1MachineDetectionData;

        [JsonInclude]
        private List<MachineDetectionData> _Sation2MachineDetectionData=new List<MachineDetectionData> ();


        public List<MachineDetectionData> Sation2MachineDetectionData => _Sation2MachineDetectionData;

        [JsonInclude]
        private Dictionary<string, MachineDetectionData> _MachineDetectionDataDic=new Dictionary<string, MachineDetectionData> ();

        public Dictionary<string, MachineDetectionData> MachineDetectionDataDic => _MachineDetectionDataDic;



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
            if (!_MachineDetectionDataDic.ContainsKey(Data.InternalBatches))
            {
                _MachineDetectionDataDic.Add(Data.InternalBatches, Data);
            }
            else
            {
                _MachineDetectionDataDic[Data.InternalBatches] = Data;
            }
            return true;

        }



        #endregion  检测数据


        #region 序列化与反序列化


        /// <summary>
        /// 序列化保存数据
        /// </summary>
        public void Save(string filePath)
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    IncludeFields = true // 确保包含标记了 JsonInclude 的字段
                };
                FileInfo fileinfo = new FileInfo (filePath);
                string folderPath = fileinfo.DirectoryName;
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }
                string json = JsonSerializer.Serialize(this, options);
                File.WriteAllText(filePath, json);
                _logger?.Info($"{this.MechanismName} 数据已保存至: {filePath}");
            }
            catch (Exception ex)
            {
                _logger?.Error($"{this.MechanismName} 保存失败: {ex.Message}");
            }
        }




        /// <summary>
        /// 反序列化加载数据
        /// </summary>
        public bool Load(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Save (filePath ); // 如果文件不存在，先保存一个默认的空数据文件
                return false;
            }

            try
            {
                string json = File.ReadAllText(filePath);
                var options = new JsonSerializerOptions { IncludeFields = true };

                // 重点：反序列化为一个临时实例
                var tempModule = JsonSerializer.Deserialize<WorkStationDataModule>(json, options);

                if (tempModule != null)
                {
                    // 手动将数据同步到当前经过 DI 初始化的实例
                    this._Station1ReciepParam = tempModule._Station1ReciepParam;
                    this._Station2ReciepParam = tempModule._Station2ReciepParam;
                    this._Station1MesDetectionData = tempModule._Station1MesDetectionData;
                    this._Station2MesDetectionData = tempModule._Station2MesDetectionData;
                    this._Sation1MachineDetectionData = tempModule._Sation1MachineDetectionData;
                    this._Sation2MachineDetectionData = tempModule._Sation2MachineDetectionData;
                    this._MachineDetectionDataDic = tempModule._MachineDetectionDataDic;

                    _logger?.Info($"{this.MechanismName} 数据加载成功");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.Error($"{this.MechanismName} 加载失败: {ex.Message}");
            }
            return false;
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


    public class WaferInfo
    {
        public string CustomerBatch { get; set; }
        public string WaferID { get; set; }
    }

}
