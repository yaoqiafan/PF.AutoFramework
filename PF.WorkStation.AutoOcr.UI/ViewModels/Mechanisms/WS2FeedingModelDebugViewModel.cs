using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using PF.WorkStation.AutoOcr.UI.Models;
using Prism.Commands;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    /// <summary>
    /// WS2FeedingModelDebugViewModel
    /// </summary>
    public class WS2FeedingModelDebugViewModel : RegionViewModelBase
    {
        private readonly WS2FeedingModel? _feedingModule;
        private DispatcherTimer _monitorTimer;

        // 供 UI 绑定底层硬件状态
        /// <summary>
        /// 获取或设置 FeedingModule
        /// </summary>
        public WS2FeedingModel? FeedingModule => _feedingModule;

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

        #region 状态监控属性 (UI 实时刷新)
        private double _zAxisPosition;
        /// <summary>
        /// 获取或设置 ZAxisPosition
        /// </summary>
        public double ZAxisPosition { get => _zAxisPosition; set => SetProperty(ref _zAxisPosition, value); }

        private double _xAxisPosition;
        /// <summary>
        /// 获取或设置 XAxisPosition
        /// </summary>
        public double XAxisPosition { get => _xAxisPosition; set => SetProperty(ref _xAxisPosition, value); }

        private bool _zAxisHasAlarm;
        /// <summary>
        /// 获取或设置 ZAxisHasAlarm
        /// </summary>
        public bool ZAxisHasAlarm { get => _zAxisHasAlarm; set => SetProperty(ref _zAxisHasAlarm, value); }

        private bool _xAxisHasAlarm;
        /// <summary>
        /// 获取或设置 XAxisHasAlarm
        /// </summary>
        public bool XAxisHasAlarm { get => _xAxisHasAlarm; set => SetProperty(ref _xAxisHasAlarm, value); }

        // IO 状态
        private bool _isBoxCommonInPlace;
        /// <summary>
        /// 获取或设置 IsBoxCommonInPlace
        /// </summary>
        public bool IsBoxCommonInPlace { get => _isBoxCommonInPlace; set => SetProperty(ref _isBoxCommonInPlace, value); }

        private bool _is8InchInPlace;
        /// <summary>
        /// 获取或设置 Is8InchInPlace
        /// </summary>
        public bool Is8InchInPlace { get => _is8InchInPlace; set => SetProperty(ref _is8InchInPlace, value); }

        private bool _is12InchInPlace;
        /// <summary>
        /// 获取或设置 Is12InchInPlace
        /// </summary>
        public bool Is12InchInPlace { get => _is12InchInPlace; set => SetProperty(ref _is12InchInPlace, value); }

        private bool _isErrorLayer1;
        /// <summary>
        /// 获取或设置 IsErrorLayer1 (错层检测1)
        /// </summary>
        public bool IsErrorLayer1 { get => _isErrorLayer1; set => SetProperty(ref _isErrorLayer1, value); }

        private bool _isErrorLayer2;
        /// <summary>
        /// 获取或设置 IsErrorLayer2 (错层检测2)
        /// </summary>
        public bool IsErrorLayer2 { get => _isErrorLayer2; set => SetProperty(ref _isErrorLayer2, value); }

        private bool _isIronTabDetected;
        /// <summary>
        /// 获取或设置 IsIronTabDetected (铁环突片检测)
        /// </summary>
        public bool IsIronTabDetected { get => _isIronTabDetected; set => SetProperty(ref _isIronTabDetected, value); }

        private bool _is8InchIronReverse;
        /// <summary>
        /// 获取或设置 Is8InchIronReverse (8寸铁环防反检测)
        /// </summary>
        public bool Is8InchIronReverse { get => _is8InchIronReverse; set => SetProperty(ref _is8InchIronReverse, value); }

        private bool _is12InchIronReverse;
        /// <summary>
        /// 获取或设置 Is12InchIronReverse (12寸铁环防反检测)
        /// </summary>
        public bool Is12InchIronReverse { get => _is12InchIronReverse; set => SetProperty(ref _is12InchIronReverse, value); }

        private bool _is8InchStopRod;
        /// <summary>
        /// 获取或设置 Is8InchStopRod (8寸料盒挡杆检测)
        /// </summary>
        public bool Is8InchStopRod { get => _is8InchStopRod; set => SetProperty(ref _is8InchStopRod, value); }

        private bool _is12InchStopRod1;
        /// <summary>
        /// 获取或设置 Is12InchStopRod1 (12寸料盒挡杆检测1)
        /// </summary>
        public bool Is12InchStopRod1 { get => _is12InchStopRod1; set => SetProperty(ref _is12InchStopRod1, value); }

        private bool _is12InchStopRod2;
        /// <summary>
        /// 获取或设置 Is12InchStopRod2 (12寸料盒挡杆检测2)
        /// </summary>
        public bool Is12InchStopRod2 { get => _is12InchStopRod2; set => SetProperty(ref _is12InchStopRod2, value); }
        #endregion

        #region 点位数据集合
        /// <summary>
        /// 获取或设置 ZAxisOriginalPoints
        /// </summary>
        public ObservableCollection<AxisPoint> ZAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();
        /// <summary>
        /// 获取或设置 XAxisOriginalPoints
        /// </summary>
        public ObservableCollection<AxisPoint> XAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();
        /// <summary>
        /// 获取或设置 ArrayedPoints
        /// </summary>
        public ObservableCollection<AxisPoint> ArrayedPoints { get; set; } = new ObservableCollection<AxisPoint>();


        // 分开定义两个传感器的 UI 绑定集合
        /// <summary>
        /// 获取或设置 RawMappingPoints1
        /// </summary>
        public ObservableCollection<RawMappingItem> RawMappingPoints1 { get; set; } = new ObservableCollection<RawMappingItem>();
        /// <summary>
        /// 获取或设置 RawMappingPoints2
        /// </summary>
        public ObservableCollection<RawMappingItem> RawMappingPoints2 { get; set; } = new ObservableCollection<RawMappingItem>();
        /// <summary>
        /// 获取或设置 FilteredMappingPoints
        /// </summary>
        public ObservableCollection<FilteredMappingItem> FilteredMappingPoints { get; set; } = new ObservableCollection<FilteredMappingItem>();
        #endregion

        #region Commands 定义
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

        // 2. 左侧原子指令
        /// <summary>
        /// InitState 命令
        /// </summary>
        public DelegateCommand InitStateCommand { get; }
        /// <summary>
        /// DetectSize 命令
        /// </summary>
        public DelegateCommand DetectSizeCommand { get; }
        /// <summary>
        /// SwitchProduction 命令
        /// </summary>
        public DelegateCommand<string> SwitchProductionCommand { get; }
        /// <summary>
        /// CanMoveZ 命令
        /// </summary>

        public DelegateCommand CanMoveZCommand { get; }
        /// <summary>
        /// CanMoveX 命令
        /// </summary>
        public DelegateCommand CanMoveXCommand { get; }
        /// <summary>
        /// CanPullOut 命令
        /// </summary>
        public DelegateCommand CanPullOutCommand { get; }
        /// <summary>
        /// SearchLayer 命令
        /// </summary>

        public DelegateCommand<double?> SearchLayerCommand { get; }
        /// <summary>
        /// GoToLayer 命令
        /// </summary>
        public DelegateCommand GoToLayerCommand { get; }

        // 3. 点位保存
        /// <summary>
        /// SaveZAxisPoints 命令
        /// </summary>
        public DelegateCommand SaveZAxisPointsCommand { get; }
        /// <summary>
        /// SaveXAxisPoints 命令
        /// </summary>
        public DelegateCommand SaveXAxisPointsCommand { get; }
        #endregion
        /// <summary>
        /// WS2FeedingModelDebugViewModel 构造函数
        /// </summary>

        public WS2FeedingModelDebugViewModel(IContainerProvider containerProvider)
        {
            // 依赖注入获取模组实例
            _feedingModule = containerProvider.Resolve<IMechanism>(nameof(WS2FeedingModel)) as WS2FeedingModel;

            // --- 绑定全局生命周期指令 ---
            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _feedingModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _feedingModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _feedingModule?.StopAsync()));

            // --- 绑定模组内部原子指令 ---
            InitStateCommand = new DelegateCommand(async () => await ExecuteAsync(() => _feedingModule?.InitializeFeedingStateAsync()));
            DetectSizeCommand = new DelegateCommand(async () => await ExecuteDetectSizeAsync());
            SwitchProductionCommand = new DelegateCommand<string>(async (size) => await ExecuteSwitchProductionAsync(size));

            CanMoveZCommand = new DelegateCommand(async () => await ExecuteCheckAsync("Z轴可动条件", () => _feedingModule?.CanMoveZAxesAsync()));
            CanMoveXCommand = new DelegateCommand(async () => await ExecuteCheckAsync("X轴可动条件", () => _feedingModule?.CanMoveXAxesAsync()));
            CanPullOutCommand = new DelegateCommand(async () => await ExecuteCheckAsync("允许拉料条件", () => _feedingModule?.CanPullOutMaterialAsync()));

            SearchLayerCommand = new DelegateCommand<double?>(async (t) => await ExecuteSearchLayerAsync(t));
            GoToLayerCommand = new DelegateCommand(async () => await ExecuteAsync(() => _feedingModule?.SwitchToLayerAsync(TargetLayer)));

            SaveZAxisPointsCommand = new DelegateCommand(SaveZAxisPoints);
            SaveXAxisPointsCommand = new DelegateCommand(SaveXAxisPoints);

            LoadOriginalPoints();
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

        private async Task ExecuteCheckAsync(string actionName, Func<Task<MechResult>> action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = $"检查 {actionName} 中...";
                var result = await action.Invoke();
                DebugMessage = result.IsSuccess
                    ? $"结果: {actionName} = 满足 (True)"
                    : $"结果: {actionName} = 不满足 (False) [{result.ErrorCode}] {result.ErrorMessage}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"检查异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ExecuteDetectSizeAsync()
        {
            if (_feedingModule == null) return;
            try
            {
                DebugMessage = "尺寸识别中...";
                var size = await _feedingModule.GetWaferBoxSizeAsync();
                DebugMessage = $"检测成功: 当前为 {size}";

                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"检测异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ExecuteSearchLayerAsync(double? runTimes)
        {
            if (_feedingModule == null) return;

            // 将 double 转换为 int，并确保至少运行 1 次
            int totalRuns = (int)Math.Max((double)1, (double)runTimes);

            try
            {
                for (int i = 0; i < totalRuns; i++)
                {
                    int currentRun = i + 1;
                    DebugMessage = $"开始第 {currentRun}/{totalRuns} 次寻层扫描...";

                    var scanResult = await _feedingModule.SearchLayerAsync();
                    if (!scanResult.IsSuccess)
                    {
                        DebugMessage = $"第 {currentRun} 次寻层扫描失败: {scanResult.ErrorMessage}";
                        MessageService.ShowMessage(DebugMessage, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return; // 失败则退出整个方法；如果想忽略错误继续下一次循环，请改为 continue;
                    }

                    var rawMap = scanResult.Data;
                    RawMappingPoints1.Clear();
                    RawMappingPoints2.Clear();

                    var keys = rawMap.Keys.ToList();
                    if (keys.Count > 0)
                    {
                        int index1 = 1;
                        foreach (var z in rawMap[keys[0]])
                            RawMappingPoints1.Add(new RawMappingItem { Index = index1++, ZPosition = z });
                    }

                    if (keys.Count > 1)
                    {
                        int index2 = 1;
                        foreach (var z in rawMap[keys[1]])
                            RawMappingPoints2.Add(new RawMappingItem { Index = index2++, ZPosition = z });
                    }

                    DebugMessage = $"第 {currentRun}/{totalRuns} 次数据获取完成，正在进行算法过滤...";
                    var filterResult = await _feedingModule.AnalyzeAndFilterMappingData(rawMap);
                    if (!filterResult.IsSuccess)
                    {
                        DebugMessage = $"第 {currentRun} 次算法过滤失败: {filterResult.ErrorMessage}";
                        MessageService.ShowMessage(DebugMessage, "警告或防呆拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    var filteredMap = filterResult.Data;
                    FilteredMappingPoints.Clear();
                    foreach (var kvp in filteredMap.OrderBy(k => k.Key))
                    {
                        FilteredMappingPoints.Add(new FilteredMappingItem { LayerIndex = kvp.Key + 1, ActualZ = kvp.Value });
                    }

                    DebugMessage = $"第 {currentRun} 次寻层完成: 共识别到 {filteredMap.Count} 层有效晶圆";

                    // 如果有多次循环，建议加一个短暂的延迟，避免硬件指令发送过快导致冲突
                    if (currentRun < totalRuns)
                    {
                        await Task.Delay(500); // 延时 500ms，可根据实际硬件要求调整或删除
                    }
                }

                // 所有循环顺利结束后，统一弹窗提示
                DebugMessage = $"任务完成: 共计执行 {totalRuns} 次寻层均已成功。";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"寻层/过滤异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "警告或防呆拦截", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ExecuteSwitchProductionAsync(string sizeStr)
        {
            if (_feedingModule == null) return;
            try
            {
                DebugMessage = $"切换 {sizeStr}寸 生产配方中...";
                E_WafeSize size = sizeStr == "8" ? E_WafeSize._8寸 : E_WafeSize._12寸;
                await _feedingModule.SwitchProductionStateAsync(size);

                // 更新界面的阵列推算表格
                ArrayedPoints.Clear();
                var dict = size == E_WafeSize._8寸 ? _feedingModule.PickingPosition_8 : _feedingModule.PickingPosition_12;
                foreach (var kvp in dict.OrderBy(k => k.Key))
                {
                    ArrayedPoints.Add(kvp.Value);
                }
                DebugMessage = $"切换成功: {sizeStr}寸 状态已就绪";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { DebugMessage = $"配方切换异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadOriginalPoints()
        {
            if (_feedingModule == null) return;
            if (_feedingModule.ZAxis?.PointTable != null)
            {
                ZAxisOriginalPoints.Clear();
                foreach (var pt in _feedingModule.ZAxis.PointTable) ZAxisOriginalPoints.Add(pt);
            }
            if (_feedingModule.XAxis?.PointTable != null)
            {
                XAxisOriginalPoints.Clear();
                foreach (var pt in _feedingModule.XAxis.PointTable) XAxisOriginalPoints.Add(pt);
            }
        }

        private void SaveZAxisPoints()
        {
            if (_feedingModule?.ZAxis == null) return;
            try
            {
                foreach (var pt in ZAxisOriginalPoints) _feedingModule.ZAxis.AddOrUpdatePoint(pt);
                _feedingModule.ZAxis.SavePointTable();
                MessageService.ShowMessage("Z轴点位保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageService.ShowMessage($"Z轴点位保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void SaveXAxisPoints()
        {
            if (_feedingModule?.XAxis == null) return;
            try
            {
                foreach (var pt in XAxisOriginalPoints) _feedingModule.XAxis.AddOrUpdatePoint(pt);
                _feedingModule.XAxis.SavePointTable();
                MessageService.ShowMessage("X轴点位保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageService.ShowMessage($"X轴点位保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        /// <summary>
        /// 后台轮询线程，用于更新坐标和IO状态指示灯
        /// </summary>
        private void StartMonitor()
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _monitorTimer.Tick += (s, e) =>
            {
                if (_feedingModule == null || !_feedingModule.IsInitialized) return;

                // 刷新轴状态
                if (_feedingModule.ZAxis != null)
                {
                    ZAxisPosition = _feedingModule.ZAxis.CurrentPosition ?? 0;
                    ZAxisHasAlarm = _feedingModule.ZAxis.HasAlarm;
                }
                if (_feedingModule.XAxis != null)
                {
                    XAxisPosition = _feedingModule.XAxis.CurrentPosition ?? 0;
                    XAxisHasAlarm = _feedingModule.XAxis.HasAlarm;
                }

                // 刷新 IO 状态
                if (_feedingModule.IO != null)
                {
                    IsBoxCommonInPlace = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右料盒公用到位) == true;
                    Is8InchInPlace = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右8寸料盒到位检测) == true;
                    Is12InchInPlace = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右12寸到料盒位检测) == true;
                    IsErrorLayer1 = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右错层公共检测) == true;
                    IsErrorLayer2 = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右错层12寸检测) == true;
                    IsIronTabDetected = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右铁环突片检测) == true;
                    Is8InchIronReverse = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右8寸铁环防反检测) == true;
                    Is12InchIronReverse = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右12寸铁环防反检测) == true;
                    Is8InchStopRod = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右8寸料盒挡杆检测) == true;
                    Is12InchStopRod1 = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右12寸料盒挡杆检测1) == true;
                    Is12InchStopRod2 = _feedingModule.IO.ReadInput(E_InPutName.上晶圆右12寸料盒挡杆检测2) == true;
                }
            };
            _monitorTimer.Start();
        }
        #endregion
    }
}