using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    public class WorkStationDetectionModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStationDetectionModule? _detectionModule;

        public WorkStationDetectionModule? DetectionModule => _detectionModule;

        private DispatcherTimer _monitorTimer;

        private string _debugMessage = "就绪";
        public string DebugMessage
        {
            get => _debugMessage;
            set => SetProperty(ref _debugMessage, value);
        }

        private int _targetLayer;
        public int TargetLayer
        {
            get => _targetLayer;
            set => SetProperty(ref _targetLayer, value);
        }

        #region 状态监控属性


        private double _xAxisPosition;
        public double XAxisPosition { get => _xAxisPosition; set => SetProperty(ref _xAxisPosition, value); }



        private double _yAxisPosition;
        public double YAxisPosition { get => _yAxisPosition; set => SetProperty(ref _yAxisPosition, value); }

        private double _zAxisPosition;
        public double ZAxisPosition { get => _zAxisPosition; set => SetProperty(ref _zAxisPosition, value); }


        private bool _xAxisHasAlarm;
        public bool XAxisHasAlarm { get => _xAxisHasAlarm; set => SetProperty(ref _xAxisHasAlarm, value); }


        private bool _yAxisHasAlarm;
        public bool YAxisHasAlarm { get => _yAxisHasAlarm; set => SetProperty(ref _yAxisHasAlarm, value); }


        private bool _zAxisHasAlarm;
        public bool ZAxisHasAlarm { get => _zAxisHasAlarm; set => SetProperty(ref _zAxisHasAlarm, value); }

        #endregion 状态监控属性


        #region Commands 定义
        public DelegateCommand InitializeModuleCommand { get; }
        public DelegateCommand ResetModuleCommand { get; }
        public DelegateCommand StopCommand { get; }


        #endregion Commands 定义


        public WorkStationDetectionModuleDebugViewModel(IContainerProvider containerProvider)
        {
            // 依赖注入获取模组实例
            _detectionModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDetectionModule)) as WorkStationDetectionModule;
            // --- 绑定全局生命周期指令 ---
            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _detectionModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _detectionModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _detectionModule?.StopAsync()));



            StartMonitor();
        }





        #region 内部执行逻辑与状态更新


        private async Task ExecuteAsync(Func<Task>? action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = "执行中...";
                await action.Invoke();
                DebugMessage = "执行成功";
            }
            catch (Exception ex)
            {
                DebugMessage = $"执行异常: {ex.Message}";
                MessageService.ShowMessage(ex.Message, "调试面板报错", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }







        private void StartMonitor()
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _monitorTimer.Tick += (s, e) =>
            {
                if (_detectionModule == null || !_detectionModule.IsInitialized) return;

                // 刷新轴状态
                if (_detectionModule.ZAxis != null)
                {
                    ZAxisPosition = _detectionModule.ZAxis.CurrentPosition ?? 0;
                    ZAxisHasAlarm = _detectionModule.ZAxis.HasAlarm;
                }
                if (_detectionModule.XAxis != null)
                {
                    XAxisPosition = _detectionModule.XAxis.CurrentPosition ?? 0;
                    XAxisHasAlarm = _detectionModule.XAxis.HasAlarm;
                }
                if (_detectionModule.YAxis != null)
                {
                    YAxisPosition = _detectionModule.YAxis.CurrentPosition ?? 0;
                    YAxisHasAlarm = _detectionModule.YAxis.HasAlarm;
                }

                // 刷新 IO 状态

            };
            _monitorTimer.Start();
        }

        #endregion 内部执行逻辑与状态更新

    }
}
