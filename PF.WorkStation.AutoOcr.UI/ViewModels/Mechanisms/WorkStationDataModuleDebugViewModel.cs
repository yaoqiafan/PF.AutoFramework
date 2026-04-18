using NPOI.SS.UserModel.Charts;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    /// <summary>
    /// WorkStationDataModuleDebugViewModel
    /// </summary>
    public class WorkStationDataModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStationDataModule? _dataModule;

        private DispatcherTimer _monitorTimer;
        int index = 0;
        string[] imageFiles = default;

        /// <summary>
        /// 供 XAML 直接绑定底层数据集合
        /// </summary>
        public WorkStationDataModule? DataModule => _dataModule;

        private string _debugMessage = "就绪";
        /// <summary>
        /// 成员
        /// </summary>
        public string DebugMessage
        {
            get => _debugMessage;
            set => SetProperty(ref _debugMessage, value);
        }

        #region 工位 1/2 MES 派生属性（简单文本）

        private string _station1InternalBatches = string.Empty;
        /// <summary>
        /// 成员
        /// </summary>
        public string Station1InternalBatches
        {
            get => _station1InternalBatches;
            set => SetProperty(ref _station1InternalBatches, value);
        }

        private string _station2InternalBatches = string.Empty;
        /// <summary>
        /// 成员
        /// </summary>
        public string Station2InternalBatches
        {
            get => _station2InternalBatches;
            set => SetProperty(ref _station2InternalBatches, value);
        }


        private string _Station1RecipeName = string.Empty;
        /// <summary>
        /// 成员
        /// </summary>

        public string Station1RecipeName
        {
            get => _Station1RecipeName;
            set => SetProperty(ref _Station1RecipeName, value);

        }

        private string _Station2RecipeName = string.Empty;
        /// <summary>
        /// 成员
        /// </summary>

        public string Station2RecipeName
        {
            get => _Station2RecipeName;
            set => SetProperty(ref _Station2RecipeName, value);

        }
        #endregion 工位 1/2 MES 派生属性（简单文本）

        #region 数据集合
        private ObservableCollection<WaferInfo> _Station1MesDetection = new ObservableCollection<WaferInfo>();
        /// <summary>
        /// 获取或设置 Station1MesDetection
        /// </summary>
        public ObservableCollection<WaferInfo> Station1MesDetection
        {
            get => _Station1MesDetection;
            set => SetProperty(ref _Station1MesDetection, value);
        }


        private ObservableCollection<WaferInfo> _Station2MesDetection = new ObservableCollection<WaferInfo>();
        /// <summary>
        /// 获取或设置 Station2MesDetection
        /// </summary>

        public ObservableCollection<WaferInfo> Station2MesDetection
        {
            get => _Station2MesDetection;
            set => SetProperty(ref _Station2MesDetection, value);
        }
        private ObservableCollection<MachineDetectionData> _Station1MachineDetection = new ObservableCollection<MachineDetectionData>();
        /// <summary>
        /// 获取或设置 Station1MachineDetection
        /// </summary>
        public ObservableCollection<MachineDetectionData> Station1MachineDetection
        {
            get => _Station1MachineDetection;
            set => SetProperty(ref _Station1MachineDetection, value);
        }

        private ObservableCollection<MachineDetectionData> _Station2MachineDetection = new ObservableCollection<MachineDetectionData>();
        /// <summary>
        /// 获取或设置 Station2MachineDetection
        /// </summary>
        public ObservableCollection<MachineDetectionData> Station2MachineDetection
        {
            get => _Station2MachineDetection;
            set => SetProperty(ref _Station2MachineDetection, value);
        }

        private ObservableCollection<MachineDetectionData> _AllMachineDetection = new ObservableCollection<MachineDetectionData>();
        /// <summary>
        /// 获取或设置 AllMachineDetection
        /// </summary>
        public ObservableCollection<MachineDetectionData> AllMachineDetection
        {
            get => _AllMachineDetection;
            set => SetProperty(ref _AllMachineDetection, value);
        }

        #endregion 数据集合







        #region Commands

        // 1. 顶部全局生命周期控制
        /// <summary>
        /// InitializeModule 命令
        /// </summary>
        public DelegateCommand InitializeModuleCommand { get; }
        /// <summary>
        /// ResetModule 命令
        /// </summary>
        public DelegateCommand ResetModuleCommand { get; }
        /// <summary>
        /// Stop 命令
        /// </summary>
        public DelegateCommand StopCommand { get; }




        /// <summary>
        /// 手动刷新数据（从 DataModule 重新拉取一次派生字段）
        /// </summary>
        public DelegateCommand RefreshDataCommand { get; }
        /// <summary>
        /// Station1ChangeLot 命令
        /// </summary>



        public DelegateCommand Station1ChangeLotCommand { get; }
        /// <summary>
        /// Station2ChangeLot 命令
        /// </summary>


        public DelegateCommand Station2ChangeLotCommand { get; }
        /// <summary>
        /// AddStation1Det 命令
        /// </summary>


        public DelegateCommand AddStation1DetCommand { get; set; }
        /// <summary>
        /// AddStation2Det 命令
        /// </summary>

        public DelegateCommand AddStation2DetCommand { get; set; }

        #endregion
        /// <summary>
        /// WorkStationDataModuleDebugViewModel 构造函数
        /// </summary>

        public WorkStationDataModuleDebugViewModel(IContainerProvider containerProvider)
        {
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule))
                as WorkStationDataModule;

            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _dataModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _dataModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _dataModule?.StopAsync()));
            Station1ChangeLotCommand = new DelegateCommand(ChangeStation1Lot);
            Station2ChangeLotCommand = new DelegateCommand(ChangeStation2Lot);
            AddStation1DetCommand = new DelegateCommand(AddStation1Det);
            AddStation2DetCommand = new DelegateCommand(AddStation2Det);
            if (_dataModule != null)
            {
                // 订阅底层模块的数据变化事件
                _dataModule.DataChanged += async (s, e) =>
                {
                    try
                    {
                        await RefreshAllAsync();
                    }
                    catch (Exception ex)
                    {
                        DebugMessage = $"自动刷新异常: {ex.Message}";
                    }
                };
            }
            else
            {
                DebugMessage = "未解析到 WorkStationDataModule 实例";
            }
            RefreshAllAsync();

            
            // 1. 获取当前运行目录 (例如: PF.WorkStation.AutoOcr.UI\bin\Debug\net8.0-windows\)
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;

            // 2. 向上回退 3 层，精准定位到当前 UI 项目的源码根目录
            // Path.GetFullPath 会自动解析 "..\" 并返回绝对路径
            string projectRootDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\"));

            // 3. 拼接目标文件夹路径
            string sampleImagesDir = Path.Combine(projectRootDir, "PF.WorkStation.AutoOcr.UI", "SampleImages");

            // 4. 读取图片
            if (Directory.Exists(sampleImagesDir))
            {
                imageFiles = Directory.GetFiles(sampleImagesDir, "*.png");
                // 拿去喂给视觉算法或显示
            }
            //StartMonitor();
        }


        #region 内部执行逻辑与状态更新


        private async Task ExecuteAsync(Func<Task> action)
        {
            try
            {
                DebugMessage = "执行中...";
                await action.Invoke();
                DebugMessage = "执行成功";
            }
            catch (Exception ex)
            {
                DebugMessage = $"执行异常: {ex.Message}";
                MessageService.ShowMessage(ex.Message, "数据模块调试面板报错", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 从 WorkStationDataModule 更新对 UI 有用的派生字段。
        /// 集合本身直接绑定 DataModule 的 ObservableCollection，由其自己通知 UI。
        /// </summary>
        private Task RefreshAllAsync()
        {
            if (_dataModule == null)
            {
                DebugMessage = "未解析到 WorkStationDataModule 实例";
                return Task.CompletedTask;
            }

            Station1MesDetection = new ObservableCollection<WaferInfo>(_dataModule.Station1MesDetectionData.CustomerWafers);
            Station2MesDetection = new ObservableCollection<WaferInfo>(_dataModule.Station2MesDetectionData.CustomerWafers);

            Station1MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation1MachineDetectionData);
            Station2MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation2MachineDetectionData);

            AllMachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.MachineDataByBatch.Values.SelectMany(x => x).ToList());
            Station2InternalBatches = _dataModule.Station2MesDetectionData?.InternalBatchId ?? string.Empty;
            Station1InternalBatches = _dataModule.Station1MesDetectionData?.InternalBatchId ?? string.Empty;
            Station1RecipeName = _dataModule.Station1ReciepParam.RecipeName;
            Station2RecipeName = _dataModule.Station2ReciepParam.RecipeName;
            return Task.CompletedTask;
        }


        /// <summary>
        /// 后台轮询线程，用于更新坐标和IO状态指示灯
        /// </summary>
        private void StartMonitor()
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _monitorTimer.Tick += (s, e) =>
            {
                RefreshAllAsync();
            };
            _monitorTimer.Start();
        }



        private void ChangeStation1Lot()
        {
            if (_dataModule != null)
            {
                MesDetectionParam info = new MesDetectionParam();
                info.Quantity = 13;
                info.InternalBatchId = "TestLot1ID";
                info.CustomerWafers = new List<WaferInfo>();
                for (int i = 0; i < 13; i++)
                {
                    info.CustomerWafers.Add(new WaferInfo()
                    {
                        CustomerBatch = $"Guest1ID{i}",
                        WaferId = i.ToString("D2")
                    });
                }
                _dataModule.UpdateStationMesInfoAsync(E_WorkSpace.工位1, info);
            }
        }

        private void ChangeStation2Lot()
        {
            if (_dataModule != null)
            {
                MesDetectionParam info = new MesDetectionParam();
                info.Quantity = 13;
                info.InternalBatchId = "TestLot2ID";
                info.CustomerWafers = new List<WaferInfo>();
                for (int i = 0; i < 13; i++)
                {
                    info.CustomerWafers.Add(new WaferInfo()
                    {
                        CustomerBatch = $"Guest2ID{i}",
                        WaferId = i.ToString("D2")
                    });
                }
                _dataModule.UpdateStationMesInfoAsync(E_WorkSpace.工位2, info);
            }
        }


      

        private void AddStation1Det()
        {
           
            var kk = Station1MachineDetection.Select(x => x.WaferId).ToList();
            var kkk = Station1MesDetection.Where(x => !kk.Contains(x.WaferId)).FirstOrDefault();
            if (kkk == null)
            {
                DebugMessage = $"工位1数据已满";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_dataModule != null)
            {
                if (index> imageFiles.Length)
                {
                    index = 0;
                }
                string path = imageFiles[index++];


                MachineDetectionData info = new MachineDetectionData()
                {
                    InternalBatchId = Station1InternalBatches,
                    CustomerBatch = kkk.CustomerBatch,
                    WaferId = kkk.WaferId,
                    OcrText = $"{kkk.CustomerBatch}-{kkk.WaferId}-A0",
                    Barcode1 = $"{kkk.CustomerBatch}-{kkk.WaferId}",
                    Barcode2 = $"{kkk.CustomerBatch}-{kkk.WaferId}",
                    Barcode3 = $"{kkk.CustomerBatch}-{kkk.WaferId}-A0",
                    ImagePath = path
                };
                _dataModule.AddMachineDetectionAsync(E_WorkSpace.工位1, info);
            }
        }

        private void AddStation2Det()
        {

            var kk = Station2MachineDetection.Select(x => x.WaferId).ToList();
            var kkk = Station2MesDetection.Where(x => !kk.Contains(x.WaferId)).FirstOrDefault();
            if (kkk == null)
            {
                DebugMessage = $"工位2数据已满";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (_dataModule != null)
            {
                if (index > imageFiles.Length)
                {
                    index = 0;
                }
                string path = imageFiles[index++];

                MachineDetectionData info = new MachineDetectionData()
                {
                    InternalBatchId = Station1InternalBatches,
                    CustomerBatch = kkk.CustomerBatch,
                    WaferId = kkk.WaferId,
                    OcrText = $"{kkk.CustomerBatch}-{kkk.WaferId}-A0",
                    Barcode1 = $"{kkk.CustomerBatch}-{kkk.WaferId}",
                    Barcode2 = $"{kkk.CustomerBatch}-{kkk.WaferId}",
                    Barcode3 = $"{kkk.CustomerBatch}-{kkk.WaferId}-A0",
                    ImagePath = path
                };
                _dataModule.AddMachineDetectionAsync(E_WorkSpace.工位2, info);
            }
        }

        #endregion  内部执行逻辑与状态更新
    }
}
