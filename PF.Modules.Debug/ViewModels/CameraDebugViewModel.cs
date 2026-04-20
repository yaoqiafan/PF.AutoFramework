using PF.Core.Constants;
using PF.Core.Interfaces.Device.Hardware.Camera.IntelligentCamera;
using PF.Infrastructure.Hardware;
using PF.UI.Infrastructure.PrismBase;
using Prism.Commands;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace PF.Modules.Debug.ViewModels
{
    /// <summary>智能相机调试 ViewModel</summary>
    public class CameraDebugViewModel : RegionViewModelBase
    {
        // 假设 BaseIntelligentCamera 继承自 BaseDevice，且包含触发和读码等方法
        private IIntelligentCamera _camera; // 如果你有明确的接口引用（如 IIntelligentCamera），请替换 dynamic
        private BaseDevice _baseDevice;
        private DispatcherTimer _pollingTimer;

        /// <summary>初始化智能相机调试 ViewModel</summary>
        public CameraDebugViewModel()
        {
            InitializeCommands();

            // 智能相机刷新率无需像轴那么高，500ms 即可
            _pollingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            _pollingTimer.Tick += OnPollingTimerTick;
        }

        #region 【Prism 导航生命周期】

        /// <summary>导航进入时加载相机设备数据</summary>
        public override void OnNavigatedTo(NavigationContext navigationContext)
        {
            base.OnNavigatedTo(navigationContext);

            if (navigationContext.Parameters.ContainsKey("Device"))
            {
                _camera = navigationContext.Parameters.GetValue<IIntelligentCamera>("Device");
                _baseDevice = _camera as BaseDevice;

                if (_baseDevice != null)
                {
                    DeviceName = _baseDevice.DeviceName;
                    DeviceDescription = $"设备类别: {_baseDevice.Category} | 模拟状态: {_baseDevice.IsSimulated}";
                }
                else
                {
                    DeviceName = "未知智能相机设备";
                    DeviceDescription = "无法获取底层设备信息";
                }

                if (_camera != null)
                {
                    _pollingTimer.Start();

                    CameraPrograms = _camera.CameraProgram;
                }


            }
        }

        /// <summary>导航离开时停止轮询</summary>
        public override void OnNavigatedFrom(NavigationContext navigationContext)
        {
            base.OnNavigatedFrom(navigationContext);
            _pollingTimer.Stop(); // 离开页面务必停止轮询
        }

        #endregion

        #region 【设备信息与状态属性】

        private string _deviceName = "未选中相机";
        /// <summary>获取或设置设备名称</summary>
        public string DeviceName { get => _deviceName; set => SetProperty(ref _deviceName, value); }

        private string _deviceDescription = "等待设备接入...";
        /// <summary>获取或设置设备描述</summary>
        public string DeviceDescription { get => _deviceDescription; set => SetProperty(ref _deviceDescription, value); }

        private bool _isConnected;
        /// <summary>获取或设置是否已连接</summary>
        public bool IsConnected { get => _isConnected; set => SetProperty(ref _isConnected, value); }

        private bool _isAlarm;
        /// <summary>获取或设置是否报警</summary>
        public bool IsAlarm { get => _isAlarm; set => SetProperty(ref _isAlarm, value); }

        private List<string> _cameraPrograms=new List<string>();
        /// <summary>获取或设置相机程序列表</summary>
        public List<string> CameraPrograms
        {
            get { return _cameraPrograms; }
            set { SetProperty(ref _cameraPrograms, value); }
        }
        #endregion

        #region 【相机特有操作属性】

        private object _targetJob = string.Empty;
        /// <summary> 目标程序号 / Job ID </summary>
        public object TargetJob  { get => _targetJob ; set => SetProperty(ref _targetJob , value); }

        private string _resultText = "等待触发...";
        /// <summary> 视觉检测或扫码结果 </summary>
        public string ResultText { get => _resultText; set => SetProperty(ref _resultText, value); }

        #endregion

        #region 【控制命令定义】

        /// <summary>连接命令</summary>
        public DelegateCommand ConnectCommand { get; private set; }
        /// <summary>断开连接命令</summary>
        public DelegateCommand DisconnectCommand { get; private set; }
        /// <summary>复位命令</summary>
        public DelegateCommand ResetCommand { get; private set; }
        /// <summary>触发检测命令</summary>
        public DelegateCommand TriggerCommand { get; private set; }
        /// <summary>切换程序命令</summary>
        public DelegateCommand ChangeJobCommand { get; private set; }
        /// <summary>模拟硬件报警命令</summary>
        public DelegateCommand SimulateAlarmCommand { get; private set; }

        private void InitializeCommands()
        {
            ConnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ConnectAsync(CancellationToken.None); });
            DisconnectCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.DisconnectAsync(); });
            ResetCommand = new DelegateCommand(async () => { if (_baseDevice != null) await _baseDevice.ResetAsync(CancellationToken.None); });

            TriggerCommand = new DelegateCommand(async () =>
            {
                if (_camera == null) return;
                try
                {
                    ResultText = "正在触发检测...";
                    // 请替换为 BaseIntelligentCamera 实际的触发及获取结果的方法
                    var result = await _camera.Tigger();
                    ResultText = result?.ToString() ?? "触发成功，无返回数据";
                }
                catch (Exception ex)
                {
                    ResultText = $"触发失败: {ex.Message}";
                }
            });

            ChangeJobCommand = new DelegateCommand(async () =>
            {
                if (_camera == null) return;
                try
                {
                    // 请替换为 BaseIntelligentCamera 实际的切换程序方法
                    await _camera.ChangeProgram(TargetJob );
                    ResultText = $"成功切换到程序号: {TargetJob }";
                }
                catch (Exception ex)
                {
                    ResultText = $"切换程序失败: {ex.Message}";
                }
            });

            SimulateAlarmCommand = new DelegateCommand(() =>
            {
                _baseDevice?.SimulateAlarm(AlarmCodes.Hardware.CameraTimeout, "调试页面手动模拟相机报警");
            });
        }

        #endregion

        #region 【定时器轮询更新】

        private void OnPollingTimerTick(object sender, EventArgs e)
        {
            if (_baseDevice == null) return;

            IsConnected = _baseDevice.IsConnected;
            IsAlarm = _baseDevice.HasAlarm;
        }

        #endregion
    }
}