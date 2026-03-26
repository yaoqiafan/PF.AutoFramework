using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.UI.UserControls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels
{
    public class HomeViewModel : RegionViewModelBase
    {

        private readonly WorkStationDataModule? _dataModule;

        private DispatcherTimer _monitorTimer;

        #region 工位 1/2 派生属性（简单文本）

        private string _station1InternalBatches = "NONE";
        public string Station1InternalBatches
        {
            get => _station1InternalBatches;
            set => SetProperty(ref _station1InternalBatches, value);
        }


        private E_DetectionStatus _station1DetStatus = E_DetectionStatus.检测中;

        public E_DetectionStatus Station1DetStatus
        {
            get => _station1DetStatus; set => SetProperty(ref _station1DetStatus, value);
        }

        private E_DetectionStatus _station2DetStatus = E_DetectionStatus.检测中;

        public E_DetectionStatus Station2DetStatus
        {
            get => _station2DetStatus; set => SetProperty(ref _station2DetStatus, value);
        }


        private string _station2InternalBatches = "NONE";
        public string Station2InternalBatches
        {
            get => _station2InternalBatches;
            set => SetProperty(ref _station2InternalBatches, value);
        }


        private string _Station1RecipeName = "NONE";

        public string Station1RecipeName
        {
            get => _Station1RecipeName;
            set => SetProperty(ref _Station1RecipeName, value);

        }

        private string _Station2RecipeName = "NONE";

        public string Station2RecipeName
        {
            get => _Station2RecipeName;
            set => SetProperty(ref _Station2RecipeName, value);

        }
        #endregion 工位 1/2 派生属性（简单文本）


        #region 数据集合

        private ObservableCollection<MachineDetectionData> _Station1MachineDetection = new ObservableCollection<MachineDetectionData>();
        public ObservableCollection<MachineDetectionData> Station1MachineDetection
        {
            get => _Station1MachineDetection;
            set => SetProperty(ref _Station1MachineDetection, value);
        }

        private ObservableCollection<MachineDetectionData> _Station2MachineDetection = new ObservableCollection<MachineDetectionData>();
        public ObservableCollection<MachineDetectionData> Station2MachineDetection
        {
            get => _Station2MachineDetection;
            set => SetProperty(ref _Station2MachineDetection, value);
        }



        #endregion 数据集合



        #region Command

        public DelegateCommand Station1ChangeLotCommand { get; }

        public DelegateCommand Station2ChangeLotCommand { get; }

        #endregion Command






        public HomeViewModel(IContainerProvider containerProvider)
        {
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule)) as WorkStationDataModule;

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


            Station2InternalBatches = _dataModule.Station2MesDetectionData?.InternalBatches ?? string.Empty;
            Station1InternalBatches = _dataModule.Station1MesDetectionData?.InternalBatches ?? string.Empty;
            Station1RecipeName = _dataModule.Station1ReciepParam.RecipeName;
            Station2RecipeName = _dataModule.Station2ReciepParam.RecipeName;
            Station1DetStatus = _dataModule.Station1MesDetectionData.DetectionStatus;
            Station2DetStatus = _dataModule.Station2MesDetectionData.DetectionStatus;
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


        private void OnDialogCallbackStation1(IDialogResult result)
        {

        }


        private void OnDialogCallbackStation2(IDialogResult result)
        {

        }
    }
}
