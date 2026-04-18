using PF.Core.Entities.Configuration;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Mechanisms;
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
    public class WorkStation2MaterialPullingModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WorkStation2MaterialPullingModule? _materialPullingModule;

        public WorkStation2MaterialPullingModule? MaterialPullingModule => _materialPullingModule;

        private readonly IParamService _paramService;

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

        private string _coderec = "NONE";
        public string Coderec { get => _coderec; set => SetProperty(ref _coderec, value); }

        #region Status Monitor Properties

        private double _yAxisPosition;
        public double YAxisPosition { get => _yAxisPosition; set => SetProperty(ref _yAxisPosition, value); }

        private bool _yAxisHasAlarm;
        public bool YAxisHasAlarm { get => _yAxisHasAlarm; set => SetProperty(ref _yAxisHasAlarm, value); }

        private bool _gipperOpen;
        public bool GipperOpen { get => _gipperOpen; set => SetProperty(ref _gipperOpen, value); }

        private bool _gipperclose;
        public bool GipperClose { get => _gipperclose; set => SetProperty(ref _gipperclose, value); }

        private bool _adjustedopen;
        public bool AdjustedOpen { get => _adjustedopen; set => SetProperty(ref _adjustedopen, value); }

        private bool _adjustedclose;
        public bool AdjustedClose { get => _adjustedclose; set => SetProperty(ref _adjustedclose, value); }

        private bool _isIronTested;
        public bool IsIronTested { get => _isIronTested; set => SetProperty(ref _isIronTested, value); }

        private bool _stackingdetection;
        public bool Stackingdetection { get => _stackingdetection; set => SetProperty(ref _stackingdetection, value); }

        private bool _wafeInPlace1;
        public bool WafeInPlace1 { get => _wafeInPlace1; set => SetProperty(ref _wafeInPlace1, value); }

        private bool _wafeInPlace2;
        public bool WafeInPlace2 { get => _wafeInPlace2; set => SetProperty(ref _wafeInPlace2, value); }

        #endregion

        #region Light Properties

        private double _infraredLightValue;
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
        public ObservableCollection<AxisPoint> YAxisOriginalPoints { get; set; } = new ObservableCollection<AxisPoint>();
        #endregion

        #region Commands
        public DelegateCommand InitializeModuleCommand { get; }
        public DelegateCommand ResetModuleCommand { get; }
        public DelegateCommand StopCommand { get; }

        public DelegateCommand IsCanResetCommand { get; }
        public DelegateCommand SaveLightValueCommand { get; }
        public DelegateCommand InitializeGipper { get; }

        public DelegateCommand Change_8StatusCommand { get; }
        public DelegateCommand Change_12StatusCommand { get; }

        public DelegateCommand OpenGipperCommand { get; }
        public DelegateCommand CloseGipperCommand { get; }

        public DelegateCommand MoveFeedingCommand { get; }
        public DelegateCommand MoveDetectionCommand { get; }
        public DelegateCommand MoveInitialCommand { get; }

        public DelegateCommand CodeTiggerCommand { get; }
        public DelegateCommand SavePointCommand { get; }
        #endregion

        public WorkStation2MaterialPullingModuleDebugViewModel(IContainerProvider containerProvider, IParamService paramService)
        {
            _paramService = paramService;
            _materialPullingModule = containerProvider.Resolve<IMechanism>(nameof(WorkStation2MaterialPullingModule)) as WorkStation2MaterialPullingModule;

            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.StopAsync()));
            IsCanResetCommand = new DelegateCommand(async () => await ExecuteCheckAsync("可初始化动作", () => _materialPullingModule?.CheckTrackIsMaterial()));
            InitializeGipper = new DelegateCommand(async () => await ExecuteCheckAsync("初始化拉料工位", () => _materialPullingModule?.CheckTrackIsMaterial()));

            Change_8StatusCommand = new DelegateCommand(async () => await ExecuteCheckAsync("切换到8寸状态", () => _materialPullingModule?.CheckWafeSizeControl(E_WafeSize._8寸)));
            Change_12StatusCommand = new DelegateCommand(async () => await ExecuteCheckAsync("切换到12寸状态", () => _materialPullingModule?.CheckWafeSizeControl(E_WafeSize._12寸)));
            OpenGipperCommand = new DelegateCommand(async () => await ExecuteCheckAsync("打开夹爪", () => _materialPullingModule?.OpenWafeGipper()));
            CloseGipperCommand = new DelegateCommand(async () => await ExecuteCheckAsync("关闭夹爪", () => _materialPullingModule?.CloseWafeGipper()));
            SavePointCommand = new DelegateCommand(SavePoint);

            MoveFeedingCommand = new DelegateCommand(async () => await ExecuteCheckAsync("移动到拉料位", () => _materialPullingModule?.InitialMoveFeeding()));
            MoveDetectionCommand = new DelegateCommand(async () => await ExecuteCheckAsync("移动到检测位", () => _materialPullingModule?.MoveDetection()));
            MoveInitialCommand = new DelegateCommand(async () => await ExecuteCheckAsync("移动到初始位", () => _materialPullingModule?.MoveInitial()));
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

        private async Task ExecuteCheckAsync(string actionName, Func<Task<bool>> action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = $"检查 {actionName} 中...";
                bool result = await action.Invoke();
                DebugMessage = $"结果: {actionName} = {(result ? "满足 (True)" : "不满足 (False)")}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"检查异常: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async Task ExecuteCheckAsync(string actionName, Func<Task<bool?>> action)
        {
            if (action == null) return;
            try
            {
                DebugMessage = $"检查 {actionName} 中...";
                bool? result = await action.Invoke();
                DebugMessage = $"结果: {actionName} = {(result.Value ? "满足 (True)" : "不满足 (False)")}";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"检查异常: {ex.Message}";
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
            var res = await _materialPullingModule.CodeScanTigger();
            if (res == null)
            {
                Coderec = "ERROR";
            }
            else
            {
                Coderec = string.Join('&', res);
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
