using log4net.Util;
using NPOI.SS.UserModel.Charts;
using PF.Core.Entities.Configuration;
using PF.Core.Entities.Hardware;
using PF.Core.Interfaces.Configuration;
using PF.Core.Interfaces.Device.Hardware.LightController;
using PF.Core.Interfaces.Device.Mechanisms;
using PF.Core.Models;
using PF.UI.Infrastructure.PrismBase;
using PF.Workstation.AutoOcr.CostParam;
using PF.WorkStation.AutoOcr.Mechanisms;
using Prism.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PF.WorkStation.AutoOcr.UI.ViewModels.Mechanisms
{
    /// <summary>
    /// WS1MaterialPullingModuleDebugViewModel
    /// </summary>
    public class WS1MaterialPullingModuleDebugViewModel : RegionViewModelBase
    {
        private readonly WS1MaterialPullingModule? _materialPullingModule;
        /// <summary>
        /// 获取或设置 MaterialPullingModule
        /// </summary>

        public WS1MaterialPullingModule? MaterialPullingModule => _materialPullingModule;

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

        private bool _isBusy;
        /// <summary>
        /// 正在执行测试标志，用于锁定 UI 按钮防连点
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (SetProperty(ref _isBusy, value))
                {
                    // 状态改变时通知 UI 刷新按钮的可用性 (IsEnabled)
                    TestPullOutCommand?.RaiseCanExecuteChanged();
                    TestPushBackCommand?.RaiseCanExecuteChanged();
                    TestFullFlowCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        #region 状态监控属性 (UI 实时刷新)

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


        //IO状态


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

        #endregion 状态监控属性 (UI 实时刷新)


        #region 光源参数属性

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
                    UpdateLihtValue(3, (int)value);

                }
            }
        }





        private void UpdateLihtValue(int chanel, int vale)
        {
            if (_materialPullingModule == null) return;
            _materialPullingModule.LightController?.SetLightValue(chanel, vale);
        }

        #endregion 光源参数属性


        #region 点位集合
        /// <summary>
        /// 获取或设置 YAxisOriginalPoints
        /// </summary>

        public ObservableCollection<AxisPoint> YAxisOriginalPoints { get; set; } = [];


        #endregion 点位集合


        #region Command定义
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
        /// MoveDetcetion 命令
        /// </summary>

        public DelegateCommand MoveDetcetionCommand { get; }
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

        /// <summary>
        /// TestPullOut 命令
        /// </summary>
        public DelegateCommand TestPullOutCommand { get; }
        /// <summary>
        /// TestPushBack 命令
        /// </summary>
        public DelegateCommand TestPushBackCommand { get; }
        /// <summary>
        /// TestFullFlow 命令
        /// </summary>
        public DelegateCommand TestFullFlowCommand { get; }

        #endregion Command定义
        /// <summary>
        /// WS1MaterialPullingModuleDebugViewModel 构造函数
        /// </summary>


        public WS1MaterialPullingModuleDebugViewModel(IContainerProvider containerProvider, IParamService paramService)
        {
            _paramService = paramService;

            _materialPullingModule = containerProvider.Resolve<IMechanism>(nameof(WS1MaterialPullingModule)) as WS1MaterialPullingModule;

            InitializeModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.InitializeAsync()));
            ResetModuleCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.ResetAsync()));
            StopCommand = new DelegateCommand(async () => await ExecuteAsync(() => _materialPullingModule?.StopAsync()));
            IsCanResetCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("可初始化动作", () => _materialPullingModule?.CheckTrackIsMaterial()));
            InitializeGipper = new DelegateCommand(async () => await ExecuteMechResultAsync("初始化拉料工位", () => _materialPullingModule?.CheckTrackIsMaterial()));

            Change_8StatusCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("切换到8寸状态", () => _materialPullingModule?.CheckWafeSizeControl(E_WafeSize._8寸)));
            Change_12StatusCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("切换到12寸状态", () => _materialPullingModule?.CheckWafeSizeControl(E_WafeSize._12寸)));
            OpenGipperCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("打开夹爪", () => _materialPullingModule?.OpenWafeGipper()));

            CloseGipperCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("闭合夹爪", () => _materialPullingModule?.CloseWafeGipper()));
            SavePointCommand = new DelegateCommand(SavePoint);

            MoveFeedingCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("移动到拉料位", () => _materialPullingModule?.InitialMoveFeeding()));
            MoveDetcetionCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("移动到检测位", () => _materialPullingModule?.MoveDetection()));
            MoveInitialCommand = new DelegateCommand(async () => await ExecuteMechResultAsync("移动到初始位", () => _materialPullingModule?.MoveInitial()));
            CodeTiggerCommand = new DelegateCommand(async () => await ExecuteAsync(() => TiggerCode()));
            SaveLightValueCommand = new DelegateCommand(async () => await ExecuteAsync(() => SaveLightValue()));

            TestPullOutCommand = new DelegateCommand(async () => await ExecuteTestPullOutAsync(), CanExecuteTest);
            TestPushBackCommand = new DelegateCommand(async () => await ExecuteTestPushBackAsync(), CanExecuteTest);
            TestFullFlowCommand = new DelegateCommand(async () => await ExecuteTestFullFlowAsync(), CanExecuteTest);

            LoadOriginalPoints();

            StartMonitor();
        }




        #region 内部逻辑与状态更新

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




        private async Task TiggerCode(CancellationToken token = default)
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


        private async Task SaveLightValue()
        {
            // 使用非泛型重载，避免 double 作为引用类型约束的泛型参数
            var info = await _paramService.SetParamAsync(
                "System.Int32",
                E_Params.WorkStation1LightBrightness.ToString(),
                InfraredLightValue
            );
        }

        // 判断当前是否可以执行测试（设备未在忙碌中）
        private bool CanExecuteTest() => !IsBusy;

        /// <summary>
        /// 测试：仅执行拉料流程 (到取料位 -> 关夹爪 -> 检测叠料 -> 拉出到检测位)
        /// </summary>
        private async Task ExecuteTestPullOutAsync()
        {
            if (_materialPullingModule == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugMessage = "[调试] 开始执行单步测试：拉料流程...";
                await InternalTestPullOutAsync(cts.Token);
                DebugMessage = "[调试] 单步测试：拉料流程完成。";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"[调试] 拉料流程测试中断: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 测试：仅执行推料流程 (送料入料盒 -> 开夹爪 -> 退回待机位 -> 防呆检查)
        /// </summary>
        private async Task ExecuteTestPushBackAsync()
        {
            if (_materialPullingModule == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugMessage = "[调试] 开始执行单步测试：推料流程...";
                await InternalTestPushBackAsync(cts.Token);
                DebugMessage = "[调试] 单步测试：推料流程完成。";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"[调试] 推料流程测试中断: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>
        /// 测试：执行完整闭环 (拉料 + 模拟视觉延时 + 推料)
        /// </summary>
        private async Task ExecuteTestFullFlowAsync()
        {
            if (_materialPullingModule == null) return;
            IsBusy = true;
            using var cts = new CancellationTokenSource();
            try
            {
                DebugMessage = "[调试] 开始执行完整闭环测试...";

                await InternalTestPullOutAsync(cts.Token);

                DebugMessage = "[调试] 模拟视觉检测中...";
                await Task.Delay(1500, cts.Token);

                await InternalTestPushBackAsync(cts.Token);

                DebugMessage = "[调试] 完整拉送料闭环测试完成。";
                MessageService.ShowMessage(DebugMessage, "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                DebugMessage = $"[调试] 完整闭环测试中断: {ex.Message}";
                MessageService.ShowMessage(DebugMessage, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task InternalTestPullOutAsync(CancellationToken token)
        {
            var resMove = await _materialPullingModule!.InitialMoveFeeding(token);
            if (!resMove.IsSuccess) throw new Exception($"移动到取料位失败: {resMove.ErrorMessage}");

            var resClose = await _materialPullingModule.CloseWafeGipper(token);
            if (!resClose.IsSuccess) throw new Exception($"关闭夹爪失败: {resClose.ErrorMessage}");

            if (!await _materialPullingModule.CheckStackedPieces(token)) throw new Exception("检测到叠料异常");

            var resDetect = await _materialPullingModule.MoveDetection(token);
            if (!resDetect.IsSuccess) throw new Exception($"拉出至检测位失败: {resDetect.ErrorMessage}");
        }

        private async Task InternalTestPushBackAsync(CancellationToken token)
        {
            var resFeed = await _materialPullingModule!.FeedingMaterialToBox(token);
            if (!resFeed.IsSuccess) throw new Exception($"送料入料盒失败: {resFeed.ErrorMessage}");

            var resOpen = await _materialPullingModule.OpenWafeGipper(token);
            if (!resOpen.IsSuccess) throw new Exception($"打开夹爪失败: {resOpen.ErrorMessage}");

            var resRetract = await _materialPullingModule.PutOverMove(token);
            if (!resRetract.IsSuccess) throw new Exception($"退回待机避让位失败: {resRetract.ErrorMessage}");

            if (!await _materialPullingModule.CheckGipperInsidePro(token)) throw new Exception("退回后夹爪内仍检测到残留带片");
        }


        /// <summary>
        /// 后台轮询线程，用于更新坐标和IO状态指示灯
        /// </summary>
        private void StartMonitor()
        {
            _monitorTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _monitorTimer.Tick += (s, e) =>
            {
                if (_materialPullingModule == null || !_materialPullingModule.IsInitialized) return;

                // 刷新轴状态
                if (_materialPullingModule.YAxis != null)
                {
                    YAxisPosition = _materialPullingModule.YAxis.CurrentPosition ?? 0;
                    YAxisHasAlarm = _materialPullingModule.YAxis.HasAlarm;
                }

                // 刷新 IO 状态
                if (_materialPullingModule.IO != null)
                {
                    // 注意：这里的枚举需要确保你的工程中定义过 E_InPutName
                    GipperOpen = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆夹爪左气缸张开) == true;
                    GipperClose = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆夹爪左气缸闭合) == true;
                    AdjustedOpen = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道左调宽气缸打开) == true;
                    AdjustedClose = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道左调宽气缸缩回) == true;
                    IsIronTested = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆夹爪左铁环有无检测) == true;
                    Stackingdetection = _materialPullingModule.IO.ReadInput(E_InPutName.夹爪左叠料检测) == true;
                    WafeInPlace1 = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道左晶圆在位检测1) == true;
                    WafeInPlace2 = _materialPullingModule.IO.ReadInput(E_InPutName.晶圆轨道左晶圆在位检测2) == true;
                }
            };
            _monitorTimer.Start();
        }


        #endregion  内部逻辑与状态更新

    }
}