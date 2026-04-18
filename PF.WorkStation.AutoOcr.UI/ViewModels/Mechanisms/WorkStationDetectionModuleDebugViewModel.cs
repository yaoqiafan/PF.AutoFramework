using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
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
    /// <summary>
    /// WorkStationDetectionModuleDebugViewModel
    /// </summary>
    public class WorkStationDetectionModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStationDetectionModule? _detectionModule;
        /// <summary>
        /// 获取或设置 DetectionModule
        /// </summary>

        public WorkStationDetectionModule? DetectionModule => _detectionModule;

        private DispatcherTimer _monitorTimer;

        private string _debugMessage = "就绪";
        /// <summary>
        /// 成员
        /// </summary>
        public string DebugMessage
        {
            get => _debugMessage;
            set => SetProperty(ref _debugMessage, value);
        }

        private int _targetLayer;
        /// <summary>
        /// 成员
        /// </summary>
        public int TargetLayer
        {
            get => _targetLayer;
            set => SetProperty(ref _targetLayer, value);
        }

        private string _camRec = "NONE";

        /// <summary>
        /// 获取或设置 CamRec
        /// </summary>
        public string CamRec
        {
            get => _camRec;
            set => SetProperty(ref _camRec, value);
        }


        #region 状态监控属性


        private double _xAxisPosition;
        /// <summary>
        /// 获取或设置 XAxisPosition
        /// </summary>
        public double XAxisPosition { get => _xAxisPosition; set => SetProperty(ref _xAxisPosition, value); }



        private double _yAxisPosition;
        /// <summary>
        /// 获取或设置 YAxisPosition
        /// </summary>
        public double YAxisPosition { get => _yAxisPosition; set => SetProperty(ref _yAxisPosition, value); }

        private double _zAxisPosition;
        /// <summary>
        /// 获取或设置 ZAxisPosition
        /// </summary>
        public double ZAxisPosition { get => _zAxisPosition; set => SetProperty(ref _zAxisPosition, value); }


        private bool _xAxisHasAlarm;
        /// <summary>
        /// 获取或设置 XAxisHasAlarm
        /// </summary>
        public bool XAxisHasAlarm { get => _xAxisHasAlarm; set => SetProperty(ref _xAxisHasAlarm, value); }


        private bool _yAxisHasAlarm;
        /// <summary>
        /// 获取或设置 YAxisHasAlarm
        /// </summary>
        public bool YAxisHasAlarm { get => _yAxisHasAlarm; set => SetProperty(ref _yAxisHasAlarm, value); }


        private bool _zAxisHasAlarm;
        /// <summary>
        /// 获取或设置 ZAxisHasAlarm
        /// </summary>
        public bool ZAxisHasAlarm { get => _zAxisHasAlarm; set => SetProperty(ref _zAxisHasAlarm, value); }

        #endregion 状态监控属性



        #region 点位定义集合表
        /// <summary>
        /// 获取或设置 XAxisOriginalPoints
        /// </summary>

        public ObservableCollection<AxisPoint> XAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();
        /// <summary>
        /// 获取或设置 YAxisOriginalPoints
        /// </summary>
        public ObservableCollection<AxisPoint> YAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();
        /// <summary>
        /// 获取或设置 ZAxisOriginalPoints
        /// </summary>

        public ObservableCollection<AxisPoint> ZAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();

        #endregion 点位定义集合表


        #region Commands 定义
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




        //点位保存
        /// <summary>
        /// SaveXAxisPoints 命令
        /// </summary>
        public DelegateCommand SaveXAxisPointsCommand { get; }
        /// <summary>
        /// SaveYAxisPoints 命令
        /// </summary>
        public DelegateCommand SaveYAxisPointsCommand { get; }
        /// <summary>
        /// SaveZAxisPoints 命令
        /// </summary>
        public DelegateCommand SaveZAxisPointsCommand { get; }

        //工位操作
        /// <summary>
        /// MoveInitial 命令
        /// </summary>
        public DelegateCommand MoveInitialCommand { get; }
        /// <summary>
        /// MoveStation1 命令
        /// </summary>

        public DelegateCommand MoveStation1Command { get; }
        /// <summary>
        /// MoveStation2 命令
        /// </summary>

        public DelegateCommand MoveStation2Command { get; }
        /// <summary>
        /// CamTigger 命令
        /// </summary>


        public DelegateCommand CamTiggerCommand { get; }
        #endregion Commands 定义
        /// <summary>
        /// WorkStationDetectionModuleDebugViewModel 构造函数
        /// </summary>


        public WorkStationDetectionModuleDebugViewModel(IContainerProvider containerProvider)
        {
            // 依赖注入获取模组实例
            _detectionModule = containerProvider.Resolve<IMechanism>(nameof(WorkStationDetectionModule)) as WorkStationDetectionModule;
            // --- 绑定全局生命周期指令 ---
            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteResultAsync(() => _detectionModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteResultAsync(() => _detectionModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _detectionModule?.StopAsync()));


            SaveXAxisPointsCommand = new DelegateCommand(SaveXAxisPoints);
            SaveYAxisPointsCommand = new DelegateCommand(SaveYAxisPoints);
            SaveZAxisPointsCommand = new DelegateCommand(SaveZAxisPoints);

            MoveInitialCommand = new DelegateCommand(async () => await ExecuteResultAsync(() => _detectionModule?.MoveInitial()));
            MoveStation1Command = new DelegateCommand(async () => await ExecuteResultAsync(() => _detectionModule?.MoveToStation1()));
            MoveStation2Command = new DelegateCommand(async () => await ExecuteResultAsync(() => _detectionModule?.MoveToStation2()));
            CamTiggerCommand = new DelegateCommand(async () => await CamTiggerAsync());
            StartMonitor();

            LoadOriginalPoints();
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


        private async Task ExecuteResultAsync(Func<Task<bool>>? action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = "执行中...";
                var flag = await action.Invoke();
                DebugMessage =  flag ? "执行成功" : "执行失败";
            }
            catch (Exception ex)
            {
                DebugMessage = $"执行异常: {ex.Message}";
                MessageService.ShowMessage(ex.Message, "调试面板报错", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        private async Task CamTiggerAsync()
        {
            if (_detectionModule == null) return;
            try
            {
                DebugMessage = "触发相机";
                string rec = (await _detectionModule.CameraTigger(false )).Item1 ;
                if (string.IsNullOrEmpty(rec))
                {
                    DebugMessage = "相机读取失败";
                    CamRec = "ERROR";
                }
                else
                {
                    DebugMessage = "相机读取成功";
                    CamRec = rec;
                }
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




        private void LoadOriginalPoints()
        {
            if (_detectionModule == null) return;
            if (_detectionModule.XAxis?.PointTable != null)
            {
                XAxisOriginalPoints.Clear();
                foreach (var pt in _detectionModule.XAxis.PointTable) XAxisOriginalPoints.Add(pt);
            }
            if (_detectionModule.YAxis?.PointTable != null)
            {
                YAxisOriginalPoints.Clear();
                foreach (var pt in _detectionModule.YAxis.PointTable) YAxisOriginalPoints.Add(pt);
            }

            if (_detectionModule.ZAxis?.PointTable != null)
            {
                ZAxisOriginalPoints.Clear();
                foreach (var pt in _detectionModule.ZAxis.PointTable) ZAxisOriginalPoints.Add(pt);
            }
        }




        private void SaveXAxisPoints()
        {
            if (_detectionModule == null) return;
            try
            {
                foreach (var pt in XAxisOriginalPoints) _detectionModule.XAxis.AddOrUpdatePoint(pt);
                _detectionModule.XAxis.SavePointTable();
                MessageService.ShowMessage("X轴点位保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"X轴点位保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveYAxisPoints()
        {
            if (_detectionModule == null) return;
            try
            {
                foreach (var pt in YAxisOriginalPoints) _detectionModule.YAxis.AddOrUpdatePoint(pt);
                _detectionModule.YAxis.SavePointTable();
                MessageService.ShowMessage("Y轴点位保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"Y轴点位保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveZAxisPoints()
        {
            if (_detectionModule == null) return;
            try
            {
                foreach (var pt in ZAxisOriginalPoints) _detectionModule.ZAxis.AddOrUpdatePoint(pt);
                _detectionModule.ZAxis.SavePointTable();
                MessageService.ShowMessage("Z轴点位保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageService.ShowMessage($"Z轴点位保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion 内部执行逻辑与状态更新

    }
}
