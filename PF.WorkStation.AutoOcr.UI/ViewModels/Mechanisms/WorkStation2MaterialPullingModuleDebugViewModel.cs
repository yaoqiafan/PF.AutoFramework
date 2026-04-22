using PF.Core.Entities.Configuration;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
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
    /// WorkStation2MaterialPullingModuleDebugViewModel
    /// </summary>
    public class WorkStation2MaterialPullingModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStation2MaterialPullingModule? _materialPullingModule;
        /// <summary>
        /// 获取或设置 MaterialPullingModule
        /// </summary>

        public WorkStation2MaterialPullingModule? MaterialPullingModule => _materialPullingModule;

        private readonly IParamService _paramService;

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

        private string _coderec = "NONE";
        /// <summary>
        /// 获取或设置 Coderec
        /// </summary>
        public string Coderec { get => _coderec; set => SetProperty(ref _coderec, value); }

        #region Status Monitor Properties

        private double _yAxisPosition;
        /// <summary>
        /// 获取或设置 YAxisPosition
        /// </summary>
        public double YAxisPosition { get => _yAxisPosition; set => SetProperty(ref _yAxisPosition, value); }

        private bool _yAxisHasAlarm;
        /// <summary>
        /// 获取或设置 YAxisHasAlarm
        /// </summary>
        public bool YAxisHasAlarm { get => _yAxisHasAlarm; set => SetProperty(ref _yAxisHasAlarm, value); }

        private bool _gipperOpen;
        /// <summary>
        /// 获取或设置 GipperOpen
        /// </summary>
        public bool GipperOpen { get => _gipperOpen; set => SetProperty(ref _gipperOpen, value); }

        private bool _gipperclose;
        /// <summary>
        /// 获取或设置 GipperClose
        /// </summary>
        public bool GipperClose { get => _gipperclose; set => SetProperty(ref _gipperclose, value); }

        private bool _adjustedopen;
        /// <summary>
        /// 获取或设置 AdjustedOpen
        /// </summary>
        public bool AdjustedOpen { get => _adjustedopen; set => SetProperty(ref _adjustedopen, value); }

        private bool _adjustedclose;
        /// <summary>
        /// 获取或设置 AdjustedClose
        /// </summary>
        public bool AdjustedClose { get => _adjustedclose; set => SetProperty(ref _adjustedclose, value); }

        private bool _isIronTested;
        /// <summary>
        /// 获取或设置 IsIronTested
        /// </summary>
        public bool IsIronTested { get => _isIronTested; set => SetProperty(ref _isIronTested, value); }

        private bool _stackingdetection;
        /// <summary>
        /// 获取或设置 Stackingdetection
        /// </summary>
        public bool Stackingdetection { get => _stackingdetection; set => SetProperty(ref _stackingdetection, value); }

        private bool _wafeInPlace1;
        /// <summary>
        /// 获取或设置 WafeInPlace1
        /// </summary>
        public bool WafeInPlace1 { get => _wafeInPlace1; set => SetProperty(ref _wafeInPlace1, value); }

        private bool _wafeInPlace2;
        /// <summary>
        /// 获取或设置 WafeInPlace2
        /// </summary>
        public bool WafeInPlace2 { get => _wafeInPlace2; set => SetProperty(ref _wafeInPlace2, value); }

        #endregion

        #region Light Properties

        private double _infraredLightValue;
        /// <summary>
        /// 成员
        /// </summary>
        public double InfraredLightValue
        {
            get => _infraredLightValue;
            set
            {
                if (value != _infraredLightValue)
                {
                    SetProperty(ref _infraredLightValue, (int)value);
                    UpdateLightValue(3, (int)value);
                }
            }
        }

        private void UpdateLightValue(int chanel, int vale)
        {
            if (_materialPullingModule == null) return;
            _materialPullingModule.LightController?.SetLightValue(chanel, vale);
        }

        #endregion

        #region Point Collections
        /// <summary>
        /// 获取或设置 YAxisOriginalPoints
        /// </summary>
        public ObservableCollection<AxisPoint> YAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();
        #endregion

        #region Commands
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
        /// IsCanReset 命令
        /// </summary>

        public DelegateCommand IsCanResetCommand { get; }
        /// <summary>
        /// SaveLightValue 命令
        /// </summary>
        public DelegateCommand SaveLightValueCommand { get; }
        /// <summary>
        /// InitializeGipper 命令
        /// </summary>
        public DelegateCommand InitializeGipper { get; }
        /// <summary>
        /// Change_8Status 命令
        /// </summary>

        public DelegateCommand Change_8StatusCommand { get; }
        /// <summary>
        /// Change_12Status 命令
        /// </summary>
        public DelegateCommand Change_12StatusCommand { get; }
        /// <summary>
        /// OpenGipper 命令
        /// </summary>

        public DelegateCommand OpenGipperCommand { get; }
        /// <summary>
        /// CloseGipper 命令
        /// </summary>
        public DelegateCommand CloseGipperCommand { get; }
        /// <summary>
        /// MoveFeeding 命令
        /// </summary>

        public DelegateCommand MoveFeedingCommand { get; }
        /// <summary>
        /// MoveDetection 命令
        /// </summary>
        public DelegateCommand MoveDetectionCommand { get; }
        /// <summary>
        /// MoveInitial 命令
        /// </summary>
        public DelegateCommand MoveInitialCommand { get; }

        /// <summary>
        /// CodeTigger 命令
        /// </summary>
        public DelegateCommand CodeTiggerCommand { get; }
        /// <summary>
        /// SavePoint 命令
        /// </summary>
        public DelegateCommand SavePointCommand { get; }
        #endregion
        /// <summary>
        /// WorkStation2MaterialPullingModuleDebugViewModel 构造函数
        /// </summary>

        public WorkStation2MaterialPullingModuleDebugViewModel(IContainerProvider containerProvider, IParamService paramService)
        {
            _paramService = paramService;
            _materialPullingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation2MaterialPullingModule)) as WorkStation2MaterialPullingModule;

            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.StopAsync()));
            IsCanResetCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("可初始化动作", () => _materialPullingModule?.CheckTrackIsMaterial()));
            InitializeGipper = new DelegateCommand(async () => await ExecuteMechResultAsync("初始化拉料工位", () => _materialPullingModule?.CheckTrackIsMaterial()));

            Change_8StatusCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("切换到8寸状态", () => _materialPullingModule?.CheckWafeSizeControl(E_WafeSize._8寸)));
            Change_12StatusCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("切换到12寸状态", () => _materialPullingModule?.CheckWafeSizeControl(E_WafeSize._12寸)));
            OpenGipperCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("打开夹爪", () => _materialPullingModule?.OpenWafeGipper()));
            CloseGipperCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("关闭夹爪", () => _materialPullingModule?.CloseWafeGipper()));
            SavePointCommand = new DelegateCommand(SavePoint);

            MoveFeedingCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("移动到拉料位", () => _materialPullingModule?.InitialMoveFeeding()));
            MoveDetectionCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("移动到检测位", () => _materialPullingModule?.MoveDetection()));
            MoveInitialCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("移动到初始位", () => _materialPullingModule?.MoveInitial()));
            CodeTiggerCommand = new DelegateCommand(async () => await ExecuteAsync(() => TriggerCode()));
            SaveLightValueCommand = new DelegateCommand(async () => await ExecuteAsync(() => SaveLightValue()));

            LoadOriginalPoints();
            StartMonitor();
        }

        #region Internal Logic

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

     

        private async Task ExecuteMechResultAsync(string actionName, Func<Task<MechResult>> action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = $"执行 {actionName} 中...";
                var result = await action.Invoke();
                if (result.IsSuccess)
                {
                    DebugMessage = $"结果: {actionName} 成功";
                    MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DebugMessage = $"结果: {actionName} 失败 [{result.ErrorCode}] {result.ErrorMessage}";
                    MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DebugMessage = $"执行异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ExecuteMechResultAsync<T>(string actionName, Func<Task<MechResult<T>>> action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = $"执行 {actionName} 中...";
                var result = await action.Invoke();
                if (result.IsSuccess)
                {
                    DebugMessage = $"结果: {actionName} 成功";
                    MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    DebugMessage = $"结果: {actionName} 失败 [{result.ErrorCode}] {result.ErrorMessage}";
                    MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                DebugMessage = $"执行异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadOriginalPoints()
        {
            if (_materialPullingModule == null) return;
            if (_materialPullingModule.YAxis?.PointTable != null)
            {
                YAxisOriginalPoints.Clear();
                foreach (var pt in _materialPullingModule.YAxis.PointTable) YAxisOriginalPoints.Add(pt);
            }
        }

        private void SavePoint()
        {
            if (_materialPullingModule?.YAxis == null) return;
            try
            {
                foreach (var pt in YAxisOriginalPoints) _materialPullingModule.YAxis.AddOrUpdatePoint(pt);
                _materialPullingModule.YAxis.SavePointTable();
                MessageService.ShowMessage("Y轴点位保存成功！", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) { MessageService.ShowMessage($"Y轴点位保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private async Task TriggerCode(CancellationToken token = default)
        {
            if (_materialPullingModule?.YAxis == null) return;
            var res = await _materialPullingModule.CodeScanTigger(token);
            if (res.IsSuccess && res.Data != null)
            {
                Coderec = string.Join('&', res.Data);
            }
            else
            {
                Coderec = $"ERROR: {res.ErrorMessage}";
            }
        }

        private async Task SaveLightValue(CancellationToken token = default)
        {
            var info = await _paramService.SetParamAsync(
                "System.Int32",
                E_Params.WorkStation2LightBrightness.ToString(),
                InfraredLightValue
            );
        }

        private void StartMonitor()
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _monitorTimer.Tick += (s, e) =>
            {
                if (_materialPullingModule == null || !_materialPullingModule.IsInitialized) return;

                if (_materialPullingModule.YAxis != null)
                {
                    YAxisPosition = _materialPullingModule.YAxis.CurrentPosition ?? 0;
                    YAxisHasAlarm = _materialPullingModule.YAxis.HasAlarm;
                }

                if (_materialPullingModule.IO != null)
                {
                    GipperOpen = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆夹爪右气缸张开) == true;
                    GipperClose = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆夹爪右气缸闭合) == true;
                    AdjustedOpen = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道右调宽气缸打开) == true;
                    AdjustedClose = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道右调宽气缸缩回) == true;
                    IsIronTested = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆夹爪右铁环有无检测) == true;
                    Stackingdetection = _materialPullingModule.IO.ReadInput(E_InPutName.夹爪右叠料检测) == true;
                    WafeInPlace1 = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道右晶圆在位检测1) == true;
                    WafeInPlace2 = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道右晶圆在位检测2) == true;
                }
            };
            _monitorTimer.Start();
        }

        #endregion
    }
}
