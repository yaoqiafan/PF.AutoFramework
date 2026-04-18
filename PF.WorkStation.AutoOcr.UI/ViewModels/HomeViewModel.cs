using NPOI.SS.Formula.Functions;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Interfaces.Identity;
using PF.Core.Interfaces.Recipe;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.UI.UserControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    /// <summary>
    /// HomeViewModel
    /// </summary>
    public class HomeViewModel : RegionViewModelBase
    {

        private readonly WorkStationDataModule? _dataModule;

        private readonly IUserService _userService;

        private DispatcherTimer _monitorTimer;

        private readonly IRecipeService<OCRRecipeParam> _recipeService;





        #region 工位 1/2 派生属性（简单文本）

        private string _station1InternalBatches = "NONE";
        /// <summary>
        /// 成员
        /// </summary>
        public string Station1InternalBatches
        {
            get => _station1InternalBatches;
            set => SetProperty(ref _station1InternalBatches, value);
        }


        private E_DetectionStatus _station1DetStatus = E_DetectionStatus.检测中;
        /// <summary>
        /// 成员
        /// </summary>

        public E_DetectionStatus Station1DetStatus
        {
            get => _station1DetStatus; set => SetProperty(ref _station1DetStatus, value);
        }

        private E_DetectionStatus _station2DetStatus = E_DetectionStatus.检测中;
        /// <summary>
        /// 成员
        /// </summary>

        public E_DetectionStatus Station2DetStatus
        {
            get => _station2DetStatus; set => SetProperty(ref _station2DetStatus, value);
        }


        private string _station2InternalBatches = "NONE";
        /// <summary>
        /// 成员
        /// </summary>
        public string Station2InternalBatches
        {
            get => _station2InternalBatches;
            set => SetProperty(ref _station2InternalBatches, value);
        }


        private string _Station1RecipeName = "NONE";
        /// <summary>
        /// 成员
        /// </summary>

        public string Station1RecipeName
        {
            get => _Station1RecipeName;
            set => SetProperty(ref _Station1RecipeName, value);

        }

        private string _Station2RecipeName = "NONE";
        /// <summary>
        /// 成员
        /// </summary>

        public string Station2RecipeName
        {
            get => _Station2RecipeName;
            set => SetProperty(ref _Station2RecipeName, value);

        }
        #endregion 工位 1/2 派生属性（简单文本）


        #region 数据集合

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


        private MachineDetectionData _Station1CurrentMachineDetection;
        /// <summary>
        /// 成员
        /// </summary>
        public MachineDetectionData Station1CurrentMachineDetection
        {
            get { return _Station1CurrentMachineDetection; }
            set { SetProperty(ref _Station1CurrentMachineDetection, value); }
        }

        private MachineDetectionData _Station2CurrentMachineDetection;
        /// <summary>
        /// 成员
        /// </summary>
        public MachineDetectionData Station2CurrentMachineDetection
        {
            get { return _Station2CurrentMachineDetection; }
            set { SetProperty(ref _Station2CurrentMachineDetection, value); }
        }


        #endregion 数据集合



        #region Command
        /// <summary>
        /// Station1ChangeLot 命令
        /// </summary>

        public DelegateCommand Station1ChangeLotCommand { get; }
        /// <summary>
        /// Station2ChangeLot 命令
        /// </summary>

        public DelegateCommand Station2ChangeLotCommand { get; }

        #endregion Command
        /// <summary>
        /// HomeViewModel 构造函数
        /// </summary>






        public HomeViewModel(IContainerProvider containerProvider, IUserService userService)
        {
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;
            _userService = userService;
            _recipeService = containerProvider.Resolve<IRecipeService<OCRRecipeParam>>();

            Station1ChangeLotCommand = new DelegateCommand(Station1ShowChangeLotView);

            Station2ChangeLotCommand = new DelegateCommand(Station2ShowChangeLotView);
            if (_dataModule != null)
            {
                _dataModule.DataChanged += async (s, e) =>
                {
                    try
                    {
                        await RefreshAllAsync();
                    }
                    catch (Exception ex)
                    {

                    }
                };
            }
            RefreshAllAsync();
        }


        /// <summary>
        /// 从 WorkStationDataModule 更新对 UI 有用的派生字段。
        /// 集合本身直接绑定 DataModule 的 ObservableCollection，由其自己通知 UI。
        /// </summary>
        private Task RefreshAllAsync()
        {
            if (_dataModule == null)
            {
                return Task.CompletedTask;
            }



            Station1MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation1MachineDetectionData);
            Station2MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation2MachineDetectionData);


            Station2InternalBatches = _dataModule.Station2MesDetectionData?.InternalBatchId ?? string.Empty;
            Station1InternalBatches = _dataModule.Station1MesDetectionData?.InternalBatchId ?? string.Empty;
            Station1RecipeName = _dataModule.Station1MesDetectionData.RecipeName;
            Station2RecipeName = _dataModule.Station2MesDetectionData.RecipeName;
            Station1DetStatus = _dataModule.Station1MesDetectionData.DetectionStatus;
            Station2DetStatus = _dataModule.Station2MesDetectionData.DetectionStatus;

            if (Station1MachineDetection.Count != 0)
            {
                MachineDetectionData? latestData = Station1MachineDetection
                    .OrderByDescending(data => data.Time)
                    .FirstOrDefault();
                if (latestData != null)
                {
                    Station1CurrentMachineDetection = latestData;
                }

            }

            if (Station2MachineDetection.Count != 0)
            {
                MachineDetectionData? latestData = Station2MachineDetection
                    .OrderByDescending(data => data.Time)
                    .FirstOrDefault();
                if (latestData != null)
                {
                    Station2CurrentMachineDetection = latestData;
                }

            }
            return Task.CompletedTask;
        }



        private async void Station1ShowChangeLotView()
        {
            var parameters = new DialogParameters();
            DialogService.ShowDialog(nameof(ChangeLotView), parameters, OnDialogCallbackStation1);
        }
        private async void Station2ShowChangeLotView()
        {
            var parameters = new DialogParameters();
            DialogService.ShowDialog(nameof(ChangeLotView), parameters, OnDialogCallbackStation2);
        }


        private async void OnDialogCallbackStation1(IDialogResult result)
        {
            if (result.Result == ButtonResult.OK)
            {
                var param = result.Parameters as DialogParameters;
                if (param != null && param.ContainsKey("Lotid") && param.ContainsKey("Userid"))
                {
                    string Userid = param.GetValue<string>("Userid");
                    string lotid = param.GetValue<string>("Lotid");
                    if ((await _userService.GetUserListAsync()).ToList().FindIndex(x => x.UserName == Userid) == -1)
                    {
                        MessageService.ShowMessage($"{Userid}用户不存在 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    var info = await _dataModule?.QueryMesAsync(lotid, Userid);
                    if (info == null)
                    {

                        MessageService.ShowMessage($"{lotid}获取检测数据错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }



                    if (!await _dataModule.UpdateStationMesInfoAsync(E_WorkSpace.工位1, info))
                    {
                        MessageService.ShowMessage($"工位1切换批次失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var kk = await _recipeService.RecipeParam("New_Recipe_141457");
                    if (kk == null)
                    {
                        MessageService.ShowMessage($"获取配方参数失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (!_dataModule.UpdateStationRecipeParam(E_WorkSpace.工位1, kk))
                    {
                        MessageService.ShowMessage($"配方切换失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    MessageService.ShowMessage($"工位1切换批次成功 ", "提示", MessageBoxButton.OK, MessageBoxImage.Information);


                }
                else
                {
                    MessageService.ShowMessage($"参数传递错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }



        }


        private async void OnDialogCallbackStation2(IDialogResult result)
        {
            if (result.Result == ButtonResult.OK)
            {
                var param = result.Parameters as DialogParameters;
                if (param != null && param.ContainsKey("Lotid") && param.ContainsKey("Userid"))
                {
                    string Userid = param.GetValue<string>("Userid");
                    string lotid = param.GetValue<string>("Lotid");

                    var info = await _dataModule?.QueryMesAsync(lotid, Userid);
                    if (info == null)
                    {
                        MessageService.ShowMessage($"{lotid}获取检测数据错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    if (await _dataModule.UpdateStationMesInfoAsync(E_WorkSpace.工位2, info))
                    {
                        MessageService.ShowMessage($"工位2切换批次成功 ", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    else
                    {
                        MessageService.ShowMessage($"工位2切换批次失败 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
                else
                {
                    MessageService.ShowMessage($"参数传递错误 ", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }
    }
}
