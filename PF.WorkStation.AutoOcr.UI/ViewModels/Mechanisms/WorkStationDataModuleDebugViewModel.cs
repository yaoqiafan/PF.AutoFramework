using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    public class WorkStationDataModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStationDataModule? _dataModule;

        /// <summary>
        /// 供 XAML 直接绑定底层数据集合
        /// </summary>
        public WorkStationDataModule? DataModule => _dataModule;

        private string _debugMessage = "就绪";
        public string DebugMessage
        {
            get => _debugMessage;
            set => SetProperty(ref _debugMessage, value);
        }

        #region 工位 1/2 MES 派生属性（简单文本）

        private string _station1InternalBatches = string.Empty;
        public string Station1InternalBatches
        {
            get => _station1InternalBatches;
            set => SetProperty(ref _station1InternalBatches, value);
        }

        private string _station2InternalBatches = string.Empty;
        public string Station2InternalBatches
        {
            get => _station2InternalBatches;
            set => SetProperty(ref _station2InternalBatches, value);
        }

        #region 数据集合
        public ObservableCollection<WaferInfo> Station1MesDetection = new ObservableCollection<WaferInfo>();

        public ObservableCollection<WaferInfo> Station2MesDetection = new ObservableCollection<WaferInfo>();

        public ObservableCollection<MachineDetectionData> Station1MachineDetection = new ObservableCollection<MachineDetectionData>();

        public ObservableCollection<MachineDetectionData> Station2MachineDetection = new ObservableCollection<MachineDetectionData>();

        public ObservableCollection<MachineDetectionData> AllMachineDetection = new ObservableCollection<MachineDetectionData>();

        #endregion 数据集合





        #endregion

        #region Commands

        /// <summary>
        /// 手动刷新数据（从 DataModule 重新拉取一次派生字段）
        /// </summary>
        public DelegateCommand RefreshDataCommand { get; }

        #endregion

        public WorkStationDataModuleDebugViewModel(IContainerProvider containerProvider)
        {
            _dataModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDataModule))
                as WorkStationDataModule;

            RefreshDataCommand = new DelegateCommand(async () => await ExecuteAsync(RefreshAllAsync));

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

            _ = RefreshAllAsync();
        }

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

            Station1MesDetection = new ObservableCollection<WaferInfo>(_dataModule.Station1MesDetectionData.CustomerWaferIDBatches);
            Station2MesDetection = new ObservableCollection<WaferInfo>(_dataModule.Station2MesDetectionData.CustomerWaferIDBatches);

            Station1MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation1MachineDetectionData);
            Station2MachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.Sation2MachineDetectionData);

            AllMachineDetection = new ObservableCollection<MachineDetectionData>(_dataModule.MachineDetectionDataDic.Select(x => x.Value).ToList());
            Station2InternalBatches = _dataModule.Station2MesDetectionData?.InternalBatches ?? string.Empty;
            Station1InternalBatches = _dataModule.Station1MesDetectionData?.InternalBatches ?? string.Empty;

            return Task.CompletedTask;
        }
    }
}
